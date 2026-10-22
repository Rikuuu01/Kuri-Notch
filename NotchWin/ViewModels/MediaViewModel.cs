using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class MediaViewModel : ObservableObject
    {
        private readonly IMediaService _mediaService;
        private readonly DispatcherTimer _timelineTimer;

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
        [NotifyPropertyChangedFor(nameof(FormattedTime))]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private TimeSpan _position = TimeSpan.Zero;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FormattedTime))]
        [NotifyPropertyChangedFor(nameof(ProgressPercentage))]
        private TimeSpan _duration = TimeSpan.Zero;

        public string PlayPauseIcon => IsPlaying ? "⏸" : "▶";

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

        public MediaViewModel(IMediaService mediaService)
        {
            _mediaService = mediaService;
            _mediaService.MediaTrackChanged += OnMediaTrackChanged;
            _mediaService.PlaybackStateChanged += OnPlaybackStateChanged;

            // Timer only runs when media is actively playing to keep CPU idle at 0%
            _timelineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timelineTimer.Tick += OnTimelineTimerTick;
        }

        private void OnMediaTrackChanged(object? sender, MediaTrackInfo track)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Title = track.Title;
                Artist = track.Artist;
                Thumbnail = track.Thumbnail;
                IsPlaying = track.IsPlaying;
                Position = track.Position;
                Duration = track.Duration;
                HasMedia = track.HasMedia;

                UpdateTimerState();
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
            if (IsPlaying && HasMedia && Duration > TimeSpan.Zero)
            {
                if (Position < Duration)
                {
                    Position = Position.Add(TimeSpan.FromSeconds(1));
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
    }
}
