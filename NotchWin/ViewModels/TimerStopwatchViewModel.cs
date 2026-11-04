using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Services;
using Serilog;

namespace NotchWin.ViewModels
{
    public class LapItem
    {
        public int LapNumber { get; set; }
        public string TimeText { get; set; } = string.Empty;
        public string DeltaText { get; set; } = string.Empty;
    }

    public partial class TimerStopwatchViewModel : ObservableObject
    {
        private readonly DispatcherTimer _tickTimer;
        private readonly Stopwatch _stopwatch = new();
        private TimeSpan _lastLapTime = TimeSpan.Zero;

        [ObservableProperty]
        private bool _isTimerMode = true;

        #region Timer Properties
        [ObservableProperty]
        private int _totalTimerSeconds = 300; // Default 5 mins

        [ObservableProperty]
        private int _remainingTimerSeconds = 300;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StartPauseText))]
        [NotifyPropertyChangedFor(nameof(IsAnyTimerRunning))]
        [NotifyPropertyChangedFor(nameof(SubNotchDisplay))]
        private bool _isTimerRunning;

        public string StartPauseText => IsTimerRunning
            ? LocalizationService.Instance["Timer.Pause"]
            : (RemainingTimerSeconds > 0 && RemainingTimerSeconds < TotalTimerSeconds
                ? LocalizationService.Instance["Timer.Resume"]
                : LocalizationService.Instance["Timer.Start"]);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubNotchDisplay))]
        private string _timerDisplay = "05:00";

        [ObservableProperty]
        private double _timerProgress = 1.0;

        public event EventHandler? TimerFinished;
        #endregion

        #region Stopwatch Properties
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StopwatchStartPauseText))]
        [NotifyPropertyChangedFor(nameof(IsAnyTimerRunning))]
        [NotifyPropertyChangedFor(nameof(SubNotchDisplay))]
        private bool _isStopwatchRunning;

        public string StopwatchStartPauseText => IsStopwatchRunning
            ? LocalizationService.Instance["Timer.Pause"]
            : (_stopwatch.ElapsedTicks > 0
                ? LocalizationService.Instance["Timer.Resume"]
                : LocalizationService.Instance["Timer.Start"]);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubNotchDisplay))]
        [NotifyPropertyChangedFor(nameof(StopwatchSubNotchDisplay))]
        private string _stopwatchMinutesSeconds = "00:00";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubNotchDisplay))]
        [NotifyPropertyChangedFor(nameof(StopwatchSubNotchDisplay))]
        private string _stopwatchCentiseconds = ".00";

        public ObservableCollection<LapItem> Laps { get; } = new();

        public bool HasLaps => Laps.Count > 0;
        public bool HasNoLaps => Laps.Count == 0;

        public bool IsAnyTimerRunning => IsTimerRunning || IsStopwatchRunning;

        public string SubNotchDisplay => IsTimerRunning
            ? TimerDisplay
            : (IsStopwatchRunning ? $"{StopwatchMinutesSeconds}{StopwatchCentiseconds}" : string.Empty);

        public string StopwatchSubNotchDisplay => $"{StopwatchMinutesSeconds}{StopwatchCentiseconds}";
        #endregion

        private readonly ISoundNotificationService? _soundNotificationService;

        public TimerStopwatchViewModel(ISoundNotificationService? soundNotificationService = null)
        {
            _soundNotificationService = soundNotificationService;
            _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _tickTimer.Tick += OnTick;

            LocalizationService.Instance.LanguageChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(StartPauseText));
                OnPropertyChanged(nameof(StopwatchStartPauseText));
            };
        }

        [RelayCommand]
        public void SwitchMode(string mode)
        {
            IsTimerMode = mode.Equals("timer", StringComparison.OrdinalIgnoreCase);
        }

        #region Timer Commands
        [RelayCommand]
        public void StartPauseTimer()
        {
            if (IsTimerRunning)
            {
                IsTimerRunning = false;
                CheckTimerActive();
                Log.Information("Timer paused at {Display}", TimerDisplay);
            }
            else
            {
                if (RemainingTimerSeconds <= 0)
                {
                    RemainingTimerSeconds = TotalTimerSeconds > 0 ? TotalTimerSeconds : 300;
                }
                _lastTimerSecondCheck = DateTime.Now;
                IsTimerRunning = true;
                _tickTimer.Start();
                Log.Information("Timer started: {Display} ({Seconds}s)", TimerDisplay, RemainingTimerSeconds);
            }
        }

        [RelayCommand]
        public void ResetTimer()
        {
            IsTimerRunning = false;
            RemainingTimerSeconds = TotalTimerSeconds;
            UpdateTimerDisplay();
            CheckTimerActive();
            Log.Information("Timer reset to {Display}", TimerDisplay);
        }

        [RelayCommand]
        public void SetPresetTimer(object? param)
        {
            if (param != null && int.TryParse(param.ToString(), out int mins))
            {
                IsTimerRunning = false;
                TotalTimerSeconds = mins * 60;
                RemainingTimerSeconds = TotalTimerSeconds;
                UpdateTimerDisplay();
                CheckTimerActive();
                Log.Information("Timer preset selected: {Mins} mins", mins);
            }
        }

        [RelayCommand]
        public void AdjustTimer(object? param)
        {
            if (param != null && int.TryParse(param.ToString(), out int deltaSecs))
            {
                int newSecs = Math.Clamp(RemainingTimerSeconds + deltaSecs, 10, 3600 * 24);
                RemainingTimerSeconds = newSecs;
                if (newSecs > TotalTimerSeconds) TotalTimerSeconds = newSecs;
                UpdateTimerDisplay();
                Log.Information("Timer adjusted by {Delta}s to {Display}", deltaSecs, TimerDisplay);
            }
        }

        private void UpdateTimerDisplay()
        {
            int m = RemainingTimerSeconds / 60;
            int s = RemainingTimerSeconds % 60;
            TimerDisplay = $"{m:D2}:{s:D2}";
            TimerProgress = TotalTimerSeconds > 0 ? (double)RemainingTimerSeconds / TotalTimerSeconds : 0.0;
        }
        #endregion

        #region Stopwatch Commands
        [RelayCommand]
        public void StartPauseStopwatch()
        {
            if (IsStopwatchRunning)
            {
                _stopwatch.Stop();
                IsStopwatchRunning = false;
                CheckTimerActive();
                Log.Information("Stopwatch paused at {Elapsed}", _stopwatch.Elapsed);
            }
            else
            {
                _stopwatch.Start();
                IsStopwatchRunning = true;
                _tickTimer.Start();
                Log.Information("Stopwatch started");
            }
        }

        [RelayCommand]
        public void ResetStopwatch()
        {
            _stopwatch.Reset();
            IsStopwatchRunning = false;
            _lastLapTime = TimeSpan.Zero;
            StopwatchMinutesSeconds = "00:00";
            StopwatchCentiseconds = ".00";
            Laps.Clear();
            CheckTimerActive();
            OnPropertyChanged(nameof(HasLaps));
            OnPropertyChanged(nameof(HasNoLaps));
            Log.Information("Stopwatch reset");
        }

        [RelayCommand]
        public void AddLap()
        {
            if (!IsStopwatchRunning) return;

            var elapsed = _stopwatch.Elapsed;
            var delta = elapsed - _lastLapTime;
            _lastLapTime = elapsed;

            Laps.Insert(0, new LapItem
            {
                LapNumber = Laps.Count + 1,
                TimeText = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}.{elapsed.Milliseconds / 10:D2}",
                DeltaText = $"+{delta.Minutes:D2}:{delta.Seconds:D2}.{delta.Milliseconds / 10:D2}"
            });
            OnPropertyChanged(nameof(HasLaps));
            OnPropertyChanged(nameof(HasNoLaps));
        }
        #endregion

        private DateTime _lastTimerSecondCheck = DateTime.MinValue;

        private void OnTick(object? sender, EventArgs e)
        {
            // Timer Tick
            if (IsTimerRunning)
            {
                if ((DateTime.Now - _lastTimerSecondCheck).TotalSeconds >= 1.0)
                {
                    _lastTimerSecondCheck = DateTime.Now;
                    if (RemainingTimerSeconds > 0)
                    {
                        RemainingTimerSeconds--;
                        UpdateTimerDisplay();
                        if (RemainingTimerSeconds == 0)
                        {
                            IsTimerRunning = false;
                            Log.Information("Timer reached 0! Triggered alarm");
                            TimerFinished?.Invoke(this, EventArgs.Empty);
                            if (_soundNotificationService != null)
                            {
                                _soundNotificationService.PlayTimerAlarm();
                            }
                            else
                            {
                                System.Media.SystemSounds.Exclamation.Play();
                            }
                        }
                    }
                }
            }

            // Stopwatch Tick
            if (IsStopwatchRunning)
            {
                var el = _stopwatch.Elapsed;
                StopwatchMinutesSeconds = $"{el.Minutes:D2}:{el.Seconds:D2}";
                StopwatchCentiseconds = $".{el.Milliseconds / 10:D2}";
            }

            CheckTimerActive();
        }

        private void CheckTimerActive()
        {
            if (!IsTimerRunning && !IsStopwatchRunning)
            {
                _tickTimer.Stop();
            }
            else
            {
                // Dynamic interval: 50ms when stopwatch is active (needs centiseconds), 250ms when only timer is active
                _tickTimer.Interval = IsStopwatchRunning ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromMilliseconds(250);
            }
        }
    }
}
