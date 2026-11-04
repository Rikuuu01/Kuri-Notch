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
        public const double CollapsedWidth = 295.0;
        public const double CollapsedHeight = 32.0;
        public const double BaseExpandedWidth = 390.0;
        public const double ExpandedLyricsWidth = 540.0;
        public const double ExpandedWeatherWidth = 620.0;
        public const double ExpandedHeight = 258.0;
        public const double ExpandedWeatherHeight = 326.0;

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

        public event EventHandler<bool>? HoverStretchChanged;

        [ObservableProperty]
        private NotchState _currentState = NotchState.Collapsed;

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
        private double _targetWidth = CollapsedWidth;
        private double? _collapsedLyricsWidthOverride;

        [ObservableProperty]
        private double _targetHeight = CollapsedHeight;

        [ObservableProperty]
        private double _targetCornerRadius = CollapsedCornerRadius;

        [ObservableProperty]
        private bool _isContentVisible;

        [ObservableProperty]
        private string _statusText = "Kuri";

        [ObservableProperty]
        private bool _isPetEnabled = true;

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
        public ShortcutViewModel Shortcuts { get; }
        public DownloadViewModel Download { get; }

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
        [NotifyPropertyChangedFor(nameof(IsShelfActive))]
        [NotifyPropertyChangedFor(nameof(IsControlCenterActive))]
        [NotifyPropertyChangedFor(nameof(IsMediaActive))]
        [NotifyPropertyChangedFor(nameof(IsWeatherActive))]
        [NotifyPropertyChangedFor(nameof(IsPrayerActive))]
        [NotifyPropertyChangedFor(nameof(IsWaterActive))]
        [NotifyPropertyChangedFor(nameof(IsTodoActive))]
        [NotifyPropertyChangedFor(nameof(IsSlide8Active))]
        [NotifyPropertyChangedFor(nameof(IsNoteActive))]
        [NotifyPropertyChangedFor(nameof(IsClipboardActive))]
        [NotifyPropertyChangedFor(nameof(IsTimerActive))]
        [NotifyPropertyChangedFor(nameof(IsShelfActive))]
        [NotifyPropertyChangedFor(nameof(CurrentSlideTitle))]
        private int _currentSlideIndex = 0;

        public bool IsSlide0Active => CurrentSlideIndex == 0;
        public bool IsSlide1Active => CurrentSlideIndex == 1;
        public bool IsSlide2Active => CurrentSlideIndex == 2;
        public bool IsSlide3Active => CurrentSlideIndex == 3;
        public bool IsSlide4Active => CurrentSlideIndex == 4;
        public bool IsSlide5Active => CurrentSlideIndex == 5;
        public bool IsSlide6Active => CurrentSlideIndex == 6;
        public bool IsSlide7Active => CurrentSlideIndex == 7;
        public bool IsSlide8Active => CurrentSlideIndex == 8;
        public bool IsNoteActive => IsPrayerEnabled ? CurrentSlideIndex == 5 : CurrentSlideIndex == 4;
        public bool IsClipboardActive => IsPrayerEnabled ? CurrentSlideIndex == 6 : CurrentSlideIndex == 5;

        public bool IsShelfActive => CurrentSlideIndex == ShelfSlideIndex;

        public bool IsControlCenterActive => CurrentSlideIndex == 0;
        public bool IsMediaActive => CurrentSlideIndex == 1;
        public bool IsWeatherActive => CurrentSlideIndex == 2;
        public bool IsPrayerActive => IsPrayerEnabled && CurrentSlideIndex == 3;
        public bool IsWaterActive => IsPrayerEnabled ? CurrentSlideIndex == 4 : CurrentSlideIndex == 3;
        public bool IsTodoActive => IsPrayerEnabled ? CurrentSlideIndex == 5 : CurrentSlideIndex == 4;
        public bool IsTimerActive => IsPrayerEnabled ? CurrentSlideIndex == 7 : CurrentSlideIndex == 6;

        public int WaterSlideIndex => IsPrayerEnabled ? 4 : 3;
        public int NoteSlideIndex => IsPrayerEnabled ? 5 : 4;
        public int TodoSlideIndex => NoteSlideIndex;
        public int ClipboardSlideIndex => IsPrayerEnabled ? 6 : 5;
        public int TimerSlideIndex => IsPrayerEnabled ? 7 : 6;
        public int ShelfSlideIndex => IsPrayerEnabled ? 8 : 7;
        public int TotalActiveSlides => IsPrayerEnabled ? 9 : 8;
        public bool IsPrayerEnabled => _settingsService.CurrentSettings.IsPrayerReminderEnabled;

        public string CurrentSlideTitle => IsPrayerEnabled
            ? CurrentSlideIndex switch
            {
                0 => L10n["Header.PusatKontrol"],
                1 => L10n["Header.SedangMemutar"],
                2 => L10n["Header.Cuaca"],
                3 => L10n["Header.JadwalSholat"],
                4 => L10n["Header.PengingatMinum"],
                5 => L10n["Header.Catatan"],
                6 => L10n["Header.Clipboard"],
                7 => L10n["Header.TimerStopwatch"],
                8 => "SHELF FILE",
                _ => "NOTCHWIN"
            }
            : CurrentSlideIndex switch
            {
                0 => L10n["Header.PusatKontrol"],
                1 => L10n["Header.SedangMemutar"],
                2 => L10n["Header.Cuaca"],
                3 => L10n["Header.PengingatMinum"],
                4 => L10n["Header.Catatan"],
                5 => L10n["Header.Clipboard"],
                6 => L10n["Header.TimerStopwatch"],
                7 => "SHELF FILE",
                _ => "NOTCHWIN"
            };

        public void NavigateToShelfSlide()
        {
            SelectSlide(ShelfSlideIndex);
        }

        public event EventHandler<int>? SlideChanged;

        partial void OnCurrentSlideIndexChanged(int value)
        {
            SlideChanged?.Invoke(this, value);
            UpdateSlideTelemetryState();
            if (CurrentState == NotchState.Expanded)
            {
                UpdateGeometryForState(CurrentState);
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public int MasterVolumePercent
        {
            get => _volumeService.VolumePercent;
            set
            {
                if (_volumeService.VolumePercent != value)
                {
                    _ = _volumeService.SetVolumeAsync(value / 100f);
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMasterVolumeMuted => _volumeService.IsMuted;

        public int ScreenBrightnessPercent
        {
            get => _brightnessService.CurrentBrightness;
            set
            {
                if (_brightnessService.CurrentBrightness != value)
                {
                    _brightnessService.SetBrightness(value);
                    OnPropertyChanged();
                }
            }
        }

        public bool IsBrightnessSupported => _brightnessService.IsSupported;

        public event EventHandler? OpenSettingsRequested;

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
            int total = TotalActiveSlides;
            if (param != null && int.TryParse(param.ToString(), out int idx) && idx >= 0 && idx < total)
            {
                CurrentSlideIndex = idx;
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
            Todo = new TodoViewModel(_todoService);
            Note = new NoteViewModel(_noteService, _soundNotificationService);
            Clipboard = new ClipboardViewModel(_clipboardService);
            Note.ToastRequested += (s, msg) => TriggerTransientNotification("📝", L10n["Tab.Note"], msg);
            Clipboard.ToastRequested += (s, msg) => TriggerTransientNotification("📋", L10n["Tab.Clipboard"], msg);

            _clipboardService.ItemAdded += (s, item) =>
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    if (item.IsImage)
                    {
                        TriggerTransientNotification("📸", L10n["Notification.ScreenshotCaptured"], $"{item.ImageWidth} \u00d7 {item.ImageHeight} px");
                    }
                    else if (item.IsText && !string.IsNullOrWhiteSpace(item.TextContent))
                    {
                        string preview = item.PreviewText.Trim();
                        if (preview.Length > 45) preview = preview.Substring(0, 42) + "...";
                        TriggerTransientNotification("📋", L10n["Notification.TextCopied"], preview);
                    }
                });
            };
            Water = new WaterTrackerViewModel(_waterService);
            Prayer = new PrayerTimesViewModel(_prayerService, _settingsService);
            Shelf = new ShelfViewModel(new ShelfService());
            Shortcuts = new ShortcutViewModel(new ShortcutService());
            Download = new DownloadViewModel(_downloadService);
            Download.CompletionNotice += (s, notice) =>
            {
                if (notice.Count > 1)
                {
                    TriggerTransientNotification("📥", L10n["Download.CompletedTitle"],
                        L10n.Get("Download.CompletedMulti", notice.Count));
                }
                else
                {
                    TriggerTransientNotification("📥", L10n["Download.CompletedTitle"], notice.FirstFileName);
                }
            };

            // The Control Center card grows to fit the download strip; resize the
            // panel live while the user is looking at it.
            Download.PropertyChanged += (s, e) =>
            {
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
            };

            _volumeService.VolumeChanged += (s, e) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(MasterVolumePercent));
                    OnPropertyChanged(nameof(IsMasterVolumeMuted));
                });
            };

            _volumeService.BluetoothAudioConnected += (s, deviceName) =>
            {
                string batteryInfo = !string.IsNullOrEmpty(_volumeService.BluetoothBatteryText) ? $" ({_volumeService.BluetoothBatteryText})" : "";
                TriggerTransientNotification("🎧", L10n["Notification.BluetoothConnected"], $"{deviceName}{batteryInfo}");
            };

            _volumeService.BluetoothAudioDisconnected += (s, deviceName) =>
            {
                TriggerTransientNotification("🎧", L10n["Notification.BluetoothDisconnected"], $"{deviceName}");
            };

            _volumeService.BluetoothRadioStateChanged += (s, isOn) =>
            {
                string title = L10n[isOn ? "Notification.BluetoothOn" : "Notification.BluetoothOff"];
                string message = L10n[isOn ? "Notification.BluetoothOnMessage" : "Notification.BluetoothOffMessage"];
                TriggerTransientNotification("🎧", title, message);
            };

            _brightnessService.BrightnessChanged += (s, b) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ScreenBrightnessPercent));
                    OnPropertyChanged(nameof(IsBrightnessSupported));
                });
            };

            _ = _brightnessService.InitializeAsync();

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
                        UpdateGeometryForState(CurrentState);
                        StateAnimationRequested?.Invoke(this, EventArgs.Empty);
                    });
                }
            };

            TimerStopwatch.TimerFinished += (s, e) =>
            {
                TriggerTransientNotification("⏱️", L10n["Notification.TimerFinishedTitle"], L10n["Notification.TimerFinishedDesc"]);
            };

            _waterService.WaterReminderTriggered += (s, msg) => ShowWaterAlarmNotification(msg);

            _waterService.ReminderCleared += (s, e) => DismissWaterAlarmNotification();

            _prayerService.PrayerTimeReached += (s, msg) =>
            {
                if (_settingsService.CurrentSettings.IsPrayerReminderEnabled)
                {
                    int durationMs = _soundNotificationService.PrayerCallDurationMs + 2000;
                    TriggerTransientNotification("🕌", L10n["Notification.PrayerReminderTitle"], msg, durationMs, isPrayer: true);
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

            ThemeManager.ApplyMaterial(settings.NotchMaterial ?? "OLED");
            ThemeManager.ApplyAccentColor(settings.AccentColor, force: false);
            FontScaleService.SetScale(settings.FontScale);
            OnPropertyChanged(nameof(FontScale));
            OnPropertyChanged(nameof(IsPrivacyActive));

            bool wasPrayerEnabled = _lastPrayerEnabled;
            bool newPrayerEnabled = settings.IsPrayerReminderEnabled;
            _lastPrayerEnabled = newPrayerEnabled;

            if (wasPrayerEnabled && !newPrayerEnabled)
            {
                // Prayer was disabled
                if (CurrentSlideIndex == 3)
                {
                    CurrentSlideIndex = 0; // If user was on prayer slide, return to Home
                }
                else if (CurrentSlideIndex > 3)
                {
                    CurrentSlideIndex--; // Shift slide index down by 1
                }
            }
            else if (!wasPrayerEnabled && newPrayerEnabled)
            {
                // Prayer was re-enabled
                if (CurrentSlideIndex >= 3)
                {
                    CurrentSlideIndex++; // Shift slide index up by 1
                }
            }

            int totalSlides = newPrayerEnabled ? 9 : 8;
            if (CurrentSlideIndex >= totalSlides)
            {
                CurrentSlideIndex = 0;
            }

            OnPropertyChanged(nameof(IsPrayerEnabled));
            OnPropertyChanged(nameof(TotalActiveSlides));
            OnPropertyChanged(nameof(CurrentSlideTitle));
            OnPropertyChanged(nameof(WaterSlideIndex));
            OnPropertyChanged(nameof(NoteSlideIndex));
            OnPropertyChanged(nameof(TodoSlideIndex));
            OnPropertyChanged(nameof(ClipboardSlideIndex));
            OnPropertyChanged(nameof(TimerSlideIndex));
            OnPropertyChanged(nameof(ShelfSlideIndex));
            OnPropertyChanged(nameof(IsPrayerActive));
            OnPropertyChanged(nameof(IsWaterActive));
            OnPropertyChanged(nameof(IsNoteActive));
            OnPropertyChanged(nameof(IsTodoActive));
            OnPropertyChanged(nameof(IsClipboardActive));
            OnPropertyChanged(nameof(IsTimerActive));
            OnPropertyChanged(nameof(IsShelfActive));

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
                if (Media.HasMedia && (Media.IsPlaying || WidgetHost.CurrentWidgetType == WidgetType.Media))
                {
                    CurrentSlideIndex = 1;
                }
                else
                {
                    CurrentSlideIndex = 0;
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
            bool isControlCenterActive = CurrentState == NotchState.Expanded && CurrentSlideIndex == 0;
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
            CurrentSlideIndex = 0;

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
            Download.Dispose();
            _downloadService.Dispose();
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
            if (CurrentState != NotchState.Collapsed)
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

                if (CurrentSlideIndex == 2 || IsNoteActive || IsClipboardActive)
                {
                    TargetHeight = ExpandedWeatherHeight;
                }
                else if (IsTodoActive)
                {
                    int tasksCount = Todo?.Tasks?.Count ?? 0;
                    double todoCardHeight = tasksCount <= 2 ? 136.0 : Math.Max(136.0, 78.0 + (tasksCount * 31.0));
                    TargetHeight = Math.Min(600.0, todoCardHeight + 42.0 + 36.0) + NavDockExtraHeight;
                }
                else
                {
                    TargetHeight = ExpandedHeight;
                    if (CurrentSlideIndex == 0 && Download?.HasActiveDownloads == true)
                    {
                        TargetHeight += DownloadStripExtraHeight;
                    }
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
                            double baseMediaW = (Battery?.HasBattery == true) ? 335.0 : 295.0;
                            collapsedWidth = Media.IsCollapsedLyricsActive ? baseMediaW + 65.0 : baseMediaW;
                            break;
                        case WidgetType.Clock:
                        default:
                            collapsedWidth = (Battery?.HasBattery == true) ? 295.0 : 250.0;
                            break;
                    }
                }

                if (IsPetEnabled)
                {
                    collapsedWidth += 28.0;
                }

                if (WidgetHost != null && WidgetHost.CurrentWidgetType == WidgetType.Media &&
                    Media.IsCollapsedLyricsActive && _collapsedLyricsWidthOverride.HasValue)
                {
                    // Lebar hasil ukur dari view (chrome + teks lirik aktif); hanya melebar, tidak menyempit.
                    collapsedWidth = Math.Max(collapsedWidth, Math.Min(_collapsedLyricsWidthOverride.Value, MaxExpandedWidth));
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
                    "📸" => MascotState.Screenshot,
                    "📋" => MascotState.Copied,
                    "🎧" => MascotState.Bluetooth,
                    "⏱️" => MascotState.Timer,
                    "💧" => MascotState.Drink,
                    "🕌" => MascotState.Adzan,
                    "⏰" => MascotState.Alarm,
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
                Notification.SetNotification("💧", L10n["Notification.WaterReminderTitle"], message, isWaterAlarm: true);
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

                string icon = isConnected ? "💾" : "⏏️";
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
