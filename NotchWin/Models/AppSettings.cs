using System;

namespace NotchWin.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "en";
        public double TopOffset { get; set; } = 0.0;
        public double NotchScale { get; set; } = 1.0;
        public double FontScale { get; set; } = 1.0;
        public int HoverCollapseDelayMs { get; set; } = 450;
        public int HoverExpandDelayMs { get; set; } = 280;
        public bool IsMediaWidgetEnabled { get; set; } = true;
        public bool IsPrivacyIndicatorEnabled { get; set; } = true;
        public int NotificationDurationSeconds { get; set; } = 3;
        public bool IsAlbumArtAccentEnabled { get; set; } = true;
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
    }
}
