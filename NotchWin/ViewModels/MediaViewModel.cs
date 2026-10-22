using System;
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
using Serilog;

namespace NotchWin.ViewModels
{
    public partial class MediaViewModel : ObservableObject
    {
        private readonly IMediaService _mediaService;
        private readonly ILyricsService _lyricsService;
        private readonly DispatcherTimer _timelineTimer;
        private CancellationTokenSource? _lyricsCts;
        private string _lastLoadedTrackKey = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _artist = string.Empty;

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
        [NotifyPropertyChangedFor(nameof(FormattedTime))]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        [NotifyPropertyChangedFor(nameof(PositionSeconds))]
        private TimeSpan _position = TimeSpan.Zero;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedTime))]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        [NotifyPropertyChangedFor(nameof(DurationTotalSeconds))]
        private TimeSpan _duration = TimeSpan.Zero;

        // Lyrics Properties
        [ObservableProperty]
        private bool _isLyricsExpanded;

        [ObservableProperty]
        private bool _isLoadingLyrics;

        [ObservableProperty]
        private bool _hasLyrics;

        [ObservableProperty]
        private bool _isSynced;

        [ObservableProperty]
        private string _lyricsStatusMessage = string.Empty;

        [ObservableProperty]
        private int _activeLyricIndex = -1;

        public ObservableCollection<LyricsLineViewModel> LyricsLines { get; } = new();

        public event EventHandler? LyricsToggled;
        public event EventHandler<int>? ActiveLyricChanged;

        public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

        public double PositionSeconds
        {
            get => Position.TotalSeconds;
            set
            {
                if (Math.Abs(Position.TotalSeconds - value) > 0.5)
                {
                    Position = TimeSpan.FromSeconds(value);
                    if (HasLyrics && IsSynced)
                    {
                        UpdateActiveLyric(Position);
                    }
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

        public MediaViewModel(IMediaService mediaService, ILyricsService? lyricsService = null)
        {
            _mediaService = mediaService;
            _lyricsService = lyricsService ?? new LyricsService();

            _mediaService.MediaTrackChanged += OnMediaTrackChanged;
            _mediaService.PlaybackStateChanged += OnPlaybackStateChanged;

            // Timer only runs when media is actively playing to keep CPU idle at 0%
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timelineTimer.Tick += OnTimelineTimerTick;
        }

        partial void OnIsLyricsExpandedChanged(bool value)
        {
            LyricsToggled?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaTrackChanged(object? sender, MediaTrackInfo track)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Title = track.Title;
                Artist = track.Artist;
                Thumbnail = track.Thumbnail;
                IsPlaying = track.IsPlaying;

                if (!IsSeeking)
                {
                    Position = track.Position;
                    Duration = track.Duration;
                }

                HasMedia = track.HasMedia;
                UpdateTimerState();

                if (track.HasMedia && !string.IsNullOrWhiteSpace(track.Title))
                {
                    string trackKey = $"{track.Artist}:::{track.Title}";
                    if (trackKey != _lastLoadedTrackKey)
                    {
                        _lastLoadedTrackKey = trackKey;
                        _ = LoadLyricsAsync(track.Title, track.Artist, track.Duration);
                    }
                }
                else
                {
                    _lastLoadedTrackKey = string.Empty;
                    ResetLyricsState();
                }
            });
        }

        private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsPlaying = isPlaying;
                UpdateTimerState();
            });
        }

        private void OnTimelineTimerTick(object? sender, EventArgs e)
        {
            if (!IsSeeking && IsPlaying && HasMedia && Duration > TimeSpan.Zero)
            {
                if (Position < Duration)
                {
                    Position = Position.Add(TimeSpan.FromSeconds(1));
                    if (HasLyrics && IsSynced)
                    {
                        UpdateActiveLyric(Position);
                    }
                }
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

        private async Task LoadLyricsAsync(string title, string artist, TimeSpan duration)
        {
            _lyricsCts?.Cancel();
            _lyricsCts?.Dispose();
            _lyricsCts = new CancellationTokenSource();
            var token = _lyricsCts.Token;

            IsLoadingLyrics = true;
            LyricsStatusMessage = "🔍 Memuat lirik...";
            HasLyrics = false;
            ActiveLyricIndex = -1;
            LyricsLines.Clear();

            try
            {
                var result = await _lyricsService.GetLyricsAsync(title, artist, duration, token);
                if (token.IsCancellationRequested) return;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLoadingLyrics = false;

                    if (result.Found)
                    {
                        if (result.IsInstrumental)
                        {
                            HasLyrics = false;
                            LyricsStatusMessage = "🎵 Musik Instrumental (Tanpa Lirik)";
                            LyricsLines.Clear();
                        }
                        else if (result.Lines.Count > 0)
                        {
                            HasLyrics = true;
                            IsSynced = result.IsSynced;
                            LyricsStatusMessage = string.Empty;
                            LyricsLines.Clear();

                            foreach (var line in result.Lines)
                            {
                                LyricsLines.Add(new LyricsLineViewModel(line.Time, line.Text));
                            }

                            if (IsSynced)
                            {
                                UpdateActiveLyric(Position);
                            }
                        }
                        else
                        {
                            HasLyrics = false;
                            LyricsStatusMessage = "Lirik tidak ditemukan untuk lagu ini";
                            LyricsLines.Clear();
                        }
                    }
                    else
                    {
                        HasLyrics = false;
                        LyricsStatusMessage = "Lirik tidak ditemukan untuk lagu ini";
                        LyricsLines.Clear();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // Discarded
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while updating lyrics in MediaViewModel");
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsLoadingLyrics = false;
                    HasLyrics = false;
                    LyricsStatusMessage = "Gagal memuat lirik";
                });
            }
        }

        private void ResetLyricsState()
        {
            _lyricsCts?.Cancel();
            IsLoadingLyrics = false;
            HasLyrics = false;
            IsSynced = false;
            LyricsStatusMessage = string.Empty;
            ActiveLyricIndex = -1;
            LyricsLines.Clear();
        }

        private void UpdateActiveLyric(TimeSpan currentPos)
        {
            if (LyricsLines.Count == 0 || !IsSynced) return;

            int newIndex = -1;
            for (int i = 0; i < LyricsLines.Count; i++)
            {
                if (LyricsLines[i].Time <= currentPos)
                {
                    newIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (newIndex != ActiveLyricIndex)
            {
                if (ActiveLyricIndex >= 0 && ActiveLyricIndex < LyricsLines.Count)
                {
                    LyricsLines[ActiveLyricIndex].SetActive(false);
                }

                ActiveLyricIndex = newIndex;

                if (newIndex >= 0 && newIndex < LyricsLines.Count)
                {
                    LyricsLines[newIndex].SetActive(true);
                    ActiveLyricChanged?.Invoke(this, newIndex);
                }
            }
        }

        [RelayCommand]
        public void ToggleLyrics()
        {
            IsLyricsExpanded = !IsLyricsExpanded;
        }

        [RelayCommand]
        public async Task SeekToLyricAsync(LyricsLineViewModel? line)
        {
            if (line != null && IsSynced && line.Time > TimeSpan.Zero)
            {
                await SeekToSecondsAsync(line.Time.TotalSeconds);
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
            Position = target;
            await _mediaService.SeekToAsync(target);
            if (HasLyrics && IsSynced)
            {
                UpdateActiveLyric(target);
            }
        }
    }
}
