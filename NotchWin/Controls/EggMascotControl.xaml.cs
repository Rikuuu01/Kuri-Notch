#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.Controls
{
    /// <summary>
    /// Egg Mascot: telur putih minimalis dengan lengan yang melambai saat sapaan startup.
    /// API publik identik dengan MascotControl / AstroMascotControl.
    /// </summary>
    public partial class EggMascotControl : UserControl
    {
        // ───────────────────────── Easing & shortcut property ─────────────────────────
        private static readonly IEasingFunction Smooth = Freeze(new SineEase { EasingMode = EasingMode.EaseInOut });
        private static readonly IEasingFunction PopOut = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.9 });

        private static readonly DependencyProperty SX = ScaleTransform.ScaleXProperty;
        private static readonly DependencyProperty SY = ScaleTransform.ScaleYProperty;
        private static readonly DependencyProperty RA = RotateTransform.AngleProperty;
        private static readonly DependencyProperty TX = TranslateTransform.XProperty;
        private static readonly DependencyProperty TY = TranslateTransform.YProperty;
        private static readonly DependencyProperty OP = UIElement.OpacityProperty;

        private static T Freeze<T>(T f) where T : Freezable
        {
            f.Freeze();
            return f;
        }

        // ───────────────────────── Keadaan internal ─────────────────────────
        private enum EyeSet { Open, Happy, Sleep }
        private enum PupilMode { Cursor, Fixed }

        private sealed record AlertRequest(MascotState Kind, TimeSpan Duration);

        private readonly Random _rng = new();
        private readonly DispatcherTimer _pupilTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(66) };
        private readonly DispatcherTimer _blinkTimer = new();
        private readonly DispatcherTimer _alertTimer = new();
        private readonly DispatcherTimer _idleTimer = new();
        private readonly List<AlertRequest> _queue = new();
        private readonly UIElement[] _icons;

        private AlertRequest? _active;
        private bool _music;
        private bool _sleeping;
        private bool _headphoneOn;
        private bool _eyesOpen = true;
        private MascotState _shown = (MascotState)(-1);

        private PupilMode _pupilMode = PupilMode.Cursor;
        private Point _pupilFixed;
        private double _pupilX, _pupilY, _pupilJitter;
        private NativePoint _lastNativePos;
        private Point _lastLocalPos;
        private bool _hasLocalPos;

        private TimeSpan _autoSleepAfter = PetCompanionService.IdleSleepAfterSeconds > 0
            ? TimeSpan.FromSeconds(PetCompanionService.IdleSleepAfterSeconds)
            : TimeSpan.Zero;

        // ───────────────────────── API publik ─────────────────────────

        /// <summary>Wujud yang sedang tampil: peringatan aktif > tidur > musik > idle.</summary>
        public MascotState CurrentState =>
            _active?.Kind ?? (_sleeping ? MascotState.Sleep : _music ? MascotState.Music : MascotState.Idle);

        /// <summary>Lama reaksi Happy. Gunakan Timeout.InfiniteTimeSpan agar bertahan sampai DismissAlert().</summary>
        public TimeSpan HappyDuration { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>Idle selama durasi ini → tertidur otomatis. TimeSpan.Zero = nonaktif.</summary>
        public TimeSpan AutoSleepAfter
        {
            get => _autoSleepAfter;
            set { _autoSleepAfter = value; RestartIdleClock(); }
        }

        /// <summary>Headphone + goyang mengikuti musik. Menyalakan musik juga membangunkan maskot.</summary>
        public bool IsMusicPlaying
        {
            get => _music;
            set
            {
                if (_music == value) return;
                _music = value;
                if (value) _sleeping = false;
                Refresh();
            }
        }

        /// <summary>Dipicu setiap peringatan selesai (habis waktu atau di-dismiss).</summary>
        public event EventHandler<MascotState>? AlertFinished;

        /// <summary>Sapaan startup: lengan melambai, mata tersenyum, kilau di kedua sisi.</summary>
        public void PlayWelcome(TimeSpan? duration = null) =>
            ShowAlert(MascotState.Welcome, duration);

        /// <summary>
        /// Idle  = bangun + musik mati.  Music = bangun + musik menyala.
        /// Sleep = tidur (status musik tidak berubah).  Selain itu diteruskan ke ShowAlert.
        /// </summary>
        public void SetState(MascotState state)
        {
            switch (state)
            {
                case MascotState.Idle:
                    _sleeping = false; _music = false; Refresh(); break;
                case MascotState.Music:
                    _sleeping = false; _music = true; Refresh(); break;
                case MascotState.Sleep:
                    _sleeping = true; Refresh(); break;
                default:
                    ShowAlert(state); break;
            }
        }

        /// <summary>
        /// Tampilkan peringatan. Prioritas: Alarm/Adzan (3) > Timer/Drink (2) > lainnya (1) > Happy (0).
        /// Prioritas lebih tinggi menyela; yang sama/lebih rendah masuk antrean; jenis yang sama memperpanjang waktu.
        /// duration = null memakai durasi bawaan; Timeout.InfiniteTimeSpan bertahan sampai DismissAlert().
        /// </summary>
        public void ShowAlert(MascotState alert, TimeSpan? duration = null)
        {
            if (!IsAlert(alert)) { SetState(alert); return; }

            var req = new AlertRequest(alert, duration ?? DefaultDuration(alert));

            if (_active == null) { Begin(req); return; }

            if (_active.Kind == alert)
            {
                _active = req;
                Arm(req.Duration);
                return;
            }

            if (Priority(alert) > Priority(_active.Kind)) { Begin(req); return; }

            if (_queue.Count < 8 && !_queue.Exists(r => r.Kind == alert))
                _queue.Add(req);
        }

        /// <summary>Akhiri peringatan aktif; lanjut ke antrean atau kembali ke keadaan dasar.</summary>
        public void DismissAlert()
        {
            if (_active != null) EndAlert();
        }

        /// <summary>Bersihkan antrean dan akhiri peringatan aktif.</summary>
        public void ClearAlerts()
        {
            _queue.Clear();
            DismissAlert();
        }

        // ───────────────────────── Siklus hidup ─────────────────────────
        public EggMascotControl()
        {
            InitializeComponent();

            _icons = new UIElement[]
            {
                IconBell, IconAlarm, IconDrink, IconTimer,
                IconAdzan, IconBluetooth, IconCopied, IconScreenshot
            };

            _pupilTimer.Tick += OnPupilTick;
            _blinkTimer.Tick += OnBlinkTick;
            _alertTimer.Tick += (_, _) => EndAlert();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
            MouseLeftButtonUp += OnPetted;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PetCompanionService.Instance.MascotAlertTriggered += OnServiceMascotAlert;
            PetCompanionService.Instance.MascotAlertCleared += OnServiceMascotAlertCleared;
            PetCompanionService.Instance.MusicStateChanged += OnServiceMusicChanged;
            PetCompanionService.Instance.StateChanged += OnServiceStateChanged;
            PetCompanionService.Instance.NotchExpandedChanged += OnServiceNotchExpandedChanged;
            _music = PetCompanionService.Instance.IsMusicPlaying;
            _sleeping = PetCompanionService.Instance.State == PetState.Sleeping;

            if (PetCompanionService.Instance.CurrentAlert.HasValue)
            {
                ShowAlert(PetCompanionService.Instance.CurrentAlert.Value, PetCompanionService.Instance.CurrentAlertDuration);
            }

            if (IsVisible)
            {
                _pupilTimer.Interval = TimeSpan.FromMilliseconds(PetCompanionService.Instance.IsNotchExpanded ? 66 : 150);
                if (!_sleeping) _pupilTimer.Start();
                if (!_sleeping) ScheduleBlink();
                if (_active != null) Arm(_active.Duration);
                Refresh(force: true);
            }
            else
            {
                _pupilTimer.Stop();
                _blinkTimer.Stop();
                ResetBody();
                StopFx();
                ClearBadge();
            }
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                _music = PetCompanionService.Instance.IsMusicPlaying;
                _sleeping = PetCompanionService.Instance.State == PetState.Sleeping;

                _pupilTimer.Interval = TimeSpan.FromMilliseconds(PetCompanionService.Instance.IsNotchExpanded ? 66 : 150);
                if (!_sleeping && !_pupilTimer.IsEnabled) _pupilTimer.Start();
                if (!_sleeping) ScheduleBlink();

                if (PetCompanionService.Instance.CurrentAlert.HasValue)
                {
                    ShowAlert(PetCompanionService.Instance.CurrentAlert.Value, PetCompanionService.Instance.CurrentAlertDuration);
                }
                else
                {
                    Refresh(force: true);
                }
            }
            else
            {
                _pupilTimer.Stop();
                _blinkTimer.Stop();
                ResetBody();
                StopFx();
                ClearBadge();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PetCompanionService.Instance.MascotAlertTriggered -= OnServiceMascotAlert;
            PetCompanionService.Instance.MascotAlertCleared -= OnServiceMascotAlertCleared;
            PetCompanionService.Instance.MusicStateChanged -= OnServiceMusicChanged;
            PetCompanionService.Instance.StateChanged -= OnServiceStateChanged;
            PetCompanionService.Instance.NotchExpandedChanged -= OnServiceNotchExpandedChanged;

            _pupilTimer.Stop();
            _blinkTimer.Stop();
            _alertTimer.Stop();
            _idleTimer.Stop();
        }

        private void OnServiceNotchExpandedChanged(object? sender, bool isExpanded)
        {
            Dispatcher.InvokeAsync(() =>
            {
                bool grooveActive = _music && !_sleeping && (_active == null || _active.Kind == MascotState.Happy);
                if (!isExpanded && !grooveActive)
                {
                    ResetBody();
                    StopFx();
                }
                _pupilTimer.Interval = TimeSpan.FromMilliseconds(isExpanded ? 66 : 150);
                Refresh(force: true);
            });
        }

        private void OnServiceStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                bool shouldSleep = PetCompanionService.Instance.State == PetState.Sleeping;
                if (_sleeping != shouldSleep)
                {
                    _sleeping = shouldSleep;
                    Refresh(force: true);
                }
            });
        }

        private void OnServiceMascotAlert(object? sender, MascotAlertEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ShowAlert(e.State, e.Duration);
            });
        }

        private void OnServiceMascotAlertCleared(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ClearAlerts();
            });
        }

        private void OnServiceMusicChanged(object? sender, bool isPlaying)
        {
            Dispatcher.InvokeAsync(() =>
            {
                IsMusicPlaying = isPlaying;
            });
        }

        /// <summary>Klik maskot: membangunkan kalau tidur, kalau tidak ia senang.</summary>
        private void OnPetted(object sender, MouseButtonEventArgs e)
        {
            if (_active != null) return;
            if (_sleeping)
            {
                PetCompanionService.Instance.TriggerPoke();
                _sleeping = false;
                Refresh();
            }
            else ShowAlert(MascotState.Happy);
        }

        // ───────────────────────── Logika peringatan ─────────────────────────
        private static bool IsAlert(MascotState s) => s >= MascotState.Happy;

        private static int Priority(MascotState s) => s switch
        {
            MascotState.Alarm or MascotState.Adzan => 3,
            MascotState.Timer or MascotState.Drink => 2,
            MascotState.Happy => 0,
            _ => 1
        };

        private TimeSpan DefaultDuration(MascotState s) => s switch
        {
            MascotState.Happy => HappyDuration,
            MascotState.Notification => TimeSpan.FromSeconds(3.5),
            MascotState.Alarm => TimeSpan.FromSeconds(10),
            MascotState.Drink => TimeSpan.FromSeconds(6),
            MascotState.Timer => TimeSpan.FromSeconds(8),
            MascotState.Adzan => TimeSpan.FromSeconds(15),
            MascotState.Bluetooth => TimeSpan.FromSeconds(3.5),
            MascotState.Copied => TimeSpan.FromSeconds(1.8),
            MascotState.Screenshot => TimeSpan.FromSeconds(1.8),
            MascotState.Welcome => TimeSpan.FromSeconds(3.4),
            _ => TimeSpan.FromSeconds(3)
        };

        private void Begin(AlertRequest req)
        {
            _active = req;
            Arm(req.Duration);
            Refresh();
        }

        private void Arm(TimeSpan duration)
        {
            _alertTimer.Stop();
            if (duration == Timeout.InfiniteTimeSpan || duration <= TimeSpan.Zero) return;
            _alertTimer.Interval = duration;
            _alertTimer.Start();
        }

        private void EndAlert()
        {
            _alertTimer.Stop();
            var finished = _active;
            _active = null;

            if (_queue.Count > 0)
            {
                var next = _queue[0];
                _queue.RemoveAt(0);
                Begin(next);
            }
            else
            {
                Refresh();
            }

            if (finished != null) AlertFinished?.Invoke(this, finished.Kind);
        }

        private void RestartIdleClock()
        {
            _idleTimer.Stop();
        }

        private void OnIdleTick(object? sender, EventArgs e)
        {
            _idleTimer.Stop();
        }

        // ───────────────────────── Penerapan wujud ─────────────────────────
        private void Refresh(bool force = false)
        {
            if (!IsVisible)
            {
                ResetBody();
                StopFx();
                ClearBadge();
                return;
            }

            var state = CurrentState;
            UpdateHeadphone(state);

            if (_sleeping)
            {
                _pupilTimer.Stop();
                _blinkTimer.Stop();
            }
            else
            {
                if (!_pupilTimer.IsEnabled) _pupilTimer.Start();
                if (!_blinkTimer.IsEnabled) ScheduleBlink();
            }

            if (!force && state == _shown) return;
            _shown = state;

            ResetBody();
            StopFx();
            ClearBadge();
            _pupilJitter = 0;

            switch (state)
            {
                case MascotState.Idle: ApplyIdle(); break;
                case MascotState.Music: ApplyMusic(); break;
                case MascotState.Sleep: ApplySleep(); break;
                case MascotState.Happy: ApplyHappy(); break;
                case MascotState.Notification: ApplyNotification(); break;
                case MascotState.Alarm: ApplyAlarm(); break;
                case MascotState.Drink: ApplyDrink(); break;
                case MascotState.Timer: ApplyTimer(); break;
                case MascotState.Adzan: ApplyAdzan(); break;
                case MascotState.Bluetooth: ApplyBluetooth(); break;
                case MascotState.Copied: ApplyCopied(); break;
                case MascotState.Screenshot: ApplyScreenshot(); break;
                case MascotState.Welcome: ApplyWelcome(); break;
            }

            RestartIdleClock();
        }

        private void UpdateHeadphone(MascotState state)
        {
            bool want = (_music || state == MascotState.Bluetooth) && state != MascotState.Adzan;
            if (want == _headphoneOn) return;
            _headphoneOn = want;

            foreach (var p in new[] { SX, SY })
            {
                HeadphoneScale.BeginAnimation(p, new DoubleAnimation(want ? 1 : 0, TimeSpan.FromSeconds(want ? 0.45 : 0.22))
                {
                    EasingFunction = want ? PopOut : Smooth
                });
            }
        }

        // ── Keadaan dasar ──
        private void ApplyIdle()
        {
            SetEyes(EyeSet.Open);
            LookCursor();
            if (PetCompanionService.Instance.IsNotchExpanded)
            {
                Run(BodyShift, TY, Seq(true, 0, (1.2, -1.6), (2.4, 0)));
                Run(BodyTilt, RA, Seq(true, 0, (0.6, 1.5), (1.8, -1.5), (2.4, 0)));
                Run(BodyScale, SY, Seq(true, 0, (1.2, 1.025), (2.4, 1)));
                Run(BodyScale, SX, Seq(true, 0, (1.2, 0.99), (2.4, 1)));
            }
            else
            {
                Reset(BodyScale, SY, 1);
                Reset(BodyScale, SX, 1);
            }
        }

        private void ApplyMusic()
        {
            SetEyes(EyeSet.Open);
            LookCursor();
            Run(BodyShift, TY, Seq(true, 0, (0.1, 0), (0.34, -2.6), (0.68, 0)));
            Run(BodyTilt, RA, Seq(true, 0, (0.17, 3.5), (0.51, -3.5), (0.68, 0)));
            Run(BodyScale, SY, Seq(true, 0, (0.1, 0.93), (0.34, 1.045), (0.68, 1)));
            Run(BodyScale, SX, Seq(true, 0, (0.1, 1.07), (0.34, 0.98), (0.68, 1)));
            Float(NoteA, NoteATranslate, 0, 1.8, 5, 0.3);
            Float(NoteB, NoteBTranslate, 0.9, 1.8, 5, -0.3);
        }

        private void ApplySleep()
        {
            SetEyes(EyeSet.Sleep);
            LookAt(0, 0);
            if (PetCompanionService.Instance.IsNotchExpanded)
            {
                Float(ZA, ZATranslate, 0, 2.4, 5, 1.5);
                Float(ZB, ZBTranslate, 0.8, 2.4, 5, 1.5);
                Float(ZC, ZCTranslate, 1.6, 2.4, 5, 1.5);
            }
            else
            {
                StopFx();
            }
        }

        // ── Reaksi & peringatan ──
        private void ApplyHappy()
        {
            SetEyes(EyeSet.Happy);
            Blush(0.8);
            Run(BodyShift, TY, Seq(true, 0, (0.2, -3), (0.5, 0)));
            Run(BodyScale, SY, Seq(true, 0, (0.2, 1.08), (0.45, 0.92), (0.5, 1)));
            Run(BodyScale, SX, Seq(true, 0, (0.2, 0.94), (0.45, 1.08), (0.5, 1)));
            Run(BodyTilt, RA, Seq(true, 0, (0.25, 5), (0.5, 0), (0.75, -5), (1.0, 0)));
            Twinkle(Spark1, Spark1Scale, 0);
            Twinkle(Spark2, Spark2Scale, 0.45);
        }

        private void ApplyNotification()
        {
            SetEyes(EyeSet.Open);
            Wide(1.12, 1.2);
            LookAt(1.4, -1.6);
            ShowBadge(IconBell);
            Run(BellRotate, RA, Seq(true, 0, (0.08, -22), (0.2, 22), (0.32, -16), (0.44, 16), (0.54, -8), (0.62, 0), (1.6, 0)));
            Run(BodyShift, TY, Seq(true, 0, (0.12, -2.5), (0.28, 0), (1.6, 0)));
        }

        private void ApplyAlarm()
        {
            SetEyes(EyeSet.Open);
            Wide(1.2, 1.3);
            LookAt(0, 0);
            _pupilJitter = 0.7;
            ShowBadge(IconAlarm);
            Run(AlarmRotate, RA, Shake(14, 10, 0.045, 0.9));
            Run(BodyShift, TX, Shake(1.1, 10, 0.045, 0.9));
            Run(BodyTilt, RA, Shake(5, 10, 0.045, 0.9));
            Run(BadgePulse, SX, Seq(true, 0, (0.1, 1.18), (0.3, 1), (0.9, 1)));
            Run(BadgePulse, SY, Seq(true, 0, (0.1, 1.18), (0.3, 1), (0.9, 1)));
        }

        private void ApplyDrink()
        {
            SetEyes(EyeSet.Open);
            Wide(1.05, 1.1);
            LookAt(1.4, -1.6);
            ShowBadge(IconDrink);
            Run(DropShift, TY, Seq(true, 0, (0.45, -0.9), (0.9, 0)));
            Run(BodyTilt, RA, Seq(true, 0, (0.8, 4), (1.6, 0), (2.4, -4), (3.2, 0)));
            Run(BodyScale, SY, Seq(true, 0, (0.15, 0.9), (0.35, 1.05), (0.5, 1), (1.6, 1)));
        }

        private void ApplyTimer()
        {
            SetEyes(EyeSet.Open);
            Wide(1.1, 1.2);
            LookAt(1.4, -1.6);
            ShowBadge(IconTimer);
            Run(TimerHandRotate, RA, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2))
            {
                RepeatBehavior = RepeatBehavior.Forever
            });
            Run(BadgePulse, SX, Seq(true, 0, (0.5, 1.07), (1.0, 1)));
            Run(BadgePulse, SY, Seq(true, 0, (0.5, 1.07), (1.0, 1)));
            Run(BodyTilt, RA, Seq(true, 0, (0.5, 3.5), (1.0, 0), (1.5, -3.5), (2.0, 0)));
        }

        private void ApplyAdzan()
        {
            SetEyes(EyeSet.Open);
            Wide(1.0, 0.55);
            LookAt(0, -1.2);
            ShowBadge(IconAdzan);
            Run(BadgePulse, SX, Seq(true, 0, (1.2, 1.08), (2.4, 1)));
            Run(BadgePulse, SY, Seq(true, 0, (1.2, 1.08), (2.4, 1)));
            Run(BadgeShift, TY, Seq(true, 0, (1.5, -0.6), (3.0, 0)));
            Run(AdzanStar, OP, Seq(true, 0, (0.6, 0.3), (1.2, 1)));
            Run(BodyTilt, RA, Seq(true, 0, (1.5, 2), (3.0, 0), (4.5, -2), (6.0, 0)));
            Run(BodyScale, SY, Seq(true, 0, (1.5, 1.03), (3.0, 1)));
        }

        private void ApplyBluetooth()
        {
            SetEyes(EyeSet.Happy);
            Blush(0.6);
            ShowBadge(IconBluetooth);
            Run(BtRing, OP, Seq(true, 0, (0.1, 0.9), (0.9, 0)));
            Run(BtRingScale, SX, Seq(true, 0, (0.9, 1.7)));
            Run(BtRingScale, SY, Seq(true, 0, (0.9, 1.7)));
            Run(BadgePulse, SX, Seq(true, 0, (0.2, 1.12), (0.5, 1), (0.9, 1)));
            Run(BadgePulse, SY, Seq(true, 0, (0.2, 1.12), (0.5, 1), (0.9, 1)));
            Run(BodyShift, TY, Seq(true, 0, (0.2, -2.5), (0.4, 0), (0.9, 0)));
        }

        private void ApplyCopied()
        {
            SetEyes(EyeSet.Happy);
            ShowBadge(IconCopied);
            Run(BodyTilt, RA, Seq(false, 0, (0.12, 7), (0.3, 0)));
            Run(BodyShift, TY, Seq(false, 0, (0.1, -1.5), (0.25, 0)));
        }

        private void ApplyScreenshot()
        {
            SetEyes(EyeSet.Open);
            LookCursor();
            ShowBadge(IconScreenshot);
            Run(FlashOverlay, OP, Seq(false, 0, (0.04, 0.95), (0.35, 0)));
            Run(BodyScale, SX, Seq(false, 0, (0.05, 1.12), (0.3, 1)));
            Run(BodyScale, SY, Seq(false, 0, (0.05, 1.12), (0.3, 1)));
            Run(EyeWide, SY, Seq(false, 0, (0.05, 0.1), (0.3, 0.1), (0.45, 1)));
        }

        /// <summary>
        /// Sapaan startup (±3 detik). Urutan (detik):
        ///   0.00  lompat masuk (squash &amp; stretch), mata membuka
        ///   0.25  lengan menyembul dari sisi telur dan terangkat
        ///   0.55  melambai 4x (-150° ↔ -105°), badan bergoyang ringan
        ///   0.50  mata berubah ^ ^ dan pipi merona
        ///   2.85  lengan turun dan memudar
        /// </summary>
        private void ApplyWelcome()
        {
            SetEyes(EyeSet.Open);
            LookAt(0, 0);

            Run(EyesOpen, OP, Seq(false, 0.5, (0.12, 0)));
            Run(EyesHappy, OP, Seq(false, 0.5, (0.12, 1)));
            Run(BlushL, OP, Seq(false, 0.5, (0.25, 0.8)));
            Run(BlushR, OP, Seq(false, 0.5, (0.25, 0.8)));

            Run(BodyShift, TY, Seq(false, 0, (0.14, -3.2), (0.32, 0), (0.42, -0.9), (0.52, 0)));
            Run(BodyScale, SY, Seq(false, 0, (0.1, 0.88), (0.25, 1.08), (0.42, 1)));
            Run(BodyScale, SX, Seq(false, 0, (0.1, 1.1), (0.25, 0.95), (0.42, 1)));
            Run(BodyTilt, RA, Seq(false, 0.3, (0.3, 3), (0.8, -3), (1.3, 3), (1.8, -3), (2.3, 3), (2.8, 0)));

            Reset(ArmRotate, RA, -60);
            Reset(ArmScale, SX, 0.3);
            Reset(ArmScale, SY, 0.3);
            Run(ArmWave, OP, Seq(false, 0.25, (0.1, 1), (2.9, 1), (3.1, 0)));
            Run(ArmScale, SX, Seq(false, 0.25, (0.22, 1.12), (0.38, 1)));
            Run(ArmScale, SY, Seq(false, 0.25, (0.22, 1.12), (0.38, 1)));
            Run(ArmRotate, RA, Seq(false, 0.25,
                (0.30, -135),
                (0.55, -150), (0.80, -105), (1.05, -150), (1.30, -105),
                (1.55, -150), (1.80, -105), (2.05, -150), (2.30, -105),
                (2.60, -135), (3.00, -60)));

            Twinkle(Spark1, Spark1Scale, 0.6);
            Twinkle(Spark2, Spark2Scale, 1.0);
        }

        // ───────────────────────── Potongan visual ─────────────────────────
        private void SetEyes(EyeSet set)
        {
            _eyesOpen = set == EyeSet.Open;
            Run(EyesOpen, OP, Go(set == EyeSet.Open ? 1 : 0, 0.15));
            Run(EyesHappy, OP, Go(set == EyeSet.Happy ? 1 : 0, 0.15));
            Run(EyesSleep, OP, Go(set == EyeSet.Sleep ? 1 : 0, 0.15));
        }

        private void Wide(double x, double y)
        {
            Run(EyeWide, SX, Go(x));
            Run(EyeWide, SY, Go(y));
        }

        private void Blush(double opacity)
        {
            Run(BlushL, OP, Go(opacity));
            Run(BlushR, OP, Go(opacity));
        }

        private void ResetBody()
        {
            Reset(BodyScale, SX, 1);
            Reset(BodyScale, SY, 1);
            Reset(BodyTilt, RA, 0);
            Reset(BodyShift, TX, 0);
            Reset(BodyShift, TY, 0);
            Reset(EyeWide, SX, 1);
            Reset(EyeWide, SY, 1);
            Reset(BlushL, OP, 0);
            Reset(BlushR, OP, 0);
            Reset(FlashOverlay, OP, 0);

            Reset(ArmWave, OP, 0);
            Reset(ArmRotate, RA, -60);
            Reset(ArmScale, SX, 1);
            Reset(ArmScale, SY, 1);
        }

        private void StopFx()
        {
            foreach (var el in new UIElement[] { NoteA, NoteB, ZA, ZB, ZC, Spark1, Spark2 })
                Reset(el, OP, 0);

            foreach (var t in new[] { NoteATranslate, NoteBTranslate, ZATranslate, ZBTranslate, ZCTranslate })
            {
                Reset(t, TX, 0);
                Reset(t, TY, 0);
            }

            foreach (var s in new[] { Spark1Scale, Spark2Scale })
            {
                Reset(s, SX, 0.3);
                Reset(s, SY, 0.3);
            }
        }

        private void ShowBadge(UIElement icon)
        {
            foreach (var i in _icons)
                i.Visibility = ReferenceEquals(i, icon) ? Visibility.Visible : Visibility.Collapsed;

            Reset(BadgeScale, SX, 0);
            Reset(BadgeScale, SY, 0);
            foreach (var p in new[] { SX, SY })
            {
                BadgeScale.BeginAnimation(p, new DoubleAnimation(1, TimeSpan.FromSeconds(0.38))
                {
                    EasingFunction = PopOut
                });
            }
        }

        private void ClearBadge()
        {
            Run(BadgeScale, SX, Go(0, 0.15));
            Run(BadgeScale, SY, Go(0, 0.15));

            Reset(BellRotate, RA, 0);
            Reset(AlarmRotate, RA, 0);
            Reset(TimerHandRotate, RA, 0);
            Reset(DropShift, TY, 0);
            Reset(BadgePulse, SX, 1);
            Reset(BadgePulse, SY, 1);
            Reset(BadgeShift, TY, 0);
            Reset(AdzanStar, OP, 1);
            Reset(BtRing, OP, 0);
            Reset(BtRingScale, SX, 0.7);
            Reset(BtRingScale, SY, 0.7);
        }

        private static void Float(UIElement el, TranslateTransform t, double delay, double cycle, double rise, double drift)
        {
            Run(el, OP, Seq(true, delay, (cycle * 0.15, 1), (cycle * 0.65, 1), (cycle, 0)));
            Run(t, TY, Seq(true, delay, (cycle, -rise)));
            Run(t, TX, Seq(true, delay, (cycle, drift)));
        }

        private static void Twinkle(UIElement el, ScaleTransform s, double delay)
        {
            Run(el, OP, Seq(true, delay, (0.25, 1), (0.7, 0), (1.0, 0)));
            Run(s, SX, Seq(true, delay, (0.25, 1.3), (0.7, 0.3), (1.0, 0.3)));
            Run(s, SY, Seq(true, delay, (0.25, 1.3), (0.7, 0.3), (1.0, 0.3)));
        }

        // ───────────────────────── Pupil mengikuti kursor + kedip ─────────────────────────
        private void LookCursor() => _pupilMode = PupilMode.Cursor;

        private void LookAt(double x, double y)
        {
            _pupilMode = PupilMode.Fixed;
            _pupilFixed = new Point(x, y);
        }

        private void OnPupilTick(object? sender, EventArgs e)
        {
            if (_sleeping) return;

            Point target = _pupilFixed;
            if (_pupilMode == PupilMode.Cursor)
                target = TryGetCursorLocal(out var c) ? Gaze(c) : new Point(0, 0);

            double diffX = target.X - _pupilX;
            double diffY = target.Y - _pupilY;

            if (_pupilJitter <= 0 && Math.Abs(diffX) < 0.005 && Math.Abs(diffY) < 0.005)
                return;

            _pupilX += diffX * 0.25;
            _pupilY += diffY * 0.25;

            double jx = 0, jy = 0;
            if (_pupilJitter > 0)
            {
                jx = (_rng.NextDouble() - 0.5) * 2 * _pupilJitter;
                jy = (_rng.NextDouble() - 0.5) * 2 * _pupilJitter;
            }

            PupilTranslate.X = _pupilX + jx;
            PupilTranslate.Y = _pupilY + jy;
        }

        /// <summary>Arah kursor (koordinat kontrol) → pergeseran pupil. Pusat mata ≈ (14, 9.3).</summary>
        private static Point Gaze(Point local)
        {
            double dx = local.X - 14, dy = local.Y - 9.3;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 0.5) return new Point(0, 0);
            double k = Math.Min(1, d / 60);
            return new Point(dx / d * k * 1.4, dy / d * k * 1.8);
        }

        private bool TryGetCursorLocal(out Point local)
        {
            local = default;
            try
            {
                if (!GetCursorPos(out var p)) return false;
                if (_hasLocalPos && p.X == _lastNativePos.X && p.Y == _lastNativePos.Y)
                {
                    local = _lastLocalPos;
                    return true;
                }
                if (PresentationSource.FromVisual(this) == null) return false;
                local = PointFromScreen(new Point(p.X, p.Y));
                _lastNativePos = p;
                _lastLocalPos = local;
                _hasLocalPos = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ScheduleBlink()
        {
            if (_sleeping) return;
            _blinkTimer.Interval = TimeSpan.FromSeconds(2 + _rng.NextDouble() * 3.5);
            _blinkTimer.Start();
        }

        private void OnBlinkTick(object? sender, EventArgs e)
        {
            _blinkTimer.Stop();
            if (_sleeping) return;
            if (_eyesOpen && _shown != MascotState.Screenshot)
            {
                var blink = _rng.NextDouble() < 0.2
                    ? Seq(false, 0, (0.07, 0.08), (0.15, 1), (0.22, 0.08), (0.3, 1))
                    : Seq(false, 0, (0.07, 0.08), (0.17, 1));
                EyeBlink.BeginAnimation(SY, blink);
            }
            ScheduleBlink();
        }

        // ───────────────────────── Helper animasi ─────────────────────────
        private static void Run(IAnimatable target, DependencyProperty prop, AnimationTimeline? anim) =>
            target.BeginAnimation(prop, anim);

        private static void Reset(DependencyObject target, DependencyProperty prop, double value)
        {
            ((IAnimatable)target).BeginAnimation(prop, null);
            target.SetValue(prop, value);
        }

        private static DoubleAnimation Go(double to, double seconds = 0.2) =>
            new(to, TimeSpan.FromSeconds(seconds)) { EasingFunction = Smooth };

        private static DoubleAnimationUsingKeyFrames Seq(bool loop, double delay, params (double t, double v)[] frames)
        {
            var a = new DoubleAnimationUsingKeyFrames();
            foreach (var (t, v) in frames)
                a.KeyFrames.Add(new EasingDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(t)), Smooth));
            if (delay > 0) a.BeginTime = TimeSpan.FromSeconds(delay);
            if (loop) a.RepeatBehavior = RepeatBehavior.Forever;
            return a;
        }

        private static DoubleAnimationUsingKeyFrames Shake(double amp, int steps, double step, double period)
        {
            var f = new List<(double t, double v)>();
            double t = 0;
            for (int i = 0; i < steps; i++)
            {
                t += step;
                f.Add((t, i % 2 == 0 ? -amp : amp));
            }
            f.Add((t + step, 0));
            f.Add((period, 0));
            return Seq(true, 0, f.ToArray());
        }

        // ───────────────────────── Posisi kursor global (Win32) ─────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);
    }
}
