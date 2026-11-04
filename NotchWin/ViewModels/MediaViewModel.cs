using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;



using System.Collections.ObjectModel;



using System.Threading;



using System.Threading.Tasks;



using System.Windows;



using System.Windows.Media;



using System.Windows.Threading;



using CommunityToolkit.Mvvm.ComponentModel;



using CommunityToolkit.Mvvm.Input;



using NotchWin.Models;



using NotchWin.Services;



using NotchWin.Widgets;



using Serilog;







namespace NotchWin.ViewModels



{



    public partial class MediaViewModel : ObservableObject, INotchWidget



    {



        private readonly IMediaService _mediaService;



        private readonly ISettingsService? _settingsService;



        private readonly ILyricsService _lyricsService;



        private readonly DispatcherTimer _timelineTimer;
        private readonly System.Diagnostics.Stopwatch _playbackStopwatch = new();
        private TimeSpan _baseTrackPosition = TimeSpan.Zero;
        private bool? _lastAnchoredPlaybackState;
        private static readonly string _songOffsetsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotchWin", "SongOffsets.json");
        private readonly ConcurrentDictionary<string, int> _songOffsets = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string _lyricsSelectionsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotchWin", "LyricsSelection.json");
        private readonly ConcurrentDictionary<string, int> _lyricsSelections = new(StringComparer.OrdinalIgnoreCase);
        private List<LyricsCandidate> _fetchedLyricsCandidates = new();
        private string _candidatesSongKey = string.Empty;
        private int _autoLyricsCandidateId;



        private CancellationTokenSource? _lyricsCts;

        private CancellationTokenSource? _restoreAccentCts;



        private string _lastLoadedLyricTitle = string.Empty;



        private string _lastLoadedLyricArtist = string.Empty;







        private readonly DispatcherTimer _pauseGraceTimer;

        private bool _isPillActive;



        public WidgetType Type => WidgetType.Media;



        public string Name => "Media";



        public int Priority => 20;



        public bool IsActive => HasMedia && _isPillActive;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(DisplayTitle))]



        private string _title = string.Empty;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(DisplayArtist))]



        [NotifyPropertyChangedFor(nameof(HasArtist))]



        private string _artist = string.Empty;







        public string DisplayTitle => !string.IsNullOrWhiteSpace(Title) ? Title : LocalizationService.Instance["Media.NoMedia"];



        public bool HasArtist => !string.IsNullOrWhiteSpace(Artist);



        public string DisplayArtist => !string.IsNullOrWhiteSpace(Artist)



            ? Artist



            : (IsPlaying || !string.IsNullOrWhiteSpace(Title) ? string.Empty : LocalizationService.Instance["Media.StartPlaying"]);







        [ObservableProperty]



        private ImageSource? _thumbnail;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]



        private bool _isPlaying;







        [ObservableProperty]



        private bool _hasMedia;







        [ObservableProperty]



        private bool _isSeeking;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(IsSpotify))]



        [NotifyPropertyChangedFor(nameof(SourceAppDisplayName))]



        private string _sourceAppId = string.Empty;







        public bool IsSpotify => !string.IsNullOrEmpty(SourceAppId) && SourceAppId.Contains("Spotify", StringComparison.OrdinalIgnoreCase);







        public string SourceAppDisplayName



        {



            get



            {



                if (string.IsNullOrEmpty(SourceAppId)) return "Music";



                if (IsSpotify) return "Spotify";



                if (SourceAppId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Google Chrome";



                if (SourceAppId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";



                if (SourceAppId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Mozilla Firefox";



                if (SourceAppId.Contains("Brave", StringComparison.OrdinalIgnoreCase)) return "Brave";



                if (SourceAppId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase)) return "Apple Music";



                if (SourceAppId.Contains("Tidal", StringComparison.OrdinalIgnoreCase)) return "TIDAL";



                if (SourceAppId.Contains("YouTubeMusic", StringComparison.OrdinalIgnoreCase)) return "YouTube Music";



                if (SourceAppId.Contains("AIMP", StringComparison.OrdinalIgnoreCase)) return "AIMP";



                if (SourceAppId.Contains("Foobar", StringComparison.OrdinalIgnoreCase)) return "foobar2000";



                if (SourceAppId.Contains("VLC", StringComparison.OrdinalIgnoreCase)) return "VLC Media Player";



                if (SourceAppId.Contains("WindowsMedia", StringComparison.OrdinalIgnoreCase) || SourceAppId.Contains("WMP", StringComparison.OrdinalIgnoreCase)) return "Media Player";







                try



                {



                    string clean = System.IO.Path.GetFileNameWithoutExtension(SourceAppId);



                    if (!string.IsNullOrWhiteSpace(clean))



                    {



                        return clean;



                    }



                }



                catch { }







                return "Music";



            }



        }







        public string CurrentHeaderDateTime => DateTime.Now.ToString("ddd d MMM yyyy  HH:mm", System.Globalization.CultureInfo.InvariantCulture);







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(FormattedTime))]



        [NotifyPropertyChangedFor(nameof(FormattedPosition))]



        [NotifyPropertyChangedFor(nameof(FormattedDuration))]



        [NotifyPropertyChangedFor(nameof(FormattedRemaining))]



        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]



        [NotifyPropertyChangedFor(nameof(PositionSeconds))]



        private TimeSpan _position = TimeSpan.Zero;







        partial void OnPositionChanged(TimeSpan value)



        {



            if (HasLyrics && !IsSeeking)



            {



                UpdateLyricsPosition(value);



            }



        }







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(FormattedTime))]



        [NotifyPropertyChangedFor(nameof(FormattedPosition))]



        [NotifyPropertyChangedFor(nameof(FormattedDuration))]



        [NotifyPropertyChangedFor(nameof(FormattedRemaining))]



        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]



        [NotifyPropertyChangedFor(nameof(DurationTotalSeconds))]



        private TimeSpan _duration = TimeSpan.Zero;







        public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";







        public string FormattedPosition => Duration > TimeSpan.Zero



            ? (Position.TotalHours >= 1 ? Position.ToString(@"h\:mm\:ss") : Position.ToString(@"m\:ss"))



            : "0:00";







        public string FormattedDuration => Duration > TimeSpan.Zero



            ? (Duration.TotalHours >= 1 ? Duration.ToString(@"h\:mm\:ss") : Duration.ToString(@"m\:ss"))



            : "0:00";







        public string FormattedRemaining



        {



            get



            {



                if (Duration <= TimeSpan.Zero) return "-0:00";



                var rem = Duration > Position ? Duration - Position : TimeSpan.Zero;



                string remStr = rem.TotalHours >= 1 ? rem.ToString(@"h\:mm\:ss") : rem.ToString(@"m\:ss");



                return $"-{remStr}";



            }



        }







        public double PositionSeconds



        {



            get => Position.TotalSeconds;



            set



            {



                if (Math.Abs(Position.TotalSeconds - value) > 0.5)



                {



                    Position = TimeSpan.FromSeconds(value);



                    UpdateLyricsPosition(Position);



                }



            }



        }







        public double DurationTotalSeconds => Duration.TotalSeconds > 0 ? Duration.TotalSeconds : 100.0;







        public double ProgressPercentage



        {



            get



            {



                if (Duration <= TimeSpan.Zero) return 0;



                double pct = (Position.TotalSeconds / Duration.TotalSeconds) * 100.0;



                return Math.Clamp(pct, 0, 100);



            }



        }







        public string FormattedTime



        {



            get



            {



                if (Duration <= TimeSpan.Zero) return string.Empty;



                string posStr = Position.TotalHours >= 1



                    ? Position.ToString(@"h\:mm\:ss")



                    : Position.ToString(@"m\:ss");



                string durStr = Duration.TotalHours >= 1



                    ? Duration.ToString(@"h\:mm\:ss")



                    : Duration.ToString(@"m\:ss");



                return $"{posStr} / {durStr}";



            }



        }







        // ================= LYRICS PROPERTIES =================



        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(IsCollapsedLyricsActive))]

        private bool _isLyricsExpanded;







        public event EventHandler<bool>? LyricsExpansionChanged;



        public event EventHandler<int>? ActiveLyricChanged;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(IsCollapsedLyricsActive))]



        [NotifyPropertyChangedFor(nameof(IsActiveLyricInterlude))]



        private bool _hasLyrics;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(IsCollapsedLyricsActive))]



        private bool _isLoadingLyrics;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(HasLyricsStatusMessage))]



        private string _lyricsStatusMessage = string.Empty;







        public bool HasLyricsStatusMessage => !string.IsNullOrWhiteSpace(LyricsStatusMessage);




        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(HasLyricsSourceInfo))]

        private string _lyricsSourceInfo = string.Empty;




        public bool HasLyricsSourceInfo => !string.IsNullOrWhiteSpace(LyricsSourceInfo);

        public ObservableCollection<LyricsCandidateViewModel> LyricsCandidates { get; } = new();

        [ObservableProperty]
        private bool _hasLyricsCandidateChoices;

        [ObservableProperty]
        private bool _isLyricsVersionPickerOpen;

        [ObservableProperty]
        private bool _isAutoLyricsCandidateSelected = true;

        [ObservableProperty]
        private string _selectedLyricsVersionLabel = string.Empty;







        public ObservableCollection<LyricsLineViewModel> LyricsLines { get; } = new();







        [ObservableProperty]



        private int _currentLyricIndex = -1;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(CollapsedDisplayLyricText))]



        private string _activeLyricText = string.Empty;







        [ObservableProperty]



        private string _previousLyricText = string.Empty;







        [ObservableProperty]



        private string _upcomingLyricText = string.Empty;







        public LyricsLineViewModel? CurrentLyricLine => (CurrentLyricIndex >= 0 && CurrentLyricIndex < LyricsLines.Count)



            ? LyricsLines[CurrentLyricIndex]



            : null;







        public LyricsLineViewModel? PreviousLyricLine => (CurrentLyricIndex > 0 && CurrentLyricIndex - 1 < LyricsLines.Count)



            ? LyricsLines[CurrentLyricIndex - 1]



            : null;







        public LyricsLineViewModel? UpcomingLyricLine => (CurrentLyricIndex >= 0 && CurrentLyricIndex + 1 < LyricsLines.Count)



            ? LyricsLines[CurrentLyricIndex + 1]



            : null;







        public bool IsActiveLyricInterlude => HasLyrics && (CurrentLyricLine?.IsInterlude ?? false);







        public bool IsCollapsedLyricsActive => IsLyricsExpanded 

            && HasLyrics 



            && LyricsLines.Count > 0 



            && !IsLoadingLyrics;







        public string CollapsedDisplayLyricText => (!string.IsNullOrWhiteSpace(ActiveLyricText) && !IsActiveLyricInterlude)



            ? ActiveLyricText 



            : Title;







        // Display pre-roll: render lyric lines slightly ahead of their LRC timestamp for readability



        public const int DefaultAutoLatencyCompensationMs = 200;







        private int _baseLrcOffsetMs = 0;







        public int TotalLyricOffsetMs => _baseLrcOffsetMs + DefaultAutoLatencyCompensationMs + LyricOffsetMs;







        [ObservableProperty]



        [NotifyPropertyChangedFor(nameof(FormattedLyricOffset))]



        private int _lyricOffsetMs = 0;







        public string FormattedLyricOffset



        {



            get



            {



                if (LyricOffsetMs == 0)



                {



                    return "Auto";



                }



                double seconds = LyricOffsetMs / 1000.0;



                if (LyricOffsetMs > 0) return $"+{seconds:F1}s";



                return $"{seconds:F1}s";



            }



        }







        public double ActiveDot1Opacity => CurrentLyricLine?.Dot1Opacity ?? 0.35;



        public double ActiveDot2Opacity => CurrentLyricLine?.Dot2Opacity ?? 0.35;



        public double ActiveDot3Opacity => CurrentLyricLine?.Dot3Opacity ?? 0.35;







        public MediaViewModel(IMediaService mediaService, ISettingsService? settingsService = null, ILyricsService? lyricsService = null)



        {



            _mediaService = mediaService;



            _settingsService = settingsService;



            _lyricsService = lyricsService ?? new LyricsService();



            _mediaService.MediaTrackChanged += OnMediaTrackChanged;



            _mediaService.PlaybackStateChanged += OnPlaybackStateChanged;



            ThemeManager.DynamicAccentRefreshRequested += OnDynamicAccentRefreshRequested;







            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };



            _timelineTimer.Tick += OnTimelineTimerTick;
            LoadSongOffsets();
            LoadLyricsSelections();



            _pauseGraceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };



            _pauseGraceTimer.Tick += OnPauseGraceTimerTick;







            LocalizationService.Instance.LanguageChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() =>



            {



                OnPropertyChanged(nameof(DisplayTitle));



                OnPropertyChanged(nameof(DisplayArtist));



            });







            if (_settingsService?.CurrentSettings != null)



            {



                _lyricOffsetMs = _settingsService.CurrentSettings.LyricOffsetMs;

                _isLyricsExpanded = _settingsService.CurrentSettings.IsLyricsEnabled;



            }



            if (_mediaService.CurrentTrack != null && _mediaService.CurrentTrack.HasMedia)

            {

                OnMediaTrackChanged(_mediaService, _mediaService.CurrentTrack);

            }



        }







        private void OnMediaTrackChanged(object? sender, MediaTrackInfo track)



        {



            Application.Current?.Dispatcher.Invoke(() =>



            {



                bool isSameTrack = string.Equals(Title, track.Title, StringComparison.OrdinalIgnoreCase) && string.Equals(Artist, track.Artist, StringComparison.OrdinalIgnoreCase);

                Title = track.Title;



                Artist = track.Artist;



                Thumbnail = track.Thumbnail;



                IsPlaying = track.IsPlaying;



                SourceAppId = track.SourceAppId;







                if (!IsSeeking)



                {







                    if (!isSameTrack || !IsPlaying)
                    {
                        string songKey = $"{track.Artist}:::{track.Title}".Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(track.Title) && _songOffsets.TryGetValue(songKey, out int customOffset))
                        {
                            LyricOffsetMs = customOffset;
                        }
                        else
                        {
                            LyricOffsetMs = 0;
                        }
                        _baseTrackPosition = track.Position;
                        if (IsPlaying) _playbackStopwatch.Restart();
                        else _playbackStopwatch.Reset();
                        Position = track.Position;
                    }
                    else
                    {
                        Position = track.Position;
                        _baseTrackPosition = track.Position;
                        _playbackStopwatch.Restart();
                    }
                    



                    Duration = track.Duration;



                }







                HasMedia = track.HasMedia;



                ThemeManager.IsMediaRunning = track.HasMedia;



                UpdateTimerState();



                UpdatePillState();







                if (HasMedia)



                {



                    _restoreAccentCts?.Cancel();

                    _restoreAccentCts?.Dispose();

                    _restoreAccentCts = null;



                    ApplyAlbumArtAccentIfAvailable();



                }



                else



                {



                    _lastLoadedLyricTitle = string.Empty;



                    _lastLoadedLyricArtist = string.Empty;



                    LyricsLines.Clear();



                    HasLyrics = false;



                    IsLoadingLyrics = false;



                    LyricsStatusMessage = string.Empty;

                    LyricsSourceInfo = string.Empty;



                    PreviousLyricText = string.Empty;



                    ActiveLyricText = string.Empty;



                    UpcomingLyricText = string.Empty;



                    CurrentLyricIndex = -1;



                    OnPropertyChanged(nameof(CurrentLyricLine));



                    OnPropertyChanged(nameof(IsActiveLyricInterlude));



                    OnPropertyChanged(nameof(IsCollapsedLyricsActive));



                    OnPropertyChanged(nameof(CollapsedDisplayLyricText));



                    ThemeManager.IsMediaRunning = false;



                    // Debounce restoring default accent (500ms) to prevent flashing default accent during session handoff (e.g. Spotify to YouTube)

                    _restoreAccentCts?.Cancel();

                    _restoreAccentCts?.Dispose();

                    _restoreAccentCts = new CancellationTokenSource();

                    var ct = _restoreAccentCts.Token;

                    _ = Task.Delay(500, ct).ContinueWith(t =>

                    {

                        if (!t.IsCanceled)

                        {

                            Application.Current?.Dispatcher.Invoke(() =>

                            {

                                if (!HasMedia)

                                {

                                    ThemeManager.RestoreDefaultAccent();

                                }

                            });

                        }

                    }, TaskScheduler.Default);



                }







                bool trackChanged = !string.Equals(track.Title, _lastLoadedLyricTitle, StringComparison.OrdinalIgnoreCase) ||



                                   !string.Equals(track.Artist, _lastLoadedLyricArtist, StringComparison.OrdinalIgnoreCase);







                if (trackChanged && HasMedia)



                {



                    _lastLoadedLyricTitle = track.Title ?? string.Empty;



                    _lastLoadedLyricArtist = track.Artist ?? string.Empty;



                    _ = FetchLyricsAsync(track.Title ?? string.Empty, track.Artist ?? string.Empty, track.Duration);



                }



            });



        }







        private void OnDynamicAccentRefreshRequested(object? sender, EventArgs e)



        {



            Application.Current?.Dispatcher.Invoke(ApplyAlbumArtAccentIfAvailable);



        }







        private void ApplyAlbumArtAccentIfAvailable()



        {



            if (HasMedia && (_settingsService?.CurrentSettings?.IsAlbumArtAccentEnabled ?? true))



            {



                if (Thumbnail is System.Windows.Media.Imaging.BitmapSource bmp)



                {



                    _ = Task.Run(() =>



                    {



                        try



                        {



                            var albumAccent = AlbumColorExtractor.ExtractDominantAccentColor(bmp);



                            if (albumAccent.HasValue)



                            {



                                Application.Current?.Dispatcher.Invoke(() =>



                                {



                                    ThemeManager.ApplyDynamicAccent(albumAccent.Value);



                                });



                            }



                        }



                        catch { }



                    });



                }



            }



        }







        private void OnPlaybackStateChanged(object? sender, bool isPlaying)



        {



            Application.Current?.Dispatcher.Invoke(() =>



            {



                IsPlaying = isPlaying;
                if (_lastAnchoredPlaybackState != isPlaying)
                {
                    _lastAnchoredPlaybackState = isPlaying;
                    if (isPlaying)
                    {
                        _baseTrackPosition = Position;
                        _playbackStopwatch.Restart();
                    }
                    else
                    {
                        _playbackStopwatch.Stop();
                        _baseTrackPosition = Position;
                    }
                }



                UpdateTimerState();



                UpdatePillState();



            });



        }







        private int _lastHeaderMinute = -1;







        private void OnTimelineTimerTick(object? sender, EventArgs e)



        {



            // Expanded notch needs smooth progress; collapsed shows only lyrics, 500ms is plenty
            double targetIntervalMs = PetCompanionService.Instance.IsNotchExpanded ? 100.0 : 150.0;
            if (Math.Abs(_timelineTimer.Interval.TotalMilliseconds - targetIntervalMs) > 1.0)
            {
                _timelineTimer.Interval = TimeSpan.FromMilliseconds(targetIntervalMs);
            }



            if (!IsSeeking && IsPlaying && HasMedia && Duration > TimeSpan.Zero)



            {



                if (Position < Duration)



                {



                    var exactPosition = _baseTrackPosition + _playbackStopwatch.Elapsed;
                    if (exactPosition > Duration) exactPosition = Duration;
                    if (exactPosition < TimeSpan.Zero) exactPosition = TimeSpan.Zero;
                    Position = exactPosition;



                    UpdateLyricsPosition(Position);



                }



            }







            int currentMin = DateTime.Now.Minute;



            if (currentMin != _lastHeaderMinute)



            {



                _lastHeaderMinute = currentMin;



                OnPropertyChanged(nameof(CurrentHeaderDateTime));



            }



        }







        private void UpdateTimerState()



        {



            if (IsPlaying && HasMedia && Duration > TimeSpan.Zero)



            {



                if (!_timelineTimer.IsEnabled)



                {



                    _timelineTimer.Start();



                }



            }



            else



            {



                if (_timelineTimer.IsEnabled)



                {



                    _timelineTimer.Stop();



                }



            }



        }







        private void UpdatePillState()



        {



            if (!HasMedia)



            {



                _pauseGraceTimer.Stop();



                if (_isPillActive)



                {



                    _isPillActive = false;



                    OnPropertyChanged(nameof(IsActive));



                }



                return;



            }







            if (IsPlaying)



            {



                _pauseGraceTimer.Stop();



                if (!_isPillActive)



                {



                    _isPillActive = true;



                    OnPropertyChanged(nameof(IsActive));



                }



            }



            else



            {



                // When paused or stopped, keep media pill active for 3 seconds before switching notch to without-media



                if (_isPillActive)



                {



                    _pauseGraceTimer.Stop();



                    _pauseGraceTimer.Start();



                }



            }



        }







        private void OnPauseGraceTimerTick(object? sender, EventArgs e)



        {



            _pauseGraceTimer.Stop();



            if (!IsPlaying && _isPillActive)



            {



                _isPillActive = false;



                OnPropertyChanged(nameof(IsActive));



            }



        }







        private async Task FetchLyricsAsync(string rawTitle, string rawArtist, TimeSpan duration)
        {
            _lyricsCts?.Cancel();
            _lyricsCts?.Dispose();
            _lyricsCts = new CancellationTokenSource();
            var ct = _lyricsCts.Token;

            _baseLrcOffsetMs = 0;
            string songKey = $"{rawArtist}:::{rawTitle}".Trim().ToLowerInvariant();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                LyricsLines.Clear();
                HasLyrics = false;
                IsLoadingLyrics = true;
                LyricsStatusMessage = LocalizationService.Instance["Media.SearchingLyrics"];
                LyricsSourceInfo = string.Empty;
                PreviousLyricText = string.Empty;
                ActiveLyricText = string.Empty;
                UpcomingLyricText = string.Empty;
                CurrentLyricIndex = -1;
                ResetLyricsCandidates();
                OnPropertyChanged(nameof(CurrentLyricLine));
                OnPropertyChanged(nameof(IsActiveLyricInterlude));
                OnPropertyChanged(nameof(IsCollapsedLyricsActive));
                OnPropertyChanged(nameof(CollapsedDisplayLyricText));
            });

            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLoadingLyrics = false;
                    LyricsStatusMessage = string.Empty;
                    LyricsSourceInfo = string.Empty;
                    OnPropertyChanged(nameof(CurrentLyricLine));
                    OnPropertyChanged(nameof(IsActiveLyricInterlude));
                    OnPropertyChanged(nameof(IsCollapsedLyricsActive));
                    OnPropertyChanged(nameof(CollapsedDisplayLyricText));
                });
                return;
            }

            try
            {
                var fetch = await _lyricsService.GetLyricsCandidatesAsync(rawTitle, rawArtist, duration, ct);

                if (ct.IsCancellationRequested) return;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (!string.Equals(Title, rawTitle, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(Artist, rawArtist, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    IsLoadingLyrics = false;

                    if (fetch.Candidates.Count == 0)
                    {
                        HasLyrics = false;
                        LyricsStatusMessage = LocalizationService.Instance["Media.LyricsNotFound"];
                        LyricsSourceInfo = string.Empty;
                        ResetLyricsCandidates();
                    }
                    else
                    {
                        PopulateLyricsCandidates(fetch, songKey);
                        ApplySelectedLyricsCandidate(fetch, songKey);
                    }

                    OnPropertyChanged(nameof(CurrentLyricLine));
                    OnPropertyChanged(nameof(IsActiveLyricInterlude));
                    OnPropertyChanged(nameof(IsCollapsedLyricsActive));
                    OnPropertyChanged(nameof(CollapsedDisplayLyricText));
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error fetching lyrics for {Title}", rawTitle);
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLoadingLyrics = false;
                    HasLyrics = false;
                    LyricsStatusMessage = LocalizationService.Instance["Media.LyricsUnavailable"];
                    LyricsSourceInfo = string.Empty;
                    ResetLyricsCandidates();
                    OnPropertyChanged(nameof(CurrentLyricLine));
                    OnPropertyChanged(nameof(IsActiveLyricInterlude));
                    OnPropertyChanged(nameof(IsCollapsedLyricsActive));
                    OnPropertyChanged(nameof(CollapsedDisplayLyricText));
                });
            }
        }

        private void ResetLyricsCandidates()
        {
            _fetchedLyricsCandidates = new List<LyricsCandidate>();
            _candidatesSongKey = string.Empty;
            _autoLyricsCandidateId = 0;
            LyricsCandidates.Clear();
            HasLyricsCandidateChoices = false;
            IsLyricsVersionPickerOpen = false;
            IsAutoLyricsCandidateSelected = true;
            SelectedLyricsVersionLabel = string.Empty;
        }

        private void PopulateLyricsCandidates(LyricsFetchResult fetch, string songKey)
        {
            _fetchedLyricsCandidates = fetch.Candidates;
            _candidatesSongKey = songKey;
            _autoLyricsCandidateId = fetch.AutoCandidateId;

            LyricsCandidates.Clear();
            foreach (var candidate in fetch.Candidates)
            {
                string durationText = string.Empty;
                if (candidate.Duration.HasValue && candidate.Duration.Value > 0)
                {
                    var duration = TimeSpan.FromSeconds(candidate.Duration.Value);
                    durationText = duration.TotalHours >= 1
                        ? duration.ToString(@"h\:mm\:ss")
                        : duration.ToString(@"m\:ss");
                }

                LyricsCandidates.Add(new LyricsCandidateViewModel
                {
                    Id = candidate.Id,
                    TrackName = candidate.TrackName,
                    ArtistName = candidate.ArtistName,
                    AlbumName = candidate.AlbumName,
                    DurationText = durationText,
                    TypeLabel = candidate.IsInstrumental ? "INSTRUMENTAL" : (candidate.IsSynced ? "SYNCED" : "PLAIN")
                });
            }

            HasLyricsCandidateChoices = LyricsCandidates.Count > 1;
            if (!HasLyricsCandidateChoices)
            {
                IsLyricsVersionPickerOpen = false;
            }
        }

        private void ApplySelectedLyricsCandidate(LyricsFetchResult fetch, string songKey)
        {
            LyricsCandidate? selected = null;
            if (_lyricsSelections.TryGetValue(songKey, out int pinnedId) && pinnedId > 0)
            {
                foreach (var candidate in fetch.Candidates)
                {
                    if (candidate.Id == pinnedId)
                    {
                        selected = candidate;
                        break;
                    }
                }
            }

            bool isAuto = selected == null;
            if (selected == null)
            {
                foreach (var candidate in fetch.Candidates)
                {
                    if (candidate.Id == fetch.AutoCandidateId)
                    {
                        selected = candidate;
                        break;
                    }
                }
                selected ??= fetch.Candidates[0];
            }

            IsAutoLyricsCandidateSelected = isAuto;
            MarkSelectedLyricsCandidate(selected);
            ApplyLyricsResult(selected.Result);
        }

        private void MarkSelectedLyricsCandidate(LyricsCandidate selected)
        {
            int selectedIndex = -1;
            for (int i = 0; i < LyricsCandidates.Count; i++)
            {
                bool isSelected = LyricsCandidates[i].Id == selected.Id;
                LyricsCandidates[i].IsSelected = isSelected;
                if (isSelected) selectedIndex = i;
            }

            SelectedLyricsVersionLabel = selectedIndex >= 0
                ? $"{selectedIndex + 1}/{LyricsCandidates.Count}"
                : string.Empty;
        }

        private void ApplyLyricsResult(LyricsResult result)
        {
            LyricsLines.Clear();
            CurrentLyricIndex = -1;
            PreviousLyricText = string.Empty;
            ActiveLyricText = string.Empty;
            UpcomingLyricText = string.Empty;
            _baseLrcOffsetMs = result.IsSynced ? result.BaseOffsetMs : 0;

            if (result.IsInstrumental)
            {
                HasLyrics = false;
                LyricsStatusMessage = LocalizationService.Instance["Media.Instrumental"];
                LyricsSourceInfo = "lrclib.net · INSTRUMENTAL";
            }
            else if (result.Found && result.Lines.Count > 0)
            {
                HasLyrics = true;
                LyricsStatusMessage = string.Empty;
                LyricsSourceInfo = result.IsSynced ? "lrclib.net · SYNCED" : "lrclib.net · PLAIN";

                if (result.IsSynced)
                {
                    if (result.Lines.Count > 0 && result.Lines[0].Time.TotalSeconds >= 5.0)
                    {
                        LyricsLines.Add(new LyricsLineViewModel(TimeSpan.Zero, "•••", isInterlude: true));
                    }

                    for (int i = 0; i < result.Lines.Count; i++)
                    {
                        var current = result.Lines[i];
                        if (string.IsNullOrWhiteSpace(current.Text))
                        {
                            LyricsLines.Add(new LyricsLineViewModel(current.Time, "•••", isInterlude: true));
                        }
                        else
                        {
                            LyricsLines.Add(new LyricsLineViewModel(current.Time, current.Text));
                        }
                    }
                }
                else
                {
                    double stepSeconds = Duration.TotalSeconds > 10 && result.Lines.Count > 0
                        ? Duration.TotalSeconds / result.Lines.Count
                        : 4.0;

                    for (int i = 0; i < result.Lines.Count; i++)
                    {
                        LyricsLines.Add(new LyricsLineViewModel(TimeSpan.FromSeconds(i * stepSeconds), result.Lines[i].Text));
                    }
                }

                UpdateLyricsPosition(Position);
                ActiveLyricChanged?.Invoke(this, CurrentLyricIndex >= 0 ? CurrentLyricIndex : 0);
                if (IsLyricsExpanded)
                {
                    LyricsExpansionChanged?.Invoke(this, true);
                }
            }
            else
            {
                HasLyrics = false;
                LyricsStatusMessage = LocalizationService.Instance["Media.LyricsNotFound"];
                LyricsSourceInfo = string.Empty;
            }

            OnPropertyChanged(nameof(CurrentLyricLine));
            OnPropertyChanged(nameof(IsActiveLyricInterlude));
            OnPropertyChanged(nameof(IsCollapsedLyricsActive));
            OnPropertyChanged(nameof(CollapsedDisplayLyricText));
        }

        [RelayCommand]
        public void ToggleLyricsVersionPicker()
        {
            if (!HasLyricsCandidateChoices)
            {
                IsLyricsVersionPickerOpen = false;
                return;
            }
            IsLyricsVersionPickerOpen = !IsLyricsVersionPickerOpen;
        }

        [RelayCommand]
        public void SelectLyricsCandidate(LyricsCandidateViewModel? candidate)
        {
            if (candidate == null) return;

            LyricsCandidate? match = null;
            foreach (var item in _fetchedLyricsCandidates)
            {
                if (item.Id == candidate.Id)
                {
                    match = item;
                    break;
                }
            }
            if (match == null) return;

            IsLyricsVersionPickerOpen = false;
            IsAutoLyricsCandidateSelected = false;
            MarkSelectedLyricsCandidate(match);
            ApplyLyricsResult(match.Result);
            SaveLyricSelection(match.Id);
        }

        [RelayCommand]
        public void UseAutoLyricsCandidate()
        {
            IsLyricsVersionPickerOpen = false;
            IsAutoLyricsCandidateSelected = true;
            SaveLyricSelection(null);

            LyricsCandidate? auto = null;
            foreach (var item in _fetchedLyricsCandidates)
            {
                if (item.Id == _autoLyricsCandidateId)
                {
                    auto = item;
                    break;
                }
            }
            if (auto == null && _fetchedLyricsCandidates.Count > 0)
            {
                auto = _fetchedLyricsCandidates[0];
            }
            if (auto == null) return;

            MarkSelectedLyricsCandidate(auto);
            ApplyLyricsResult(auto.Result);
        }

        partial void OnIsLyricsExpandedChanged(bool value)
        {
            if (!value)
            {
                IsLyricsVersionPickerOpen = false;
            }
        }

        private void LoadLyricsSelections()
        {
            try
            {
                if (File.Exists(_lyricsSelectionsPath))
                {
                    var json = File.ReadAllText(_lyricsSelectionsPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _lyricsSelections[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load LyricsSelection.json");
            }
        }

        private void SaveLyricSelection(int? candidateId)
        {
            string songKey = _candidatesSongKey;
            if (string.IsNullOrWhiteSpace(songKey)) return;

            if (candidateId.HasValue && candidateId.Value > 0)
            {
                _lyricsSelections[songKey] = candidateId.Value;
            }
            else
            {
                _lyricsSelections.TryRemove(songKey, out _);
            }
            SaveLyricsSelectionsAsync();
        }

        private void SaveLyricsSelectionsAsync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(_lyricsSelectionsPath)!;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var json = JsonSerializer.Serialize(_lyricsSelections);
                    await File.WriteAllTextAsync(_lyricsSelectionsPath, json);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to write LyricsSelection.json");
                }
            });
        }
        private void UpdateLyricsPosition(TimeSpan currentPos)



        {



            if (!HasLyrics || LyricsLines.Count == 0)



            {



                return;



            }







            // Automatic calibration (embedded LRC offset + system latency compensation) + user manual offset



            int totalOffset = TotalLyricOffsetMs;



            var effectivePos = totalOffset != 0 



                ? currentPos.Add(TimeSpan.FromMilliseconds(totalOffset)) 



                : currentPos;







            int bestIdx = -1;



            for (int i = 0; i < LyricsLines.Count; i++)



            {



                if (LyricsLines[i].Time <= effectivePos)



                {



                    bestIdx = i;



                }



                else



                {



                    break;



                }



            }







            if (bestIdx == -1)



            {



                bestIdx = 0;



            }







            if (bestIdx != CurrentLyricIndex)



            {



                int oldIdx = CurrentLyricIndex;



                CurrentLyricIndex = bestIdx;







                if (oldIdx == -1)



                {



                    for (int i = 0; i < LyricsLines.Count; i++)



                    {



                        LyricsLines[i].SetActive(i == bestIdx);



                    }



                }



                else



                {



                    if (oldIdx >= 0 && oldIdx < LyricsLines.Count)



                    {



                        LyricsLines[oldIdx].SetActive(false);



                    }



                    if (bestIdx >= 0 && bestIdx < LyricsLines.Count)



                    {



                        LyricsLines[bestIdx].SetActive(true);



                    }



                }







                PreviousLyricText = bestIdx > 0 ? LyricsLines[bestIdx - 1].Text : string.Empty;



                ActiveLyricText = LyricsLines[bestIdx].Text;



                UpcomingLyricText = bestIdx + 1 < LyricsLines.Count ? LyricsLines[bestIdx + 1].Text : string.Empty;







                OnPropertyChanged(nameof(CurrentLyricLine));



                OnPropertyChanged(nameof(PreviousLyricLine));



                OnPropertyChanged(nameof(UpcomingLyricLine));



                OnPropertyChanged(nameof(IsActiveLyricInterlude));



                OnPropertyChanged(nameof(CollapsedDisplayLyricText));







                ActiveLyricChanged?.Invoke(this, bestIdx);



            }










        }







        [RelayCommand]



        public void ToggleLyrics()



        {



             IsLyricsExpanded = !IsLyricsExpanded;

            if (_settingsService?.CurrentSettings != null)

             {

                _settingsService.CurrentSettings.IsLyricsEnabled = IsLyricsExpanded;

                _ = Task.Delay(400).ContinueWith(_ => _settingsService.SaveAsync(_settingsService.CurrentSettings));

            }



            OnPropertyChanged(nameof(IsCollapsedLyricsActive));



            OnPropertyChanged(nameof(CollapsedDisplayLyricText));



            LyricsExpansionChanged?.Invoke(this, IsLyricsExpanded);



            if (IsLyricsExpanded && HasLyrics)



            {



                int targetIdx = CurrentLyricIndex >= 0 ? CurrentLyricIndex : 0;



                ActiveLyricChanged?.Invoke(this, targetIdx);



            }



        }







        [RelayCommand]



        public void IncreaseLyricOffset()



        {



            if (LyricOffsetMs < 5000)



            {



                LyricOffsetMs += 100;



                SaveLyricOffset();



                UpdateLyricsPosition(Position);



            }



        }







        [RelayCommand]



        public void DecreaseLyricOffset()



        {



            if (LyricOffsetMs > -5000)



            {



                LyricOffsetMs -= 100;



                SaveLyricOffset();



                UpdateLyricsPosition(Position);



            }



        }







        [RelayCommand]



        public void ResetLyricOffset()



        {



            LyricOffsetMs = 0;



            SaveLyricOffset();



            UpdateLyricsPosition(Position);



        }







                private void SaveLyricOffset()
        {
            string songKey = $"{Artist}:::{Title}".Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(Title))
            {
                if (LyricOffsetMs == 0)
                {
                    _songOffsets.TryRemove(songKey, out _);
                }
                else
                {
                    _songOffsets[songKey] = LyricOffsetMs;
                }
                SaveSongOffsetsAsync();
            }
        }

        private void LoadSongOffsets()
        {
            try
            {
                if (File.Exists(_songOffsetsPath))
                {
                    var json = File.ReadAllText(_songOffsetsPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _songOffsets[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to load SongOffsets.json");
            }
        }

        private void SaveSongOffsetsAsync()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(_songOffsetsPath)!;
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var json = JsonSerializer.Serialize(_songOffsets);
                    await File.WriteAllTextAsync(_songOffsetsPath, json);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Failed to write SongOffsets.json");
                }
            });
        }







        [RelayCommand]



        public async Task SeekToLyricLineAsync(LyricsLineViewModel? line)



        {



            if (line != null && line.Time >= TimeSpan.Zero)



            {



                await SeekToSecondsAsync(line.Time.TotalSeconds);



            }



        }







        [RelayCommand]



        public async Task SeekToActiveLyricAsync()



        {



            if (CurrentLyricLine != null)



            {



                await SeekToLyricLineAsync(CurrentLyricLine);



            }



        }







        [RelayCommand]



        public async Task SeekToPreviousLyricAsync()



        {



            if (PreviousLyricLine != null)



            {



                await SeekToLyricLineAsync(PreviousLyricLine);



            }



        }







        [RelayCommand]



        public async Task SeekToUpcomingLyricAsync()



        {



            if (UpcomingLyricLine != null)



            {



                await SeekToLyricLineAsync(UpcomingLyricLine);



            }



        }







        [RelayCommand]



        public async Task TogglePlayPauseAsync()



        {



            await _mediaService.TogglePlayPauseAsync();



        }







        [RelayCommand]



        public async Task SkipNextAsync()



        {



            await _mediaService.SkipNextAsync();



        }







        [RelayCommand]



        public async Task SkipPreviousAsync()



        {



            await _mediaService.SkipPreviousAsync();



        }







        [RelayCommand]



        public async Task SeekToSecondsAsync(double seconds)



        {



            var target = TimeSpan.FromSeconds(seconds);



            _baseTrackPosition = target;
            if (IsPlaying) _playbackStopwatch.Restart();
            else _playbackStopwatch.Reset();
            Position = target;



            UpdateLyricsPosition(target);



            await _mediaService.SeekToAsync(target);



        }



    }



}



