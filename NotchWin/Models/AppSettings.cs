using System;

namespace NotchWin.Models
{
    public class AppSettings
    {
        public static AppSettings Current => Services.SettingsService.Instance?.CurrentSettings ?? new();

        public string Language { get; set; } = "en";
        public double TopOffset { get; set; } = 0.0;
        public double NotchScale { get; set; } = 1.0;
        public double FontScale { get; set; } = 1.0;
        public int HoverCollapseDelayMs { get; set; } = 450;
        public int HoverExpandDelayMs { get; set; } = 280;
        public bool IsMediaWidgetEnabled { get; set; } = false;
        public bool IsPrivacyIndicatorEnabled { get; set; } = true;
        public int NotificationDurationSeconds { get; set; } = 3;
        public bool IsAccentColorEnabled { get; set; } = true;
        public bool ShowWeatherInCollapsedNotch { get; set; } = false;
        public bool ShowBatteryInCollapsedNotch { get; set; } = false;
        public bool ShowPetInCollapsedNotch { get; set; } = false;
        public bool IsAlbumArtAccentEnabled { get; set; } = true;
        /// <summary>Garis cahaya sisa waktu timer/stopwatch di sekeliling outline notch tertutup.</summary>
        public bool IsNotchTimerGlowEnabled { get; set; } = true;
        /// <summary>Cahaya berjalan keliling outline notch saat alert tampil (maks 20 detik).</summary>
        public bool IsNotchAlertGlowEnabled { get; set; } = true;
        public string AccentColor { get; set; } = "Blue";
        public string NotchMaterial { get; set; } = "OLED";
        public string CustomAccentColorHex { get; set; } = "#3B82F6";
        public bool IsAutostartEnabled { get; set; } = false;
        public bool StartupSoundEnabled { get; set; } = true;
        public bool IsFullscreenAutoHideEnabled { get; set; } = true;
        /// <summary>
        /// true  = tampilkan garis tipis (slim bar) saat aplikasi fullscreen aktif.
        /// false = sembunyikan notch sepenuhnya (perilaku lama).
        /// Berlaku hanya jika <see cref="IsFullscreenAutoHideEnabled"/> aktif.
        /// </summary>
        public bool IsFullscreenSlimBarEnabled { get; set; } = true;
        public string TargetMonitorDeviceName { get; set; } = string.Empty;
        public int LyricOffsetMs { get; set; } = 0;
        public bool IsLyricsEnabled { get; set; } = false;
        public bool IsLyricsKaraokeEnabled { get; set; } = true;
        public bool IsSyllableSyncEnabled { get; set; } = true;
        public bool IsLrcRedEnabled { get; set; } = true;
        public string SpicyLyricsApiKey { get; set; } = string.Empty;
        public bool LyricsBold { get; set; } = true;
        public double LyricsTextSize { get; set; } = 12.0;

        // Slide: Cuaca (Weather)
        public bool IsWeatherEnabled { get; set; } = true;
        public string WeatherCity { get; set; } = "Jakarta";
        public double WeatherLatitude { get; set; } = -6.2088;
        public double WeatherLongitude { get; set; } = 106.8456;
        public bool IsFahrenheit { get; set; } = false;

        // Slide: Pengingat Sholat (Prayer Times)
        public bool IsPrayerReminderEnabled { get; set; } = true;
        public string PrayerCityPreset { get; set; } = "Jakarta";
        public double PrayerLatitude { get; set; } = -6.2088;
        public double PrayerLongitude { get; set; } = 106.8456;
        public int PrayerAlertBeforeMinutes { get; set; } = 10;
        public bool PrayerSoundEnabled { get; set; } = true;

        // Slide: Pengingat Minum (Water Tracker)
        public bool IsWaterReminderEnabled { get; set; } = true;
        public int WaterDailyGoalMl { get; set; } = 2000;
        public int WaterReminderIntervalMinutes { get; set; } = 45;
        public bool WaterSoundEnabled { get; set; } = true;

        // Slide: Todo & Timer
        public bool IsDownloadMonitorEnabled { get; set; } = true;
        public bool IsPetEnabled { get; set; } = true;
        public PetStyle PetStyle { get; set; } = PetStyle.PastelMochi;

        // Shelf & Phone Share
        public bool ShelfRemoveAfterDrag { get; set; } = false;
        public bool IsPhoneShareEnabled { get; set; } = true;
        public string PhoneFolder { get; set; } = string.Empty;

        // Screen Time Tracking
        public bool IsScreenTimeEnabled { get; set; } = true;
        public int ScreenTimeIdleSeconds { get; set; } = 120;

        // Modul Navbar (Urutan & Enable/Disable Menu Notch)
        public System.Collections.Generic.List<NavbarModuleConfig> NavbarModules { get; set; } = GetDefaultNavbarModules();

        public static System.Collections.Generic.List<NavbarModuleConfig> GetDefaultNavbarModules() => new()
        {
            new NavbarModuleConfig("home", true),
            new NavbarModuleConfig("control", true),
            new NavbarModuleConfig("media", true),
            new NavbarModuleConfig("weather", true),
            new NavbarModuleConfig("prayer", true),
            new NavbarModuleConfig("water", true),
            new NavbarModuleConfig("note", true),
            new NavbarModuleConfig("clipboard", true),
            new NavbarModuleConfig("timer", true),
            new NavbarModuleConfig("shelf", true),
            new NavbarModuleConfig("screentime", true),
        };
    }
}
