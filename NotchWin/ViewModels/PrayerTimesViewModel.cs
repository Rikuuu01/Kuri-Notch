using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class PrayerTimesViewModel : ObservableObject
    {
        private readonly IPrayerTimesService _prayerService;
        private readonly ISettingsService _settingsService;
        private DispatcherTimer? _liveCountdownTimer;

        [ObservableProperty]
        private string _cityName = "Jakarta";

        [ObservableProperty]
        private string _subuhTime = "04:30";

        [ObservableProperty]
        private string _dhuhaTime = "06:15";

        [ObservableProperty]
        private string _dzuhurTime = "11:55";

        [ObservableProperty]
        private string _asharTime = "15:10";

        [ObservableProperty]
        private string _maghribTime = "18:00";

        [ObservableProperty]
        private string _isyaTime = "19:10";

        [ObservableProperty]
        private string _imsakTime = "04:20";

        [ObservableProperty]
        private string _nextPrayerName = "Dzuhur";

        [ObservableProperty]
        private string _nextPrayerCountdown = "00:00:00";

        [ObservableProperty]
        private int _activePrayerIndex = 2;

        public bool IsSubuhActive => ActivePrayerIndex == 0;
        public bool IsDhuhaActive => ActivePrayerIndex == 1;
        public bool IsDzuhurActive => ActivePrayerIndex == 2;
        public bool IsAsharActive => ActivePrayerIndex == 3;
        public bool IsMaghribActive => ActivePrayerIndex == 4;
        public bool IsIsyaActive => ActivePrayerIndex == 5;

        public PrayerTimesViewModel(IPrayerTimesService prayerService, ISettingsService settingsService)
        {
            _prayerService = prayerService;
            _settingsService = settingsService;

            _prayerService.ScheduleUpdated += OnScheduleUpdated;
            LocalizationService.Instance.LanguageChanged += (_, _) =>
            {
                if (_prayerService.CurrentSchedule != null)
                {
                    App.Current?.Dispatcher?.Invoke(() => ApplySchedule(_prayerService.CurrentSchedule));
                }
            };
            if (_prayerService.CurrentSchedule != null)
            {
                ApplySchedule(_prayerService.CurrentSchedule);
            }
        }

        private void OnScheduleUpdated(object? sender, PrayerSchedule s)
        {
            App.Current?.Dispatcher?.Invoke(() => ApplySchedule(s));
        }

        private void ApplySchedule(PrayerSchedule s)
        {
            CityName = _settingsService.CurrentSettings.PrayerCityPreset;
            SubuhTime = s.Subuh.ToString(@"hh\:mm");
            DhuhaTime = s.Dhuha.ToString(@"hh\:mm");
            DzuhurTime = s.Dzuhur.ToString(@"hh\:mm");
            AsharTime = s.Ashar.ToString(@"hh\:mm");
            MaghribTime = s.Maghrib.ToString(@"hh\:mm");
            IsyaTime = s.Isya.ToString(@"hh\:mm");
            ImsakTime = s.Imsak.ToString(@"hh\:mm");

            NextPrayerName = GetLocalizedPrayerName(s.NextPrayerName);
            var r = s.RemainingToNext;
            NextPrayerCountdown = $"{Math.Floor(r.TotalHours):00}:{r.Minutes:D2}:{r.Seconds:D2}";
            ActivePrayerIndex = s.ActivePrayerIndex;

            OnPropertyChanged(nameof(IsSubuhActive));
            OnPropertyChanged(nameof(IsDhuhaActive));
            OnPropertyChanged(nameof(IsDzuhurActive));
            OnPropertyChanged(nameof(IsAsharActive));
            OnPropertyChanged(nameof(IsMaghribActive));
            OnPropertyChanged(nameof(IsIsyaActive));
        }

        public void StartLiveCountdown()
        {
            if (_liveCountdownTimer == null)
            {
                _liveCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _liveCountdownTimer.Tick += (s, e) => UpdateCountdown();
            }
            if (!_liveCountdownTimer.IsEnabled)
            {
                _liveCountdownTimer.Start();
                UpdateCountdown();
            }
        }

        public void StopLiveCountdown()
        {
            _liveCountdownTimer?.Stop();
        }

        private void UpdateCountdown()
        {
            var s = _prayerService.CurrentSchedule;
            if (s != null)
            {
                var r = s.NextPrayerTime - DateTime.Now.TimeOfDay;
                if (r < TimeSpan.Zero) r += TimeSpan.FromDays(1);
                NextPrayerCountdown = $"{Math.Floor(r.TotalHours):00}:{r.Minutes:D2}:{r.Seconds:D2}";
            }
        }

        private static string GetLocalizedPrayerName(string name)
        {
            if (name.Contains("Besok") || name.Contains("Tomorrow"))
            {
                return $"{LocalizationService.Instance["Prayer.Subuh"]} ({LocalizationService.Instance["Prayer.Tomorrow"]})";
            }

            return name switch
            {
                "Subuh" => LocalizationService.Instance["Prayer.Subuh"],
                "Dhuha" => LocalizationService.Instance["Prayer.Dhuha"],
                "Dzuhur" => LocalizationService.Instance["Prayer.Dhuhr"],
                "Ashar" => LocalizationService.Instance["Prayer.Asr"],
                "Maghrib" => LocalizationService.Instance["Prayer.Maghrib"],
                "Isya" => LocalizationService.Instance["Prayer.Isha"],
                _ => name
            };
        }
    }
}
