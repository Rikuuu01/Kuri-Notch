using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class WaterTrackerViewModel : ObservableObject
    {
        private readonly IWaterTrackerService _waterService;

        [ObservableProperty]
        private int _currentIntakeMl;

        [ObservableProperty]
        private int _dailyGoalMl = 2000;

        [ObservableProperty]
        private double _progressPercentage;

        [ObservableProperty]
        private string _intakeText = "0 / 2,000 ml";

        [ObservableProperty]
        private string _currentIntakeLitersText = "0.0 L";

        [ObservableProperty]
        private string _dailyGoalLitersText = "/ 2.0 L";

        [ObservableProperty]
        private string _percentageText = "0%";

        [ObservableProperty]
        private string _glassesCountText = "0 / 8 Glasses";

        [ObservableProperty]
        private string _countdownText = "--:--";

        [ObservableProperty]
        private string _sittingDurationText = "0m";

        [ObservableProperty]
        private double _sittingProgress;

        [ObservableProperty]
        private string _sittingStatusText = "Optimal Posture";

        [ObservableProperty]
        private bool _isAlarmActive;

        [ObservableProperty]
        private bool _isUserIdle;

        [ObservableProperty]
        private bool _isSittingCardFlipped;

        [ObservableProperty]
        private string _totalSittingTodayText = "0m";

        [ObservableProperty]
        private string _longestSessionTodayText = "0m";

        [ObservableProperty]
        private string _breaksCountTodayText = "0x";

        [ObservableProperty]
        private string _statusMessage = "Stay hydrated while staying focused 💧";

        public bool CanUndo => _waterService.State.LastAddedMl > 0;

        /// <summary>
        /// When false (e.g. notch is collapsed or idle), 1-second background countdown ticks do not marshal to UI thread.
        /// </summary>
        public bool IsUiActive { get; set; } = false;

        public void RefreshUi()
        {
            UpdateTicker();
        }

        public WaterTrackerViewModel(IWaterTrackerService waterService)
        {
            _waterService = waterService;
            _waterService.StateChanged += (_, s) => Application.Current?.Dispatcher.Invoke(() => ApplyState(s));
            _waterService.CountdownTicked += (_, _) =>
            {
                if (IsUiActive)
                {
                    Application.Current?.Dispatcher.Invoke(UpdateTicker);
                }
            };
            LocalizationService.Instance.LanguageChanged += (_, _) => Application.Current?.Dispatcher.Invoke(() =>
            {
                ApplyState(_waterService.State);
                UpdateTicker();
            });

            ApplyState(_waterService.State);
            UpdateTicker();
        }

        private void ApplyState(WaterTrackerState state)
        {
            CurrentIntakeMl = state.CurrentIntakeMl;
            DailyGoalMl = state.DailyGoalMl > 0 ? state.DailyGoalMl : 2000;
            ProgressPercentage = Math.Clamp((double)CurrentIntakeMl / DailyGoalMl, 0.0, 1.0);
            
            // Liters formatting
            double liters = CurrentIntakeMl / 1000.0;
            CurrentIntakeLitersText = liters == 0.0 ? "0.0 L" : (liters % 1 == 0 ? $"{liters:0.0} L" : $"{liters:0.##} L");
            DailyGoalLitersText = $"/ {(DailyGoalMl / 1000.0):0.#} L";
            
            IntakeText = $"{CurrentIntakeMl:N0} / {DailyGoalMl:N0} ml";
            PercentageText = $"{Math.Round(ProgressPercentage * 100):0}%";

            int glassesDone = (int)Math.Floor(CurrentIntakeMl / 250.0);
            int totalGlasses = (int)Math.Ceiling(DailyGoalMl / 250.0);
            GlassesCountText = LocalizationService.Instance.Get("Water.GlassesLog", glassesDone, totalGlasses);
            OnPropertyChanged(nameof(CanUndo));
        }

        private void UpdateTicker()
        {
            var remaining = _waterService.TimeUntilNextDrink;
            var sitting = _waterService.SittingDuration;
            int interval = _waterService.IntervalMinutes > 0 ? _waterService.IntervalMinutes : 45;
            string hourSuffix = string.Equals(LocalizationService.Instance.CurrentLanguage, "id", StringComparison.OrdinalIgnoreCase) ? "j" : "h";

            CountdownText = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            
            int totalMins = (int)sitting.TotalMinutes;
            if (totalMins >= 60)
            {
                SittingDurationText = $"{totalMins / 60}{hourSuffix} {totalMins % 60}m";
            }
            else
            {
                SittingDurationText = $"{totalMins}m";
            }

            SittingProgress = Math.Clamp(sitting.TotalMinutes / interval, 0.0, 1.0);
            IsUserIdle = _waterService.IsUserIdle;

            IsAlarmActive = _waterService.IsAlarmActive;
            if (IsAlarmActive)
            {
                StatusMessage = LocalizationService.Instance["Water.StatusOverdue"];
                SittingStatusText = LocalizationService.Instance["Sedentary.StatusOverdue"];
            }
            else if (IsUserIdle)
            {
                StatusMessage = LocalizationService.Instance["Water.StatusStandby"];
                SittingStatusText = LocalizationService.Instance["Sedentary.StatusStandby"];
            }
            else if (sitting.TotalMinutes >= interval * 0.75)
            {
                StatusMessage = LocalizationService.Instance["Water.StatusWarning"];
                SittingStatusText = LocalizationService.Instance["Sedentary.StatusWarning"];
            }
            else
            {
                StatusMessage = LocalizationService.Instance["Water.StatusNormal"];
                SittingStatusText = LocalizationService.Instance["Sedentary.StatusOptimal"];
            }

            // Update daily sitting statistics for flipped card
            int totalSecs = _waterService.State.TotalSittingSecondsToday;
            int totalHours = totalSecs / 3600;
            int totalRemainderMins = (totalSecs % 3600) / 60;
            TotalSittingTodayText = totalHours > 0 ? $"{totalHours}{hourSuffix} {totalRemainderMins}m" : $"{totalRemainderMins}m";

            int longestSecs = _waterService.State.LongestSessionSecondsToday;
            int lHours = longestSecs / 3600;
            int lMins = (longestSecs % 3600) / 60;
            LongestSessionTodayText = lHours > 0 ? $"{lHours}{hourSuffix} {lMins}m" : $"{lMins}m";

            BreaksCountTodayText = LocalizationService.Instance.Get("Sedentary.BreaksUnit", _waterService.State.BreaksCountToday);
        }

        [RelayCommand]
        public void ToggleSittingFlip()
        {
            IsSittingCardFlipped = !IsSittingCardFlipped;
        }

        [RelayCommand]
        public void AddWater(object? param)
        {
            if (param != null && int.TryParse(param.ToString(), out int ml))
            {
                _waterService.AddWater(ml);
            }
            else
            {
                _waterService.AddWater(250); // Default 1 glass
            }
            OnPropertyChanged(nameof(CanUndo));
        }

        [RelayCommand]
        public void Snooze()
        {
            _waterService.Snooze(2);
        }

        [RelayCommand]
        public void DismissAlarm()
        {
            _waterService.DismissAlarm();
        }

        [RelayCommand]
        public void ResetSitting()
        {
            _waterService.ResetSitting();
        }

        [RelayCommand]
        public void Undo()
        {
            _waterService.UndoLast();
            OnPropertyChanged(nameof(CanUndo));
        }

        [RelayCommand]
        public void Reset()
        {
            _waterService.ResetToday();
            OnPropertyChanged(nameof(CanUndo));
        }
    }
}
