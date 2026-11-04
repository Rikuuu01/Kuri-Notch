using System;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Services;
using NotchWin.Widgets;

namespace NotchWin.ViewModels
{
    public partial class ClockViewModel : ObservableObject, INotchWidget
    {
        private readonly DispatcherTimer _timer;
        private static readonly CultureInfo IdCulture = new("id-ID");
        private static readonly CultureInfo EnCulture = new("en-US");

        public WidgetType Type => WidgetType.Clock;
        public string Name => "Clock";
        public int Priority => 0;
        public bool IsActive => true;

        [ObservableProperty]
        private string _currentTime = string.Empty;

        [ObservableProperty]
        private string _currentDate = string.Empty;

        [ObservableProperty]
        private string _fullDate = string.Empty;

        public ClockViewModel()
        {
            UpdateDateTime();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                UpdateDateTime();
                ScheduleNextTick();
            };
            ScheduleNextTick();
            _timer.Start();

            LocalizationService.Instance.LanguageChanged += (s, e) =>
            {
                _lastDate = DateTime.MinValue;
                _lastMinute = -1;
                UpdateDateTime();
                ScheduleNextTick();
            };
        }

        /// <summary>Tick tepat setelah pergantian menit; cukup satu wake-up per menit saat idle.</summary>
        private void ScheduleNextTick()
        {
            var now = DateTime.Now;
            double msUntilNextMinute = 60000 - (now.Second * 1000 + now.Millisecond);
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(msUntilNextMinute + 120, 250));
        }

        public void Pause()
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
            }
        }

        public void Resume()
        {
            UpdateDateTime();
            if (!_timer.IsEnabled)
            {
                ScheduleNextTick();
                _timer.Start();
            }
        }

        private DateTime _lastDate = DateTime.MinValue;
        private int _lastMinute = -1;

        private void UpdateDateTime()
        {
            var now = DateTime.Now;
            if (now.Minute != _lastMinute)
            {
                _lastMinute = now.Minute;
                CurrentTime = now.ToString("HH:mm");
            }

            if (now.Date != _lastDate)
            {
                _lastDate = now.Date;
                var culture = string.Equals(LocalizationService.Instance.CurrentLanguage, "id", StringComparison.OrdinalIgnoreCase)
                    ? IdCulture
                    : EnCulture;

                CurrentDate = now.ToString("ddd, d MMM", culture);
                FullDate = now.ToString("dddd, d MMMM yyyy", culture);
            }
        }
    }
}
