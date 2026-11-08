using System;
using System.IO;
using System.Runtime.InteropServices;
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

        private MediaTrackInfo? _lockedPlayingTrack;
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
                // Guard: If our current session is already actively playing, don't switch to another session from the same browser
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
                                Log.Debug("Keeping currently playing session for {AppId}; ignoring same-app hover session switch.", _currentSession.SourceAppUserModelId);
                                return;
                            }
                        }
                    }
                }

                candidate = _sessionManager.GetCurrentSession();
                var info = candidate?.GetPlaybackInfo();

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

        private GlobalSystemMediaTransportControlsSession? FindFallbackSession(GlobalSystemMediaTransportControlsSession? excludeSession)
        {
            try
            {
                if (_sessionManager == null) return null;
                var sessions = _sessionManager.GetSessions();
                return sessions.FirstOrDefault(s =>
                {
                    if (excludeSession != null && s == excludeSession) return false;
                    var pi = s.GetPlaybackInfo();
                    return pi != null &&
                           pi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed &&
                           pi.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped;
                });
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query fallback SMTC session");
                return null;
            }
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
                var fallback = FindFallbackSession(null);
                if (fallback != null)
                {
                    lock (_lock)
                    {
                        _currentSession = fallback;
                        SubscribeToSession(_currentSession);
                    }
                    await RefreshTrackInfoAsync();
                    return;
                }

                _lockedPlayingTrack = null;
                ApplyTrackInfo(MediaTrackInfo.Empty);
                return;
            }

            try
            {
                var playbackInfo = session.GetPlaybackInfo();

                // If playback is closed or stopped, check for a fallback active/paused session first before declaring Empty!
                if (playbackInfo == null ||
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed ||
                    playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                {
                    var fallback = FindFallbackSession(session);
                    if (fallback != null)
                    {
                        lock (_lock)
                        {
                            UnsubscribeFromSession(session);
                            _currentSession = fallback;
                            SubscribeToSession(_currentSession);
                        }
                        await RefreshTrackInfoAsync();
                        return;
                    }

                    _lockedPlayingTrack = null;
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
                    var fallback = FindFallbackSession(session);
                    if (fallback != null)
                    {
                        lock (_lock)
                        {
                            UnsubscribeFromSession(session);
                            _currentSession = fallback;
                            SubscribeToSession(_currentSession);
                        }
                        await RefreshTrackInfoAsync();
                        return;
                    }

                    _lockedPlayingTrack = null;
                    ApplyTrackInfo(MediaTrackInfo.Empty);
                    return;
                }

                GlobalSystemMediaTransportControlsSessionTimelineProperties? timeline = null;
                try
                {
                    timeline = session.GetTimelineProperties();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to get timeline properties from session");
                }

                var startTime = timeline?.StartTime ?? TimeSpan.Zero;
                var endTime = timeline?.EndTime ?? TimeSpan.Zero;
                var dur = endTime > startTime ? endTime - startTime : TimeSpan.Zero;

                var pos = timeline?.Position ?? TimeSpan.Zero;
                if (pos > TimeSpan.Zero && startTime > TimeSpan.Zero && pos >= startTime)
                {
                    pos -= startTime;
                }

                // GSMTC reports Position at the instant LastUpdatedTime occurred.
                // When actively playing, calculate current position by adding elapsed time since LastUpdatedTime.
                if (timeline != null && isPlaying && timeline.LastUpdatedTime > DateTimeOffset.MinValue && timeline.LastUpdatedTime.Year >= 2020)
                {
                    var elapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                    if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromHours(24))
                    {
                        pos += elapsed;
                    }
                }

                if (dur > TimeSpan.Zero && pos > dur)
                {
                    pos = dur;
                }
                else if (pos < TimeSpan.Zero)
                {
                    pos = TimeSpan.Zero;
                }

                var newTrack = new MediaTrackInfo
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    Thumbnail = null, // Will load below if accepted
                    IsPlaying = isPlaying,
                    Position = pos,
                    Duration = dur > TimeSpan.Zero ? dur : TimeSpan.Zero,
                    HasMedia = !string.IsNullOrWhiteSpace(props.Title),
                    SourceAppId = session.SourceAppUserModelId ?? string.Empty
                };

                // === YOUTUBE HOVER PREVIEW DETECTION ===
                // If a YouTube video is currently actively playing with a confirmed duration,
                // and a title change arrives that shares the EXACT same duration and is already mid-track,
                // it is mathematically guaranteed to be an inline thumbnail hover preview.
                if (IsLikelyYouTubeHoverPreview(newTrack))
                {
                    return;
                }

                _userRequestedSkip = false;

                // Load thumbnail for verified track
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
                        bitmap.DecodePixelWidth = 200;
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
                newTrack.Thumbnail = thumbnail;

                // Re-anchor playback position right before applying to eliminate any latency
                // introduced by asynchronous thumbnail stream loading and image decoding.
                if (timeline != null && isPlaying && timeline.LastUpdatedTime > DateTimeOffset.MinValue && timeline.LastUpdatedTime.Year >= 2020)
                {
                    var freshElapsed = DateTimeOffset.UtcNow - timeline.LastUpdatedTime;
                    if (freshElapsed > TimeSpan.Zero && freshElapsed < TimeSpan.FromHours(24))
                    {
                        var freshPos = timeline.Position;
                        if (freshPos > TimeSpan.Zero && startTime > TimeSpan.Zero && freshPos >= startTime)
                        {
                            freshPos -= startTime;
                        }
                        freshPos += freshElapsed;
                        if (dur > TimeSpan.Zero && freshPos > dur) freshPos = dur;
                        else if (freshPos < TimeSpan.Zero) freshPos = TimeSpan.Zero;
                        newTrack.Position = freshPos;
                    }
                }

                // Update locked playing track reference
                if (newTrack.HasMedia && newTrack.Duration > TimeSpan.FromSeconds(5))
                {
                    _lockedPlayingTrack = newTrack;
                }
                else if (!newTrack.HasMedia)
                {
                    _lockedPlayingTrack = null;
                }

                ApplyTrackInfo(newTrack);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error refreshing track info from SMTC");
            }
        }

        private bool IsLikelyYouTubeHoverPreview(MediaTrackInfo candidate)
        {
            if (_userRequestedSkip) return false;
            if (_lockedPlayingTrack == null) return false;
            if (string.IsNullOrEmpty(_lockedPlayingTrack.Title)) return false;
            if (string.Equals(candidate.Title, _lockedPlayingTrack.Title, StringComparison.OrdinalIgnoreCase)) return false;
            if (!IsBrowserApp(candidate.SourceAppId)) return false;

            // Both must have valid durations (> 5s) to compare mathematically
            if (_lockedPlayingTrack.Duration <= TimeSpan.FromSeconds(5) || candidate.Duration <= TimeSpan.FromSeconds(5))
            {
                return false;
            }

            // Mathematical fingerprint:
            // In Chromium browsers (Edge, Chrome, Brave, etc.), YouTube inline thumbnail hover previews
            // update the SMTC metadata (title/artist/artwork) on the active tab/session,
            // BUT the timeline duration remains strictly bound to the primary video's duration.
            // If an incoming browser track has a DIFFERENT title but the EXACT same duration (< 1.5s delta),
            // it is an inline thumbnail hover preview and must be rejected.
            bool sameDuration = Math.Abs((candidate.Duration - _lockedPlayingTrack.Duration).TotalSeconds) < 1.5;

            if (sameDuration)
            {
                Log.Information("IGNORED YouTube hover preview '{PreviewTitle}' (active track '{CurrentTitle}' duration {Duration})",
                    candidate.Title, _lockedPlayingTrack.Title, candidate.Duration);
                return true;
            }

            return false;
        }

        private void ApplyTrackInfo(MediaTrackInfo track)
        {
            CurrentTrack = track;
            Log.Information("Media Track Applied: '{Title}' by '{Artist}' (IsPlaying={IsPlaying}, Position={Position}, Duration={Duration})",
                track.Title, track.Artist, track.IsPlaying, track.Position, track.Duration);

            MediaTrackChanged?.Invoke(this, track);
            PlaybackStateChanged?.Invoke(this, track.IsPlaying);
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void SendMediaKey(byte vkCode)
        {
            try
            {
                keybd_event(vkCode, 0, 0, UIntPtr.Zero);
                keybd_event(vkCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send virtual media key 0x{VkCode:X2}", vkCode);
            }
        }

        public async Task TogglePlayPauseAsync()
        {
            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session == null && _sessionManager != null)
            {
                try { session = _sessionManager.GetCurrentSession(); } catch { }
            }

            bool success = false;
            if (session != null)
            {
                try
                {
                    success = await session.TryTogglePlayPauseAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to toggle play/pause via SMTC");
                }
            }

            if (!success)
            {
                SendMediaKey(VK_MEDIA_PLAY_PAUSE);
            }
        }

        public async Task SkipNextAsync()
        {
            _userRequestedSkip = true;
            _lockedPlayingTrack = null;

            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session == null && _sessionManager != null)
            {
                try { session = _sessionManager.GetCurrentSession(); } catch { }
            }

            bool success = false;
            if (session != null)
            {
                try
                {
                    success = await session.TrySkipNextAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to skip next via SMTC");
                }
            }

            if (!success)
            {
                SendMediaKey(VK_MEDIA_NEXT_TRACK);
            }
        }

        public async Task SkipPreviousAsync()
        {
            _userRequestedSkip = true;
            _lockedPlayingTrack = null;

            GlobalSystemMediaTransportControlsSession? session;
            lock (_lock) { session = _currentSession; }
            if (session == null && _sessionManager != null)
            {
                try { session = _sessionManager.GetCurrentSession(); } catch { }
            }

            bool success = false;
            if (session != null)
            {
                try
                {
                    success = await session.TrySkipPreviousAsync();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to skip previous via SMTC");
                }
            }

            if (!success)
            {
                SendMediaKey(VK_MEDIA_PREV_TRACK);
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

            _lockedPlayingTrack = null;

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
