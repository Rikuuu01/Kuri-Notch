using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class CalendarDayItem : ObservableObject
    {
        [ObservableProperty]
        private string _dayLetter = string.Empty;

        [ObservableProperty]
        private int _dayNumber;

        [ObservableProperty]
        private bool _isToday;

        [ObservableProperty]
        private bool _isSunday;

        [ObservableProperty]
        private DateTime _date;
    }

    public partial class HomeCalendarViewModel : ObservableObject
    {
        private static readonly CultureInfo IdCulture = new("id-ID");
        private readonly DispatcherTimer _dayCheckTimer;

        [ObservableProperty]
        private string _currentMonth = string.Empty;

        [ObservableProperty]
        private string _currentYear = string.Empty;

        [ObservableProperty]
        private string _todaySummary = string.Empty;

        [ObservableProperty]
        private string _agendaSummary = "Tidak ada agenda hari ini";

        public ObservableCollection<CalendarDayItem> WeekDays { get; } = new();

        public HomeCalendarViewModel()
        {
            UpdateWeekView();

            LocalizationService.Instance.LanguageChanged += (_, _) =>
            {
                UpdateWeekView();
            };

            // Check every minute if the date transitioned across midnight
            _dayCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _dayCheckTimer.Tick += (s, e) => UpdateWeekView();
            _dayCheckTimer.Start();
        }

        public void UpdateWeekView()
        {
            var now = DateTime.Now;
            bool isIndonesian = LocalizationService.Instance.CurrentLanguage == LocalizationService.LangId;
            var culture = isIndonesian ? IdCulture : CultureInfo.InvariantCulture;

            CurrentMonth = now.ToString("MMMM", culture);
            CurrentYear = now.ToString("yyyy", culture);
            TodaySummary = now.ToString("dddd, d MMM", culture);

            // Week starts on Sunday (Minggu): Sunday = 0
            int dayOfWeek = (int)now.DayOfWeek;
            var startOfWeek = now.Date.AddDays(-dayOfWeek);

            string[] dayLetters = isIndonesian
                ? new[] { "M", "S", "S", "R", "K", "J", "S" }
                : new[] { "S", "M", "T", "W", "T", "F", "S" };

            if (WeekDays.Count != 7)
            {
                WeekDays.Clear();
                for (int i = 0; i < 7; i++)
                {
                    WeekDays.Add(new CalendarDayItem());
                }
            }

            for (int i = 0; i < 7; i++)
            {
                var dayDate = startOfWeek.AddDays(i);
                var item = WeekDays[i];
                item.DayLetter = dayLetters[i];
                item.DayNumber = dayDate.Day;
                item.IsToday = (dayDate.Date == now.Date);
                item.IsSunday = (i == 0);
                item.Date = dayDate;
            }
        }
    }
}
