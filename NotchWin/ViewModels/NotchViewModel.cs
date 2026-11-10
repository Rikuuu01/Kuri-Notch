using System;

using System.Threading;

using System.Threading.Tasks;

using System.Windows;

using System.Windows.Media;

using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using NotchWin.Interop;

using NotchWin.Models;

using NotchWin.Services;

using NotchWin.Widgets;

using Serilog;



namespace NotchWin.ViewModels

{

    public partial class NotchViewModel : ObservableObject

    {

        // Default geometry constants

        public const double CollapsedWidth = 186.0;

        public const double CollapsedHeight = 32.0;

        public const double BaseExpandedWidth = 390.0;

        public const double ExpandedLyricsWidth = 540.0;

        public const double ExpandedWeatherWidth = 620.0;

        public const double ExpandedHeight = 264.0;

        public const double ExpandedControlCenterHeight = 304.0;

        public const double ExpandedMediaHeight = 232.0;
        public const double ExpandedMediaPickerHeight = 310.0;
        public const double ExpandedScreenTimeHeight = 356.0;

        public const double ExpandedWeatherHeight = 286.0;

        public const double ExpandedHomeHeight = 236.0;

        public const double ExpandedPrayerHeight = 216.0;

        public const double ExpandedWaterHeight = 216.0;

        public const double ExpandedNoteHeight = 274.0;

        public const double ExpandedClipboardHeight = 274.0;

        public const double ExpandedTimerHeight = 216.0;

        public const double ExpandedShelfHeight = 216.0;



        // Additional row for the hover navbar above the shortcut dock (24px strip + 10px gap).

        public const double NavDockExtraHeight = 34.0;



        // Additional height for the Control Center download strip (24px strip + 5px gap + slack).

        private const double DownloadStripExtraHeight = 32.0;



        /// <summary>

        /// Lebar notch dinamis yang secara otomatis mengikuti ukuran slide terlebar di antara semua slide.

        /// Saat ini slide cuaca (620px) adalah yang terlebar, sehingga seluruh slide otomatis mengikuti 620px.

        /// Jika di masa depan ada slide baru yang lebih lebar, semua slide akan otomatis mengikuti lebar tersebut.

        /// </summary>

        public static double MaxExpandedWidth => Math.Max(BaseExpandedWidth, Math.Max(ExpandedLyricsWidth, ExpandedWeatherWidth));



        public const double ExpandedWidth = 620.0;



        // Radius for bottom-left and bottom-right corners

        public const double CollapsedCornerRadius = 16.0;

        public const double ExpandedCornerRadius = 22.0;



        private CancellationTokenSource? _collapseCts;

        private TimeSpan _collapseDelay = TimeSpan.FromMilliseconds(450);

        private CancellationTokenSource? _expandCts;

        private TimeSpan _expandDelay = TimeSpan.FromMilliseconds(280);

        private CancellationTokenSource? _peekCloseCts;

        private long _peekOpenedTick;

        private const int PeekMorphGuardMs = 700;

        private bool _lastPrayerEnabled = true;

        private DispatcherTimer? _waterAlarmSoundTimer;
        private readonly DispatcherTimer _timerAlternationTimer;

        private bool _alternateBetweenTimerAndStopwatch;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCollapsedTimerPhaseActive))]
        [NotifyPropertyChangedFor(nameof(ShowTimerIconInCollapsed))]
        [NotifyPropertyChangedFor(nameof(ShowStopwatchIconInCollapsed))]
        [NotifyPropertyChangedFor(nameof(IsCollapsedStopwatchDisplay))]
        private bool _isCollapsedTimerAlternatePhase;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCollapsedTimerPhaseActive))]
        private bool _isCollapsedDownloadPhaseActive;

        public bool IsCollapsedTimerPhaseActive => IsCollapsedTimerAlternatePhase && TimerStopwatch?.IsAnyTimerRunning == true && !IsCollapsedDownloadPhaseActive;

        public bool IsCollapsedStopwatchDisplay
        {
            get
            {
                if (TimerStopwatch == null) return false;
                if (TimerStopwatch.IsTimerRunning && TimerStopwatch.IsStopwatchRunning)
                {
                    return _alternateBetweenTimerAndStopwatch;
                }
                return TimerStopwatch.IsStopwatchRunning && !TimerStopwatch.IsTimerRunning;
            }
        }

        public bool ShowTimerIconInCollapsed => IsCollapsedTimerPhaseActive && !IsCollapsedStopwatchDisplay;
        public bool ShowStopwatchIconInCollapsed => IsCollapsedTimerPhaseActive && IsCollapsedStopwatchDisplay;

        public event EventHandler<bool>? HoverStretchChanged;

        [ObservableProperty]
        private NotchState _currentState = NotchState.Collapsed;

        partial void OnCurrentStateChanged(NotchState value)
        {
            UpdateTimerAlternationState();
        }



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(PinRotationAngle))]

        [NotifyPropertyChangedFor(nameof(PinIconBrush))]

        [NotifyPropertyChangedFor(nameof(PinToolTipText))]

        private bool _isLockedExpanded;



        public LocalizationService L10n => LocalizationService.Instance;



        public double PinRotationAngle => IsLockedExpanded ? 0.0 : -45.0;



        public Brush PinIconBrush => IsLockedExpanded

            ? (Brush)(Application.Current?.FindResource("AccentLightBrush") ?? new SolidColorBrush(Color.FromRgb(59, 130, 246)))

            : new SolidColorBrush(Color.FromRgb(113, 113, 122));



        public string PinToolTipText => IsLockedExpanded

            ? L10n["Pin.Unpin"]

            : L10n["Pin.Pin"];



        partial void OnIsLockedExpandedChanged(bool value)

        {

            if (!value && CurrentState == NotchState.Expanded)

            {

                ScheduleDelayedCollapse();

                if (IsSlimBarPeekActive)

                {

                    ScheduleSlimBarPeekClose((int)Math.Max(100, _collapseDelay.TotalMilliseconds) + 340);

                }

            }

        }



        [RelayCommand]

        public void TogglePin()

        {

            IsLockedExpanded = !IsLockedExpanded;

        }



        [ObservableProperty]
        private bool _showWeatherInCollapsedNotch = false;

        [ObservableProperty]
        private bool _showBatteryInCollapsedNotch = false;

        [ObservableProperty]
        private bool _showPetInCollapsedNotch = false;

        public bool IsCollapsedWeatherVisible => ShowWeatherInCollapsedNotch;
        public bool IsCollapsedBatteryVisible => ShowBatteryInCollapsedNotch && (Battery?.HasBattery == true);
        public bool IsCollapsedPetVisible => ShowPetInCollapsedNotch && IsPetEnabled;

        partial void OnShowWeatherInCollapsedNotchChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCollapsedWeatherVisible));
            UpdateGeometryForState(CurrentState);
        }

        partial void OnShowBatteryInCollapsedNotchChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCollapsedBatteryVisible));
            UpdateGeometryForState(CurrentState);
        }

        partial void OnShowPetInCollapsedNotchChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCollapsedPetVisible));
            UpdateGeometryForState(CurrentState);
        }

        [ObservableProperty]

        private double _targetWidth = CollapsedWidth;

        partial void OnTargetWidthChanged(double oldValue, double newValue)
        {
            if (Math.Abs(oldValue - newValue) > 0.5)
            {
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private double? _collapsedLyricsWidthOverride;



        [ObservableProperty]

        private bool _isWelcomeGreetingActive;

        partial void OnIsWelcomeGreetingActiveChanged(bool value)
        {
            UpdateGeometryForState(CurrentState);
            StateAnimationRequested?.Invoke(this, EventArgs.Empty);
        }



        [ObservableProperty]

        private double _targetHeight = CollapsedHeight;

        partial void OnTargetHeightChanged(double oldValue, double newValue)
        {
            if (Math.Abs(oldValue - newValue) > 0.5)
            {
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
            }
        }



        [ObservableProperty]

        private double _targetCornerRadius = CollapsedCornerRadius;



        [ObservableProperty]

        private bool _isContentVisible;



        [ObservableProperty]

        private string _statusText = "Kuri";



        [ObservableProperty]
        private bool _isPetEnabled = true;

        partial void OnIsPetEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCollapsedPetVisible));
            UpdateGeometryForState(CurrentState);
        }



        [ObservableProperty]

        private bool _isPhoneShareEnabled = true;



        [ObservableProperty]

        private bool _isNotchTimerGlowEnabled = true;



        [ObservableProperty]

        private bool _isNotchAlertGlowEnabled = true;



        private readonly IMediaService _mediaService;

        private readonly IVolumeService _volumeService;

        private readonly IBatteryService _batteryService;

        private readonly ISettingsService _settingsService;

        private readonly IFullscreenDetector _fullscreenDetector;

        private readonly IDisplayService _displayService;

        private readonly IPrivacyService _privacyService;

        private readonly IBrightnessService _brightnessService;

        private readonly ISystemMonitorService _systemMonitorService;

        private readonly IWeatherService _weatherService;

        private readonly IPrayerTimesService _prayerService;

        private readonly ITodoService _todoService;

        private readonly INoteService _noteService;

        private readonly IClipboardService _clipboardService;

        private readonly IWaterTrackerService _waterService;

        private readonly ISoundNotificationService _soundNotificationService;

        private readonly IDownloadMonitorService _downloadService;

        private readonly IShelfService _shelfService;



        public WidgetHost WidgetHost { get; }

        public MediaViewModel Media { get; }

        public VolumeViewModel Volume { get; }

        public BatteryViewModel Battery { get; }

        public ClockViewModel Clock { get; }

        public NotificationViewModel Notification { get; }

        public SystemHealthViewModel SystemHealth { get; }

        public WeatherViewModel Weather { get; }

        public TimerStopwatchViewModel TimerStopwatch { get; }

        public TodoViewModel Todo { get; }

        public NoteViewModel Note { get; }

        public ClipboardViewModel Clipboard { get; }

        public WaterTrackerViewModel Water { get; }

        public PrayerTimesViewModel Prayer { get; }

        public ShelfViewModel Shelf { get; }

        public PhoneShareService PhoneShare { get; }

        public IScreenTimeService ScreenTimeService { get; }

        public ScreenTimeViewModel ScreenTime { get; }

        public ShortcutViewModel Shortcuts { get; }

        public DownloadViewModel Download { get; }

        public HomeCalendarViewModel HomeCalendar { get; } = new();

        public WifiService Wifi { get; } = new();

        public BluetoothManagerService BluetoothMgr { get; } = new();



        public IBrightnessService BrightnessService => _brightnessService;



        #region Slide Carousel & Quick Control Properties



        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(IsSlide0Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide1Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide2Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide3Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide4Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide5Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide6Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide7Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide8Active))]

        [NotifyPropertyChangedFor(nameof(IsSlide9Active))]

        [NotifyPropertyChangedFor(nameof(IsHomeActive))]

        [NotifyPropertyChangedFor(nameof(IsShelfActive))]

        [NotifyPropertyChangedFor(nameof(IsControlCenterActive))]

        [NotifyPropertyChangedFor(nameof(IsMediaActive))]

        [NotifyPropertyChangedFor(nameof(IsWeatherActive))]

        [NotifyPropertyChangedFor(nameof(IsPrayerActive))]

        [NotifyPropertyChangedFor(nameof(IsWaterActive))]

        [NotifyPropertyChangedFor(nameof(IsTodoActive))]

        [NotifyPropertyChangedFor(nameof(IsNoteActive))]

        [NotifyPropertyChangedFor(nameof(IsClipboardActive))]

        [NotifyPropertyChangedFor(nameof(IsTimerActive))]

        [NotifyPropertyChangedFor(nameof(CurrentSlideTitle))]
        [NotifyPropertyChangedFor(nameof(CurrentActiveModuleId))]

        private int _currentSlideIndex = 0;



        private readonly System.Collections.Generic.List<string> _activeModuleIds = new();
        public System.Collections.Generic.IReadOnlyList<string> ActiveModuleIds => _activeModuleIds;

        public string CurrentActiveModuleId => (CurrentSlideIndex >= 0 && CurrentSlideIndex < _activeModuleIds.Count) ? _activeModuleIds[CurrentSlideIndex] : "home";

        public int GetSlideIndex(string id)
        {
            int idx = _activeModuleIds.IndexOf(id.ToLowerInvariant());
            return idx >= 0 ? idx : -1;
        }

        public bool IsSlide0Active => CurrentSlideIndex == 0;

        public bool IsSlide1Active => CurrentSlideIndex == 1;

        public bool IsSlide2Active => CurrentSlideIndex == 2;

        public bool IsSlide3Active => CurrentSlideIndex == 3;

        public bool IsSlide4Active => CurrentSlideIndex == 4;

        public bool IsSlide5Active => CurrentSlideIndex == 5;

        public bool IsSlide6Active => CurrentSlideIndex == 6;

        public bool IsSlide7Active => CurrentSlideIndex == 7;

        public bool IsSlide8Active => CurrentSlideIndex == 8;

        public bool IsSlide9Active => CurrentSlideIndex == 9;



        public bool IsHomeActive => CurrentActiveModuleId == "home";

        public bool IsControlCenterActive => CurrentActiveModuleId == "control";

        public bool IsMediaActive => CurrentActiveModuleId == "media";

        public bool IsWeatherActive => CurrentActiveModuleId == "weather";

        public bool IsPrayerActive => CurrentActiveModuleId == "prayer";

        public bool IsWaterActive => CurrentActiveModuleId == "water";

        public bool IsTodoActive => CurrentActiveModuleId == "note";

        public bool IsNoteActive => CurrentActiveModuleId == "note";

        public bool IsClipboardActive => CurrentActiveModuleId == "clipboard";

        public bool IsTimerActive => CurrentActiveModuleId == "timer";

        public bool IsShelfActive => CurrentActiveModuleId == "shelf";

        public bool IsScreenTimeActive => CurrentActiveModuleId == "screentime";

        public int ScreenTimeSlideIndex => GetSlideIndex("screentime");



        public int HomeSlideIndex => GetSlideIndex("home");

        public int ControlCenterSlideIndex => GetSlideIndex("control");

        public int MediaSlideIndex => GetSlideIndex("media");

        public int WeatherSlideIndex => GetSlideIndex("weather");

        public int PrayerSlideIndex => GetSlideIndex("prayer");

        public int WaterSlideIndex => GetSlideIndex("water");

        public int NoteSlideIndex => GetSlideIndex("note");

        public int TodoSlideIndex => NoteSlideIndex;

        public int ClipboardSlideIndex => GetSlideIndex("clipboard");

        public int TimerSlideIndex => GetSlideIndex("timer");

        public int ShelfSlideIndex => GetSlideIndex("shelf");

        public int TotalActiveSlides => _activeModuleIds.Count > 0 ? _activeModuleIds.Count : 1;

        public bool IsPrayerEnabled => _activeModuleIds.Contains("prayer");



        public string CurrentSlideTitle => CurrentActiveModuleId switch
        {
            "home" => L10n["Header.Home"],
            "control" => L10n["Header.PusatKontrol"],
            "media" => L10n["Header.SedangMemutar"],
            "weather" => L10n["Header.Cuaca"],
            "prayer" => L10n["Header.JadwalSholat"],
            "water" => L10n["Header.PengingatMinum"],
            "note" => L10n["Header.Catatan"],
            "clipboard" => L10n["Header.Clipboard"],
            "timer" => L10n["Header.TimerStopwatch"],
            "shelf" => L10n["Header.Shelf"],
            "screentime" => L10n["Header.ScreenTime"],
            _ => "KURI"
        };



        public void NavigateToShelfSlide()

        {

            Shelf.RefreshMissingStates();

            SelectSlide(ShelfSlideIndex);

        }



        #region Home Menu & Notification Banner



        [ObservableProperty]

        private bool _isHomeNotificationActive = false;



        partial void OnIsHomeNotificationActiveChanged(bool value)

        {

            if (CurrentState == NotchState.Expanded && CurrentSlideIndex == HomeSlideIndex)

            {

                double oldHeight = TargetHeight;

                UpdateGeometryForState(CurrentState);

                if (Math.Abs(oldHeight - TargetHeight) > 0.5)

                {

                    StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                }

            }

        }



        [ObservableProperty]

        private string _homeNotificationType = "prayer"; // "prayer", "timer", "water"



        [ObservableProperty]

        private string _homeNotificationTitle = "Waktunya salat Dzuhur";



        [ObservableProperty]

        private string _homeNotificationSubtitle = "12:04 \u00b7 adzan sedang berbunyi";



        [ObservableProperty]

        private string _homeNotificationActionText = "Matikan";



        [ObservableProperty]

        private bool _isWifiToggledOn = true;



        [ObservableProperty]

        private bool _isBluetoothToggledOn = true;



        [ObservableProperty]

        private bool _isKeepAwakeOn;



        [ObservableProperty]

        private bool _isAirplaneModeOn;



        [ObservableProperty]

        private bool _isDarkModeOn = true;



        [ObservableProperty]

        private bool _isMicrophoneMuted;



        [ObservableProperty]

        private bool _isEyeBreaksOn;

        [ObservableProperty]
        private bool _isWifiExpanded;

        [ObservableProperty]
        private bool _isBluetoothExpanded;

        [ObservableProperty]
        private bool _isAudioOutputExpanded;

        partial void OnIsWifiExpandedChanged(bool value)
        {
            if (value)
            {
                IsBluetoothExpanded = false;
                IsAudioOutputExpanded = false;
                _ = Wifi.RefreshNetworksAsync();
            }
            UpdateGeometryForState(CurrentState);
            StateAnimationRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnIsBluetoothExpandedChanged(bool value)
        {
            if (value)
            {
                IsWifiExpanded = false;
                IsAudioOutputExpanded = false;
                _ = BluetoothMgr.RefreshDevicesAsync(_volumeService.BluetoothDeviceName);
            }
            UpdateGeometryForState(CurrentState);
            StateAnimationRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnIsAudioOutputExpandedChanged(bool value)
        {
            if (value)
            {
                IsWifiExpanded = false;
                IsBluetoothExpanded = false;
                _volumeService.RefreshAvailableDevices();
            }
            UpdateGeometryForState(CurrentState);
            StateAnimationRequested?.Invoke(this, EventArgs.Empty);
        }



        public string WifiStatusLabel

        {

            get

            {

                if (!IsWifiToggledOn) return L10n["Control.Off"];

                if (!string.IsNullOrEmpty(SystemHealth?.NetworkName) && SystemHealth.NetworkName != "Disconnected")

                    return SystemHealth.NetworkName;

                return "Wi-Fi";

            }

        }



        public string BluetoothStatusLabel

        {

            get

            {

                if (!IsBluetoothToggledOn) return L10n["Control.BluetoothOff"];

                if (Volume != null && Volume.IsBluetoothAudio && !string.IsNullOrEmpty(Volume.BluetoothDeviceName))

                    return Volume.BluetoothDeviceName;

                return L10n["Control.NotConnected"];

            }

        }



        partial void OnIsWifiToggledOnChanged(bool value)

        {

            OnPropertyChanged(nameof(WifiStatusLabel));

        }



        partial void OnIsBluetoothToggledOnChanged(bool value)

        {

            OnPropertyChanged(nameof(BluetoothStatusLabel));

        }



        [RelayCommand]

        public void DismissHomeNotification()

        {

            IsHomeNotificationActive = false;

            if (HomeNotificationType == "prayer")

            {

                _soundNotificationService.StopPrayerCall();

            }

            else if (HomeNotificationType == "timer")

            {

                TimerStopwatch.ResetTimer();

            }

            else if (HomeNotificationType == "water")

            {

                DismissWaterAlarmNotification();

            }

            else if (HomeNotificationType == "alarm")

            {

                // Alarm dismissed

            }

        }



        [RelayCommand]

        public void SnoozeHomeNotification()

        {

            IsHomeNotificationActive = false;

            if (HomeNotificationType == "prayer")

            {

                _soundNotificationService.StopPrayerCall();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };

                timer.Tick += (s, e) =>

                {

                    timer.Stop();

                    if (_settingsService.CurrentSettings.IsPrayerReminderEnabled)

                    {

                        IsHomeNotificationActive = true;

                        HomeNotificationType = "prayer";

                        HomeNotificationTitle = $"Waktunya salat {Prayer.NextPrayerName}";

                        HomeNotificationSubtitle = $"{DateTime.Now:HH:mm} \u00b7 pengingat tunda (5 mnt)";

                        HomeNotificationActionText = "Matikan";

                        if (_settingsService.CurrentSettings.PrayerSoundEnabled)

                        {

                            try { _soundNotificationService.PlayPrayerCall(); } catch { }

                        }

                    }

                };

                timer.Start();

            }

            else if (HomeNotificationType == "timer")

            {

                TimerStopwatch.SetPresetTimer(5);
                TimerStopwatch.StartPauseTimer();

            }

            else if (HomeNotificationType == "water")

            {

                SnoozeWaterAlarm();

            }

            else if (HomeNotificationType == "alarm")

            {

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };

                timer.Tick += (s, e) =>

                {

                    timer.Stop();

                    IsHomeNotificationActive = true;

                    HomeNotificationType = "alarm";

                    HomeNotificationTitle = "Alarm Pengingat (Tunda)";

                    HomeNotificationSubtitle = $"{DateTime.Now:HH:mm} \u00B7 alarm berdering";

                    HomeNotificationActionText = "Matikan";

                    try { _soundNotificationService.PlayTimerAlarm(); } catch { }

                };

                timer.Start();

            }

        }



        [RelayCommand]

        public async Task QuickToggleWifi()

        {

            try

            {

                var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();

                bool radioFound = false;

                foreach (var radio in radios)

                {

                    if (radio.Kind == Windows.Devices.Radios.RadioKind.WiFi)

                    {

                        var target = radio.State == Windows.Devices.Radios.RadioState.On

                            ? Windows.Devices.Radios.RadioState.Off

                            : Windows.Devices.Radios.RadioState.On;

                        await radio.SetStateAsync(target);

                        IsWifiToggledOn = (target == Windows.Devices.Radios.RadioState.On);

                        radioFound = true;

                        break;

                    }

                }



                if (!radioFound)

                {

                    bool nextState = !IsWifiToggledOn;

                    string action = nextState ? "enable" : "disable";

                    var psi = new System.Diagnostics.ProcessStartInfo("netsh", $"interface set interface name=\"Wi-Fi\" admin={action}")

                    {

                        CreateNoWindow = true,

                        UseShellExecute = false

                    };

                    var proc = System.Diagnostics.Process.Start(psi);

                    if (proc != null) await proc.WaitForExitAsync();

                    IsWifiToggledOn = nextState;

                }

            }

            catch (Exception ex)

            {

                Serilog.Log.Warning(ex, "Failed to toggle Wi-Fi radio");

                IsWifiToggledOn = !IsWifiToggledOn;

            }

        }



        [RelayCommand]

        public async Task QuickToggleBluetooth()

        {

            try

            {

                bool newState = await _volumeService.ToggleBluetoothRadioAsync();

                IsBluetoothToggledOn = newState;

            }

            catch (Exception ex)

            {

                Serilog.Log.Warning(ex, "Failed to toggle Bluetooth radio");

                IsBluetoothToggledOn = !IsBluetoothToggledOn;

            }

        }



        [RelayCommand]

        public async Task QuickToggleMute()

        {

            await Volume.ToggleMuteAsync();

        }



        [RelayCommand]

        public void QuickScreenshot()

        {

            QuickActionHelper.LaunchSnippingTool();

        }



        [RelayCommand]

        public void ToggleKeepAwake()

        {

            IsKeepAwakeOn = !IsKeepAwakeOn;

            QuickActionHelper.SetKeepAwake(IsKeepAwakeOn);

            TriggerTransientNotification(IsKeepAwakeOn ? "\u2615" : "\U0001F4A4", "Keep Awake", IsKeepAwakeOn ? "Layar dan sistem dicegah tidur" : "Mode tidur sistem kembali normal");

        }



        [RelayCommand]

        public void ToggleAirplaneMode()

        {

            IsAirplaneModeOn = !IsAirplaneModeOn;

        }



        [RelayCommand]

        public void ToggleDarkMode()

        {

            IsDarkModeOn = QuickActionHelper.ToggleDarkMode();

            TriggerTransientNotification("\U0001F313", L10n["Control.DarkMode"], IsDarkModeOn ? L10n["Notification.ThemeDarkActive"] : L10n["Notification.ThemeLightActive"]);

        }



        [RelayCommand]

        public void ToggleMicrophoneMute()

        {

            IsMicrophoneMuted = QuickActionHelper.ToggleMicrophoneMute();

            TriggerTransientNotification(IsMicrophoneMuted ? "\U0001F3A4\u274C" : "\U0001F3A4", L10n["Control.Microphone"], IsMicrophoneMuted ? L10n["Notification.MicrophoneMuted"] : L10n["Notification.MicrophoneActive"]);

        }



        [RelayCommand]

        public void ToggleEyeBreaks()

        {

            IsEyeBreaksOn = !IsEyeBreaksOn;

            TriggerTransientNotification(IsEyeBreaksOn ? "\U0001F441\uFE0F" : "\U0001F440", "Eye Breaks", IsEyeBreaksOn ? "Pengingat istirahat mata 20-20-20 aktif" : "Pengingat istirahat mata dinonaktifkan");

        }



        [RelayCommand]

        public void OpenWifiSettings()

        {

            QuickActionHelper.OpenAvailableNetworks();

        }



        [RelayCommand]

        public void OpenBluetoothSettings()

        {

            QuickActionHelper.OpenBluetoothSettings();

        }

        [RelayCommand]
        public void ToggleWifiExpand()
        {
            IsWifiExpanded = !IsWifiExpanded;
        }

        [RelayCommand]
        public void ToggleBluetoothExpand()
        {
            IsBluetoothExpanded = !IsBluetoothExpanded;
        }

        [RelayCommand]
        public void ToggleAudioOutputExpand()
        {
            IsAudioOutputExpanded = !IsAudioOutputExpanded;
        }

        [RelayCommand]
        public async Task ConnectWifiAsync(WifiNetworkItem? item)
        {
            if (item == null) return;
            await Wifi.ConnectAsync(item);
        }

        [RelayCommand]
        public async Task DisconnectWifiAsync()
        {
            await Wifi.DisconnectAsync();
        }

        [RelayCommand]
        public async Task RefreshWifiAsync()
        {
            await Wifi.RefreshNetworksAsync();
        }

        [RelayCommand]
        public async Task RefreshBluetoothAsync()
        {
            await BluetoothMgr.RefreshDevicesAsync(_volumeService.BluetoothDeviceName);
        }

        [RelayCommand]
        public async Task SelectBluetoothDeviceAsync(BluetoothDeviceItem? item)
        {
            if (item == null) return;
            if (item.DeviceType == "audio")
            {
                foreach (var dev in Volume.AvailableDevices)
                {
                    if (dev.Name.Contains(item.Name, StringComparison.OrdinalIgnoreCase) ||
                        item.Name.Contains(dev.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        await Volume.SelectAudioDeviceAsync(dev);
                        break;
                    }
                }
            }
            else
            {
                QuickActionHelper.OpenBluetoothSettings();
            }
        }

        [RelayCommand]
        public async Task SelectAudioOutputDeviceAsync(AudioDeviceInfo? device)
        {
            if (device == null) return;
            await Volume.SelectAudioDeviceAsync(device);
            IsAudioOutputExpanded = false;
        }



        #endregion



        public event EventHandler<int>? SlideChanged;



        partial void OnCurrentSlideIndexChanged(int value)

        {

            SlideChanged?.Invoke(this, value);

            UpdateSlideTelemetryState();

            if (value == ScreenTimeSlideIndex)

            {

                ScreenTime.Refresh();

            }

            if (value != MediaSlideIndex && Media != null)

            {

                Media.IsLyricsVersionPickerOpen = false;

            }

            if (value != ControlCenterSlideIndex)
            {
                IsWifiExpanded = false;
                IsBluetoothExpanded = false;
                IsAudioOutputExpanded = false;
            }

            if (CurrentState == NotchState.Expanded)

            {

                UpdateGeometryForState(CurrentState);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

            }

        }



        private int _cachedVolumePercent = -1;

        public int MasterVolumePercent
        {
            get => _cachedVolumePercent >= 0 ? _cachedVolumePercent : _volumeService.VolumePercent;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (_cachedVolumePercent != clamped)
                {
                    _cachedVolumePercent = clamped;
                    _ = _volumeService.SetVolumeAsync(clamped / 100f);
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMasterVolumeMuted => _volumeService.IsMuted;

        private int _cachedBrightnessPercent = -1;

        public int ScreenBrightnessPercent
        {
            get => _cachedBrightnessPercent >= 0 ? _cachedBrightnessPercent : _brightnessService.CurrentBrightness;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (_cachedBrightnessPercent != clamped)
                {
                    _cachedBrightnessPercent = clamped;
                    _brightnessService.SetBrightness(clamped);
                    OnPropertyChanged();
                }
            }
        }



        public bool IsBrightnessSupported => _brightnessService.IsSupported;

        public bool IsSliderDragging { get; set; }



        public event EventHandler? OpenSettingsRequested;



        [RelayCommand]

        public void ShowPhoneShare()

        {

            if (!IsPhoneShareEnabled) return;



            if (PhoneShare.IsPanelOpen)

            {

                PhoneShare.IsPanelOpen = false;

                UpdateGeometryForState(CurrentState);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                return;

            }



            OpenPhoneSharePanel();

            UpdateGeometryForState(CurrentState);

            StateAnimationRequested?.Invoke(this, EventArgs.Empty);

        }



        private void OpenPhoneSharePanel()

        {

            if (!PhoneShare.IsRunning)

            {

                PhoneShare.Start();

                if (!PhoneShare.IsRunning)

                {

                    TriggerTransientNotification("\U0001F4F1", L10n["Phone.WindowTitle"], L10n["Phone.Error"]);

                    return;

                }

            }



            PhoneShare.IsPanelOpen = true;

        }



        [RelayCommand]

        public void NextSlide()

        {

            int total = TotalActiveSlides;

            CurrentSlideIndex = (CurrentSlideIndex + 1) % total;

        }



        [RelayCommand]

        public void PrevSlide()

        {

            int total = TotalActiveSlides;

            CurrentSlideIndex = (CurrentSlideIndex + total - 1) % total;

        }



        [RelayCommand]
        public void SelectSlide(object? param)
        {
            if (param == null) return;
            int total = TotalActiveSlides;
            if (int.TryParse(param.ToString(), out int idx))
            {
                if (idx >= 0 && idx < total)
                {
                    CurrentSlideIndex = idx;
                }
                return;
            }

            string id = param.ToString()?.ToLowerInvariant() ?? string.Empty;
            int targetIdx = GetSlideIndex(id);
            if (targetIdx >= 0 && targetIdx < total)
            {
                CurrentSlideIndex = targetIdx;
            }
        }



        [RelayCommand]

        public void ToggleMasterMute()

        {

            _ = _volumeService.ToggleMuteAsync();

        }



        [RelayCommand]

        public void CaptureScreen()

        {

            QuickActionHelper.LaunchSnippingTool();

        }



        [RelayCommand]

        public void LockComputer()

        {

            QuickActionHelper.LockComputer();

        }



        [RelayCommand]

        public void OpenSettings()

        {

            OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

        }



        [RelayCommand]

        public void LaunchTaskManager()

        {

            QuickActionHelper.LaunchTaskManager();

        }



        #endregion



        public bool IsCameraInUse => _privacyService.IsCameraInUse;

        public bool IsMicrophoneInUse => _privacyService.IsMicrophoneInUse;

        public bool IsPrivacyActive => _settingsService.CurrentSettings.IsPrivacyIndicatorEnabled && (IsCameraInUse || IsMicrophoneInUse);



        public ISettingsService SettingsService => _settingsService;

        public IDisplayService DisplayService => _displayService;

        public IPrivacyService PrivacyService => _privacyService;



        public bool IsFullscreenActive { get; private set; }

        public bool IsManuallyHidden { get; private set; }



        /// <summary>

        /// True saat notch dibuka sementara dari slim bar (dipicu hover 3 detik).

        /// Selama aktif, notch boleh tampil walau sedang fullscreen.

        /// </summary>

        public bool IsSlimBarPeekActive { get; private set; }



        /// <summary>Notch normal hanya tampil jika tidak fullscreen dan tidak manual-hidden.</summary>

        public bool IsNotchVisible => (!IsFullscreenActive || IsSlimBarPeekActive) && !IsManuallyHidden;



        /// <summary>Garis tipis tampil saat fullscreen aktif, mode slim aktif, dan tidak manual-hidden.</summary>

        public bool IsSlimBarVisible => IsFullscreenActive

            && _settingsService.CurrentSettings.IsFullscreenSlimBarEnabled

            && !IsManuallyHidden

            && !IsSlimBarPeekActive;



        /// <summary>Sembunyi total saat fullscreen jika mode slim dimatikan.</summary>

        public bool IsFullscreenHidden => IsFullscreenActive

            && !_settingsService.CurrentSettings.IsFullscreenSlimBarEnabled

            && !IsSlimBarPeekActive;



        public double NotchScale => _settingsService.CurrentSettings.NotchScale > 0 ? _settingsService.CurrentSettings.NotchScale : 1.0;

        public double FontScale => _settingsService.CurrentSettings.FontScale > 0 ? _settingsService.CurrentSettings.FontScale : 1.0;



        public event EventHandler? RequestCloseEvent;

        public event EventHandler? StateAnimationRequested;

        public event EventHandler? VisibilityChanged;

        public event EventHandler? PositionUpdateRequested;



        public NotchViewModel(

            IMediaService? mediaService = null, 

            IVolumeService? volumeService = null, 

            IBatteryService? batteryService = null,

            ISettingsService? settingsService = null,

            IFullscreenDetector? fullscreenDetector = null,

            IDisplayService? displayService = null,

            IPrivacyService? privacyService = null,

            IBrightnessService? brightnessService = null,

            ISystemMonitorService? systemMonitorService = null)

        {

            _mediaService = mediaService ?? new MediaService();

            _volumeService = volumeService ?? new VolumeService();

            _batteryService = batteryService ?? new BatteryService();

            _settingsService = settingsService ?? new SettingsService();

            _fullscreenDetector = fullscreenDetector ?? new FullscreenDetector();

            _displayService = displayService ?? new DisplayService();

            _privacyService = privacyService ?? new PrivacyService();

            _brightnessService = brightnessService ?? new BrightnessService();

            _systemMonitorService = systemMonitorService ?? new SystemMonitorService();

            _weatherService = new WeatherService();

            _prayerService = new PrayerTimesService();

            _todoService = new TodoService();

            _noteService = new NoteService();

            _clipboardService = new ClipboardService();

            _waterService = new WaterTrackerService();

            _soundNotificationService = new SoundNotificationService();

            _downloadService = new DownloadMonitorService();



            WidgetHost = new WidgetHost();

            Media = new MediaViewModel(_mediaService, _settingsService);

            Volume = new VolumeViewModel(_volumeService);

            Battery = new BatteryViewModel(_batteryService);

            Clock = new ClockViewModel();

            Notification = new NotificationViewModel();

            SystemHealth = new SystemHealthViewModel(_systemMonitorService);

            Weather = new WeatherViewModel(_weatherService, _settingsService);

            TimerStopwatch = new TimerStopwatchViewModel(_soundNotificationService);

            _timerAlternationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _timerAlternationTimer.Tick += OnTimerAlternationTick;

            Todo = new TodoViewModel(_todoService);

            Note = new NoteViewModel(_noteService, _soundNotificationService);

            Clipboard = new ClipboardViewModel(_clipboardService);

            Note.ToastRequested += (s, msg) => TriggerTransientNotification("\U0001F4DD", L10n["Tab.Note"], msg);

            Clipboard.ToastRequested += (s, msg) => TriggerTransientNotification("\U0001F4CB", L10n["Tab.Clipboard"], msg);



            _clipboardService.ItemAdded += (s, item) =>

            {

                Application.Current?.Dispatcher.InvokeAsync(() =>

                {

                    if (item.IsImage)

                    {

                        TriggerTransientNotification("\U0001F4F8", L10n["Notification.ScreenshotCaptured"], $"{item.ImageWidth} \u00d7 {item.ImageHeight} px");

                    }

                    else if (item.IsText && !string.IsNullOrWhiteSpace(item.TextContent))

                    {

                        string preview = item.PreviewText.Trim();

                        if (preview.Length > 45) preview = preview.Substring(0, 42) + "...";

                        TriggerTransientNotification("\U0001F4CB", L10n["Notification.TextCopied"], preview);

                    }

                });

            };

            Water = new WaterTrackerViewModel(_waterService);

            Prayer = new PrayerTimesViewModel(_prayerService, _settingsService);

            _shelfService = new ShelfService();

            Shelf = new ShelfViewModel(_shelfService);

            PhoneShare = new PhoneShareService(_shelfService, _settingsService);

            ScreenTimeService = new ScreenTimeService();
            ScreenTime = new ScreenTimeViewModel(ScreenTimeService);
            ScreenTimeService.IsEnabled = _settingsService.CurrentSettings.IsScreenTimeEnabled;
            ScreenTimeService.IdleThresholdSeconds = _settingsService.CurrentSettings.ScreenTimeIdleSeconds;

            PhoneShare.ToastRequested += (s, toast) => TriggerTransientNotification(toast.Icon, toast.Title, toast.Message);

            PhoneShare.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PhoneShareService.IsPanelOpen) && IsShelfActive)
                {
                    UpdateGeometryForState(CurrentState);
                    StateAnimationRequested?.Invoke(this, EventArgs.Empty);
                }
            };

            Shortcuts = new ShortcutViewModel(new ShortcutService());

            Download = new DownloadViewModel(_downloadService);

            Download.CompletionNotice += (s, notice) =>

            {

                if (notice.Count > 1)

                {

                    TriggerTransientNotification("\U0001F4E5", L10n["Download.CompletedTitle"],

                        L10n.Get("Download.CompletedMulti", notice.Count));

                }

                else

                {

                    TriggerTransientNotification("\U0001F4E5", L10n["Download.CompletedTitle"], notice.FirstFileName);

                }

            };



            // The Control Center card grows to fit the download strip; resize the

            // panel live while the user is looking at it.

            Download.PropertyChanged += (s, e) =>

            {

                if (e.PropertyName == nameof(DownloadViewModel.HasActiveDownloads))
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UpdateTimerAlternationState();
                    });
                }

                if (e.PropertyName != nameof(DownloadViewModel.HasActiveDownloads)) return;

                if (CurrentState != NotchState.Expanded || CurrentSlideIndex != 0) return;



                double oldHeight = TargetHeight;

                UpdateGeometryForState(CurrentState);

                if (Math.Abs(oldHeight - TargetHeight) > 0.5)

                {

                    StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                }

            };



            WidgetHost.WidgetEnabledPredicate = IsWidgetEnabled;

            try
            {
                _isDarkModeOn = QuickActionHelper.GetIsDarkMode();
                _isMicrophoneMuted = QuickActionHelper.GetMicrophoneMute();
            }
            catch { }



            WidgetHost.PropertyChanged += (s, e) =>

            {

                if (e.PropertyName == nameof(WidgetHost.IsTransientActive) &&

                    !WidgetHost.IsTransientActive &&

                    Notification.IsWaterAlarm)

                {

                    // HUD volume/baterai sempat menimpa alarm air; tampilkan kembali sampai user memilih aksi.

                    WidgetHost.TriggerPersistent(WidgetType.Notification);

                }

            };



            WidgetHost.RegisterWidget(Clock);

            WidgetHost.RegisterWidget(Media);

            WidgetHost.RegisterWidget(Volume);

            WidgetHost.RegisterWidget(Battery);

            WidgetHost.RegisterWidget(Notification);

            WidgetHost.RegisterWidget(TimerStopwatch);

            TimerStopwatch.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TimerStopwatchViewModel.IsAnyTimerRunning) ||
                    e.PropertyName == nameof(TimerStopwatchViewModel.IsTimerRunning) ||
                    e.PropertyName == nameof(TimerStopwatchViewModel.IsStopwatchRunning) ||
                    e.PropertyName == nameof(TimerStopwatchViewModel.ShowTimerInCollapsed) ||
                    e.PropertyName == nameof(TimerStopwatchViewModel.ShowStopwatchInCollapsed))
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(IsCollapsedTimerPhaseActive));
                        OnPropertyChanged(nameof(IsCollapsedStopwatchDisplay));
                        OnPropertyChanged(nameof(ShowTimerIconInCollapsed));
                        OnPropertyChanged(nameof(ShowStopwatchIconInCollapsed));
                        UpdateTimerAlternationState();
                    });
                }
            };



            Todo.TasksChanged += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    if (CurrentState == NotchState.Expanded && IsTodoActive)

                    {

                        UpdateGeometryForState(CurrentState);

                        StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                    }

                });

            };



            Media.PropertyChanged += (s, e) =>

            {

                if (e.PropertyName == nameof(MediaViewModel.IsActive))

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        WidgetHost.EvaluateHighestPriorityWidget();

                    });

                }

                else if (e.PropertyName == nameof(MediaViewModel.IsCollapsedLyricsActive))

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        if (CurrentState == NotchState.Collapsed)

                        {

                            UpdateGeometryForState(CurrentState);

                            StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                        }

                    });

                }

                else if (e.PropertyName == nameof(MediaViewModel.IsLyricsVersionPickerOpen))

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        if (CurrentState == NotchState.Expanded && CurrentSlideIndex == MediaSlideIndex)

                        {

                            UpdateGeometryForState(CurrentState);

                            StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                        }

                    });

                }

            };



            _volumeService.VolumeChanged += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    if (!IsSliderDragging)

                    {

                        _cachedVolumePercent = -1;

                        OnPropertyChanged(nameof(MasterVolumePercent));

                    }

                    OnPropertyChanged(nameof(IsMasterVolumeMuted));

                });

            };



            _volumeService.BluetoothAudioConnected += (s, deviceName) =>

            {

                string batteryInfo = !string.IsNullOrEmpty(_volumeService.BluetoothBatteryText) ? $" ({_volumeService.BluetoothBatteryText})" : "";

                TriggerTransientNotification("\U0001F3A7", L10n["Notification.BluetoothConnected"], $"{deviceName}{batteryInfo}");

            };



            _volumeService.BluetoothAudioDisconnected += (s, deviceName) =>

            {

                TriggerTransientNotification("\U0001F3A7", L10n["Notification.BluetoothDisconnected"], $"{deviceName}");

            };



            _volumeService.BluetoothRadioStateChanged += (s, isOn) =>

            {

                string title = L10n[isOn ? "Notification.BluetoothOn" : "Notification.BluetoothOff"];

                string message = L10n[isOn ? "Notification.BluetoothOnMessage" : "Notification.BluetoothOffMessage"];

                TriggerTransientNotification("\U0001F3A7", title, message);

            };



            _brightnessService.BrightnessChanged += (s, b) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    if (!IsSliderDragging)

                    {

                        _cachedBrightnessPercent = -1;

                        OnPropertyChanged(nameof(ScreenBrightnessPercent));

                    }

                });

            };



            _ = _brightnessService.InitializeAsync().ContinueWith(_ =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    OnPropertyChanged(nameof(IsBrightnessSupported));

                    OnPropertyChanged(nameof(ScreenBrightnessPercent));

                });

            });



            _privacyService.PrivacyStateChanged += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    OnPropertyChanged(nameof(IsCameraInUse));

                    OnPropertyChanged(nameof(IsMicrophoneInUse));

                    OnPropertyChanged(nameof(IsPrivacyActive));

                });

            };

            _privacyService.Start();



            // Configure settings

            _lastPrayerEnabled = _settingsService.CurrentSettings.IsPrayerReminderEnabled;

            ApplyCurrentSettings(_settingsService.CurrentSettings);

            _settingsService.SettingsChanged += (s, settings) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    ApplyCurrentSettings(settings);

                });

            };



            // Fullscreen Auto-Hide Hook

            _fullscreenDetector.FullscreenChanged += (s, isFullscreen) =>

            {

                if (_settingsService.CurrentSettings.IsFullscreenAutoHideEnabled)

                {

                    IsFullscreenActive = isFullscreen;

                    if (isFullscreen)

                    {

                        Collapse();

                    }

                    else

                    {

                        // Keluar fullscreen: batalkan peek dan kembali ke notch normal.

                        IsSlimBarPeekActive = false;

                        CancelPendingSlimBarPeekClose();

                    }

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        VisibilityChanged?.Invoke(this, EventArgs.Empty);

                    });

                }

            };

            if (_settingsService.CurrentSettings.IsFullscreenAutoHideEnabled)

            {

                _fullscreenDetector.Start();

            }



            // Display changes

            _displayService.DisplayConfigurationChanged += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    PositionUpdateRequested?.Invoke(this, EventArgs.Empty);

                });

            };



            // Transient trigger: Volume HUD overlay on volume key press

            Volume.VolumeTransientTriggered += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    WidgetHost.TriggerTransient(WidgetType.Volume, TimeSpan.FromMilliseconds(1600));

                });

            };



            // Transient trigger: Battery charger plug-in overlay

            Battery.BatteryTransientTriggered += (s, e) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    WidgetHost.TriggerTransient(WidgetType.Battery, TimeSpan.FromMilliseconds(2500));

                });

            };



            // Switch to/from Media widget when active playback changes

            _mediaService.MediaTrackChanged += (s, track) =>

            {

                PetCompanionService.Instance.IsMusicPlaying = track.IsPlaying;

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    WidgetHost.EvaluateHighestPriorityWidget();

                });

            };

            _mediaService.PlaybackStateChanged += (s, isPlaying) =>

            {

                PetCompanionService.Instance.IsMusicPlaying = isPlaying;

            };



            WidgetHost.ActiveWidgetChanged += (s, type) =>

            {

                Application.Current?.Dispatcher.Invoke(() =>

                {

                    if (!Media.HasMedia)

                    {

                        // Hanya kembalikan ke warna aksen default jika sama sekali tidak ada sesi media di sistem

                        ThemeManager.RestoreDefaultAccent();

                    }

                    else if (_settingsService.CurrentSettings.IsAlbumArtAccentEnabled)

                    {

                        if (Media.Thumbnail is System.Windows.Media.Imaging.BitmapSource bmp)

                        {

                            var albumAccent = AlbumColorExtractor.ExtractDominantAccentColor(bmp);

                            if (albumAccent.HasValue)

                            {

                                ThemeManager.ApplyDynamicAccent(albumAccent.Value);

                            }

                        }

                        else if (ThemeManager.CurrentAlbumAccent.HasValue)

                        {

                            ThemeManager.ApplyDynamicAccent(ThemeManager.CurrentAlbumAccent.Value);

                        }

                    }



                    UpdateGeometryForState(CurrentState);

                    StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                });

            };



            // Update collapsed width when laptop battery is detected

            Battery.PropertyChanged += (s, e) =>

            {

                if (e.PropertyName == nameof(Battery.HasBattery))

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        OnPropertyChanged(nameof(IsCollapsedBatteryVisible));

                        UpdateGeometryForState(CurrentState);

                        StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                    });

                }

            };



            TimerStopwatch.TimerFinished += (s, e) =>

            {

                IsHomeNotificationActive = true;

                HomeNotificationType = "timer";

                HomeNotificationTitle = "Waktu Timer Selesai!";

                HomeNotificationSubtitle = $"{DateTime.Now:HH:mm} \u00b7 alarm timer berdering";

                HomeNotificationActionText = "Matikan";



                TriggerTransientNotification("\u23F1\uFE0F", L10n["Notification.TimerFinishedTitle"], L10n["Notification.TimerFinishedDesc"]);

            };



            _waterService.WaterReminderTriggered += (s, msg) =>

            {

                IsHomeNotificationActive = true;

                HomeNotificationType = "water";

                HomeNotificationTitle = "Waktunya Minum Air!";

                HomeNotificationSubtitle = msg;

                HomeNotificationActionText = "Selesai";



                ShowWaterAlarmNotification(msg);

            };



            _waterService.ReminderCleared += (s, e) =>

            {

                if (HomeNotificationType == "water")

                {

                    IsHomeNotificationActive = false;

                }

                DismissWaterAlarmNotification();

            };



            _noteService.ReminderDue += (s, item) =>

            {

                Application.Current?.Dispatcher.InvokeAsync(() =>

                {

                    IsHomeNotificationActive = true;

                    HomeNotificationType = "alarm";

                    HomeNotificationTitle = string.IsNullOrWhiteSpace(item.Text) ? "Alarm Pengingat" : item.Text;

                    HomeNotificationSubtitle = $"{DateTime.Now:HH:mm} \u00B7 alarm pengingat berdering";

                    HomeNotificationActionText = "Matikan";



                    TriggerTransientNotification("\u23F0", "Alarm Pengingat", item.Text);

                    if (item.IsAlarmEnabled)

                    {

                        try { _soundNotificationService.PlayTimerAlarm(); } catch { }

                    }

                });

            };



            _ = Task.Run(async () =>

            {

                try

                {

                    var radios = await Windows.Devices.Radios.Radio.GetRadiosAsync();

                    foreach (var radio in radios)

                    {

                        if (radio.Kind == Windows.Devices.Radios.RadioKind.WiFi)

                        {

                            var on = radio.State == Windows.Devices.Radios.RadioState.On;

                            Application.Current?.Dispatcher?.InvokeAsync(() => IsWifiToggledOn = on);

                        }

                        else if (radio.Kind == Windows.Devices.Radios.RadioKind.Bluetooth)

                        {

                            var on = radio.State == Windows.Devices.Radios.RadioState.On;

                            Application.Current?.Dispatcher?.InvokeAsync(() => IsBluetoothToggledOn = on);

                        }

                    }

                }

                catch { }

            });



            _prayerService.PrayerTimeReached += (s, msg) =>

            {

                if (_settingsService.CurrentSettings.IsPrayerReminderEnabled)

                {

                    IsHomeNotificationActive = true;

                    HomeNotificationType = "prayer";

                    HomeNotificationTitle = $"Waktunya salat {msg}";

                    HomeNotificationSubtitle = $"{DateTime.Now:HH:mm} \u00b7 adzan sedang berbunyi";

                    HomeNotificationActionText = "Matikan";



                    int durationMs = _soundNotificationService.PrayerCallDurationMs + 2000;

                    TriggerTransientNotification("\U0001F54C", L10n["Notification.PrayerReminderTitle"], msg, durationMs, isPrayer: true);

                    if (_settingsService.CurrentSettings.PrayerSoundEnabled)

                    {

                        try

                        {

                            _soundNotificationService.PlayPrayerCall();

                        }

                        catch { }

                    }

                }

            };



            LocalizationService.Instance.LanguageChanged += (s, lang) =>

            {

                OnPropertyChanged(nameof(CurrentSlideTitle));

                OnPropertyChanged(nameof(PinToolTipText));

            };



            _ = _mediaService.InitializeAsync();

            _ = _volumeService.InitializeAsync();

            _ = _batteryService.InitializeAsync();



            WidgetHost.EvaluateHighestPriorityWidget();

            UpdateGeometryForState(NotchState.Collapsed);

        }

        public void UpdateTimerAlternationState()
        {
            bool hasTimer = TimerStopwatch?.IsAnyTimerRunning == true;
            bool hasDownload = Download?.HasActiveDownloads == true;

            if (CurrentState == NotchState.Collapsed && (hasTimer || hasDownload))
            {
                if (!_timerAlternationTimer.IsEnabled)
                {
                    IsCollapsedTimerAlternatePhase = false;
                    IsCollapsedDownloadPhaseActive = false;
                    _timerAlternationTimer.Start();
                }
            }
            else
            {
                _timerAlternationTimer.Stop();
                if (IsCollapsedTimerAlternatePhase)
                {
                    IsCollapsedTimerAlternatePhase = false;
                }
                if (IsCollapsedDownloadPhaseActive)
                {
                    IsCollapsedDownloadPhaseActive = false;
                }
            }
        }

        private void OnTimerAlternationTick(object? sender, EventArgs e)
        {
            bool hasTimer = TimerStopwatch?.IsAnyTimerRunning == true;
            bool hasDownload = Download?.HasActiveDownloads == true;

            if (CurrentState != NotchState.Collapsed || (!hasTimer && !hasDownload))
            {
                _timerAlternationTimer.Stop();
                IsCollapsedTimerAlternatePhase = false;
                IsCollapsedDownloadPhaseActive = false;
                UpdateGeometryForState(CurrentState);
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (hasDownload && !hasTimer)
            {
                IsCollapsedTimerAlternatePhase = false;
                IsCollapsedDownloadPhaseActive = !IsCollapsedDownloadPhaseActive;
            }
            else if (hasTimer && !hasDownload)
            {
                IsCollapsedDownloadPhaseActive = false;
                IsCollapsedTimerAlternatePhase = !IsCollapsedTimerAlternatePhase;
                if (IsCollapsedTimerAlternatePhase && TimerStopwatch.IsTimerRunning && TimerStopwatch.IsStopwatchRunning)
                {
                    _alternateBetweenTimerAndStopwatch = !_alternateBetweenTimerAndStopwatch;
                    OnPropertyChanged(nameof(IsCollapsedStopwatchDisplay));
                    OnPropertyChanged(nameof(ShowTimerIconInCollapsed));
                    OnPropertyChanged(nameof(ShowStopwatchIconInCollapsed));
                }
            }
            else
            {
                // Both Download and Timer running: Cycle Normal -> Download -> Timer -> Normal ...
                if (!IsCollapsedDownloadPhaseActive && !IsCollapsedTimerAlternatePhase)
                {
                    IsCollapsedDownloadPhaseActive = true;
                    IsCollapsedTimerAlternatePhase = false;
                }
                else if (IsCollapsedDownloadPhaseActive)
                {
                    IsCollapsedDownloadPhaseActive = false;
                    IsCollapsedTimerAlternatePhase = true;
                    if (TimerStopwatch.IsTimerRunning && TimerStopwatch.IsStopwatchRunning)
                    {
                        _alternateBetweenTimerAndStopwatch = !_alternateBetweenTimerAndStopwatch;
                        OnPropertyChanged(nameof(IsCollapsedStopwatchDisplay));
                        OnPropertyChanged(nameof(ShowTimerIconInCollapsed));
                        OnPropertyChanged(nameof(ShowStopwatchIconInCollapsed));
                    }
                }
                else
                {
                    IsCollapsedDownloadPhaseActive = false;
                    IsCollapsedTimerAlternatePhase = false;
                }
            }

            UpdateGeometryForState(CurrentState);
            StateAnimationRequested?.Invoke(this, EventArgs.Empty);
        }

        private bool IsWidgetEnabled(WidgetType type)

        {

            var s = _settingsService.CurrentSettings;

            return type switch

            {

                WidgetType.Media => s.IsMediaWidgetEnabled,

                _ => true

            };

        }



        private void ApplyCurrentSettings(AppSettings settings)

        {

            _collapseDelay = TimeSpan.FromMilliseconds(Math.Max(100, settings.HoverCollapseDelayMs));

            _expandDelay = TimeSpan.FromMilliseconds(Math.Max(100, settings.HoverExpandDelayMs));



            if (settings.IsDownloadMonitorEnabled) _downloadService.Start();

            else _downloadService.Stop();



            ThemeManager.IsAccentColorEnabled = settings.IsAccentColorEnabled;
            ThemeManager.ApplyMaterial(settings.NotchMaterial ?? "OLED");

            ThemeManager.ApplyAccentColor(settings.AccentColor, force: false);

            ShowWeatherInCollapsedNotch = settings.ShowWeatherInCollapsedNotch;
            ShowBatteryInCollapsedNotch = settings.ShowBatteryInCollapsedNotch;
            ShowPetInCollapsedNotch = settings.ShowPetInCollapsedNotch;
            OnPropertyChanged(nameof(IsCollapsedWeatherVisible));
            OnPropertyChanged(nameof(IsCollapsedBatteryVisible));
            OnPropertyChanged(nameof(IsCollapsedPetVisible));

            FontScaleService.SetScale(settings.FontScale);

            OnPropertyChanged(nameof(FontScale));

            OnPropertyChanged(nameof(IsPrivacyActive));



            _activeModuleIds.Clear();
            var modules = settings.NavbarModules ?? AppSettings.GetDefaultNavbarModules();
            foreach (var mod in modules)
            {
                if (string.Equals(mod.Id, "home", StringComparison.OrdinalIgnoreCase) || mod.IsEnabled)
                {
                    _activeModuleIds.Add(mod.Id.ToLowerInvariant());
                }
            }
            if (!_activeModuleIds.Contains("home"))
            {
                _activeModuleIds.Insert(0, "home");
            }

            if (CurrentSlideIndex >= TotalActiveSlides)
            {
                CurrentSlideIndex = 0;
            }

            OnPropertyChanged(nameof(ActiveModuleIds));
            OnPropertyChanged(nameof(TotalActiveSlides));
            OnPropertyChanged(nameof(CurrentActiveModuleId));
            OnPropertyChanged(nameof(CurrentSlideTitle));
            OnPropertyChanged(nameof(HomeSlideIndex));
            OnPropertyChanged(nameof(ControlCenterSlideIndex));
            OnPropertyChanged(nameof(MediaSlideIndex));
            OnPropertyChanged(nameof(WeatherSlideIndex));
            OnPropertyChanged(nameof(PrayerSlideIndex));
            OnPropertyChanged(nameof(WaterSlideIndex));
            OnPropertyChanged(nameof(NoteSlideIndex));
            OnPropertyChanged(nameof(TodoSlideIndex));
            OnPropertyChanged(nameof(ClipboardSlideIndex));
            OnPropertyChanged(nameof(TimerSlideIndex));
            OnPropertyChanged(nameof(ShelfSlideIndex));
            OnPropertyChanged(nameof(IsScreenTimeActive));
            OnPropertyChanged(nameof(ScreenTimeSlideIndex));
            OnPropertyChanged(nameof(IsHomeActive));
            OnPropertyChanged(nameof(IsControlCenterActive));
            OnPropertyChanged(nameof(IsMediaActive));
            OnPropertyChanged(nameof(IsWeatherActive));
            OnPropertyChanged(nameof(IsPrayerActive));
            OnPropertyChanged(nameof(IsWaterActive));
            OnPropertyChanged(nameof(IsNoteActive));
            OnPropertyChanged(nameof(IsTodoActive));
            OnPropertyChanged(nameof(IsClipboardActive));
            OnPropertyChanged(nameof(IsTimerActive));
            OnPropertyChanged(nameof(IsShelfActive));



            IsPhoneShareEnabled = settings.IsPhoneShareEnabled;
            if (ScreenTimeService != null)
            {
                ScreenTimeService.IsEnabled = settings.IsScreenTimeEnabled;
                ScreenTimeService.IdleThresholdSeconds = settings.ScreenTimeIdleSeconds;
            }

            if (!settings.IsPhoneShareEnabled)

            {

                PhoneShare.Stop();

            }



            if (settings.IsPrayerReminderEnabled)

            {

                _prayerService.UpdateLocation(settings.PrayerCityPreset, settings.PrayerLatitude, settings.PrayerLongitude);

                _prayerService.StartMonitoring();

            }

            else

            {

                _prayerService.StopMonitoring();

            }



            if (settings.IsWeatherEnabled)

            {

                _ = _weatherService.RefreshAsync(settings.WeatherCity, settings.WeatherLatitude, settings.WeatherLongitude, settings.IsFahrenheit);

                _weatherService.StartPeriodicUpdates();

            }

            else

            {

                _weatherService.StopPeriodicUpdates();

            }



            if (settings.IsWaterReminderEnabled)

            {

                _waterService.UpdateGoal(settings.WaterDailyGoalMl);

                _waterService.StartReminder(settings.WaterReminderIntervalMinutes);

            }

            else

            {

                _waterService.StopReminder();

            }



            if (Notification.IsWaterAlarm)

            {

                if (settings.WaterSoundEnabled) StartWaterAlarmSoundLoop();

                else _waterAlarmSoundTimer?.Stop();

            }



            // If auto-hide was disabled while fullscreen was active, restore visibility

            if (settings.IsFullscreenAutoHideEnabled)

            {

                _fullscreenDetector.Start();

            }

            else

            {

                _fullscreenDetector.Stop();

                if (IsFullscreenActive)

                {

                    IsFullscreenActive = false;

                    VisibilityChanged?.Invoke(this, EventArgs.Empty);

                }

            }



            // Peek dari slim bar hanya valid saat fullscreen + mode slim aktif.

            if (!settings.IsFullscreenAutoHideEnabled || !settings.IsFullscreenSlimBarEnabled)

            {

                IsSlimBarPeekActive = false;

                CancelPendingSlimBarPeekClose();

            }



            // Jika mode fullscreen (slim bar / hide) diubah saat sedang fullscreen,

            // minta view melakukan transisi ulang tanpa menunggu event detektor.

            if (IsFullscreenActive)

            {

                VisibilityChanged?.Invoke(this, EventArgs.Empty);

            }





            PositionUpdateRequested?.Invoke(this, EventArgs.Empty);

            IsNotchTimerGlowEnabled = settings.IsNotchTimerGlowEnabled;

            IsNotchAlertGlowEnabled = settings.IsNotchAlertGlowEnabled;

            IsPetEnabled = settings.IsPetEnabled;

            PetCompanionService.Instance.IsEnabled = settings.IsPetEnabled;

            PetCompanionService.Instance.Style = settings.PetStyle;

            WidgetHost.EvaluateHighestPriorityWidget();

            double oldWidth = TargetWidth;

            double oldHeight = TargetHeight;

            UpdateGeometryForState(CurrentState);

            if (Math.Abs(oldWidth - TargetWidth) > 0.5 || Math.Abs(oldHeight - TargetHeight) > 0.5)

            {

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

            }

        }



        public void ToggleManualVisibility()

        {

            IsManuallyHidden = !IsManuallyHidden;

            if (IsManuallyHidden)

            {

                IsSlimBarPeekActive = false;

                CancelPendingSlimBarPeekClose();

                Collapse();

            }

            Log.Information("Manual visibility toggled: IsManuallyHidden={IsManuallyHidden}", IsManuallyHidden);

            VisibilityChanged?.Invoke(this, EventArgs.Empty);

        }



        /// <summary>

        /// Membuka notch sementara dari slim bar saat fullscreen (dipicu hover 3 detik).

        /// Notch muncul dalam state collapsed; hover/klik berikutnya mengikuti perilaku normal.

        /// </summary>

        public void OpenSlimBarPeek()

        {

            if (!IsFullscreenActive || IsManuallyHidden) return;

            if (!_settingsService.CurrentSettings.IsFullscreenSlimBarEnabled) return;

            if (IsSlimBarPeekActive) return;



            CancelPendingSlimBarPeekClose();



            if (CurrentState != NotchState.Collapsed)

            {

                Collapse();

            }



            IsSlimBarPeekActive = true;

            _peekOpenedTick = Environment.TickCount64;

            VisibilityChanged?.Invoke(this, EventArgs.Empty);

            Log.Debug("Fullscreen slim bar hover: opening notch peek.");

        }



        /// <summary>

        /// Menutup notch peek dan mengembalikan tampilan ke slim bar.

        /// </summary>

        public void CloseSlimBarPeek()

        {

            if (!IsSlimBarPeekActive) return;

            if (IsLockedExpanded) return;



            CancelPendingSlimBarPeekClose();



            if (CurrentState == NotchState.Expanded)

            {

                Collapse();

                return;

            }



            IsSlimBarPeekActive = false;

            VisibilityChanged?.Invoke(this, EventArgs.Empty);

            Log.Debug("Fullscreen slim bar hover: closing notch peek.");

        }



        private void ScheduleSlimBarPeekClose(int delayMs)

        {

            CancelPendingSlimBarPeekClose();



            // Cegah peek ditutup saat morph masuk masih berjalan; kalau tidak,

            // animasi balik akan memotong morph dan terlihat melompat.

            if (_peekOpenedTick != 0L)

            {

                int sinceOpenMs = (int)(Environment.TickCount64 - _peekOpenedTick);

                delayMs = Math.Max(delayMs, Math.Max(0, PeekMorphGuardMs - sinceOpenMs));

            }

            _peekCloseCts = new CancellationTokenSource();

            var token = _peekCloseCts.Token;



            Task.Delay(delayMs, token).ContinueWith(t =>

            {

                if (t.IsCanceled) return;



                Application.Current?.Dispatcher.Invoke(() =>

                {

                    if (!IsSlimBarPeekActive || IsLockedExpanded) return;



                    if (CurrentState == NotchState.Expanded)

                    {

                        Collapse();

                        return;

                    }



                    IsSlimBarPeekActive = false;

                    VisibilityChanged?.Invoke(this, EventArgs.Empty);

                });

            }, TaskScheduler.Default);

        }



        private void CancelPendingSlimBarPeekClose()

        {

            if (_peekCloseCts != null)

            {

                _peekCloseCts.Cancel();

                _peekCloseCts.Dispose();

                _peekCloseCts = null;

            }

        }



        [RelayCommand]

        public void OnMouseEnter()

        {

            CancelPendingCollapse();

            CancelPendingSlimBarPeekClose();



            // Alarm minum aktif: hover tidak boleh membuka notch.

            if (Notification.IsWaterAlarm) return;



            // Khusus peek dari slim bar saat fullscreen: hover hanya mempertahankan

            // pill notch dan memberi feedback stretch. Panel penuh baru terbuka

            // lewat klik pill (OnPillClicked). Di notch biasa, hover tetap expand.

            if (IsSlimBarPeekActive)

            {

                HoverStretchChanged?.Invoke(this, true);

                return;

            }



            if (CurrentState == NotchState.Collapsed)

            {

                ScheduleDelayedExpand();

            }

        }



        [RelayCommand]

        public void OnMouseLeave()

        {

            CancelPendingExpand();



            if (IsLockedExpanded || (Volume != null && Volume.IsDevicePickerOpen) || (Media != null && Media.IsLyricsVersionPickerOpen))

            {

                return;

            }



            if (IsSlimBarPeekActive)

            {

                int delay = CurrentState == NotchState.Expanded

                    ? (int)Math.Max(100, _collapseDelay.TotalMilliseconds) + 340

                    : (int)Math.Max(100, _collapseDelay.TotalMilliseconds);

                ScheduleSlimBarPeekClose(delay);

            }



            ScheduleDelayedCollapse();

        }



        [RelayCommand]

        public void OnPillClicked()

        {

            CancelPendingExpand();

            CancelPendingCollapse();



            if (CurrentState == NotchState.Collapsed)

            {

                // Saat peek fullscreen, klik pill cukup membuka panel tanpa mengunci;

                // agar notch tetap kembali ke slim bar saat kursor keluar.

                Expand(isLock: !IsSlimBarPeekActive);

            }

        }



        public void Expand(bool isLock = false)

        {

            CancelPendingExpand();

            CancelPendingCollapse();

            CancelPendingSlimBarPeekClose();



            if (isLock)

            {

                IsLockedExpanded = true;

            }



            if (CurrentState != NotchState.Expanded)
            {
                if (IsCollapsedTimerPhaseActive && TimerStopwatch?.IsAnyTimerRunning == true)
                {
                    TimerStopwatch.IsTimerMode = !IsCollapsedStopwatchDisplay;
                    CurrentSlideIndex = TimerSlideIndex;
                }
                else
                {
                    CurrentSlideIndex = WidgetHost.CurrentWidgetType == WidgetType.Timer ? TimerSlideIndex : 0;
                }



                Water.IsUiActive = true;

                Water.RefreshUi();



                CurrentState = NotchState.Expanded;

                PetCompanionService.Instance.IsNotchExpanded = true;

                UpdateGeometryForState(NotchState.Expanded);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

                UpdateSlideTelemetryState();

            }

        }



        private void UpdateSlideTelemetryState()

        {

            bool isControlCenterActive = CurrentState == NotchState.Expanded && CurrentSlideIndex == ControlCenterSlideIndex;

            if (isControlCenterActive)

            {

                SystemHealth.StartMonitoring();

                _batteryService.SetDetailedTelemetryActive(true);

            }

            else

            {

                SystemHealth.StopMonitoring();

                _batteryService.SetDetailedTelemetryActive(false);

            }

            bool isPrayerActive = CurrentState == NotchState.Expanded && IsPrayerActive;

            if (isPrayerActive)

            {

                Prayer.StartLiveCountdown();

            }

            else

            {

                Prayer.StopLiveCountdown();

            }

        }



        public void Collapse()

        {

            CancelPendingExpand();

            CancelPendingCollapse();

            IsLockedExpanded = false;

            SystemHealth.StopMonitoring();

            _batteryService.SetDetailedTelemetryActive(false);

            Prayer.StopLiveCountdown();

            Water.IsUiActive = false;

            if (Volume != null)

            {

                Volume.IsDevicePickerOpen = false;

            }

            if (Media != null)

            {

                Media.IsLyricsVersionPickerOpen = false;

            }



            bool didCollapse = CurrentState != NotchState.Collapsed;

            if (didCollapse)

            {

                CurrentState = NotchState.Collapsed;

                PetCompanionService.Instance.IsNotchExpanded = false;

                UpdateGeometryForState(NotchState.Collapsed);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

            }



            if (IsSlimBarPeekActive)

            {

                // Tunggu animasi collapse hanya kalau memang ada perubahan state.

                // Untuk hover-peek yang belum diklik, state sudah collapsed, jadi

                // jangan tambah 340 ms lagi: peek bisa langsung bermorph balik.

                ScheduleSlimBarPeekClose(didCollapse ? 340 : 0);

            }

        }



        public void RefreshBatteryState()

        {

            _batteryService.RefreshPowerState();

        }



        [RelayCommand]

        public void RequestClose()

        {

            RequestCloseEvent?.Invoke(this, EventArgs.Empty);

        }



        public void NavigateToTimerSlide()

        {

            CancelPendingCollapse();

            IsLockedExpanded = true;

            CurrentSlideIndex = TimerSlideIndex;



            if (CurrentState != NotchState.Expanded)

            {

                CurrentState = NotchState.Expanded;

                PetCompanionService.Instance.IsNotchExpanded = true;

                UpdateGeometryForState(NotchState.Expanded);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

            }



            SlideChanged?.Invoke(this, CurrentSlideIndex);

        }



        public void NavigateToDownloadsPanel()

        {

            CancelPendingCollapse();

            CurrentSlideIndex = ControlCenterSlideIndex;



            if (CurrentState != NotchState.Expanded)

            {

                CurrentState = NotchState.Expanded;

                PetCompanionService.Instance.IsNotchExpanded = true;

                UpdateGeometryForState(NotchState.Expanded);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);

            }



            UpdateSlideTelemetryState();

            SlideChanged?.Invoke(this, CurrentSlideIndex);

        }



        public void Shutdown()

        {

            PhoneShare.Dispose();

            Download.Dispose();

            _downloadService.Dispose();
            ScreenTimeService?.Dispose();

        }



        private void ScheduleDelayedCollapse()

        {

            CancelPendingCollapse();

            _collapseCts = new CancellationTokenSource();

            var token = _collapseCts.Token;



            Task.Delay(_collapseDelay, token).ContinueWith(t =>

            {

                if (!t.IsCanceled)

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        if (!IsLockedExpanded && !(Volume != null && Volume.IsDevicePickerOpen) && !(Media != null && Media.IsLyricsVersionPickerOpen))

                        {

                            Collapse();

                        }

                    });

                }

            }, TaskScheduler.Default);

        }



        public void CancelPendingCollapse()

        {

            if (_collapseCts != null)

            {

                _collapseCts.Cancel();

                _collapseCts.Dispose();

                _collapseCts = null;

            }

        }



        private void ScheduleDelayedExpand()

        {

            CancelPendingExpand();



            HoverStretchChanged?.Invoke(this, true);



            _expandCts = new CancellationTokenSource();

            var token = _expandCts.Token;



            Task.Delay(_expandDelay, token).ContinueWith(t =>

            {

                if (!t.IsCanceled)

                {

                    Application.Current?.Dispatcher.Invoke(() =>

                    {

                        if (CurrentState == NotchState.Collapsed)

                        {

                            Expand(isLock: false);

                        }

                    });

                }

            }, TaskScheduler.Default);

        }



        public void CancelPendingExpand()

        {

            if (_expandCts != null)

            {

                _expandCts.Cancel();

                _expandCts.Dispose();

                _expandCts = null;

            }



            if (CurrentState == NotchState.Collapsed)

            {

                HoverStretchChanged?.Invoke(this, false);

            }

        }



        /// <summary>

        /// Lebar notch collapsed hasil pengukuran view saat lirik aktif (null = kembali ke lebar default).

        /// </summary>

        public void SetCollapsedLyricsWidthOverride(double? width)

        {

            bool same = _collapsedLyricsWidthOverride.HasValue == width.HasValue &&

                        (!width.HasValue || Math.Abs(_collapsedLyricsWidthOverride!.Value - width.Value) < 0.5);

            if (same)

            {

                return;

            }



            _collapsedLyricsWidthOverride = width;

            if (CurrentState != NotchState.Collapsed || IsWelcomeGreetingActive)

            {

                return;

            }



            UpdateGeometryForState(CurrentState);

            StateAnimationRequested?.Invoke(this, EventArgs.Empty);

        }



        private void UpdateGeometryForState(NotchState state)

        {

            if (state == NotchState.Expanded)

            {

                // Lebar notch konsisten mengikuti slide yang paling lebar (saat ini Cuaca 620px)

                TargetWidth = MaxExpandedWidth;



                if (CurrentSlideIndex == WeatherSlideIndex)

                {

                    TargetHeight = ExpandedWeatherHeight;

                }

                else if (IsNoteActive)

                {

                    TargetHeight = ExpandedNoteHeight;

                }

                else if (IsClipboardActive)

                {

                    TargetHeight = ExpandedClipboardHeight;

                }

                else if (CurrentSlideIndex == HomeSlideIndex)

                {

                    TargetHeight = IsHomeNotificationActive ? (ExpandedHomeHeight + 56.0) : ExpandedHomeHeight;

                }

                else if (IsTodoActive)

                {

                    int tasksCount = Todo?.Tasks?.Count ?? 0;

                    double todoCardHeight = tasksCount <= 2 ? 136.0 : Math.Max(136.0, 78.0 + (tasksCount * 31.0));

                    TargetHeight = Math.Min(600.0, todoCardHeight + 42.0 + 36.0) + NavDockExtraHeight;

                }

                else if (CurrentSlideIndex == MediaSlideIndex)

                {

                    TargetHeight = (Media?.IsLyricsVersionPickerOpen == true) ? ExpandedMediaPickerHeight : ExpandedMediaHeight;

                }

                else if (CurrentSlideIndex == ControlCenterSlideIndex)

                {

                    double baseCcH = ExpandedControlCenterHeight;

                    if (Download?.HasActiveDownloads == true)

                    {

                        baseCcH += DownloadStripExtraHeight;

                    }

                    if (IsWifiExpanded || IsBluetoothExpanded)
                    {
                        baseCcH += 184.0;
                    }
                    else if (IsAudioOutputExpanded)
                    {
                        baseCcH += 122.0;
                    }

                    TargetHeight = baseCcH;

                }

                else if (IsPrayerActive)

                {

                    TargetHeight = ExpandedPrayerHeight;

                }

                else if (IsWaterActive)

                {

                    TargetHeight = ExpandedWaterHeight;

                }

                else if (IsTimerActive)

                {

                    TargetHeight = ExpandedTimerHeight;

                }

                else if (IsShelfActive)

                {

                    TargetHeight = (PhoneShare?.IsPanelOpen == true) ? 286.0 : ExpandedShelfHeight;

                }

                else if (IsScreenTimeActive || CurrentSlideIndex == ScreenTimeSlideIndex)

                {

                    TargetHeight = ExpandedScreenTimeHeight;

                }

                else

                {

                    TargetHeight = ExpandedHeight;

                }



                TargetCornerRadius = ExpandedCornerRadius;

            }

            else

            {

                // Dynamic collapsed width based on active widget

                double collapsedWidth = CollapsedWidth;



                if (WidgetHost != null)

                {

                    switch (WidgetHost.CurrentWidgetType)

                    {

                        case WidgetType.Notification:

                            collapsedWidth = Notification.IsWaterAlarm ? 340.0 : 280.0;

                            break;

                        case WidgetType.Volume:

                            collapsedWidth = 240.0;

                            break;

                        case WidgetType.Battery:

                            collapsedWidth = 230.0;

                            break;

                        case WidgetType.Media:

                            double baseMediaW = 240.0;
                            if (ShowWeatherInCollapsedNotch) baseMediaW += 68.0;
                            if (ShowBatteryInCollapsedNotch && Battery?.HasBattery == true) baseMediaW += 68.0;
                            if (ShowPetInCollapsedNotch && IsPetEnabled) baseMediaW += 34.0;

                            collapsedWidth = Media.IsCollapsedLyricsActive ? baseMediaW + 140.0 : baseMediaW;

                            break;

                        case WidgetType.Timer:

                            double baseTimerW = 210.0;
                            if (ShowWeatherInCollapsedNotch) baseTimerW += 68.0;
                            if (ShowBatteryInCollapsedNotch && Battery?.HasBattery == true) baseTimerW += 68.0;
                            if (ShowPetInCollapsedNotch && IsPetEnabled) baseTimerW += 34.0;

                            collapsedWidth = baseTimerW;

                            break;

                        case WidgetType.Clock:

                        default:

                            double baseClockW = CollapsedWidth;
                            if (ShowWeatherInCollapsedNotch) baseClockW += 68.0;
                            if (ShowBatteryInCollapsedNotch && Battery?.HasBattery == true) baseClockW += 68.0;
                            if (ShowPetInCollapsedNotch && IsPetEnabled) baseClockW += 38.0;

                            collapsedWidth = baseClockW;

                            break;

                    }

                }



                if (WidgetHost != null && WidgetHost.CurrentWidgetType == WidgetType.Media &&

                    Media.IsCollapsedLyricsActive && _collapsedLyricsWidthOverride.HasValue)

                {

                    // Lebar notch mengikuti area lirik aktif: minimal selebar widget yang sedang aktif agar tidak tumpang tindih

                    collapsedWidth = Math.Max(collapsedWidth, Math.Min(_collapsedLyricsWidthOverride.Value, MaxExpandedWidth));

                }

                if (IsCollapsedDownloadPhaseActive)
                {
                    collapsedWidth = Math.Max(265.0, collapsedWidth);
                }

                TargetWidth = collapsedWidth;

                TargetHeight = CollapsedHeight;

                TargetCornerRadius = CollapsedCornerRadius;

            }

        }



        public void SuspendPower()

        {

            Log.Information("Entering Power Suspend: Pausing timers and trimming working set");

            Clock.Pause();

            _downloadService.Suspend();

            _privacyService.Pause();

            _fullscreenDetector.Stop();



            try

            {

                NativeMethods.EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle);

            }

            catch { }

        }



        public void ResumePower()

        {

            Log.Information("Resuming from Power Suspend: Restoring timers");

            Clock.Resume();

            _privacyService.Resume();

            if (_settingsService.CurrentSettings.IsFullscreenAutoHideEnabled)

            {

                _fullscreenDetector.Start();

            }

            if (_settingsService.CurrentSettings.IsDownloadMonitorEnabled)

            {

                _downloadService.Resume();

            }

            RefreshBatteryState();



            PositionUpdateRequested?.Invoke(this, EventArgs.Empty);

        }



        public void TriggerTransientNotification(string icon, string title, string msg, int durationMs = 0, bool isPrayer = false)

        {

            var app = Application.Current;

            if (app == null || app.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;



            int effectiveDurationMs;

            if (isPrayer)

            {

                effectiveDurationMs = durationMs > 0 ? durationMs : 15000;

            }

            else

            {

                int settingSeconds = _settingsService?.CurrentSettings?.NotificationDurationSeconds ?? 3;

                effectiveDurationMs = durationMs > 0 ? durationMs : Math.Max(2000, settingSeconds * 1000);

            }



            Action action = () =>

            {

                if (Notification.IsWaterAlarm) return;



                Notification.SetNotification(icon, title, msg, isPrayer);

                WidgetHost.TriggerTransient(WidgetType.Notification, TimeSpan.FromMilliseconds(effectiveDurationMs));



                // Otomatis kaitkan event notifikasi ke badge maskot

                MascotState mascotAlert = isPrayer ? MascotState.Adzan : icon switch

                {

                    "\U0001F4F8" => MascotState.Screenshot,

                    "\U0001F4CB" => MascotState.Copied,

                    "\U0001F3A7" => MascotState.Bluetooth,

                    "\u23F1\uFE0F" => MascotState.Timer,

                    "\U0001F4A7" => MascotState.Drink,

                    "\U0001F54C" => MascotState.Adzan,

                    "\u23F0" => MascotState.Alarm,

                    _ => MascotState.Notification

                };

                PetCompanionService.Instance.TriggerAlert(mascotAlert, TimeSpan.FromMilliseconds(effectiveDurationMs));

            };



            if (app.Dispatcher.CheckAccess())

            {

                action();

            }

            else

            {

                app.Dispatcher.InvokeAsync(action);

            }

        }



        [RelayCommand]

        private void SnoozeWaterAlarm()

        {

            _waterService.Snooze(2);

            DismissWaterAlarmNotification();

        }



        [RelayCommand]

        private void DrinkWaterFromAlarm()

        {

            _waterService.AddWater(250);

            DismissWaterAlarmNotification();

        }



        public void PlayStartupSoundIfEnabled()

        {

            if (!_settingsService.CurrentSettings.StartupSoundEnabled) return;

            try { _soundNotificationService.PlayStartupSound(); } catch { }

        }



        private void ShowWaterAlarmNotification(string message)

        {

            var app = Application.Current;

            if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;



            void action()

            {

                CancelPendingExpand();

                Notification.SetNotification("\U0001F4A7", L10n["Notification.WaterReminderTitle"], message, isWaterAlarm: true);

                WidgetHost.TriggerPersistent(WidgetType.Notification);

                UpdateGeometryForState(CurrentState);

                StateAnimationRequested?.Invoke(this, EventArgs.Empty);



                PetCompanionService.Instance.SetPersistentAlert(MascotState.Drink);



                if (_settingsService.CurrentSettings.WaterSoundEnabled)

                {

                    try { _soundNotificationService.PlayWaterReminder(); } catch { }

                    StartWaterAlarmSoundLoop();

                }

                else

                {

                    _waterAlarmSoundTimer?.Stop();

                }

            }



            if (app.Dispatcher.CheckAccess()) action();

            else app.Dispatcher.InvokeAsync(action);

        }



        private void StartWaterAlarmSoundLoop()

        {

            _waterAlarmSoundTimer ??= new DispatcherTimer(DispatcherPriority.Background)

            {

                Interval = TimeSpan.FromSeconds(15)

            };

            _waterAlarmSoundTimer.Tick -= OnWaterAlarmSoundTick;

            _waterAlarmSoundTimer.Tick += OnWaterAlarmSoundTick;

            _waterAlarmSoundTimer.Start();

        }



        private void OnWaterAlarmSoundTick(object? sender, EventArgs e)

        {

            if (!Notification.IsWaterAlarm || !_waterService.IsAlarmActive)

            {

                _waterAlarmSoundTimer?.Stop();

                return;

            }



            try { _soundNotificationService.PlayWaterReminder(); } catch { }

        }



        private void DismissWaterAlarmNotification()

        {

            var app = Application.Current;

            if (app?.Dispatcher == null || app.Dispatcher.HasShutdownStarted) return;



            if (app.Dispatcher.CheckAccess()) DismissWaterAlarmCore();

            else app.Dispatcher.InvokeAsync(DismissWaterAlarmCore);

        }



        private void DismissWaterAlarmCore()

        {

            _waterAlarmSoundTimer?.Stop();

            if (!Notification.IsWaterAlarm) return;



            Notification.IsWaterAlarm = false;

            PetCompanionService.Instance.SetPersistentAlert(null);

            WidgetHost.DismissTransient();

            UpdateGeometryForState(CurrentState);

            StateAnimationRequested?.Invoke(this, EventArgs.Empty);

        }



        public void RefreshBluetoothRadioState()

        {

            _volumeService.RefreshBluetoothRadioState();

        }



        public void TriggerUsbNotification(bool isConnected, string? driveName = null)

        {

            Application.Current?.Dispatcher.Invoke(() =>

            {

                if (Notification.IsWaterAlarm) return;



                string icon = isConnected ? "\U0001F4BE" : "\u23CF\uFE0F";

                string title = isConnected ? L10n["Notification.StorageConnected"] : L10n["Notification.StorageDisconnected"];

                string msg = !string.IsNullOrEmpty(driveName)

                    ? L10n.Get(isConnected ? "Notification.DriveReady" : "Notification.DriveRemoved", driveName)

                    : (isConnected ? L10n["Notification.UsbReady"] : L10n["Notification.UsbRemoved"]);



                int settingSeconds = _settingsService?.CurrentSettings?.NotificationDurationSeconds ?? 3;

                int durationMs = Math.Max(2000, settingSeconds * 1000);

                Notification.SetNotification(icon, title, msg);

                WidgetHost.TriggerTransient(WidgetType.Notification, TimeSpan.FromMilliseconds(durationMs));

                PetCompanionService.Instance.TriggerAlert(MascotState.Notification, TimeSpan.FromMilliseconds(durationMs));

            });

        }

    }

}

