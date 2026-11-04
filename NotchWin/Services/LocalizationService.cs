using System;

using System.Collections.Generic;

using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;



namespace NotchWin.Services

{

    public partial class LocalizationService : ObservableObject, ILocalizationService

    {

        private static LocalizationService? _instance;

        public static LocalizationService Instance => _instance ??= new LocalizationService();



        public const string LangEn = "en";

        public const string LangId = "id";



        [ObservableProperty]

        private string _currentLanguage = LangEn;



        public event EventHandler<string>? LanguageChanged;



        partial void OnCurrentLanguageChanged(string value)

        {

            OnPropertyChanged("Item");

            OnPropertyChanged("Item[]");

            LanguageChanged?.Invoke(this, value);

        }



        public string this[string key] => Get(key);



        public string Get(string key, params object[] args)

        {

            if (string.IsNullOrEmpty(key)) return string.Empty;



            var dict = string.Equals(CurrentLanguage, LangId, StringComparison.OrdinalIgnoreCase)

                ? IndonesianStrings

                : EnglishStrings;



            if (dict.TryGetValue(key, out var val))

            {

                return (args != null && args.Length > 0) ? string.Format(CultureInfo.InvariantCulture, val, args) : val;

            }



            // Fallback to English

            if (EnglishStrings.TryGetValue(key, out var enVal))

            {

                return (args != null && args.Length > 0) ? string.Format(CultureInfo.InvariantCulture, enVal, args) : enVal;

            }



            return key;

        }



        private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.OrdinalIgnoreCase)

        {

            // Headers

            ["Header.SedangMemutar"] = "NOW PLAYING",

            ["Header.PusatKontrol"] = "CONTROL CENTER",

            ["Header.Cuaca"] = "WEATHER",

            ["Header.JadwalSholat"] = "PRAYER TIMES",

            ["Header.PengingatMinum"] = "HYDRATION & HEALTH",

            ["Header.DaftarTugas"] = "TO-DO LIST",

            ["Header.TimerStopwatch"] = "TIMER & STOPWATCH",



            // Top Navigation Tabs

            ["Tab.ControlCenter"] = "Control",

            ["Tab.Music"] = "Music",

            ["Tab.Weather"] = "Weather",

            ["Tab.Prayer"] = "Prayer",

            ["Tab.Hydration"] = "Hydration",

            ["Tab.Todo"] = "To-Do",

            ["Tab.Timer"] = "Timer",

            ["Tab.Settings"] = "Settings",



            // Collapsed / Media

            ["Media.NoMedia"] = "No media playing",

            ["Media.StartPlaying"] = "Play music on Spotify or YouTube",

            ["Media.SearchingLyrics"] = "Searching lyrics...",

            ["Media.LyricsNotFound"] = "Lyrics not found",

            ["Media.LyricsUnavailable"] = "Lyrics unavailable",

            ["Media.LyricsVersions"] = "LYRICS VERSIONS",

            ["Media.LyricsVersionAuto"] = "Auto (best)",

            ["Media.LyricsVersionTooltip"] = "Choose lyric version",

            ["Media.Instrumental"] = "🎵 Instrumental track (no vocals)",

            ["Media.KaraokeTooltip"] = "Synchronized Lyrics (Karaoke)",

            ["Media.CloseLyrics"] = "Close Lyrics View",

            ["Media.PreviousTrack"] = "Previous Track",

            ["Media.PlayPause"] = "Play / Pause",

            ["Media.NextTrack"] = "Next Track",

            ["Media.DelayAdjust"] = "Lyrics Sync Offset",



            // Pin & System

            ["Pin.Pin"] = "Pin Notch (keeps open on mouse leave)",

            ["Pin.Unpin"] = "Unpin Notch (auto-collapses on mouse leave)",

            ["Volume.Mute"] = "Muted",

            ["Battery.Charging"] = "Charging",

            ["Battery.FullyCharged"] = "Fully Charged",

            ["Battery.BatteryLow"] = "Battery Low",

            ["Notification.StorageConnected"] = "Storage Connected",

            ["Notification.StorageDisconnected"] = "Storage Disconnected",

            ["Notification.UsbReady"] = "USB Drive ready to use",

            ["Notification.UsbRemoved"] = "USB Device has been removed",

            ["Notification.BluetoothOn"] = "Bluetooth On",

            ["Notification.BluetoothOff"] = "Bluetooth Off",

            ["Notification.BluetoothOnMessage"] = "Bluetooth radio is now on",

            ["Notification.BluetoothOffMessage"] = "Bluetooth radio is now off",

            ["Notification.DriveReady"] = "Drive ({0}) ready to use",

            ["Notification.DriveRemoved"] = "Drive ({0}) has been removed",

            ["Notification.TimerFinishedTitle"] = "Timer Finished!",

            ["Notification.TimerFinishedDesc"] = "Countdown timer has completed.",

            ["Notification.WaterReminderTitle"] = "Time to Drink Water!",

            ["Notification.WaterSnoozeAction"] = "Snooze 2m",

            ["Notification.WaterDrinkAction"] = "Drink",

            ["Notification.PrayerReminderTitle"] = "Prayer Time",

            ["Notification.DismissTooltip"] = "Click to dismiss notification",

            ["Notification.Today"] = "Today",

            ["Notification.Tomorrow"] = "Tomorrow",

            ["Notification.AlarmSet"] = "Alarm set: {0} ({1})",

            ["Notification.AlarmTimeSet"] = "Alarm time set: {0}:{1}",

            ["Notification.Reminder"] = "Reminder: {0}",

            ["Notification.NoteCreated"] = "New note created",

            ["Notification.NoteCleared"] = "Note cleared",

            ["Notification.NoteDeleted"] = "Note deleted",

            ["Notification.NoteCopied"] = "Note copied to Clipboard!",

            ["Notification.FocusTimeComplete"] = "Focus Time Complete! Time to take a short break.",

            ["Notification.TimerMinutes"] = "Timer: {0} Minutes",

            ["Notification.ClipboardCopied"] = "Copied to Clipboard!",

            ["Notification.ScreenshotCaptured"] = "Screenshot Captured",

            ["Notification.TextCopied"] = "Text Copied",

            ["Notification.BluetoothConnected"] = "Bluetooth Connected",

            ["Notification.BluetoothDisconnected"] = "Bluetooth Disconnected",



            // Control Center

            ["Control.Volume"] = "Master Volume",

            ["Control.Brightness"] = "Screen Brightness",

            ["Control.QuickActions"] = "QUICK ACTIONS",

            ["Control.BatteryLevel"] = "Battery Level",

            ["Control.PluggedIn"] = "Plugged in",

            ["Control.OnBattery"] = "On battery power",

            ["Action.Screenshot"] = "Screenshot",

            ["Action.Lock"] = "Lock",

            ["Action.Weather"] = "Weather",

            ["Action.Settings"] = "Settings",



            // Weather

            ["Weather.FeelsLike"] = "Feels like",

            ["Weather.Humidity"] = "Humidity",

            ["Weather.HumidityShort"] = "Humidity",

            ["Weather.Wind"] = "Wind",

            ["Weather.WindShort"] = "Wind",

            ["Weather.Precipitation"] = "Rain",

            ["Weather.RainShort"] = "Rain",

            ["Weather.UvIndex"] = "UV Index",

            ["Weather.Forecast"] = "7-Day Forecast",

            ["Weather.Today"] = "Today",

            ["Weather.Now"] = "Now",

            ["Weather.Searching"] = "Searching location...",

            ["Weather.Updated"] = "Updated {0}",

            ["Weather.NotUpdated"] = "Not updated",

            ["Weather.Loading"] = "Loading Weather...",
            ["Weather.DataSourceBmkg"] = "Data: BMKG (Indonesian Met Agency)",
            ["Weather.DataSourceOpenMeteo"] = "Data: Open-Meteo (Global ECMWF)",



            // Prayer Times

            ["Prayer.Subuh"] = "Subuh",
            ["Prayer.Fajr"] = "Subuh",
            ["Prayer.Dhuha"] = "Dhuha",

            ["Prayer.Sunrise"] = "Sunrise",

            ["Prayer.Dhuhr"] = "Dhuhr",

            ["Prayer.Asr"] = "Asr",

            ["Prayer.Maghrib"] = "Maghrib",

            ["Prayer.Isha"] = "Isha",

            ["Prayer.NextPrayer"] = "Next Prayer",

            ["Prayer.Countdown"] = "Countdown",

            ["Prayer.Towards"] = "Towards",
            ["Prayer.Tomorrow"] = "Tomorrow",



            // Hydration & Sedentary

            ["Water.Title"] = "DRINK WATER",

            ["Water.DailyTarget"] = "Daily Target",

            ["Water.Add250"] = "+250 ml",

            ["Water.Reset"] = "Reset",

            ["Water.TodayLog"] = "Today: {0:F1}L of {1:F1}L",

            ["Water.GlassesLog"] = "{0} / {1} Glasses (250ml)",

            ["Water.StatusNormal"] = "Stay hydrated while working 💧",

            ["Water.StatusWarning"] = "Take a break and drink water 💧",

            ["Water.StatusOverdue"] = "⚠️ Time to Drink Water!",

            ["Water.StatusStandby"] = "Idle - Timer paused 💤",

            ["Sedentary.Title"] = "SCREEN TIME",

            ["Sedentary.SittingTime"] = "Sitting Time",

            ["Sedentary.Standby"] = "User Idle / Standby",

            ["Sedentary.StatsHeader"] = "DAILY ACTIVITY STATS",

            ["Sedentary.StatsDesc"] = "Screen time recorded while active",

            ["Sedentary.ClickToFlip"] = "Click for daily stats",

            ["Sedentary.Back"] = "↩ Back",

            ["Sedentary.TotalToday"] = "Total Today",

            ["Sedentary.LongestSession"] = "Longest Session",

            ["Sedentary.StandBreaks"] = "Stand Breaks",

            ["Sedentary.BreaksUnit"] = "{0}x Breaks",

            ["Sedentary.StatusOptimal"] = "Posture optimal",

            ["Sedentary.StatusWarning"] = "⚠️ Stretch your body soon",

            ["Sedentary.StatusOverdue"] = "⚠️ Sitting too long!",

            ["Sedentary.StatusStandby"] = "💤 Standby (Timer paused)",



            // Todo

            ["Todo.Title"] = "TODAY'S TASKS",

            ["Todo.Summary"] = "{0}/{1} Done",

            ["Todo.EmptyTitle"] = "No tasks for today",

            ["Todo.EmptyDesc"] = "Type below to add a new task",

            ["Todo.Placeholder"] = "Add a new task...",



            // Timer & Stopwatch

            ["Timer.TabTimer"] = "Timer",

            ["Timer.TabStopwatch"] = "Stopwatch",

            ["Timer.Start"] = "Start",

            ["Timer.Pause"] = "Pause",

            ["Timer.Resume"] = "Resume",

            ["Timer.Reset"] = "Reset",

            ["Timer.Lap"] = "Lap",

            ["Timer.NoLaps"] = "No laps recorded yet",

            ["Timer.PressLap"] = "Press Lap while running",

            ["Timer.Preset1"] = "1 Min",

            ["Timer.Preset5"] = "5 Min",

            ["Timer.Preset10"] = "10 Min",

            ["Timer.Preset25"] = "25 Min",

            ["Timer.Quick"] = "Quick",

            ["Timer.Rest"] = "Rest",

            ["Timer.Focus"] = "Focus",

            ["Timer.Pomodoro"] = "Pomodoro",



            // Settings Window

            ["Settings.Title"] = "Kuri Settings",

            ["Settings.Subtitle"] = "Customize display preferences, position, language, and overlay features",

            ["Settings.CategoryAppearance"] = "Appearance & Display",

            ["Settings.CategorySystem"] = "System & Behavior",

            ["Settings.CategoryWidgets"] = "Notch Widgets",

            ["Settings.CategoryWeather"] = "Weather Info",

            ["Settings.CategoryPrayer"] = "Prayer Times",

            ["Settings.CategoryWater"] = "Water Reminder",

            ["Settings.DescSystem"] = "Configure Windows autostart, fullscreen behavior, and privacy indicators",

            ["Settings.DescWidgets"] = "Customize active modules and widgets on the notch",

            ["Settings.DescWeather"] = "Weather forecast, real-time temperature, and city location settings",

            ["Settings.DescPrayer"] = "Automatic 5 prayer times schedule and adhan audio alerts",

            ["Settings.DescWater"] = "Regular water drink reminders and daily hydration tracking",

            ["Settings.SelectMascotTitle"] = "CHOOSE COMPANION MASCOT (PURE VECTOR)",

            ["Settings.AccentPreviewTooltip"] = "Accent Color Preview",


















            ["Settings.LanguageSection"] = "LANGUAGE / BAHASA",

            ["Settings.LanguageLabel"] = "Display Language",

            ["Settings.LanguageDesc"] = "Select between English (default) or Bahasa Indonesia",

            ["Settings.PosDisplaySection"] = "POSITION & DISPLAY",

            ["Settings.NotchScaleTitle"] = "Notch Size (Scale)",

            ["Settings.NotchScaleDesc"] = "Adjust physical notch scale according to screen resolution",

            ["Settings.FontScaleTitle"] = "Font Size (Text Scale)",

            ["Settings.FontScaleDesc"] = "Scale text size up or down across the notch interface (80% - 130%)",

            ["Settings.FontScalePresetSmall"] = "Small (85%)",

            ["Settings.FontScalePresetNormal"] = "Standard (100%)",

            ["Settings.FontScalePresetLarge"] = "Large (115%)",

            ["Settings.HoverDelayTitle"] = "Auto-collapse Delay",

            ["Settings.HoverDelayDesc"] = "Wait duration before notch collapses after cursor leaves",

            ["Settings.MonitorTitle"] = "Target Display Monitor",

            ["Settings.MonitorDesc"] = "Select the monitor where the Dynamic Notch is displayed",

            ["Settings.AccentColorTitle"] = "Theme Accent Color",

            ["Settings.NotchMaterialTitle"] = "Notch Background Material",

            ["Settings.NotchMaterialDesc"] = "Choose visual material: True OLED Black, Tinted Glass, or Fluent Mica/Acrylic",

            ["Settings.CustomHexTitle"] = "Custom Accent Color (HEX)",

            ["Settings.CustomHexDesc"] = "Enter 6-digit hex code (e.g. #3B82F6, #10B981, #EC4899)",

            ["Header.Catatan"] = "NOTE & PRODUCTIVITY",

            ["Tab.Note"] = "Note",

            ["Header.Clipboard"] = "CLIPBOARD HISTORY",

            ["Tab.Clipboard"] = "Clipboard",

            ["Header.Shelf"] = "SHELF & TEMPORARY FILES",

            ["Tab.Shelf"] = "Shelf",

            ["Settings.AccentColorDesc"] = "Select accent color for sliders, buttons, and glow highlights",

            ["Settings.AlbumArtAccentTitle"] = "Album Artwork Dynamic Accent",

            ["Settings.AlbumArtAccentDesc"] = "Automatically adapt notch accent color based on current song artwork",

            ["Settings.SystemSection"] = "SYSTEM INTEGRATION",

            ["Settings.AutostartTitle"] = "Start with Windows",

            ["Settings.AutostartDesc"] = "Fast startup at Windows sign-in via a scheduled task (Administrator approval required)",

            ["Settings.AutostartFallbackMessage"] = "The fast startup task could not be registered, so autostart used the classic method and may start later. Turn autostart off and on again, then approve the Administrator prompt.",

            ["Settings.AutostartEnableFailedMessage"] = "Kuri could not enable autostart. Check Windows permissions or approve the Administrator prompt, then try again.",

            ["Settings.AutostartDisableFailedMessage"] = "Kuri could not remove the startup task because Administrator approval was not granted. Autostart is still enabled.",

            ["Settings.StartupSoundTitle"] = "Startup Welcome Sound",

            ["Settings.StartupSoundDesc"] = "Play a short welcome chime when Kuri starts",

            ["Settings.FullscreenHideTitle"] = "Auto-hide on Fullscreen Apps",

            ["Settings.FullscreenHideDesc"] = "Temporarily hide notch when playing games or full-screen movies",

            ["Settings.FullscreenSlimBarTitle"] = "Show Slim Line in Fullscreen",

            ["Settings.FullscreenSlimBarDesc"] = "Keep a small notch line at the top of the screen instead of hiding completely",

            ["Settings.WidgetsSection"] = "WIDGETS & OVERLAYS",

            ["Settings.MediaTitle"] = "Music & Media Player Overlay",

            ["Settings.MediaDesc"] = "Show currently playing song, album cover, and playback controls",

            ["Settings.PrivacyTitle"] = "Camera & Microphone Privacy Dots",

            ["Settings.PrivacyDesc"] = "Show green/orange dots when webcam or mic is active",

            ["Settings.NotificationDurationTitle"] = "Notification Duration",
            ["Settings.NotificationDurationDesc"] = "How long floating notifications (USB, Bluetooth, Water, etc.) remain visible (Adzan excluded)",
            ["Settings.SecondsFormat"] = "{0} seconds",

            ["Settings.WeatherSection"] = "WEATHER INFORMATION",

            ["Settings.WeatherEnableTitle"] = "Enable Weather Slide",

            ["Settings.WeatherEnableDesc"] = "Display live temperature, conditions, and forecast",

            ["Settings.WeatherLocationTitle"] = "Weather City Location",

            ["Settings.WeatherLocationDesc"] = "Search or pick city location for weather data",

            ["Settings.WeatherFahrenheitTitle"] = "Use Fahrenheit (°F)",

            ["Settings.WeatherFahrenheitDesc"] = "Default is Celsius (°C)",

            ["Settings.PrayerSection"] = "PRAYER SCHEDULE",

            ["Settings.PrayerEnableTitle"] = "Enable Prayer Times Slide",

            ["Settings.PrayerEnableDesc"] = "Display 5 daily prayer times and countdown",

            ["Settings.PrayerLocationTitle"] = "Prayer City Location",

            ["Settings.PrayerLocationDesc"] = "Calculate prayer times based on city coordinates",

            ["Settings.PrayerSoundTitle"] = "Play Prayer Call Notification Sound",

            ["Settings.PrayerSoundDesc"] = "Play opening Adhan call (Allahu Akbar) when prayer time arrives",

            ["Settings.HydrationSection"] = "HYDRATION & SCREEN TIME",

            ["Settings.WaterEnableTitle"] = "Enable Water Reminder",

            ["Settings.WaterEnableDesc"] = "Track daily water intake and remind regularly",

            ["Settings.WaterGoalTitle"] = "Daily Target (ml)",

            ["Settings.WaterGoalDesc"] = "Recommended standard is 2,000 ml (8 glasses)",

            ["Settings.WaterIntervalTitle"] = "Reminder Frequency",

            ["Settings.WaterIntervalDesc"] = "Timer pauses when laptop/PC is standby",

            ["Settings.WaterSoundTitle"] = "Water Reminder Sound",

            ["Settings.WaterSoundDesc"] = "Play soft water droplet sound",

            ["Settings.SearchPlaceholder"] = "Search city or location...",

            ["Settings.SearchButton"] = "Search",

            ["Settings.SearchingLocation"] = "Searching location...",

            ["Settings.NoLocationFound"] = "No location found.",

            ["Settings.LocationsFound"] = "{0} location(s) found:",

            ["Settings.QuickPresets"] = "Quick presets:",

            ["Settings.Active"] = "Active",

            ["Settings.WeatherCurrentLocation"] = "Current Weather Location",

            ["Settings.SearchLocationManual"] = "Search City / Location Manually",

            ["Settings.SyncLocationPrayer"] = "Sync Location with Prayer Times",

            ["Settings.SyncLocationPrayerDesc"] = "Automatically sync prayer location coordinates when weather location changes",

            ["Settings.PrayerCurrentLocation"] = "Current Prayer Location",

            ["Settings.SearchPrayerManual"] = "Search Prayer Location Separately",

            ["Settings.QuickPresetsPrayer"] = "Quick presets for prayer cities:",

            ["Settings.QuickPresetsWeather"] = "Quick presets for cities:",

            ["Settings.DownloadMonitorTitle"] = "Download Monitor",

            ["Settings.DownloadMonitorDesc"] = "Show active downloads from browsers and other apps under the notch",

            ["Download.CompletedTitle"] = "Download complete",

            ["Download.CompletedMulti"] = "{0} downloads complete",

            ["Download.Paused"] = "Paused",

            ["Download.ActiveCount"] = "{0} downloads",

            ["Download.OpenFolder"] = "Open download folder",

            ["Download.Downloading"] = "Downloading...",

            ["Settings.SaveClose"] = "Save & Close",

            ["Settings.Close"] = "Close",

            ["Settings.ResetDefaults"] = "Reset to Defaults",

            ["Tray.Settings"] = "⚙️  Kuri Settings",

            ["Tray.Toggle"] = "👁️  Toggle Notch Visibility",

            ["Tray.Exit"] = "❌  Exit Kuri",

        };



        private static readonly Dictionary<string, string> IndonesianStrings = new(StringComparer.OrdinalIgnoreCase)

        {

            // Headers

            ["Header.SedangMemutar"] = "SEDANG MEMUTAR",

            ["Header.PusatKontrol"] = "PUSAT KONTROL",

            ["Header.Cuaca"] = "CUACA",

            ["Header.JadwalSholat"] = "JADWAL SHOLAT",

            ["Header.PengingatMinum"] = "PENGINGAT MINUM & DUDUK",

            ["Header.DaftarTugas"] = "DAFTAR TUGAS",

            ["Header.TimerStopwatch"] = "TIMER & STOPWATCH",



            // Top Navigation Tabs

            ["Tab.ControlCenter"] = "Kontrol",

            ["Tab.Music"] = "Musik",

            ["Tab.Weather"] = "Cuaca",

            ["Tab.Prayer"] = "Sholat",

            ["Tab.Hydration"] = "Minum",

            ["Tab.Todo"] = "Tugas",

            ["Tab.Timer"] = "Timer",

            ["Tab.Settings"] = "Pengaturan",



            // Collapsed / Media

            ["Media.NoMedia"] = "Tidak ada media diputar",

            ["Media.StartPlaying"] = "Mulai putar lagu di Spotify atau YouTube",

            ["Media.SearchingLyrics"] = "Mencari lirik...",

            ["Media.LyricsNotFound"] = "Lirik tidak ditemukan",

            ["Media.LyricsUnavailable"] = "Lirik belum tersedia",

            ["Media.LyricsVersions"] = "VERSI LIRIK",

            ["Media.LyricsVersionAuto"] = "Auto (terbaik)",

            ["Media.LyricsVersionTooltip"] = "Pilih versi lirik",

            ["Media.Instrumental"] = "🎵 Lagu instrumental (tanpa vokal)",

            ["Media.KaraokeTooltip"] = "Lirik Lagu Sinkron (Karaoke)",

            ["Media.CloseLyrics"] = "Tutup Tampilan Lirik",

            ["Media.PreviousTrack"] = "Lagu Sebelumnya",

            ["Media.PlayPause"] = "Play / Pause",

            ["Media.NextTrack"] = "Lagu Berikutnya",

            ["Media.DelayAdjust"] = "Sesuaikan Delay Lirik",



            // Pin & System

            ["Pin.Pin"] = "Sematkan Notch (tetap terbuka saat mouse keluar)",

            ["Pin.Unpin"] = "Buka Kunci Notch (otomatis mengecil saat mouse keluar)",

            ["Volume.Mute"] = "Senyap",

            ["Battery.Charging"] = "Mengisi Daya",

            ["Battery.FullyCharged"] = "Baterai Penuh",

            ["Battery.BatteryLow"] = "Baterai Lemah",

            ["Notification.StorageConnected"] = "Penyimpanan Tersambung",

            ["Notification.StorageDisconnected"] = "Penyimpanan Dilepas",

            ["Notification.UsbReady"] = "Drive USB siap digunakan",

            ["Notification.UsbRemoved"] = "Perangkat USB telah dicabut",

            ["Notification.BluetoothOn"] = "Bluetooth Aktif",

            ["Notification.BluetoothOff"] = "Bluetooth Nonaktif",

            ["Notification.BluetoothOnMessage"] = "Radio Bluetooth telah diaktifkan",

            ["Notification.BluetoothOffMessage"] = "Radio Bluetooth telah dimatikan",

            ["Notification.DriveReady"] = "Drive ({0}) siap digunakan",

            ["Notification.DriveRemoved"] = "Perangkat ({0}) telah dicabut",

            ["Notification.TimerFinishedTitle"] = "Waktu Timer Habis!",

            ["Notification.TimerFinishedDesc"] = "Penghitung waktu telah selesai.",

            ["Notification.WaterReminderTitle"] = "Waktunya Minum Air!",

            ["Notification.WaterSnoozeAction"] = "Tunda 2m",

            ["Notification.WaterDrinkAction"] = "Minum",

            ["Notification.PrayerReminderTitle"] = "Waktu Sholat",

            ["Notification.DismissTooltip"] = "Klik untuk menutup notifikasi",

            ["Notification.Today"] = "Hari ini",

            ["Notification.Tomorrow"] = "Besok",

            ["Notification.AlarmSet"] = "Alarm diatur: {0} ({1})",

            ["Notification.AlarmTimeSet"] = "Waktu alarm diatur: {0}:{1}",

            ["Notification.Reminder"] = "Pengingat: {0}",

            ["Notification.NoteCreated"] = "Catatan baru dibuat",

            ["Notification.NoteCleared"] = "Catatan dikosongkan",

            ["Notification.NoteDeleted"] = "Catatan dihapus",

            ["Notification.NoteCopied"] = "Catatan disalin ke Clipboard!",

            ["Notification.FocusTimeComplete"] = "Waktu Fokus Selesai! Saatnya istirahat sejenak.",

            ["Notification.TimerMinutes"] = "Timer: {0} Menit",

            ["Notification.ClipboardCopied"] = "Disalin ke Papan Klip!",

            ["Notification.ScreenshotCaptured"] = "Tangkapan Layar Tersimpan",

            ["Notification.TextCopied"] = "Teks Disalin",

            ["Notification.BluetoothConnected"] = "Bluetooth Terhubung",

            ["Notification.BluetoothDisconnected"] = "Bluetooth Terputus",



            // Control Center

            ["Control.Volume"] = "Volume Master",

            ["Control.Brightness"] = "Kecerahan Layar",

            ["Control.QuickActions"] = "AKSI CEPAT",

            ["Control.BatteryLevel"] = "Level Baterai",

            ["Control.PluggedIn"] = "Terhubung listrik",

            ["Control.OnBattery"] = "Menggunakan baterai",

            ["Action.Screenshot"] = "Screenshot",

            ["Action.Lock"] = "Kunci PC",

            ["Action.Weather"] = "Cuaca",

            ["Action.Settings"] = "Pengaturan",



            // Weather

            ["Weather.FeelsLike"] = "Terasa",

            ["Weather.Humidity"] = "Kelembapan",

            ["Weather.HumidityShort"] = "Lembap",

            ["Weather.Wind"] = "Angin",

            ["Weather.WindShort"] = "Angin",

            ["Weather.Precipitation"] = "Hujan",

            ["Weather.RainShort"] = "Hujan",

            ["Weather.UvIndex"] = "Indeks UV",

            ["Weather.Forecast"] = "Prakiraan 7 Hari",

            ["Weather.Today"] = "Hari ini",

            ["Weather.Now"] = "Sekarang",

            ["Weather.Searching"] = "Mencari lokasi...",

            ["Weather.Updated"] = "Diperbarui {0}",

            ["Weather.NotUpdated"] = "Belum diperbarui",

            ["Weather.Loading"] = "Memuat Cuaca...",
            ["Weather.DataSourceBmkg"] = "Data: BMKG (Badan Meteorologi RI)",
            ["Weather.DataSourceOpenMeteo"] = "Data: Open-Meteo (Global ECMWF)",



            // Prayer Times

            ["Prayer.Fajr"] = "Subuh",

            ["Prayer.Subuh"] = "Subuh",
            ["Prayer.Dhuha"] = "Dhuha",
            ["Prayer.Sunrise"] = "Terbit",

            ["Prayer.Dhuhr"] = "Dzuhur",

            ["Prayer.Asr"] = "Ashar",

            ["Prayer.Maghrib"] = "Maghrib",

            ["Prayer.Isha"] = "Isya",

            ["Prayer.NextPrayer"] = "Sholat Berikutnya",

            ["Prayer.Countdown"] = "Menuju",

            ["Prayer.Towards"] = "Menuju",
            ["Prayer.Tomorrow"] = "Besok",



            // Hydration & Sedentary

            ["Water.Title"] = "PENGINGAT MINUM",

            ["Water.DailyTarget"] = "Target Harian",

            ["Water.Add250"] = "+250 ml",

            ["Water.Reset"] = "Batal",

            ["Water.TodayLog"] = "Hari ini: {0:F1}L dari {1:F1}L",

            ["Water.GlassesLog"] = "{0} / {1} Gelas (250ml)",

            ["Water.StatusNormal"] = "Tetap terhidrasi saat fokus bekerja 💧",

            ["Water.StatusWarning"] = "Segera istirahat dan minum air 💧",

            ["Water.StatusOverdue"] = "⚠️ Waktunya Minum Air!",

            ["Water.StatusStandby"] = "Kursor diam - Timer dijeda 💤",

            ["Sedentary.Title"] = "PENGINGAT DUDUK",

            ["Sedentary.SittingTime"] = "Waktu Duduk",

            ["Sedentary.Standby"] = "Mode Standby",

            ["Sedentary.StatsHeader"] = "STATISTIK DUDUK HARIAN",

            ["Sedentary.StatsDesc"] = "Lama waktu aktif depan layar",

            ["Sedentary.ClickToFlip"] = "Klik untuk statistik harian",

            ["Sedentary.Back"] = "↩ Kembali",

            ["Sedentary.TotalToday"] = "Total Hari Ini",

            ["Sedentary.LongestSession"] = "Sesi Terlama",

            ["Sedentary.StandBreaks"] = "Frekuensi Berdiri",

            ["Sedentary.BreaksUnit"] = "{0}x Berdiri",

            ["Sedentary.StatusOptimal"] = "Postur duduk aman",

            ["Sedentary.StatusWarning"] = "⚠️ Segera peregangan",

            ["Sedentary.StatusOverdue"] = "⚠️ Duduk terlalu lama!",

            ["Sedentary.StatusStandby"] = "💤 Standby (Timer dijeda)",



            // Todo

            ["Todo.Title"] = "TUGAS HARI INI",

            ["Todo.Summary"] = "{0}/{1} Selesai",

            ["Todo.EmptyTitle"] = "Belum ada tugas hari ini",

            ["Todo.EmptyDesc"] = "Ketik di bawah untuk menambah tugas baru",

            ["Todo.Placeholder"] = "Tambah tugas baru...",



            // Timer & Stopwatch

            ["Timer.TabTimer"] = "Timer",

            ["Timer.TabStopwatch"] = "Stopwatch",

            ["Timer.Start"] = "Mulai",

            ["Timer.Pause"] = "Jeda",

            ["Timer.Resume"] = "Lanjut",

            ["Timer.Reset"] = "Reset",

            ["Timer.Lap"] = "Putaran",

            ["Timer.NoLaps"] = "Belum ada putaran tercatat",

            ["Timer.PressLap"] = "Tekan Lap saat berjalan",

            ["Timer.Preset1"] = "1 Mnt",

            ["Timer.Preset5"] = "5 Mnt",

            ["Timer.Preset10"] = "10 Mnt",

            ["Timer.Preset25"] = "25 Mnt",

            ["Timer.Quick"] = "Cepat",

            ["Timer.Rest"] = "Istirahat",

            ["Timer.Focus"] = "Fokus",

            ["Timer.Pomodoro"] = "Pomodoro",



            // Settings Window

            ["Settings.Title"] = "Pengaturan Kuri",

            ["Settings.Subtitle"] = "Sesuaikan preferensi tampilan, posisi, bahasa, dan fitur overlay",

            ["Settings.CategoryAppearance"] = "Tampilan & Umum",

            ["Settings.CategorySystem"] = "Sistem & Perilaku",

            ["Settings.CategoryWidgets"] = "Widget Notch",

            ["Settings.CategoryWeather"] = "Cuaca",

            ["Settings.CategoryPrayer"] = "Jadwal Sholat",
            ["Settings.CategoryWater"] = "Pengingat Minum",
            ["Settings.DescSystem"] = "Konfigurasi autostart Windows, perilaku fullscreen, dan sensor privasi",
            ["Settings.DescWidgets"] = "Kustomisasi modul dan widget yang aktif pada notch",
            ["Settings.DescWeather"] = "Prakiraan cuaca, suhu real-time, dan pengaturan lokasi kota",
            ["Settings.DescPrayer"] = "Jadwal sholat 5 waktu otomatis dan notifikasi adzan",
            ["Settings.DescWater"] = "Pengingat minum air teratur dan target hidrasi harian",
            ["Settings.SelectMascotTitle"] = "PILIH MASKOT COMPANION (VEKTOR MURNI)",
            ["Settings.AccentPreviewTooltip"] = "Preview Warna Aksen",

















            ["Settings.LanguageSection"] = "BAHASA / LANGUAGE",

            ["Settings.LanguageLabel"] = "Bahasa Tampilan",

            ["Settings.LanguageDesc"] = "Pilih antara Bahasa Inggris (default) atau Bahasa Indonesia",

            ["Settings.PosDisplaySection"] = "POSISI & TAMPILAN",

            ["Settings.NotchScaleTitle"] = "Ukuran Notch (Skala)",

            ["Settings.NotchScaleDesc"] = "Sesuaikan ukuran fisik notch sesuai selera dan resolusi layar",

            ["Settings.FontScaleTitle"] = "Ukuran Font (Skala Teks)",

            ["Settings.FontScaleDesc"] = "Perbesar atau perkecil ukuran teks pada notch (80% - 130%)",

            ["Settings.FontScalePresetSmall"] = "Kecil (85%)",

            ["Settings.FontScalePresetNormal"] = "Standar (100%)",

            ["Settings.FontScalePresetLarge"] = "Besar (115%)",

            ["Settings.HoverDelayTitle"] = "Jeda Otomatis Menutup",

            ["Settings.HoverDelayDesc"] = "Waktu tunggu sebelum notch kembali mengecil saat mouse keluar",

            ["Settings.MonitorTitle"] = "Pilih Layar / Monitor",

            ["Settings.MonitorDesc"] = "Pilih monitor tempat Dynamic Notch akan ditampilkan",

            ["Settings.AccentColorTitle"] = "Warna Aksen Tema",

            ["Settings.NotchMaterialTitle"] = "Material Latar Belakang Notch",

            ["Settings.NotchMaterialDesc"] = "Pilih material visual: True OLED Black, Tinted Glass, atau Fluent Mica/Acrylic",

            ["Settings.CustomHexTitle"] = "Warna Aksen Kustom (HEX)",

            ["Settings.CustomHexDesc"] = "Ketik kode warna hex (contoh #3B82F6, #10B981, #EC4899)",

            ["Header.Catatan"] = "CATATAN & PRODUKTIVITAS",

            ["Tab.Note"] = "Catatan",

            ["Header.Clipboard"] = "RIWAYAT PAPAN KLIP",

            ["Tab.Clipboard"] = "Papan Klip",

            ["Header.Shelf"] = "SHELF & BERKAS SEMENTARA",

            ["Tab.Shelf"] = "Shelf",

            ["Settings.AccentColorDesc"] = "Pilih warna aksen untuk slider, tombol, dan indikator",

            ["Settings.AlbumArtAccentTitle"] = "Warna Aksen Dinamis Album",

            ["Settings.AlbumArtAccentDesc"] = "Otomatis menyelaraskan gradasi notch dengan warna cover album lagu yang sedang diputar",

            ["Settings.SystemSection"] = "INTEGRASI SISTEM",

            ["Settings.AutostartTitle"] = "Jalankan Otomatis saat Windows Menyala",

            ["Settings.AutostartDesc"] = "Autostart cepat saat sign-in Windows lewat tugas terjadwal (perlu persetujuan Administrator)",

            ["Settings.AutostartFallbackMessage"] = "Tugas terjadwal tidak dapat didaftarkan, jadi autostart memakai metode klasik dan bisa terlambat. Matikan lalu nyalakan lagi autostart, lalu setujui prompt Administrator.",

            ["Settings.AutostartEnableFailedMessage"] = "Kuri gagal mengaktifkan autostart. Periksa izin Windows atau setujui prompt Administrator, lalu coba lagi.",

            ["Settings.AutostartDisableFailedMessage"] = "Kuri gagal menghapus tugas startup karena persetujuan Administrator tidak diberikan. Autostart masih aktif.",

            ["Settings.StartupSoundTitle"] = "Suara Selamat Datang",

            ["Settings.StartupSoundDesc"] = "Putar nada selamat datang singkat setiap kali Kuri dijalankan",

            ["Settings.FullscreenHideTitle"] = "Sembunyikan Otomatis saat Layar Penuh",

            ["Settings.FullscreenHideDesc"] = "Sembunyikan notch otomatis saat bermain game atau menonton video layar penuh",

            ["Settings.FullscreenSlimBarTitle"] = "Tampilkan Garis Tipis saat Layar Penuh",

            ["Settings.FullscreenSlimBarDesc"] = "Tampilkan garis notch kecil di atas layar, tidak sembunyi total",

            ["Settings.WidgetsSection"] = "WIDGET & FITUR",

            ["Settings.MediaTitle"] = "Pemutar Musik & Media",

            ["Settings.MediaDesc"] = "Tampilkan lagu yang sedang diputar, cover album, dan kontrol audio",

            ["Settings.PrivacyTitle"] = "Indikator Privasi Kamera & Mikrofon",

            ["Settings.PrivacyDesc"] = "Tampilkan titik hijau/oranye saat webcam atau mic aktif",

            ["Settings.NotificationDurationTitle"] = "Durasi Notifikasi Notch",
            ["Settings.NotificationDurationDesc"] = "Lama notifikasi mengambang (USB, Bluetooth, Air Minum, dll.) tampil di notch (Adzan dikecualikan)",
            ["Settings.SecondsFormat"] = "{0} detik",

            ["Settings.WeatherSection"] = "INFORMASI CUACA",

            ["Settings.WeatherEnableTitle"] = "Aktifkan Widget Cuaca",

            ["Settings.WeatherEnableDesc"] = "Tampilkan suhu, cuaca terkini, dan prakiraan harian",

            ["Settings.WeatherLocationTitle"] = "Lokasi Kota Cuaca",

            ["Settings.WeatherLocationDesc"] = "Cari atau pilih lokasi kota untuk data cuaca",

            ["Settings.WeatherFahrenheitTitle"] = "Gunakan Satuan Fahrenheit (°F)",

            ["Settings.WeatherFahrenheitDesc"] = "Secara bawaan menggunakan satuan Celsius (°C)",

            ["Settings.PrayerSection"] = "JADWAL SHOLAT",

            ["Settings.PrayerEnableTitle"] = "Aktifkan Widget Jadwal Sholat",

            ["Settings.PrayerEnableDesc"] = "Tampilkan jadwal 5 waktu sholat dan countdown menuju sholat berikutnya",

            ["Settings.PrayerLocationTitle"] = "Lokasi Kota Sholat",

            ["Settings.PrayerLocationDesc"] = "Hitung waktu sholat berdasarkan koordinat kota",

            ["Settings.PrayerSoundTitle"] = "Bunyikan Notifikasi Audio saat Waktu Sholat",

            ["Settings.PrayerSoundDesc"] = "Bunyikan bait pembuka adzan (Allahu Akbar) saat waktu sholat telah tiba",

            ["Settings.HydrationSection"] = "PENGINGAT MINUM & DUDUK",

            ["Settings.WaterEnableTitle"] = "Aktifkan Pengingat Minum",

            ["Settings.WaterEnableDesc"] = "Catat asupan air harian dan ingatkan secara berkala",

            ["Settings.WaterGoalTitle"] = "Target Harian (ml)",

            ["Settings.WaterGoalDesc"] = "Rekomendasi standar 2.000 ml (8 gelas)",

            ["Settings.WaterIntervalTitle"] = "Interval Waktu Pengingat",

            ["Settings.WaterIntervalDesc"] = "Timer otomatis terjeda saat komputer standby",

            ["Settings.WaterSoundTitle"] = "Suara Pengingat Minum",

            ["Settings.WaterSoundDesc"] = "Bunyikan suara tetesan air saat pengingat minum aktif",

            ["Settings.SearchPlaceholder"] = "Ketik nama kota atau lokasi...",

            ["Settings.SearchButton"] = "Cari",

            ["Settings.SearchingLocation"] = "Mencari lokasi...",

            ["Settings.NoLocationFound"] = "Lokasi tidak ditemukan.",

            ["Settings.LocationsFound"] = "{0} lokasi ditemukan:",

            ["Settings.QuickPresets"] = "Pilihan cepat:",

            ["Settings.Active"] = "Aktif",

            ["Settings.WeatherCurrentLocation"] = "Lokasi Cuaca Saat Ini",

            ["Settings.SearchLocationManual"] = "Cari Lokasi / Kota Manual",

            ["Settings.SyncLocationPrayer"] = "Selaraskan Lokasi dengan Jadwal Sholat",

            ["Settings.SyncLocationPrayerDesc"] = "Otomatis samakan koordinat lokasi jadwal sholat saat lokasi cuaca diubah",

            ["Settings.PrayerCurrentLocation"] = "Lokasi Jadwal Sholat Saat Ini",

            ["Settings.SearchPrayerManual"] = "Cari Lokasi Sholat Mandiri",

            ["Settings.QuickPresetsPrayer"] = "Pilihan Cepat Kota Sholat:",

            ["Settings.QuickPresetsWeather"] = "Pilihan Cepat Kota Indonesia:",

            ["Settings.DownloadMonitorTitle"] = "Pemantau Unduhan",

            ["Settings.DownloadMonitorDesc"] = "Tampilkan unduhan aktif dari browser dan aplikasi lain di bawah notch",

            ["Download.CompletedTitle"] = "Unduhan selesai",

            ["Download.CompletedMulti"] = "{0} unduhan selesai",

            ["Download.Paused"] = "Dijeda",

            ["Download.ActiveCount"] = "{0} unduhan",

            ["Download.OpenFolder"] = "Buka folder unduhan",

            ["Download.Downloading"] = "Mengunduh...",

            ["Settings.SaveClose"] = "Simpan & Tutup",

            ["Settings.Close"] = "Tutup",

            ["Settings.ResetDefaults"] = "Atur Ulang Pengaturan",

            ["Tray.Settings"] = "⚙️  Pengaturan Kuri",

            ["Tray.Toggle"] = "👁️  Sembunyikan / Tampilkan Notch",

            ["Tray.Exit"] = "❌  Keluar Kuri",

        };

    }

}

