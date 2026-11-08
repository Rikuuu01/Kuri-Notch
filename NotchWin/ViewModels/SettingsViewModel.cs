using System;

using System.Collections.ObjectModel;

using System.IO;

using System.Linq;

using System.Threading.Tasks;

using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using NotchWin.Models;

using NotchWin.Services;

using Serilog;



namespace NotchWin.ViewModels

{

    public partial class SettingsViewModel : ObservableObject

    {

        private readonly ISettingsService _settingsService;

        private readonly IAutostartService _autostartService;

        private readonly IDisplayService _displayService;

        private readonly ILocationSearchService _locationSearchService = new LocationSearchService();



        private bool _isInitializing = true;



        public LocalizationService L10n => LocalizationService.Instance;



        public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new()

        {

            new LanguageOption { Code = "en", DisplayName = "English (Default)" },

            new LanguageOption { Code = "id", DisplayName = "Bahasa Indonesia" }

        };

        public ObservableCollection<NavbarModuleItemViewModel> NavbarModules { get; } = new();

        [RelayCommand]
        public void MoveModuleUp(NavbarModuleItemViewModel? item)
        {
            if (item == null) return;
            int idx = NavbarModules.IndexOf(item);
            if (idx > 0)
            {
                NavbarModules.Move(idx, idx - 1);
                UpdateModuleOrderState();
                AutoSave();
            }
        }

        [RelayCommand]
        public void MoveModuleDown(NavbarModuleItemViewModel? item)
        {
            if (item == null) return;
            int idx = NavbarModules.IndexOf(item);
            if (idx >= 0 && idx < NavbarModules.Count - 1)
            {
                NavbarModules.Move(idx, idx + 1);
                UpdateModuleOrderState();
                AutoSave();
            }
        }

        [RelayCommand]
        public void ClearScreenTimeHistory()
        {
            var vm = System.Windows.Application.Current?.MainWindow?.DataContext as NotchViewModel;
            vm?.ScreenTimeService?.ClearAllHistory();
            System.Windows.MessageBox.Show(L10n["Settings.ScreenTimeCleared"], L10n["Settings.CategoryScreenTime"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        public void ResetNavbarModules()
        {
            _isInitializing = true;
            try
            {
                LoadNavbarModules(AppSettings.GetDefaultNavbarModules());
            }
            finally
            {
                _isInitializing = false;
            }
            AutoSave();
        }

        private void LoadNavbarModules(System.Collections.Generic.List<NavbarModuleConfig>? configs)
        {
            NavbarModules.Clear();
            var list = configs ?? AppSettings.GetDefaultNavbarModules();
            var defaults = AppSettings.GetDefaultNavbarModules();

            var fullList = new System.Collections.Generic.List<NavbarModuleConfig>(list);
            foreach (var def in defaults)
            {
                if (!fullList.Any(m => string.Equals(m.Id, def.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    fullList.Add(def);
                }
            }

            foreach (var cfg in fullList)
            {
                var info = GetModuleDisplayInfo(cfg.Id);
                bool enabled = string.Equals(cfg.Id, "home", StringComparison.OrdinalIgnoreCase) || cfg.IsEnabled;
                var item = new NavbarModuleItemViewModel(cfg.Id, info.Title, info.Desc, info.Icon, enabled, OnModuleItemChanged);
                NavbarModules.Add(item);
            }
            UpdateModuleOrderState();
        }

        private void OnModuleItemChanged()
        {
            if (_isInitializing) return;
            var prayerItem = NavbarModules.FirstOrDefault(m => string.Equals(m.Id, "prayer", StringComparison.OrdinalIgnoreCase));
            if (prayerItem != null && IsPrayerReminderEnabled != prayerItem.IsEnabled)
            {
                _isInitializing = true;
                IsPrayerReminderEnabled = prayerItem.IsEnabled;
                _isInitializing = false;
            }
            AutoSave();
        }

        private void UpdateModuleOrderState()
        {
            for (int i = 0; i < NavbarModules.Count; i++)
            {
                NavbarModules[i].OrderNumber = i + 1;
                NavbarModules[i].CanMoveUp = i > 0;
                NavbarModules[i].CanMoveDown = i < NavbarModules.Count - 1;
            }
        }

        private (string Title, string Desc, string Icon) GetModuleDisplayInfo(string id) => id.ToLowerInvariant() switch
        {
            "home" => (L10n["Settings.ModuleHome"], L10n["Settings.ModuleHomeDesc"], "🏠"),
            "control" => (L10n["Settings.ModuleControl"], L10n["Settings.ModuleControlDesc"], "🎛️"),
            "media" => (L10n["Settings.ModuleMedia"], L10n["Settings.ModuleMediaDesc"], "🎵"),
            "weather" => (L10n["Settings.ModuleWeather"], L10n["Settings.ModuleWeatherDesc"], "⛅"),
            "prayer" => (L10n["Settings.ModulePrayer"], L10n["Settings.ModulePrayerDesc"], "🕌"),
            "water" => (L10n["Settings.ModuleWater"], L10n["Settings.ModuleWaterDesc"], "💧"),
            "note" => (L10n["Settings.ModuleNote"], L10n["Settings.ModuleNoteDesc"], "📝"),
            "clipboard" => (L10n["Settings.ModuleClipboard"], L10n["Settings.ModuleClipboardDesc"], "📋"),
            "timer" => (L10n["Settings.ModuleTimer"], L10n["Settings.ModuleTimerDesc"], "⏱️"),
            "shelf" => (L10n["Settings.ModuleShelf"], L10n["Settings.ModuleShelfDesc"], "📁"),
            _ => (id, string.Empty, "📦")
        };



        [ObservableProperty]

        private LanguageOption? _selectedLanguageOption;



        partial void OnSelectedLanguageOptionChanged(LanguageOption? value)

        {

            if (value != null && !_isInitializing)

            {

                LocalizationService.Instance.CurrentLanguage = value.Code;

                UpdateLocalizedDisplayStrings();

                AutoSave();

            }

        }



        [ObservableProperty]

        private int _selectedCategoryIndex = 0;



        [RelayCommand]

        public void SelectCategory(object? param)

        {

            if (param != null && int.TryParse(param.ToString(), out int idx))

            {

                SelectedCategoryIndex = idx;

            }

        }



        [ObservableProperty]

        private double _notchScale = 1.0;

        [ObservableProperty]
        private double _fontScale = 1.0;

        public double PreviewFontSizeTitle => Math.Round(11.0 * FontScale, 1);
        public double PreviewFontSizeArtist => Math.Round(9.5 * FontScale, 1);
        public double PreviewFontSizeNormal => Math.Round(11.0 * FontScale, 1);



        [ObservableProperty]

        private int _hoverCollapseDelayMs;



        [ObservableProperty]

        private bool _isAutostartEnabled;



        [ObservableProperty]

        private bool _startupSoundEnabled = true;



        [ObservableProperty]

        private bool _isFullscreenAutoHideEnabled;

        [ObservableProperty]
        private bool _isFullscreenSlimBarEnabled;



        [ObservableProperty]

        private bool _isMediaWidgetEnabled;

        [ObservableProperty]
        private bool _isLyricsKaraokeEnabled = true;

        [ObservableProperty]
        private bool _isSyllableSyncEnabled = true;

        [ObservableProperty]
        private bool _isLrcRedEnabled = true;

        [ObservableProperty]
        private string _spicyLyricsApiKey = string.Empty;

        [ObservableProperty]
        private bool _showSecondLyricLine = true;

        [ObservableProperty]
        private bool _lyricsBold = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LyricsTextSizeDisplay))]
        private double _lyricsTextSize = 12.0;

        public string LyricsTextSizeDisplay => $"{LyricsTextSize:0}";

        [ObservableProperty]

        private bool _isPrivacyIndicatorEnabled;

        [ObservableProperty]

        private bool _isDownloadMonitorEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NotificationDurationDisplay))]
        private int _notificationDurationSeconds = 3;

        public string NotificationDurationDisplay => L10n.Get("Settings.SecondsFormat", NotificationDurationSeconds);



        [ObservableProperty]

        private bool _shelfRemoveAfterDrag;

        [ObservableProperty]
        private bool _isPhoneShareEnabled = true;

        [ObservableProperty]
        private bool _isScreenTimeEnabled = true;

        [ObservableProperty]
        private int _screenTimeIdleSeconds = 120;

        [ObservableProperty]
        private bool _isNotchTimerGlowEnabled = true;

        [ObservableProperty]
        private bool _isNotchAlertGlowEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PhoneFolderDisplay))]
        private string _phoneFolder = string.Empty;

        public string PhoneFolderDisplay => string.IsNullOrWhiteSpace(PhoneFolder)
            ? Path.Combine(NotchWin.Interop.NativeMethods.GetKnownDownloadsFolder(), "Kuri Shelf")
            : PhoneFolder;

        [RelayCommand]
        public void BrowsePhoneFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = L10n.Get("Settings.PhoneFolderTitle"),
                InitialDirectory = Directory.Exists(PhoneFolderDisplay) ? PhoneFolderDisplay : string.Empty
            };

            if (dialog.ShowDialog() == true)
            {
                PhoneFolder = dialog.FolderName;
            }
        }



        [ObservableProperty]

        private bool _isAlbumArtAccentEnabled = true;



        [ObservableProperty]
        private bool _isAccentColorEnabled = true;

        [ObservableProperty]
        private bool _showWeatherInCollapsedNotch = false;

        [ObservableProperty]
        private bool _showBatteryInCollapsedNotch = false;

        [ObservableProperty]
        private bool _showPetInCollapsedNotch = false;

        [ObservableProperty]

        private string _selectedAccentColor = "Blue";

        [ObservableProperty]
        private string _selectedMaterial = "True OLED Black";

        [ObservableProperty]
        private string _customAccentColorHex = "#3B82F6";

        [ObservableProperty]
        private bool _isCustomAccentSelected;



        [ObservableProperty]

        private MonitorDetails? _selectedMonitor;



        // Slide: Cuaca

        [ObservableProperty]

        private bool _isWeatherEnabled = true;



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActiveWeatherLocationFormatted))]

        private string _weatherCity = "Jakarta";



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActiveWeatherLocationFormatted))]

        private double _weatherLatitude = -6.2088;



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActiveWeatherLocationFormatted))]

        private double _weatherLongitude = 106.8456;



        public string ActiveWeatherLocationFormatted => $"📍 {WeatherCity} ({WeatherLatitude:F2}, {WeatherLongitude:F2})";



        [ObservableProperty]

        private bool _isFahrenheit = false;



        // Slide: Pengingat Sholat

        [ObservableProperty]

        private bool _isPrayerReminderEnabled = true;



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActivePrayerLocationFormatted))]

        private string _prayerCityPreset = "Jakarta";



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActivePrayerLocationFormatted))]

        private double _prayerLatitude = -6.2088;



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(ActivePrayerLocationFormatted))]

        private double _prayerLongitude = 106.8456;



        public string ActivePrayerLocationFormatted => $"📍 {PrayerCityPreset} ({PrayerLatitude:F2}, {PrayerLongitude:F2})";



        [ObservableProperty]

        private int _prayerAlertBeforeMinutes = 10;



        [ObservableProperty]

        private bool _prayerSoundEnabled = true;



        [ObservableProperty]

        private bool _isLocationSynced = true;



        // Manual Location Search Properties

        [ObservableProperty]

        private string _weatherSearchQuery = string.Empty;



        [ObservableProperty]

        private bool _isSearchingWeather = false;



        [ObservableProperty]

        private bool _hasWeatherSearchResults = false;



        [ObservableProperty]

        private string _weatherSearchStatus = string.Empty;



        public ObservableCollection<SearchResultLocation> WeatherSearchResults { get; } = new();



        [ObservableProperty]

        private string _prayerSearchQuery = string.Empty;



        [ObservableProperty]

        private bool _isSearchingPrayer = false;



        [ObservableProperty]

        private bool _hasPrayerSearchResults = false;



        [ObservableProperty]

        private string _prayerSearchStatus = string.Empty;



        public ObservableCollection<SearchResultLocation> PrayerSearchResults { get; } = new();



        // Slide: Pengingat Minum

        [ObservableProperty]

        private bool _isWaterReminderEnabled = true;



        [ObservableProperty]

        private int _waterDailyGoalMl = 2000;



        [ObservableProperty]

        private int _waterReminderIntervalMinutes = 45;



        [ObservableProperty]

        private bool _waterSoundEnabled = true;



        public ObservableCollection<WaterIntervalOption> WaterIntervalOptions { get; } = new()

        {

            new WaterIntervalOption { Minutes = 30, DisplayName = "Setiap 30 Menit (Sangat Teratur)" },

            new WaterIntervalOption { Minutes = 45, DisplayName = "Setiap 45 Menit (Rekomendasi Ergonomis)" },

            new WaterIntervalOption { Minutes = 60, DisplayName = "Setiap 60 Menit (1 Jam Sekali)" },

            new WaterIntervalOption { Minutes = 90, DisplayName = "Setiap 90 Menit (1.5 Jam Sekali)" },

            new WaterIntervalOption { Minutes = 120, DisplayName = "Setiap 120 Menit (2 Jam Sekali)" }

        };



        [ObservableProperty]

        private WaterIntervalOption? _selectedWaterInterval;



        [ObservableProperty]
        private bool _isPetEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPastelMochiSelected))]
        private PetStyle _selectedPetStyle = PetStyle.PastelMochi;

        public ObservableCollection<PetCharacterInfo> FilteredPetCharacters { get; } = new();

        [ObservableProperty]
        private string _selectedPetCategory = "All";

        public bool IsPastelMochiSelected => SelectedPetStyle == PetStyle.PastelMochi;



        public ObservableCollection<MonitorDetails> AvailableMonitors { get; } = new();

        public ObservableCollection<string> AvailableCities { get; } = new()

        {

            "Jakarta", "Bandung", "Surabaya", "Semarang", "Yogyakarta",

            "Medan", "Palembang", "Padang", "Pekanbaru", "Banda Aceh",

            "Denpasar", "Makassar", "Banjarmasin", "Balikpapan", "Samarinda",

            "Pontianak", "Manado", "Mataram", "Ambon", "Jayapura"

        };

        public ObservableCollection<string> AvailableAccentColors { get; } = new()

        {

            "Windows (Auto)",

            "Blue",

            "Purple",

            "Emerald",

            "Amber",

            "Rose",

            "Cyan",
            "Custom (Hex)",

        };

        public ObservableCollection<string> AvailableMaterials { get; } = new()
        {
            "True OLED Black",
            "Tinted Glass",
            "Fluent Mica / Acrylic"
        };



        public SettingsViewModel(

            ISettingsService settingsService,

            IAutostartService autostartService,

            IDisplayService displayService)

        {

            _settingsService = settingsService;

            _autostartService = autostartService;

            _displayService = displayService;



            LoadSettings();

        }



        public void LoadSettings()

        {

            _isInitializing = true;



            try

            {

                var settings = _settingsService.CurrentSettings;

                var langCode = string.IsNullOrEmpty(settings.Language) ? "en" : settings.Language;

                LocalizationService.Instance.CurrentLanguage = langCode;

                SelectedLanguageOption = AvailableLanguages.FirstOrDefault(l => l.Code.Equals(langCode, StringComparison.OrdinalIgnoreCase)) ?? AvailableLanguages[0];

                UpdateLocalizedDisplayStrings();



                NotchScale = settings.NotchScale > 0 ? settings.NotchScale : 1.0;
                FontScale = settings.FontScale > 0 ? settings.FontScale : 1.0;

                HoverCollapseDelayMs = settings.HoverCollapseDelayMs;

                IsAutostartEnabled = _autostartService.GetAutostartMode() != AutostartMode.Disabled;

                StartupSoundEnabled = settings.StartupSoundEnabled;

                IsFullscreenAutoHideEnabled = settings.IsFullscreenAutoHideEnabled;

                IsFullscreenSlimBarEnabled = settings.IsFullscreenSlimBarEnabled;

                IsMediaWidgetEnabled = settings.IsMediaWidgetEnabled;

                IsLyricsKaraokeEnabled = settings.IsLyricsKaraokeEnabled;
                IsSyllableSyncEnabled = settings.IsSyllableSyncEnabled;
                IsLrcRedEnabled = settings.IsLrcRedEnabled;
                SpicyLyricsApiKey = settings.SpicyLyricsApiKey ?? string.Empty;
                LyricsBold = settings.LyricsBold;
                LyricsTextSize = Math.Clamp(settings.LyricsTextSize > 0 ? settings.LyricsTextSize : 12.0, 10.0, 14.0);

                IsPrivacyIndicatorEnabled = settings.IsPrivacyIndicatorEnabled;

                IsDownloadMonitorEnabled = settings.IsDownloadMonitorEnabled;
                NotificationDurationSeconds = Math.Clamp(settings.NotificationDurationSeconds, 2, 10);

                ShelfRemoveAfterDrag = settings.ShelfRemoveAfterDrag;
                IsPhoneShareEnabled = settings.IsPhoneShareEnabled;
                IsScreenTimeEnabled = settings.IsScreenTimeEnabled;
                ScreenTimeIdleSeconds = settings.ScreenTimeIdleSeconds > 0 ? settings.ScreenTimeIdleSeconds : 120;
                IsNotchTimerGlowEnabled = settings.IsNotchTimerGlowEnabled;
                IsNotchAlertGlowEnabled = settings.IsNotchAlertGlowEnabled;
                PhoneFolder = settings.PhoneFolder ?? string.Empty;

                IsAccentColorEnabled = settings.IsAccentColorEnabled;
                ThemeManager.IsAccentColorEnabled = settings.IsAccentColorEnabled;
                ShowWeatherInCollapsedNotch = settings.ShowWeatherInCollapsedNotch;
                ShowBatteryInCollapsedNotch = settings.ShowBatteryInCollapsedNotch;
                ShowPetInCollapsedNotch = settings.ShowPetInCollapsedNotch;

                IsAlbumArtAccentEnabled = settings.IsAlbumArtAccentEnabled;

                SelectedAccentColor = string.IsNullOrEmpty(settings.AccentColor) ? "Blue" : settings.AccentColor;
                string mat = settings.NotchMaterial ?? "OLED";
                SelectedMaterial = mat switch
                {
                    "TintedGlass" => "Tinted Glass",
                    "Acrylic" => "Fluent Mica / Acrylic",
                    _ => "True OLED Black"
                };
                ThemeManager.CurrentMaterial = mat;

                CustomAccentColorHex = string.IsNullOrEmpty(settings.CustomAccentColorHex) ? "#3B82F6" : settings.CustomAccentColorHex;

                if (settings.AccentColor != null && settings.AccentColor.StartsWith("#"))
                {
                    SelectedAccentColor = "Custom (Hex)";
                    IsCustomAccentSelected = true;
                    CustomAccentColorHex = settings.AccentColor;
                }
                else
                {
                    SelectedAccentColor = string.IsNullOrEmpty(settings.AccentColor) ? "Blue" : settings.AccentColor;
                    IsCustomAccentSelected = SelectedAccentColor == "Custom (Hex)";
                }
                ThemeManager.ApplyMaterial(mat);
                ThemeManager.ApplyAccentColor(IsCustomAccentSelected ? CustomAccentColorHex : SelectedAccentColor, force: false);



                // Cuaca

                IsWeatherEnabled = settings.IsWeatherEnabled;

                WeatherCity = string.IsNullOrEmpty(settings.WeatherCity) ? "Jakarta" : settings.WeatherCity;

                WeatherLatitude = settings.WeatherLatitude != 0 ? settings.WeatherLatitude : -6.2088;

                WeatherLongitude = settings.WeatherLongitude != 0 ? settings.WeatherLongitude : 106.8456;

                IsFahrenheit = settings.IsFahrenheit;



                // Sholat

                IsPrayerReminderEnabled = settings.IsPrayerReminderEnabled;

                PrayerCityPreset = string.IsNullOrEmpty(settings.PrayerCityPreset) ? "Jakarta" : settings.PrayerCityPreset;

                PrayerLatitude = settings.PrayerLatitude != 0 ? settings.PrayerLatitude : -6.2088;

                PrayerLongitude = settings.PrayerLongitude != 0 ? settings.PrayerLongitude : 106.8456;

                PrayerAlertBeforeMinutes = settings.PrayerAlertBeforeMinutes;

                PrayerSoundEnabled = settings.PrayerSoundEnabled;



                // Minum

                IsWaterReminderEnabled = settings.IsWaterReminderEnabled;

                WaterDailyGoalMl = settings.WaterDailyGoalMl > 0 ? settings.WaterDailyGoalMl : 2000;

                WaterReminderIntervalMinutes = settings.WaterReminderIntervalMinutes > 0 ? settings.WaterReminderIntervalMinutes : 45;

                WaterSoundEnabled = settings.WaterSoundEnabled;

                WaterIntervalOption? intervalMatch = null;

                foreach (var opt in WaterIntervalOptions)

                {

                    if (opt.Minutes == WaterReminderIntervalMinutes)

                    {

                        intervalMatch = opt;

                        break;

                    }

                }

                SelectedWaterInterval = intervalMatch ?? WaterIntervalOptions[1];



                // Pet

                IsPetEnabled = settings.IsPetEnabled;
                SelectedPetStyle = settings.PetStyle;
                NotchWin.Services.PetCompanionService.Instance.Style = settings.PetStyle;
                RefreshFilteredPetCharacters();

                // Modul Navbar
                LoadNavbarModules(settings.NavbarModules);



                // Load monitors

                AvailableMonitors.Clear();

                var monitors = _displayService.GetMonitors();

                foreach (var m in monitors)

                {

                    AvailableMonitors.Add(m);

                }



                SelectedMonitor = monitors.FirstOrDefault(m => m.DeviceName == settings.TargetMonitorDeviceName)

                                   ?? monitors.FirstOrDefault(m => m.IsPrimary)

                                   ?? monitors.FirstOrDefault();

            }

            finally

            {

                _isInitializing = false;

            }

        }



        partial void OnNotchScaleChanged(double value) => AutoSave();

        partial void OnFontScaleChanged(double value)
        {
            OnPropertyChanged(nameof(PreviewFontSizeTitle));
            OnPropertyChanged(nameof(PreviewFontSizeArtist));
            OnPropertyChanged(nameof(PreviewFontSizeNormal));
            FontScaleService.SetScale(value);
            AutoSave();
        }

        [RelayCommand]
        public void DecreaseFontScale()
        {
            FontScale = Math.Max(0.80, Math.Round(FontScale - 0.05, 2));
        }

        [RelayCommand]
        public void IncreaseFontScale()
        {
            FontScale = Math.Min(1.30, Math.Round(FontScale + 0.05, 2));
        }

        [RelayCommand]
        public void SetFontScalePreset(object? param)
        {
            if (param != null && double.TryParse(param.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double scale))
            {
                FontScale = Math.Clamp(scale, 0.80, 1.30);
            }
        }

        partial void OnHoverCollapseDelayMsChanged(int value) => AutoSave();

        partial void OnIsFullscreenAutoHideEnabledChanged(bool value) => AutoSave();

        partial void OnIsFullscreenSlimBarEnabledChanged(bool value) => AutoSave();

        partial void OnIsMediaWidgetEnabledChanged(bool value) => AutoSave();

        partial void OnIsLyricsKaraokeEnabledChanged(bool value) => AutoSave();

        partial void OnIsSyllableSyncEnabledChanged(bool value) => AutoSave();

        partial void OnIsLrcRedEnabledChanged(bool value) => AutoSave();

        partial void OnSpicyLyricsApiKeyChanged(string value) => AutoSave();


        partial void OnLyricsBoldChanged(bool value) => AutoSave();

        partial void OnLyricsTextSizeChanged(double value)
        {
            OnPropertyChanged(nameof(LyricsTextSizeDisplay));
            AutoSave();
        }

        partial void OnIsPrivacyIndicatorEnabledChanged(bool value) => AutoSave();

        partial void OnIsDownloadMonitorEnabledChanged(bool value) => AutoSave();

        partial void OnShelfRemoveAfterDragChanged(bool value) => AutoSave();

        partial void OnIsPhoneShareEnabledChanged(bool value) => AutoSave();
        partial void OnIsScreenTimeEnabledChanged(bool value) => AutoSave();
        partial void OnScreenTimeIdleSecondsChanged(int value) => AutoSave();

        partial void OnIsNotchTimerGlowEnabledChanged(bool value) => AutoSave();

        partial void OnIsNotchAlertGlowEnabledChanged(bool value) => AutoSave();

        partial void OnPhoneFolderChanged(string value) => AutoSave();

        partial void OnNotificationDurationSecondsChanged(int value)
        {
            OnPropertyChanged(nameof(NotificationDurationDisplay));
            AutoSave();
        }

        partial void OnIsAlbumArtAccentEnabledChanged(bool value)

        {

            if (!_isInitializing)

            {

                if (!value)

                {

                    ThemeManager.RestoreDefaultAccent();

                }

                else

                {

                    ThemeManager.RequestDynamicAccentRefresh();

                }

            }

            AutoSave();

        }



        // Cuaca

        partial void OnIsWeatherEnabledChanged(bool value) => AutoSave();

        partial void OnWeatherCityChanged(string value) => AutoSave();

        partial void OnIsFahrenheitChanged(bool value) => AutoSave();



        // Sholat

        partial void OnIsPrayerReminderEnabledChanged(bool value)
        {
            var prayerItem = NavbarModules.FirstOrDefault(m => string.Equals(m.Id, "prayer", StringComparison.OrdinalIgnoreCase));
            if (prayerItem != null && prayerItem.IsEnabled != value)
            {
                prayerItem.IsEnabled = value;
            }
            AutoSave();
        }

        partial void OnPrayerCityPresetChanged(string value) => AutoSave();

        partial void OnPrayerAlertBeforeMinutesChanged(int value) => AutoSave();

        partial void OnPrayerSoundEnabledChanged(bool value)

        {

            if (value && !_isInitializing)

            {

                try { new SoundNotificationService().PlayPrayerCall(); } catch { }

            }

            AutoSave();

        }



        // Minum

        partial void OnIsWaterReminderEnabledChanged(bool value) => AutoSave();

        partial void OnWaterDailyGoalMlChanged(int value) => AutoSave();

        partial void OnWaterReminderIntervalMinutesChanged(int value) => AutoSave();

        partial void OnWaterSoundEnabledChanged(bool value)

        {

            if (value && !_isInitializing)

            {

                try { new SoundNotificationService().PlayWaterReminder(); } catch { }

            }

            AutoSave();

        }

        partial void OnSelectedWaterIntervalChanged(WaterIntervalOption? value)

        {

            if (!_isInitializing && value != null && WaterReminderIntervalMinutes != value.Minutes)

            {

                WaterReminderIntervalMinutes = value.Minutes;

                AutoSave();

            }

        }



        partial void OnIsPetEnabledChanged(bool value)
        {
            NotchWin.Services.PetCompanionService.Instance.IsEnabled = value;
            AutoSave();
        }

        [RelayCommand]
        public void SetPetStyle(string style)
        {
            if (Enum.TryParse<PetStyle>(style, true, out var parsed))
            {
                SelectedPetStyle = parsed;
            }
        }

        [RelayCommand]
        public void FilterPetCategory(string category)
        {
            SelectedPetCategory = category;
            RefreshFilteredPetCharacters();
        }

        [RelayCommand]
        public void SelectPetCharacter(PetCharacterInfo info)
        {
            if (info != null)
            {
                SelectedPetStyle = info.Style;
            }
        }

        public void RefreshFilteredPetCharacters()
        {
            FilteredPetCharacters.Clear();
            foreach (var character in PetRegistry.AllCharacters)
            {
                if (SelectedPetCategory == "All" || character.Category.Equals(SelectedPetCategory, StringComparison.OrdinalIgnoreCase))
                {
                    character.IsSelected = (character.Style == SelectedPetStyle);
                    FilteredPetCharacters.Add(character);
                }
            }
        }

        partial void OnSelectedPetStyleChanged(PetStyle value)
        {
            NotchWin.Services.PetCompanionService.Instance.Style = value;
            foreach (var c in FilteredPetCharacters)
            {
                c.IsSelected = (c.Style == value);
            }
            AutoSave();
        }



        partial void OnSelectedMaterialChanged(string value)
        {
            if (_isInitializing) return;
            string code = value switch
            {
                "Tinted Glass" => "TintedGlass",
                "Fluent Mica / Acrylic" => "Acrylic",
                _ => "OLED"
            };
            ThemeManager.ApplyMaterial(code);
            AutoSave();
        }

        partial void OnIsAccentColorEnabledChanged(bool value)
        {
            if (_isInitializing) return;
            ThemeManager.IsAccentColorEnabled = value;
            ThemeManager.ApplyAccentColor(IsCustomAccentSelected ? CustomAccentColorHex : SelectedAccentColor, force: false);
            AutoSave();
        }

        partial void OnShowWeatherInCollapsedNotchChanged(bool value)
        {
            if (_isInitializing) return;
            AutoSave();
        }

        partial void OnShowBatteryInCollapsedNotchChanged(bool value)
        {
            if (_isInitializing) return;
            AutoSave();
        }

        partial void OnShowPetInCollapsedNotchChanged(bool value)
        {
            if (_isInitializing) return;
            AutoSave();
        }

        partial void OnCustomAccentColorHexChanged(string value)
        {
            if (_isInitializing) return;
            if (IsCustomAccentSelected && !string.IsNullOrWhiteSpace(value) && value.StartsWith("#") && (value.Length == 7 || value.Length == 9))
            {
                ThemeManager.ApplyAccentColor(value, force: false);
                AutoSave();
            }
        }

        partial void OnSelectedAccentColorChanged(string value)
        {
            if (_isInitializing) return;
            if (value == "Custom (Hex)")
            {
                IsCustomAccentSelected = true;
                ThemeManager.ApplyAccentColor(CustomAccentColorHex, force: false);
            }
            else
            {
                IsCustomAccentSelected = false;
                ThemeManager.ApplyAccentColor(value, force: false);
            }
            AutoSave();
        }



        partial void OnIsAutostartEnabledChanged(bool value)

        {

            if (_isInitializing) return;

            try

            {

                AutostartMode mode = _autostartService.SetAutostart(value);

                if (value && mode == AutostartMode.RunKey)

                {

                    AutoSave();

                    ShowAutostartMessage(LocalizationService.Instance["Settings.AutostartFallbackMessage"]);

                }

                else if (value && mode == AutostartMode.Disabled)

                {

                    RevertAutostartToggle(false);

                    ShowAutostartMessage(LocalizationService.Instance["Settings.AutostartEnableFailedMessage"]);

                }

                else if (!value && mode == AutostartMode.LogonTask)

                {

                    RevertAutostartToggle(true);

                    ShowAutostartMessage(LocalizationService.Instance["Settings.AutostartDisableFailedMessage"]);

                }

                else

                {

                    AutoSave();

                }

            }

            catch (Exception ex)

            {

                Log.Error(ex, "Failed to toggle autostart in SettingsViewModel");

                RevertAutostartToggle(_autostartService.GetAutostartMode() != AutostartMode.Disabled);

                ShowAutostartMessage(LocalizationService.Instance[value ? "Settings.AutostartEnableFailedMessage" : "Settings.AutostartDisableFailedMessage"]);

            }

        }

        private void RevertAutostartToggle(bool enabled)

        {

            _isInitializing = true;

            IsAutostartEnabled = enabled;

            _isInitializing = false;

        }

        private static void ShowAutostartMessage(string message)

        {

            Window? owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (owner != null)

            {

                MessageBox.Show(owner, message, "Kuri", MessageBoxButton.OK, MessageBoxImage.Warning);

            }

            else

            {

                MessageBox.Show(message, "Kuri", MessageBoxButton.OK, MessageBoxImage.Warning);

            }

        }



        partial void OnStartupSoundEnabledChanged(bool value)

        {

            if (value && !_isInitializing)

            {

                try { new SoundNotificationService().PlayStartupSound(); } catch { }

            }

            AutoSave();

        }



        partial void OnSelectedMonitorChanged(MonitorDetails? value)

        {

            if (_isInitializing) return;

            AutoSave();

        }



        [RelayCommand]

        public async Task ResetToDefaultsAsync()

        {

            _isInitializing = true;

            try

            {

                var defaults = new AppSettings();

                NotchScale = defaults.NotchScale;
                FontScale = defaults.FontScale;

                HoverCollapseDelayMs = defaults.HoverCollapseDelayMs;

                IsFullscreenAutoHideEnabled = defaults.IsFullscreenAutoHideEnabled;

                IsFullscreenSlimBarEnabled = defaults.IsFullscreenSlimBarEnabled;

                IsMediaWidgetEnabled = defaults.IsMediaWidgetEnabled;

                IsLyricsKaraokeEnabled = defaults.IsLyricsKaraokeEnabled;
                IsSyllableSyncEnabled = defaults.IsSyllableSyncEnabled;
                IsLrcRedEnabled = defaults.IsLrcRedEnabled;
                SpicyLyricsApiKey = defaults.SpicyLyricsApiKey;
                LyricsBold = defaults.LyricsBold;
                LyricsTextSize = defaults.LyricsTextSize;

                IsPetEnabled = defaults.IsPetEnabled;
                SelectedPetStyle = defaults.PetStyle;

                IsPrivacyIndicatorEnabled = defaults.IsPrivacyIndicatorEnabled;

                IsDownloadMonitorEnabled = defaults.IsDownloadMonitorEnabled;
                NotificationDurationSeconds = defaults.NotificationDurationSeconds;

                ShelfRemoveAfterDrag = defaults.ShelfRemoveAfterDrag;
                IsPhoneShareEnabled = defaults.IsPhoneShareEnabled;
                IsScreenTimeEnabled = defaults.IsScreenTimeEnabled;
                ScreenTimeIdleSeconds = defaults.ScreenTimeIdleSeconds;
                IsNotchTimerGlowEnabled = defaults.IsNotchTimerGlowEnabled;
                IsNotchAlertGlowEnabled = defaults.IsNotchAlertGlowEnabled;
                PhoneFolder = defaults.PhoneFolder;

                IsAccentColorEnabled = defaults.IsAccentColorEnabled;
                ThemeManager.IsAccentColorEnabled = defaults.IsAccentColorEnabled;
                ShowWeatherInCollapsedNotch = defaults.ShowWeatherInCollapsedNotch;
                ShowBatteryInCollapsedNotch = defaults.ShowBatteryInCollapsedNotch;
                ShowPetInCollapsedNotch = defaults.ShowPetInCollapsedNotch;

                IsAlbumArtAccentEnabled = defaults.IsAlbumArtAccentEnabled;

                SelectedAccentColor = defaults.AccentColor;

                ThemeManager.ApplyAccentColor(defaults.AccentColor, force: false);

                _autostartService.SetAutostart(defaults.IsAutostartEnabled);

                IsAutostartEnabled = _autostartService.GetAutostartMode() != AutostartMode.Disabled;

                StartupSoundEnabled = defaults.StartupSoundEnabled;



                SelectedMonitor = AvailableMonitors.FirstOrDefault(m => m.IsPrimary) ?? AvailableMonitors.FirstOrDefault();



                SelectedLanguageOption = AvailableLanguages.FirstOrDefault(l => l.Code == "en") ?? AvailableLanguages[0];

                LocalizationService.Instance.CurrentLanguage = "en";

                UpdateLocalizedDisplayStrings();

                // Reset modul navbar
                LoadNavbarModules(defaults.NavbarModules);

            }

            finally

            {

                _isInitializing = false;

            }



            await SaveCurrentSettingsAsync();

        }



        private void UpdateLocalizedDisplayStrings()

        {

            bool isId = string.Equals(LocalizationService.Instance.CurrentLanguage, "id", StringComparison.OrdinalIgnoreCase);

            if (WaterIntervalOptions.Count >= 5)

            {

                WaterIntervalOptions[0].DisplayName = isId ? "Setiap 30 Menit (Sangat Teratur)" : "Every 30 Minutes (Frequent)";

                WaterIntervalOptions[1].DisplayName = isId ? "Setiap 45 Menit (Rekomendasi Ergonomis)" : "Every 45 Minutes (Ergonomic Recommended)";

                WaterIntervalOptions[2].DisplayName = isId ? "Setiap 60 Menit (1 Jam Sekali)" : "Every 60 Minutes (Hourly)";

                WaterIntervalOptions[3].DisplayName = isId ? "Setiap 90 Menit (1.5 Jam Sekali)" : "Every 90 Minutes (1.5 Hours)";

                WaterIntervalOptions[4].DisplayName = isId ? "Setiap 120 Menit (2 Jam Sekali)" : "Every 120 Minutes (2 Hours)";

            }

            foreach (var item in NavbarModules)
            {
                var info = GetModuleDisplayInfo(item.Id);
                item.Title = info.Title;
                item.Description = info.Desc;
            }

        }



        private void AutoSave()

        {

            if (_isInitializing) return;

            _ = SaveCurrentSettingsAsync();

        }



        [RelayCommand]

        public async Task SearchWeatherLocationAsync()

        {

            if (string.IsNullOrWhiteSpace(WeatherSearchQuery)) return;

            IsSearchingWeather = true;

            WeatherSearchStatus = L10n["Settings.SearchingLocation"];

            try

            {

                var results = await _locationSearchService.SearchAsync(WeatherSearchQuery);

                WeatherSearchResults.Clear();

                foreach (var r in results)

                {

                    WeatherSearchResults.Add(r);

                }

                HasWeatherSearchResults = WeatherSearchResults.Count > 0;

                WeatherSearchStatus = HasWeatherSearchResults ? L10n.Get("Settings.LocationsFound", WeatherSearchResults.Count) : L10n["Settings.NoLocationFound"];

            }

            catch (Exception ex)

            {

                Log.Warning(ex, "Error searching weather location");

                WeatherSearchStatus = L10n["Settings.NoLocationFound"];

            }

            finally

            {

                IsSearchingWeather = false;

            }

        }



        [RelayCommand]

        public void SelectWeatherLocation(SearchResultLocation? loc)

        {

            if (loc == null) return;

            WeatherCity = loc.FormattedDisplay;

            WeatherLatitude = loc.Latitude;

            WeatherLongitude = loc.Longitude;

            HasWeatherSearchResults = false;

            WeatherSearchResults.Clear();

            WeatherSearchQuery = string.Empty;



            if (IsLocationSynced)

            {

                PrayerCityPreset = loc.FormattedDisplay;

                PrayerLatitude = loc.Latitude;

                PrayerLongitude = loc.Longitude;

                HasPrayerSearchResults = false;

                PrayerSearchResults.Clear();

            }



            AutoSave();

        }



        [RelayCommand]

        public async Task SearchPrayerLocationAsync()

        {

            if (string.IsNullOrWhiteSpace(PrayerSearchQuery)) return;

            IsSearchingPrayer = true;

            PrayerSearchStatus = L10n["Settings.SearchingLocation"];

            try

            {

                var results = await _locationSearchService.SearchAsync(PrayerSearchQuery);

                PrayerSearchResults.Clear();

                foreach (var r in results)

                {

                    PrayerSearchResults.Add(r);

                }

                HasPrayerSearchResults = PrayerSearchResults.Count > 0;

                PrayerSearchStatus = HasPrayerSearchResults ? L10n.Get("Settings.LocationsFound", PrayerSearchResults.Count) : L10n["Settings.NoLocationFound"];

            }

            catch (Exception ex)

            {

                Log.Warning(ex, "Error searching prayer location");

                PrayerSearchStatus = L10n["Settings.NoLocationFound"];

            }

            finally

            {

                IsSearchingPrayer = false;

            }

        }



        [RelayCommand]

        public void SelectPrayerLocation(SearchResultLocation? loc)

        {

            if (loc == null) return;

            PrayerCityPreset = loc.FormattedDisplay;

            PrayerLatitude = loc.Latitude;

            PrayerLongitude = loc.Longitude;

            HasPrayerSearchResults = false;

            PrayerSearchResults.Clear();

            PrayerSearchQuery = string.Empty;



            AutoSave();

        }



        [RelayCommand]

        public void QuickSelectWeatherCity(string cityName)

        {

            var match = _locationSearchService.OfflinePresets.FirstOrDefault(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))

                        ?? new SearchResultLocation { Name = cityName, Latitude = -6.2088, Longitude = 106.8456 };

            SelectWeatherLocation(match);

        }



        [RelayCommand]

        public void QuickSelectPrayerCity(string cityName)

        {

            var match = _locationSearchService.OfflinePresets.FirstOrDefault(c => c.Name.Equals(cityName, StringComparison.OrdinalIgnoreCase))

                        ?? new SearchResultLocation { Name = cityName, Latitude = -6.2088, Longitude = 106.8456 };

            SelectPrayerLocation(match);

        }



        private async Task SaveCurrentSettingsAsync()

        {

            try

            {

                var settings = new AppSettings

                {

                    TopOffset = 0.0,

                    NotchScale = NotchScale,
                    FontScale = FontScale,

                    HoverCollapseDelayMs = HoverCollapseDelayMs,

                    IsAutostartEnabled = IsAutostartEnabled,

                    StartupSoundEnabled = StartupSoundEnabled,

                    IsFullscreenAutoHideEnabled = IsFullscreenAutoHideEnabled,

                    IsFullscreenSlimBarEnabled = IsFullscreenSlimBarEnabled,

                    IsMediaWidgetEnabled = IsMediaWidgetEnabled,

                    IsLyricsEnabled = _settingsService.CurrentSettings.IsLyricsEnabled,
                    LyricOffsetMs = _settingsService.CurrentSettings.LyricOffsetMs,
                    IsLyricsKaraokeEnabled = IsLyricsKaraokeEnabled,
                    IsSyllableSyncEnabled = IsSyllableSyncEnabled,
                    IsLrcRedEnabled = IsLrcRedEnabled,
                    SpicyLyricsApiKey = SpicyLyricsApiKey?.Trim() ?? string.Empty,
                    LyricsBold = LyricsBold,
                    LyricsTextSize = LyricsTextSize,

                    IsPrivacyIndicatorEnabled = IsPrivacyIndicatorEnabled,

                    IsDownloadMonitorEnabled = IsDownloadMonitorEnabled,
                    NotificationDurationSeconds = NotificationDurationSeconds,

                    IsAlbumArtAccentEnabled = IsAlbumArtAccentEnabled,
                    IsAccentColorEnabled = IsAccentColorEnabled,
                    ShowWeatherInCollapsedNotch = ShowWeatherInCollapsedNotch,
                    ShowBatteryInCollapsedNotch = ShowBatteryInCollapsedNotch,
                    ShowPetInCollapsedNotch = ShowPetInCollapsedNotch,

                    AccentColor = IsCustomAccentSelected ? CustomAccentColorHex : SelectedAccentColor,
                    NotchMaterial = SelectedMaterial switch
                    {
                        "Tinted Glass" => "TintedGlass",
                        "Fluent Mica / Acrylic" => "Acrylic",
                        _ => "OLED"
                    },
                    CustomAccentColorHex = CustomAccentColorHex,

                    TargetMonitorDeviceName = SelectedMonitor?.DeviceName ?? string.Empty,



                    // Cuaca

                    IsWeatherEnabled = IsWeatherEnabled,

                    WeatherCity = WeatherCity,

                    WeatherLatitude = WeatherLatitude,

                    WeatherLongitude = WeatherLongitude,

                    IsFahrenheit = IsFahrenheit,



                    // Sholat

                    IsPrayerReminderEnabled = IsPrayerReminderEnabled,

                    PrayerCityPreset = PrayerCityPreset,

                    PrayerLatitude = PrayerLatitude,

                    PrayerLongitude = PrayerLongitude,

                    PrayerAlertBeforeMinutes = PrayerAlertBeforeMinutes,

                    PrayerSoundEnabled = PrayerSoundEnabled,



                    // Minum

                    IsWaterReminderEnabled = IsWaterReminderEnabled,

                    WaterDailyGoalMl = WaterDailyGoalMl,

                    WaterReminderIntervalMinutes = WaterReminderIntervalMinutes,

                    WaterSoundEnabled = WaterSoundEnabled,



                    // Language

                    Language = SelectedLanguageOption?.Code ?? "en",



                    // Pet

                    IsPetEnabled = IsPetEnabled,
                    PetStyle = SelectedPetStyle,

                    // Shelf & Phone Share

                    ShelfRemoveAfterDrag = ShelfRemoveAfterDrag,
                    IsPhoneShareEnabled = IsPhoneShareEnabled,
                    IsScreenTimeEnabled = IsScreenTimeEnabled,
                    ScreenTimeIdleSeconds = ScreenTimeIdleSeconds,
                    IsNotchTimerGlowEnabled = IsNotchTimerGlowEnabled,
                    IsNotchAlertGlowEnabled = IsNotchAlertGlowEnabled,
                    PhoneFolder = PhoneFolder,

                    NavbarModules = NavbarModules.Select(m => new NavbarModuleConfig(m.Id, m.IsEnabled)).ToList(),

                };



                await _settingsService.SaveAsync(settings);

            }

            catch (Exception ex)

            {

                Log.Error(ex, "Error saving settings from SettingsViewModel");

            }

        }

    }



    public partial class WaterIntervalOption : ObservableObject

    {

        public int Minutes { get; set; }

        [ObservableProperty]

        private string _displayName = string.Empty;

    }



    public class LanguageOption

    {

        public string Code { get; set; } = "en";

        public string DisplayName { get; set; } = "English";

    }

    public partial class NavbarModuleItemViewModel : ObservableObject
    {
        private readonly Action _onChanged;

        public string Id { get; }

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _iconGlyph = string.Empty;

        public bool CanDisable => !string.Equals(Id, "home", StringComparison.OrdinalIgnoreCase);

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private int _orderNumber;

        [ObservableProperty]
        private bool _canMoveUp;

        [ObservableProperty]
        private bool _canMoveDown;

        public NavbarModuleItemViewModel(string id, string title, string description, string iconGlyph, bool isEnabled, Action onChanged)
        {
            Id = id;
            _title = title;
            _description = description;
            _iconGlyph = iconGlyph;
            _isEnabled = isEnabled;
            _onChanged = onChanged;
        }

        partial void OnIsEnabledChanged(bool value)
        {
            if (string.Equals(Id, "home", StringComparison.OrdinalIgnoreCase) && !value)
            {
                _isEnabled = true;
                OnPropertyChanged(nameof(IsEnabled));
                return;
            }
            _onChanged?.Invoke();
        }
    }

}

