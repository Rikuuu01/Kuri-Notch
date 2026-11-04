using System;

using System.Collections.ObjectModel;

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

        private bool _isPrivacyIndicatorEnabled;

        [ObservableProperty]

        private bool _isDownloadMonitorEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NotificationDurationDisplay))]
        private int _notificationDurationSeconds = 3;

        public string NotificationDurationDisplay => L10n.Get("Settings.SecondsFormat", NotificationDurationSeconds);



        [ObservableProperty]

        private bool _isAlbumArtAccentEnabled = true;



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

                IsPrivacyIndicatorEnabled = settings.IsPrivacyIndicatorEnabled;

                IsDownloadMonitorEnabled = settings.IsDownloadMonitorEnabled;
                NotificationDurationSeconds = Math.Clamp(settings.NotificationDurationSeconds, 2, 10);

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

        partial void OnIsPrivacyIndicatorEnabledChanged(bool value) => AutoSave();

        partial void OnIsDownloadMonitorEnabledChanged(bool value) => AutoSave();

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

        partial void OnIsPrayerReminderEnabledChanged(bool value) => AutoSave();

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

                IsPetEnabled = defaults.IsPetEnabled;
                SelectedPetStyle = defaults.PetStyle;

                IsPrivacyIndicatorEnabled = defaults.IsPrivacyIndicatorEnabled;

                IsDownloadMonitorEnabled = defaults.IsDownloadMonitorEnabled;
                NotificationDurationSeconds = defaults.NotificationDurationSeconds;

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

                    IsPrivacyIndicatorEnabled = IsPrivacyIndicatorEnabled,

                    IsDownloadMonitorEnabled = IsDownloadMonitorEnabled,
                    NotificationDurationSeconds = NotificationDurationSeconds,

                    IsAlbumArtAccentEnabled = IsAlbumArtAccentEnabled,

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

}

