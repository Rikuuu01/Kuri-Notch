using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class MediaService : IMediaService, IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private readonly object _lock = new object();
        private bool _isDisposed;

        private CancellationTokenSource? _hoverFilterCts;
        private bool _userRequestedSkip;

        public event EventHandler<MediaTrackInfo>? MediaTrackChanged;
        public event EventHandler<bool>? PlaybackStateChanged;

        public MediaTrackInfo CurrentTrack { get; private set; } = MediaTrackInfo.Empty;

        public async Task InitializeAsync()
        {
            try
            {
                Log.Information("Initializing Windows SMTC MediaService...");
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

                if (_sessionManager != null)
                {
                    _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;

                    await SelectActiveSessionAsync();
                }
                else
                {
                    Log.Warning("SMTC SessionManager is not available on this system.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize SMTC MediaService");
            }
        }

        private async void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            try
            {
                await SelectActiveSessionAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CurrentSessionChanged in SMTC");
            }
        }

        private async void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            try
            {
                await SelectActiveSessionAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling SessionsChanged in SMTC");
            }
        }

        private async Task SelectActiveSessionAsync()
        {
            if (_sessionManager == null) return;

            GlobalSystemMediaTransportControlsSession? candidate = null;

            try
            {
                // 1. Guard against hover previews: If our current session is already actively playing,
                // do NOT switch to another session from the same browser/app!
                lock (_lock)
                {
                    if (_currentSession != null)
                    {
                        var curInfo = _currentSession.GetPlaybackInfo();
                        if (curInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        {
                            var newCurrent = _sessionManager.GetCurrentSession();
                            if (newCurrent != null && newCurrent != _currentSession &&
                                newCurrent.SourceAppUserModelId == _currentSession.SourceAppUserModelId)
                            {
                                Log.Debug("Keeping currently playing session for {AppId}; ignoring same-app hover preview.", _currentSession.SourceAppUserModelId);
                                return;
                            }
                        }
                    }
                }

                // 2. Query recommended session from OS
                candidate = _sessionManager.GetCurrentSession();
                var info = candidate?.GetPlaybackInfo();

                // If candidate is closed or stopped, look through all sessions for one that is currently active/playing
                if (info == null ||
                    info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed ||
                    info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    var sessions = _sessionManager.GetSessions();
                    candidate = sessions.FirstOrDefault(s =>
                    {
                        var pi = s.GetPlaybackInfo();
                        return pi != null &&
                               pi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed &&
                               pi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped;
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to query SMTC sessions");
            }

            lock (_lock)
            {
                if (_currentSession == candidate)
                {
                    return;
                }

                if (_currentSession != null)
                {
                    UnsubscribeFromSession(_currentSession);
                }

                _currentSession = candidate;

                if (_currentSession != null)
                {
                    SubscribeToSession(_currentSession);
                }
            }

            await RefreshTrackInfoAsync();
        }

        private void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
        {
            session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged += Session_PlaybackInfoChanged;
            session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
        }

        private void UnsubscribeFromSession(GlobalSystemMediaTransportControlsSession session)
        {
            try
            {
                session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
                session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
                session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged;
            }
            catch
            {
            }
        }

        private async void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            await RefreshTrackInfoAsync();
        }

        private async void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            await RefreshTrackInfoAsync();
        }

        private async void Session_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            await RefreshTrackInfoAsync();
        }

        public async Task RefreshTrackInfoAsync()
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock)
            {
                session = _currentSession;
            }

            if (session == null)
            {
                CancelHoverFilter();
                ApplyTrackInfo(MediaTrackInfo.Empty);
                return;
            }

            try
            {
                var playbackInfo = session.GetPlaybackInfo();

                // If playback is closed or stopped, clear media
                if (playbackInfo == null ||
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed ||
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    CancelHoverFilter();
                    ApplyTrackInfo(MediaTrackInfo.Empty);
                    return;
                }

                bool isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                GlobalSystemMediaTransportControlsSessionMediaProperties? props = null;
                try
                {
                    props = await session.TryGetMediaPropertiesAsync();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to get media properties from session");
                }

                if (props == null || (string.IsNullOrWhiteSpace(props.Title) && string.IsNullOrWhiteSpace(props.Artist)))
                {
                    CancelHoverFilter();
                    ApplyTrackInfo(MediaTrackInfo.Empty);
                    return;
                }

                // Load thumbnail if available
                ImageSource? thumbnail = null;
                if (props.Thumbnail != null)
                {
                    try
                    {
                        using var stream = await props.Thumbnail.OpenReadAsync();
                        using var netStream = stream.AsStreamForRead();
                        using var memStream = new MemoryStream();
                        await netStream.CopyToAsync(memStream);
                        memStream.Position = 0;

                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = memStream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        thumbnail = bitmap;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Failed to decode media thumbnail stream");
                    }
                }

                var timeline = session.GetTimelineProperties();
                var pos = timeline?.Position ?? TimeSpan.Zero;
                var dur = (timeline?.EndTime ?? TimeSpan.Zero) - (timeline?.StartTime ?? TimeSpan.Zero);

                var track = new MediaTrackInfo
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    Thumbnail = thumbnail,
                    IsPlaying = isPlaying,
                    Position = pos >= TimeSpan.Zero ? pos : TimeSpan.Zero,
                    Duration = dur > TimeSpan.Zero ? dur : TimeSpan.Zero,
                    HasMedia = !string.IsNullOrWhiteSpace(props.Title),
                    SourceAppId = session.SourceAppUserModelId ?? string.Empty
                };

                // === BROWSER HOVER PREVIEW FILTER ===
                // When an existing video is already playing and a sudden new title arrives from a web browser
                // (YouTube preview video), hold the update for 1.8 seconds.
                // If the user was just hovering, YouTube will revert within 1.8s and the notch stays on the real video.
                // If the user actually clicked the video to watch it, the title persists and applies normally.
                if (!_userRequestedSkip &&
                    !string.IsNullOrEmpty(CurrentTrack.Title) &&
                    track.Title != CurrentTrack.Title &&
                    IsBrowserApp(track.SourceAppId))
                {
                    Log.Information("Potential YouTube hover preview detected ('{NewTitle}' vs current '{CurTitle}'). Holding filter for 1.8s...",
                        track.Title, CurrentTrack.Title);

                    _hoverFilterCts?.Cancel();
                    _hoverFilterCts?.Dispose();
                    _hoverFilterCts = new CancellationTokenSource();
                    var token = _hoverFilterCts.Token;

                    var candidate = track;
                    _ = Task.Delay(1800, token).ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            Log.Information("Hover filter passed: User navigated to new video '{NewTitle}'", candidate.Title);
                            ApplyTrackInfo(candidate);
                        }
                    }, TaskScheduler.Default);

                    return;
                }

                _userRequestedSkip = false;
                CancelHoverFilter();
                ApplyTrackInfo(track);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing track info from SMTC");
            }
        }

        private void ApplyTrackInfo(MediaTrackInfo track)
        {
            CurrentTrack = track;
            Log.Information("Media Track Applied: '{Title}' by '{Artist}' (IsPlaying={IsPlaying}, Duration={Duration})",
                track.Title, track.Artist, track.IsPlaying, track.Duration);

            MediaTrackChanged?.Invoke(this, track);
            PlaybackStateChanged?.Invoke(this, track.IsPlaying);
        }

        private void CancelHoverFilter()
        {
            if (_hoverFilterCts != null)
            {
                _hoverFilterCts.Cancel();
                _hoverFilterCts.Dispose();
                _hoverFilterCts = null;
            }
        }

        private static bool IsBrowserApp(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return false;
            var lower = appId.ToLowerInvariant();
            return lower.Contains("edge") ||
                   lower.Contains("chrome") ||
                   lower.Contains("firefox") ||
                   lower.Contains("brave") ||
                   lower.Contains("opera") ||
                   lower.Contains("vivaldi");
        }

        public async Task TogglePlayPauseAsync()
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session != null)
            {
                try
                {
                    await session.TryTogglePlayPauseAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to toggle play/pause via SMTC");
                }
            }
        }

        public async Task SkipNextAsync()
        {
            _userRequestedSkip = true;
            CancelHoverFilter();

            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session != null)
            {
                try
                {
                    await session.TrySkipNextAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to skip next via SMTC");
                }
            }
        }

        public async Task SkipPreviousAsync()
        {
            _userRequestedSkip = true;
            CancelHoverFilter();

            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session != null)
            {
                try
                {
                    await session.TrySkipPreviousAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to skip previous via SMTC");
                }
            }
        }

        public async Task SeekToAsync(TimeSpan position)
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session != null)
            {
                try
                {
                    await session.TryChangePlaybackPositionAsync(position.Ticks);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to seek playback position via SMTC");
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            CancelHoverFilter();

            lock (_lock)
            {
                if (_currentSession != null)
                {
                    UnsubscribeFromSession(_currentSession);
                    _currentSession = null;
                }
            }

            if (_sessionManager != null)
            {
                _sessionManager.CurrentSessionChanged -= SessionManager_CurrentSessionChanged;
                _sessionManager.SessionsChanged -= SessionManager_SessionsChanged;
                _sessionManager = null;
            }
        }
    }
}
