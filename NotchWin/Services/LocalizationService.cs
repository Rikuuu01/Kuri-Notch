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

            ["Header.Home"] = "HOME",

            ["Header.SedangMemutar"] = "NOW PLAYING",

            ["Header.PusatKontrol"] = "CONTROL CENTER",

            ["Header.Cuaca"] = "WEATHER",

            ["Header.JadwalSholat"] = "PRAYER TIMES",

            ["Header.PengingatMinum"] = "HYDRATION & HEALTH",

            ["Header.DaftarTugas"] = "TO-DO LIST",

            ["Header.TimerStopwatch"] = "TIMER & STOPWATCH",



            // Top Navigation Tabs

            ["Tab.Home"] = "Home",

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
            ["Media.LyricsPickerTitle"] = "Select Lyrics Version",
            ["Media.LyricsBackToLyrics"] = "Lyrics",

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

            ["Settings.CategoryModules"] = "Navbar Modules",

            ["Settings.DescSystem"] = "Configure Windows autostart, fullscreen behavior, and privacy indicators",

            ["Settings.DescWidgets"] = "Customize active modules and widgets on the notch",

            ["Settings.DescWeather"] = "Weather forecast, real-time temperature, and city location settings",

            ["Settings.DescPrayer"] = "Automatic 5 prayer times schedule and adhan audio alerts",

            ["Settings.DescWater"] = "Regular water drink reminders and daily hydration tracking",

            ["Settings.DescModules"] = "Manage, reorder, and enable or disable slides/menus in the notch navbar",

            ["Settings.ModulesTitle"] = "ACTIVE NAVBAR MODULES",
            ["Settings.ModulesDesc"] = "Use arrow buttons to adjust order and switches to enable or disable modules.",
            ["Settings.ResetModules"] = "Reset Order",
            ["Settings.ModuleHome"] = "Home / Dashboard",
            ["Settings.ModuleHomeDesc"] = "Clock, date, battery, system status, and companion pet",
            ["Settings.ModuleControl"] = "Control Center",
            ["Settings.ModuleControlDesc"] = "Volume, brightness, quick toggles, and system shortcuts",
            ["Settings.ModuleMedia"] = "Media Player",
            ["Settings.ModuleMediaDesc"] = "Playback controls, album art, and synchronized lyrics",
            ["Settings.ModuleWeather"] = "Weather Forecast",
            ["Settings.ModuleWeatherDesc"] = "Live temperature, weather condition, and city forecasts",
            ["Settings.ModulePrayer"] = "Prayer Times",
            ["Settings.ModulePrayerDesc"] = "Muslim daily prayer schedule countdown and reminders",
            ["Settings.ModuleWater"] = "Water & Posture",
            ["Settings.ModuleWaterDesc"] = "Hydration tracking, posture breaks, and daily targets",
            ["Settings.ModuleNote"] = "Quick Notes",
            ["Settings.ModuleNoteDesc"] = "Instant memo notes, scratchpad, and quick reminders",
            ["Settings.ModuleClipboard"] = "Clipboard History",
            ["Settings.ModuleClipboardDesc"] = "Recently copied snippets with 1-click clipboard paste",
            ["Settings.ModuleTimer"] = "Timer & Stopwatch",
            ["Settings.ModuleTimerDesc"] = "Focus timer presets, countdown, stopwatch, and laps",
            ["Settings.ModuleShelf"] = "Drop Shelf & Phone Share",
            ["Settings.ModuleShelfDesc"] = "File drop shelf, temporary storage, and QR phone sharing",
            ["Settings.ModuleScreenTime"] = "Screen Time",
            ["Settings.ModuleScreenTimeDesc"] = "App usage duration, foreground activity, and top apps",
            ["Settings.CategoryScreenTime"] = "Screen Time",
            ["Settings.ScreenTimeTitle"] = "SCREEN TIME SETTINGS",
            ["Settings.ScreenTimeDesc"] = "Counts time in the foreground app (idle time excluded) and app switches. Stored locally in screentime.json (35 days).",
            ["Settings.EnableScreenTime"] = "Track Screen Time",
            ["Settings.ScreenTimeIdleThreshold"] = "Idle Detection Threshold",
            ["Settings.ScreenTimeIdleDesc"] = "Pause counting after no mouse or keyboard input (seconds):",
            ["Settings.ClearScreenTimeHistory"] = "Clear Screen Time History",
            ["Settings.ScreenTimeCleared"] = "Screen Time history has been cleared.",
            ["Header.ScreenTime"] = "SCREEN TIME",
            ["Tab.ScreenTime"] = "Screen",
            ["ScreenTime.Header"] = "SCREEN TIME",
            ["ScreenTime.TotalToday"] = "Screen Time Today",
            ["ScreenTime.AppSwitches"] = "switches",
            ["ScreenTime.IdleExcluded"] = "Idle excluded",
            ["ScreenTime.MostUsed"] = "MOST USED APPS",
            ["ScreenTime.NoActivity"] = "Screen Time starts counting now.",
            ["ScreenTime.CategoryBrowsing"] = "Browsing",
            ["ScreenTime.CategoryDevelopment"] = "Development",
            ["ScreenTime.CategoryEntertainment"] = "Entertainment",
            ["ScreenTime.CategoryProductivity"] = "Productivity",
            ["ScreenTime.CategorySystem"] = "System",
            ["ScreenTime.CategoryOther"] = "Other",

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

            ["Settings.AccentColorEnabledTitle"] = "Enable Accent Colors",

            ["Settings.AccentColorEnabledDesc"] = "Enable vibrant accent colors for borders, buttons, and highlights. When turned off, a sleek neutral monochrome appearance is used.",

            ["Settings.CollapsedSection"] = "COLLAPSED NOTCH (STATUS BAR)",

            ["Settings.ShowWeatherCollapsedTitle"] = "Show Weather in Collapsed Notch",

            ["Settings.ShowWeatherCollapsedDesc"] = "Display weather condition icon and temperature in the collapsed notch status bar.",

            ["Settings.ShowBatteryCollapsedTitle"] = "Show Battery in Collapsed Notch",

            ["Settings.ShowBatteryCollapsedDesc"] = "Display battery percentage and level indicator in the collapsed notch status bar.",

            ["Settings.ShowPetCollapsedTitle"] = "Show Mascot in Collapsed Notch",

            ["Settings.ShowPetCollapsedDesc"] = "Display active pet companion animation in the collapsed notch status bar.",

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

            ["Shelf.EmptyTitle"] = "Drag & Drop Files / Images Here",
            ["Shelf.EmptyDesc"] = "Store temporary files to copy or move between folders & apps (or click to select files)",
            ["Shelf.Header"] = "TEMPORARY FILE SHELF",
            ["Shelf.AddButton"] = "+ Add",
            ["Shelf.CopyAllButton"] = "Copy All",
            ["Shelf.ClearButton"] = "Clear",
            ["Shelf.OpenButton"] = "Open",
            ["Shelf.CopyButton"] = "Copy",
            ["Shelf.RemoveTooltip"] = "Remove from Shelf",
            ["Shelf.SelectFilesTitle"] = "Select files to temporarily store in Shelf",
            ["Shelf.EmptyCount"] = "Empty Shelf",
            ["Shelf.OneItem"] = "1 Item",
            ["Shelf.ItemsCount"] = "{0} Items",
            ["Shelf.FileNotFound"] = "File not found",
            ["Shelf.DriveNotFound"] = "Drive not found",
            ["Shelf.PhoneButton"] = "Phone",
            ["Shelf.PhoneTooltip"] = "Share files with your phone over Wi-Fi",

            ["DropAction.Title"] = "DROP CHOICE",
            ["DropAction.Instruction"] = "Point and release the file on a destination below:",
            ["DropAction.ShelfTitle"] = "Keep in Shelf",
            ["DropAction.ShelfDesc"] = "Stash files or folders to copy or move them later",
            ["DropAction.ShelfBadge"] = "📥 Drop to Shelf",
            ["DropAction.ShelfBadgeActive"] = "✨ Drop to keep in Shelf!",
            ["DropAction.ConvertTitle"] = "Convert Image",
            ["DropAction.ConvertDesc"] = "JPG to PNG, PNG/BMP/GIF to JPG. Same folder.",
            ["DropAction.ConvertBadge"] = "🔄 Drop to Convert",
            ["DropAction.ConvertBadgeActive"] = "✨ Drop to convert now!",
            ["DropAction.DockTitle"] = "Add to Dock",
            ["DropAction.DockDesc"] = "Pin a shortcut in the bottom dock for quick access",
            ["DropAction.DockBadge"] = "🚀 Drop to Dock",
            ["DropAction.DockBadgeActive"] = "✨ Drop to add to Dock!",

            ["Convert.SuccessTitle"] = "Converted",
            ["Convert.SuccessMessage"] = "{0} file(s) converted",
            ["Convert.NothingTitle"] = "Nothing to do",
            ["Convert.NothingMessage"] = "No convertible images in the drop",
            ["Convert.FailedTitle"] = "Conversion failed",
            ["Convert.FailedMessage"] = "{0} file(s) could not be converted",

            ["Phone.WindowTitle"] = "Phone Share",
            ["Phone.ScanHint"] = "Scan with phone camera",
            ["Phone.OpenHint"] = "Phone and PC must be on the same Wi-Fi network. Allow Kuri through Windows Firewall if prompted.",
            ["Phone.CopyLink"] = "Copy Link",
            ["Phone.LinkCopied"] = "Link copied",
            ["Phone.Start"] = "Start",
            ["Phone.Stop"] = "Stop",
            ["Phone.StatusStopped"] = "Server stopped",
            ["Phone.StatusRunning"] = "Waiting for phone...",
            ["Phone.StatusConnected"] = "Phone connected",
            ["Phone.Error"] = "Could not start the share server",
            ["Phone.UploadNotificationTitle"] = "File from phone",
            ["Phone.TextNotificationTitle"] = "Text from phone",
            ["Phone.LinkNotificationTitle"] = "Link from phone",
            ["Phone.UploadFolderUnavailable"] = "Upload folder is not available",

            ["PhonePage.Title"] = "Kuri Phone Share",
            ["PhonePage.Subtitle"] = "Send files and text to your PC",
            ["PhonePage.UploadTitle"] = "Send files",
            ["PhonePage.ChooseFile"] = "Choose files",
            ["PhonePage.Uploading"] = "Uploading...",
            ["PhonePage.Uploaded"] = "Sent",
            ["PhonePage.UploadFailed"] = "Upload failed",
            ["PhonePage.TooLarge"] = "File is too large",
            ["PhonePage.TextTitle"] = "Send text or link",
            ["PhonePage.TextPlaceholder"] = "Type or paste text or a link here...",
            ["PhonePage.SendText"] = "Send to PC",
            ["PhonePage.Sent"] = "Sent to PC clipboard",
            ["PhonePage.FilesTitle"] = "Files on your PC",
            ["PhonePage.NoFiles"] = "Shelf is empty",
            ["PhonePage.Download"] = "Download",
            ["PhonePage.Connected"] = "Connected",
            ["PhonePage.Offline"] = "Offline",
            ["PhonePage.EmptyText"] = "Text is empty",
            ["PhonePage.TextTooLong"] = "Text is too long",
            ["PhonePage.MaxFileSize"] = "Max file size: {0}",
            ["PhonePage.MaxTextSize"] = "Max text length: {0} characters",

            ["Settings.ShelfSection"] = "SHELF & PHONE",
            ["Settings.ShelfRemoveAfterDragTitle"] = "Remove from Shelf after drag out",
            ["Settings.ShelfRemoveAfterDragDesc"] = "Remove the shelf entry when you drag it to another app. The original file is never deleted.",
            ["Settings.PhoneShareTitle"] = "Phone Share",
            ["Settings.PhoneShareDesc"] = "Send files and text from your phone through a local Wi-Fi link",
            ["Settings.PhoneFolderTitle"] = "Phone upload folder",
            ["Settings.BrowseButton"] = "Browse",
            ["Settings.PhoneShareHint"] = "Phone and PC must be connected to the same Wi-Fi network.",

            ["Settings.AccentColorDesc"] = "Select accent color for sliders, buttons, and glow highlights",

            ["Settings.AlbumArtAccentTitle"] = "Album Artwork Dynamic Accent",

            ["Settings.AlbumArtAccentDesc"] = "Automatically adapt notch accent color based on current song artwork",

            ["Settings.NotchTimerGlowTitle"] = "Timer Glow Outline",

            ["Settings.NotchTimerGlowDesc"] = "Trace a glowing line around the closed notch that shortens with the time left (green, amber, red) and blinks when it ends. The stopwatch traces a growing yellow line each minute.",

            ["Settings.NotchAlertGlowTitle"] = "Alert Glow Outline",

            ["Settings.NotchAlertGlowDesc"] = "A thin light runs round the closed notch when an alert shows — reminders, prayer times, downloads, charging — in its color, for the first 20 seconds.",

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

            ["Settings.LyricsKaraokeTitle"] = "Karaoke lyrics",
            ["Settings.LyricsKaraokeDesc"] = "Fill the current line with color as the song plays (timed lyrics only)",
            ["Settings.SyllableSyncTitle"] = "Spicy Lyrics (Word-by-word sync)",
            ["Settings.LrcRedTitle"] = "lrc.red Lyrics Provider",
            ["Settings.LrcRedDesc"] = "Fetch word-synced (TTML) and line-synced lyrics from lrc.red community database",
            ["Settings.SyllableSyncDesc"] = "Accurate word-by-word karaoke timing via Spicy Lyrics & TTML syllable precision",
            ["Settings.SpicyLyricsApiKeyTitle"] = "Spicy Lyrics API Key (Optional)",
            ["Settings.SpicyLyricsApiKeyDesc"] = "Developer token from developers.spicylyrics.org for uncached tracks. Leave blank to use free public mirror.",



            ["Settings.LyricsBoldTitle"] = "Bold lyrics",

            ["Settings.LyricsBoldDesc"] = "Use bold text for the active lyric line",

            ["Settings.LyricsTextSizeTitle"] = "Lyrics text size",

            ["Settings.LyricsTextSizeDesc"] = "Adjust the size of lyrics in the player",

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

            ["Header.Home"] = "BERANDA",

            ["Header.SedangMemutar"] = "SEDANG MEMUTAR",

            ["Header.PusatKontrol"] = "PUSAT KONTROL",

            ["Header.Cuaca"] = "CUACA",

            ["Header.JadwalSholat"] = "JADWAL SHOLAT",

            ["Header.PengingatMinum"] = "PENGINGAT MINUM & DUDUK",

            ["Header.DaftarTugas"] = "DAFTAR TUGAS",

            ["Header.TimerStopwatch"] = "TIMER & STOPWATCH",



            // Top Navigation Tabs

            ["Tab.Home"] = "Home",

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
            ["Media.LyricsPickerTitle"] = "Pilih Versi Lirik",
            ["Media.LyricsBackToLyrics"] = "Lirik",

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
            ["Settings.CategoryModules"] = "Modul Navbar",
            ["Settings.DescSystem"] = "Konfigurasi autostart Windows, perilaku fullscreen, dan sensor privasi",
            ["Settings.DescWidgets"] = "Kustomisasi modul dan widget yang aktif pada notch",
            ["Settings.DescWeather"] = "Prakiraan cuaca, suhu real-time, dan pengaturan lokasi kota",
            ["Settings.DescPrayer"] = "Jadwal sholat 5 waktu otomatis dan notifikasi adzan",
            ["Settings.DescWater"] = "Pengingat minum air teratur dan target hidrasi harian",
            ["Settings.DescModules"] = "Atur posisi urutan dan aktifkan/nonaktifkan menu slide di navbar notch",
            ["Settings.ModulesTitle"] = "MODUL NAVBAR NOTCH",
            ["Settings.ModulesDesc"] = "Gunakan tombol panah untuk mengubah posisi urutan dan sakelar untuk mengaktifkan modul.",
            ["Settings.ResetModules"] = "Pulihkan Urutan",
            ["Settings.ModuleHome"] = "Beranda / Dashboard",
            ["Settings.ModuleHomeDesc"] = "Jam, tanggal, status baterai, performa sistem, dan maskot",
            ["Settings.ModuleControl"] = "Pusat Kontrol",
            ["Settings.ModuleControlDesc"] = "Volume, kecerahan, toggle cepat, dan jalan pintas sistem",
            ["Settings.ModuleMedia"] = "Pemutar Musik & Media",
            ["Settings.ModuleMediaDesc"] = "Kendali pemutaran media dan sinkronisasi lirik real-time",
            ["Settings.ModuleWeather"] = "Prakiraan Cuaca",
            ["Settings.ModuleWeatherDesc"] = "Suhu udara, kondisi cuaca langsung, dan ramalan kota",
            ["Settings.ModulePrayer"] = "Jadwal Sholat & Adzan",
            ["Settings.ModulePrayerDesc"] = "Hitung mundur jadwal sholat 5 waktu dan notifikasi adzan",
            ["Settings.ModuleWater"] = "Pengingat Minum & Postur",
            ["Settings.ModuleWaterDesc"] = "Pelacak minum air teratur, peregangan postur, dan target harian",
            ["Settings.ModuleNote"] = "Catatan Cepat",
            ["Settings.ModuleNoteDesc"] = "Memo kilat, catatan ringkas, dan pengingat harian",
            ["Settings.ModuleClipboard"] = "Riwayat Clipboard",
            ["Settings.ModuleClipboardDesc"] = "Daftar teks yang baru disalin dan 1-klik salin ulang",
            ["Settings.ModuleTimer"] = "Timer & Stopwatch",
            ["Settings.ModuleTimerDesc"] = "Preset fokus Pomodoro, hitung mundur, stopwatch, dan lap",
            ["Settings.ModuleShelf"] = "Drop Shelf & Berbagi HP",
            ["Settings.ModuleShelfDesc"] = "Penampung berkas sementara dan berbagi nirkabel via QR HP",
            ["Settings.ModuleScreenTime"] = "Waktu Layar",
            ["Settings.ModuleScreenTimeDesc"] = "Pantau durasi aplikasi, aktivitas foreground, dan aplikasi teratas",
            ["Settings.CategoryScreenTime"] = "Waktu Layar",
            ["Settings.ScreenTimeTitle"] = "PENGATURAN WAKTU LAYAR",
            ["Settings.ScreenTimeDesc"] = "Menghitung waktu di aplikasi yang sedang di depan (tanpa waktu diam) dan jumlah perpindahan aplikasi. Data tersimpan di screentime.json (35 hari).",
            ["Settings.EnableScreenTime"] = "Pantau Waktu Layar",
            ["Settings.ScreenTimeIdleThreshold"] = "Batas Deteksi Diam (Idle)",
            ["Settings.ScreenTimeIdleDesc"] = "Hentikan hitungan jika tidak ada input mouse/keyboard selama (detik):",
            ["Settings.ClearScreenTimeHistory"] = "Hapus Riwayat Waktu Layar",
            ["Settings.ScreenTimeCleared"] = "Riwayat waktu layar telah dibersihkan.",
            ["Header.ScreenTime"] = "WAKTU LAYAR",
            ["Tab.ScreenTime"] = "Layar",
            ["ScreenTime.Header"] = "WAKTU LAYAR",
            ["ScreenTime.TotalToday"] = "Waktu Layar Hari Ini",
            ["ScreenTime.AppSwitches"] = "kali ganti",
            ["ScreenTime.IdleExcluded"] = "Tanpa waktu diam",
            ["ScreenTime.MostUsed"] = "PALING SERING DIGUNAKAN",
            ["ScreenTime.NoActivity"] = "Waktu layar mulai dihitung sekarang.",
            ["ScreenTime.CategoryBrowsing"] = "Browsing",
            ["ScreenTime.CategoryDevelopment"] = "Development",
            ["ScreenTime.CategoryEntertainment"] = "Entertainment",
            ["ScreenTime.CategoryProductivity"] = "Productivity",
            ["ScreenTime.CategorySystem"] = "System",
            ["ScreenTime.CategoryOther"] = "Lainnya",
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

            ["Settings.AccentColorEnabledTitle"] = "Gunakan Warna Aksen",

            ["Settings.AccentColorEnabledDesc"] = "Aktifkan warna aksen dinamis untuk border, tombol, dan highlight. Bila dimatikan, notch akan memakai tema monokrom/netral yang minimalis.",

            ["Settings.CollapsedSection"] = "NOTCH TERTUTUP (STATUS BAR)",

            ["Settings.ShowWeatherCollapsedTitle"] = "Tampilkan Cuaca di Notch Tertutup",

            ["Settings.ShowWeatherCollapsedDesc"] = "Tampilkan ikon kondisi cuaca dan suhu di bilah notch saat tertutup.",

            ["Settings.ShowBatteryCollapsedTitle"] = "Tampilkan Baterai di Notch Tertutup",

            ["Settings.ShowBatteryCollapsedDesc"] = "Tampilkan persentase dan ikon baterai di bilah notch saat tertutup.",

            ["Settings.ShowPetCollapsedTitle"] = "Tampilkan Maskot di Notch Tertutup",

            ["Settings.ShowPetCollapsedDesc"] = "Tampilkan maskot pendamping di bilah notch saat tertutup jika maskot aktif.",

            ["Settings.AccentColorTitle"] = "Warna Aksen Tema",

            ["Settings.NotchMaterialTitle"] = "Material Latar Belakang Notch",

            ["Settings.NotchMaterialDesc"] = "Pilih material visual: True OLED Black, Tinted Glass, atau Fluent Mica/Acrylic",

            ["Settings.CustomHexTitle"] = "Warna Aksen Kustom (HEX)",

            ["Settings.CustomHexDesc"] = "Ketik kode warna hex (contoh #3B82F6, #10B981, #EC4899)",

            ["Header.Catatan"] = "CATATAN & PRODUKTIVITAS",

            ["Tab.Note"] = "Catatan",

            ["Header.Clipboard"] = "RIWAYAT PAPAN KLIP",

            ["Tab.Clipboard"] = "Klip",

            ["Header.Shelf"] = "SHELF & BERKAS SEMENTARA",

            ["Tab.Shelf"] = "Shelf",

            ["Shelf.EmptyTitle"] = "Seret & Lepas File / Gambar di Sini",
            ["Shelf.EmptyDesc"] = "Simpan file sementara untuk disalin atau dipindahkan antar folder & aplikasi (atau klik untuk memilih file)",
            ["Shelf.Header"] = "SHELF BERKAS SEMENTARA",
            ["Shelf.AddButton"] = "+ Tambah",
            ["Shelf.CopyAllButton"] = "Salin Semua",
            ["Shelf.ClearButton"] = "Bersihkan",
            ["Shelf.OpenButton"] = "Buka",
            ["Shelf.CopyButton"] = "Salin",
            ["Shelf.RemoveTooltip"] = "Hapus dari Shelf",
            ["Shelf.SelectFilesTitle"] = "Pilih file untuk disimpan sementara di Shelf",
            ["Shelf.EmptyCount"] = "Shelf Kosong",
            ["Shelf.OneItem"] = "1 Item",
            ["Shelf.ItemsCount"] = "{0} Item",
            ["Shelf.FileNotFound"] = "File tidak ditemukan",
            ["Shelf.DriveNotFound"] = "Drive tidak ditemukan",
            ["Shelf.PhoneButton"] = "HP",
            ["Shelf.PhoneTooltip"] = "Bagikan file dengan HP lewat Wi-Fi",

            ["DropAction.Title"] = "PILIHAN DROP",
            ["DropAction.Instruction"] = "Arahkan dan lepaskan file pada pilihan tujuan di bawah:",
            ["DropAction.ShelfTitle"] = "Simpan di Shelf",
            ["DropAction.ShelfDesc"] = "Simpan file/folder untuk disalin atau dipindah nanti",
            ["DropAction.ShelfBadge"] = "📥 Lepaskan ke Shelf",
            ["DropAction.ShelfBadgeActive"] = "✨ Lepaskan untuk Simpan ke Shelf!",
            ["DropAction.ConvertTitle"] = "Konversi Gambar",
            ["DropAction.ConvertDesc"] = "JPG ke PNG, PNG/BMP/GIF ke JPG. Folder sama.",
            ["DropAction.ConvertBadge"] = "🔄 Lepaskan untuk Konversi",
            ["DropAction.ConvertBadgeActive"] = "✨ Lepaskan untuk konversi sekarang!",
            ["DropAction.DockTitle"] = "Tambahkan ke Dock",
            ["DropAction.DockDesc"] = "Sematkan pintasan di dock bawah untuk akses cepat",
            ["DropAction.DockBadge"] = "🚀 Lepaskan ke Dock",
            ["DropAction.DockBadgeActive"] = "✨ Lepaskan untuk Tambah ke Dock!",

            ["Convert.SuccessTitle"] = "Dikonversi",
            ["Convert.SuccessMessage"] = "{0} file berhasil dikonversi",
            ["Convert.NothingTitle"] = "Tidak ada yang dikonversi",
            ["Convert.NothingMessage"] = "Tidak ada gambar yang bisa dikonversi",
            ["Convert.FailedTitle"] = "Konversi gagal",
            ["Convert.FailedMessage"] = "{0} file gagal dikonversi",

            ["Phone.WindowTitle"] = "Berbagi ke HP",
            ["Phone.ScanHint"] = "Pindai dengan kamera HP",
            ["Phone.OpenHint"] = "HP dan PC harus berada di jaringan Wi-Fi yang sama. Izinkan Kuri di Windows Firewall jika diminta.",
            ["Phone.CopyLink"] = "Salin Tautan",
            ["Phone.LinkCopied"] = "Tautan disalin",
            ["Phone.Start"] = "Mulai",
            ["Phone.Stop"] = "Hentikan",
            ["Phone.StatusStopped"] = "Server dihentikan",
            ["Phone.StatusRunning"] = "Menunggu HP...",
            ["Phone.StatusConnected"] = "HP terhubung",
            ["Phone.Error"] = "Server berbagi tidak dapat dimulai",
            ["Phone.UploadNotificationTitle"] = "File dari HP",
            ["Phone.TextNotificationTitle"] = "Teks dari HP",
            ["Phone.LinkNotificationTitle"] = "Tautan dari HP",
            ["Phone.UploadFolderUnavailable"] = "Folder unggahan tidak tersedia",

            ["PhonePage.Title"] = "Berbagi ke Kuri",
            ["PhonePage.Subtitle"] = "Kirim file dan teks ke PC",
            ["PhonePage.UploadTitle"] = "Kirim file",
            ["PhonePage.ChooseFile"] = "Pilih file",
            ["PhonePage.Uploading"] = "Mengunggah...",
            ["PhonePage.Uploaded"] = "Terkirim",
            ["PhonePage.UploadFailed"] = "Unggahan gagal",
            ["PhonePage.TooLarge"] = "File terlalu besar",
            ["PhonePage.TextTitle"] = "Kirim teks atau tautan",
            ["PhonePage.TextPlaceholder"] = "Ketik atau tempel teks atau tautan di sini...",
            ["PhonePage.SendText"] = "Kirim ke PC",
            ["PhonePage.Sent"] = "Terkirim ke clipboard PC",
            ["PhonePage.FilesTitle"] = "File di PC kamu",
            ["PhonePage.NoFiles"] = "Shelf masih kosong",
            ["PhonePage.Download"] = "Unduh",
            ["PhonePage.Connected"] = "Terhubung",
            ["PhonePage.Offline"] = "Terputus",
            ["PhonePage.EmptyText"] = "Teks kosong",
            ["PhonePage.TextTooLong"] = "Teks terlalu panjang",
            ["PhonePage.MaxFileSize"] = "Ukuran file maksimal: {0}",
            ["PhonePage.MaxTextSize"] = "Panjang teks maksimal: {0} karakter",

            ["Settings.ShelfSection"] = "SHELF & HP",
            ["Settings.ShelfRemoveAfterDragTitle"] = "Hapus dari Shelf setelah diseret keluar",
            ["Settings.ShelfRemoveAfterDragDesc"] = "Hapus entri shelf saat kamu menyeretnya ke aplikasi lain. File asli tidak pernah dihapus.",
            ["Settings.PhoneShareTitle"] = "Berbagi ke HP",
            ["Settings.PhoneShareDesc"] = "Kirim file dan teks dari HP melalui tautan Wi-Fi lokal",
            ["Settings.PhoneFolderTitle"] = "Folder unggahan HP",
            ["Settings.BrowseButton"] = "Pilih",
            ["Settings.PhoneShareHint"] = "HP dan PC harus terhubung ke jaringan Wi-Fi yang sama.",

            ["Settings.AccentColorDesc"] = "Pilih warna aksen untuk slider, tombol, dan indikator",

            ["Settings.AlbumArtAccentTitle"] = "Warna Aksen Dinamis Album",

            ["Settings.AlbumArtAccentDesc"] = "Otomatis menyelaraskan gradasi notch dengan warna cover album lagu yang sedang diputar",

            ["Settings.NotchTimerGlowTitle"] = "Garis Cahaya Timer",

            ["Settings.NotchTimerGlowDesc"] = "Menggambar garis cahaya di sekeliling notch tertutup yang memendek mengikuti sisa waktu (hijau, kuning, merah) dan berkedip saat selesai. Stopwatch menggambar garis kuning yang memanjang tiap menit.",

            ["Settings.NotchAlertGlowTitle"] = "Garis Cahaya Alert",

            ["Settings.NotchAlertGlowDesc"] = "Cahaya tipis berjalan keliling di notch tertutup saat alert tampil — pengingat, waktu sholat, unduhan, charging — sesuai warnanya, selama 20 detik pertama.",

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

            ["Settings.LyricsKaraokeTitle"] = "Lirik karaoke",
            ["Settings.LyricsKaraokeDesc"] = "Baris yang sedang dinyanyikan terisi warna seiring lagu berjalan (hanya lirik berwaktu)",
            ["Settings.SyllableSyncTitle"] = "Spicy Lyrics (Sinkronisasi suku kata)",
            ["Settings.LrcRedTitle"] = "Penyedia Lirik lrc.red",
            ["Settings.LrcRedDesc"] = "Ambil lirik tersinkronisasi suku kata (TTML) dan baris dari database komunitas lrc.red",
            ["Settings.SyllableSyncDesc"] = "Sapuan karaoke presisi kata-demi-kata via Spicy Lyrics & TTML suku kata",
            ["Settings.SpicyLyricsApiKeyTitle"] = "Spicy Lyrics API Key (Opsional)",
            ["Settings.SpicyLyricsApiKeyDesc"] = "Token developer dari developers.spicylyrics.org untuk lagu yang belum tercache. Kosongkan untuk memakai mirror publik gratis.",



            ["Settings.LyricsBoldTitle"] = "Lirik tebal",

            ["Settings.LyricsBoldDesc"] = "Gunakan teks tebal untuk baris lirik yang aktif",

            ["Settings.LyricsTextSizeTitle"] = "Ukuran teks lirik",

            ["Settings.LyricsTextSizeDesc"] = "Atur ukuran lirik di pemutar",

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

