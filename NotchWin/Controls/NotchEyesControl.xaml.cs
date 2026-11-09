using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.Controls
{
    /// <summary>Jenis notifikasi clipboard.</summary>
    public enum ClipboardKind
    {
        /// <summary>Teks disalin (ikon clipboard + centang).</summary>
        Text,
        /// <summary>Tangkapan layar diambil (ikon kamera + kilat).</summary>
        Screenshot
    }

    /// <summary>
    /// Notch dengan dua mata beranimasi (Notch Eyes Mascot).
    /// Menggunakan BeginAnimation langsung pada properti dependency transform.
    /// 
    /// Mendukung mode compact (IsCompact="True") untuk slot maskot standar notch (28x20),
    /// serta mode native (236x45).
    /// </summary>
    public partial class NotchEyesControl : UserControl
    {
        // ---------- Dependency Properties ----------
        public static readonly DependencyProperty IsCompactProperty =
            DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(NotchEyesControl),
                new PropertyMetadata(false, OnIsCompactChanged));

        public bool IsCompact
        {
            get => (bool)GetValue(IsCompactProperty);
            set => SetValue(IsCompactProperty, value);
        }

        private static void OnIsCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotchEyesControl control)
            {
                control.ApplyCompactMode();
            }
        }

        // ---------- Palet aksen ----------
        private static readonly Color Cream = Color.FromRgb(0xF5, 0xF5, 0xF7);
        private static readonly Color Amber = Color.FromRgb(0xFF, 0xB5, 0x47); // alarm
        private static readonly Color Mint = Color.FromRgb(0x5E, 0xE0, 0xB0);  // salat
        private static readonly Color Blue = Color.FromRgb(0x7A, 0xA7, 0xFF);  // pesan, sleep
        private static readonly Color Aqua = Color.FromRgb(0x5E, 0xC8, 0xFF);  // minum
        private static readonly Color Yellow = Color.FromRgb(0xFF, 0xD1, 0x66); // clipboard
        private static readonly Color Lime = Color.FromRgb(0xC3, 0xF0, 0x60);  // musik

        private const double NativePillClosed = 210, NativePillOpen = 236, NativeIconBoxOpen = 36;
        private const double CompactPillClosed = 34, CompactPillOpen = 58, CompactIconBoxOpen = 24;

        private double _pillClosed = NativePillClosed;
        private double _pillOpen = NativePillOpen;
        private double _iconBoxOpen = NativeIconBoxOpen;

        private enum Kind { Idle, Sleep, Waking, Music, MusicExit, Welcome, Happy, Alarm, AlarmExit, Prayer, Message, Drink, DrinkDone, Clipboard }

        // ---------- Easing & keyframe ----------
        private enum Ease { Linear, CubicInOut, CubicIn, CubicOut, SineInOut, BackOut, QuadIn, QuadOut }

        private readonly record struct Key(double T, double V, Ease E);

        private static Key K(double t, double v, Ease e = Ease.CubicInOut) => new(t, v, e);

        private static IEasingFunction? Fn(Ease e) => e switch
        {
            Ease.Linear => null,
            Ease.CubicInOut => new CubicEase { EasingMode = EasingMode.EaseInOut },
            Ease.CubicIn => new CubicEase { EasingMode = EasingMode.EaseIn },
            Ease.CubicOut => new CubicEase { EasingMode = EasingMode.EaseOut },
            Ease.SineInOut => new SineEase { EasingMode = EasingMode.EaseInOut },
            Ease.BackOut => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 },
            Ease.QuadIn => new QuadraticEase { EasingMode = EasingMode.EaseIn },
            Ease.QuadOut => new QuadraticEase { EasingMode = EasingMode.EaseOut },
            _ => null
        };

        // ---------- Status ----------
        private SolidColorBrush _accent;
        private readonly List<(IAnimatable Target, DependencyProperty Prop)> _active = new();
        private readonly DispatcherTimer _endTimer = new();
        private readonly DispatcherTimer _sleepTimer = new() { Interval = TimeSpan.FromMinutes(5) };
        private readonly UIElement[] _icons;
        private readonly UIElement[] _pills;
        private readonly UIElement[] _happy;
        private readonly UIElement[] _calm;
        private Action? _onEnd;
        private Kind _kind = Kind.Idle;
        private int _priority = -1;     // prioritas notifikasi yang sedang tampil, -1 = tidak ada
        private bool _musicOn;
        private bool _asleep;

        /// <summary>Tempo musik untuk animasi mengangguk. Default 120 BPM (1 ketukan = 500 ms).</summary>
        public double Bpm { get; set; } = 120;

        /// <summary>Lama tanpa aktivitas sebelum notch tidur. Default 5 menit.</summary>
        public TimeSpan SleepAfter
        {
            get => _sleepTimer.Interval;
            set { _sleepTimer.Interval = value; if (_sleepTimer.IsEnabled) { _sleepTimer.Stop(); _sleepTimer.Start(); } }
        }

        public NotchEyesControl()
        {
            InitializeComponent();

            // Gunakan brush mandiri yang tidak pernah di-freeze agar mutasi warna per-state selalu aman
            _accent = new SolidColorBrush(Cream);
            Resources["AccentBrush"] = _accent;

            _icons = new UIElement[] { IcoHand, IcoAlarm, IcoMoon, IcoMsg, IcoDrop, IcoClip, IcoCamera, IcoEq };
            _pills = new UIElement[] { LPill, RPill };
            _happy = new UIElement[] { LHappy, RHappy };
            _calm = new UIElement[] { LCalm, RCalm };

            _endTimer.Tick += (_, _) =>
            {
                _endTimer.Stop();
                var action = _onEnd;
                _onEnd = null;
                action?.Invoke();
            };
            _sleepTimer.Tick += (_, _) =>
            {
                if (_priority < 0 && !_musicOn && _kind == Kind.Idle)
                {
                    _asleep = true;
                    EnterBaseline();
                }
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void SubscribeServiceEvents()
        {
            PetCompanionService.Instance.MascotAlertTriggered -= OnServiceMascotAlert;
            PetCompanionService.Instance.MascotAlertTriggered += OnServiceMascotAlert;
            PetCompanionService.Instance.MascotAlertCleared -= OnServiceMascotAlertCleared;
            PetCompanionService.Instance.MascotAlertCleared += OnServiceMascotAlertCleared;
            PetCompanionService.Instance.MusicStateChanged -= OnServiceMusicChanged;
            PetCompanionService.Instance.MusicStateChanged += OnServiceMusicChanged;
            PetCompanionService.Instance.StateChanged -= OnServiceStateChanged;
            PetCompanionService.Instance.StateChanged += OnServiceStateChanged;
        }

        private void UnsubscribeServiceEvents()
        {
            PetCompanionService.Instance.MascotAlertTriggered -= OnServiceMascotAlert;
            PetCompanionService.Instance.MascotAlertCleared -= OnServiceMascotAlertCleared;
            PetCompanionService.Instance.MusicStateChanged -= OnServiceMusicChanged;
            PetCompanionService.Instance.StateChanged -= OnServiceStateChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyCompactMode();
            SubscribeServiceEvents();

            _musicOn = PetCompanionService.Instance.IsMusicPlaying;
            _asleep = PetCompanionService.Instance.State == PetState.Sleeping;

            if (PetCompanionService.Instance.CurrentAlert.HasValue)
            {
                ShowAlert(PetCompanionService.Instance.CurrentAlert.Value, PetCompanionService.Instance.CurrentAlertDuration);
            }
            else
            {
                EnterBaseline();
            }
            if (!_sleepTimer.IsEnabled) _sleepTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeServiceEvents();
            StopAll();
            _sleepTimer.Stop();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                SubscribeServiceEvents();
                ApplyCompactMode();
                _musicOn = PetCompanionService.Instance.IsMusicPlaying;
                _asleep = PetCompanionService.Instance.State == PetState.Sleeping;

                if (PetCompanionService.Instance.CurrentAlert.HasValue)
                {
                    ShowAlert(PetCompanionService.Instance.CurrentAlert.Value, PetCompanionService.Instance.CurrentAlertDuration);
                }
                else
                {
                    EnterBaseline();
                }
                if (!_sleepTimer.IsEnabled) _sleepTimer.Start();
            }
            else
            {
                StopAll();
                _sleepTimer.Stop();
                UnsubscribeServiceEvents();
            }
        }

        private void OnServiceMascotAlert(object? sender, MascotAlertEventArgs e)
        {
            Dispatcher.InvokeAsync(() => ShowAlert(e.State, e.Duration));
        }

        private void OnServiceMascotAlertCleared(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(DismissAlert);
        }

        private void OnServiceMusicChanged(object? sender, bool isPlaying)
        {
            Dispatcher.InvokeAsync(() => SetMusicPlaying(isPlaying));
        }

        private void OnServiceStateChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                bool shouldSleep = PetCompanionService.Instance.State == PetState.Sleeping;
                if (shouldSleep && !_asleep)
                {
                    GoToSleep();
                }
                else if (!shouldSleep && _asleep)
                {
                    WakeUp();
                }
            });
        }

        private void ApplyCompactMode()
        {
            if (IsCompact)
            {
                _pillClosed = CompactPillClosed;
                _pillOpen = CompactPillOpen;
                _iconBoxOpen = CompactIconBoxOpen;
                MainGrid.Width = (_priority >= 0 || _kind == Kind.Music) ? CompactPillOpen : CompactPillClosed;
                MainGrid.Height = 26;
                Pill.Height = 26;
                Pill.Background = Brushes.Transparent;
                Pill.CornerRadius = new CornerRadius(0, 0, 13, 13);
                FlashRing.CornerRadius = new CornerRadius(0, 0, 13, 13);
                Canvas.SetLeft(Z1, 24);
                Canvas.SetTop(Z1, 1);
                Canvas.SetLeft(Z2, 30);
                Canvas.SetTop(Z2, -4);
            }
            else
            {
                _pillClosed = NativePillClosed;
                _pillOpen = NativePillOpen;
                _iconBoxOpen = NativeIconBoxOpen;
                MainGrid.Width = NativePillOpen;
                MainGrid.Height = 45;
                Pill.Height = 45;
                Pill.Background = Brushes.Black;
                Pill.CornerRadius = new CornerRadius(0, 0, 25, 25);
                FlashRing.CornerRadius = new CornerRadius(0, 0, 25, 25);
                Canvas.SetLeft(Z1, 140);
                Canvas.SetTop(Z1, 8);
                Canvas.SetLeft(Z2, 152);
                Canvas.SetTop(Z2, 2);
            }
            Pill.Width = (_priority >= 0 || _kind == Kind.Music) ? _pillOpen : _pillClosed;
        }

        // =====================================================================================
        //  API PUBLIK
        // =====================================================================================

        /// <summary>
        /// Tangani reaksi/peringatan berdasarkan MascotState standar aplikasi NotchWin.
        /// </summary>
        public void ShowAlert(MascotState state, TimeSpan? duration = null)
        {
            switch (state)
            {
                case MascotState.Welcome:
                    PlayWelcome();
                    break;
                case MascotState.Alarm:
                case MascotState.Timer:
                    ShowAlarm();
                    break;
                case MascotState.Adzan:
                    ShowPrayer();
                    break;
                case MascotState.Notification:
                case MascotState.Bluetooth:
                    ShowMessage();
                    break;
                case MascotState.Drink:
                    ShowDrink();
                    break;
                case MascotState.Copied:
                    ShowClipboard(ClipboardKind.Text);
                    break;
                case MascotState.Screenshot:
                    ShowClipboard(ClipboardKind.Screenshot);
                    break;
                case MascotState.Happy:
                    ShowHappy(duration);
                    break;
                case MascotState.Music:
                    SetMusicPlaying(true);
                    break;
                case MascotState.Sleep:
                    GoToSleep();
                    break;
                case MascotState.Idle:
                    WakeUp();
                    break;
            }
        }

        /// <summary>Akhiri peringatan aktif.</summary>
        public void DismissAlert()
        {
            if (_kind == Kind.Alarm)
            {
                DismissAlarm();
            }
            else if (_kind == Kind.Drink)
            {
                ConfirmDrink();
            }
            else if (_priority >= 0)
            {
                StopAll();
                Finish();
            }
        }

        /// <summary>Bersihkan semua peringatan dan kembali ke keadaan dasar.</summary>
        public void ClearAlerts()
        {
            StopAll();
            Finish();
        }

        /// <summary>Reaksi senang (mata tersenyum melengkung ^ ^, lompatan riang).</summary>
        public void ShowHappy(TimeSpan? duration = null)
        {
            if (!Acquire(Kind.Happy, 50)) return;
            double total = (duration ?? TimeSpan.FromSeconds(2.0)).TotalSeconds;
            SetAccent(Cream);
            ShowIcon(null);

            Both(ScaleTransform.ScaleYProperty, false, 0,
                K(0, 1), K(0.12, 1.25, Ease.BackOut), K(0.35, 1));
            Fade(_pills, K(0, 1), K(0.1, 0, Ease.Linear), K(total - 0.25, 0), K(total, 1));
            Fade(_happy, K(0, 0), K(0.1, 1, Ease.Linear), K(total - 0.25, 1), K(total, 0));
            Track(EyesTx, TranslateTransform.YProperty, false, 0,
                K(0, 0), K(0.18, -3, Ease.QuadOut), K(0.36, 0, Ease.QuadIn), K(total, 0));

            Schedule(total, Finish);
        }

        /// <summary>Sapaan saat aplikasi dibuka (sekali, 2,4 detik): mata melek, senang, tangan melambai.</summary>
        public void PlayWelcome()
        {
            if (!Acquire(Kind.Welcome, 90)) return;
            const double total = 2.4;
            SetAccent(Cream);
            ShowIcon(IcoHand);

            Both(ScaleTransform.ScaleYProperty, false, 0,
                K(0, 0.18), K(0.1, 0.18), K(0.3, 1.4, Ease.BackOut), K(0.55, 1.4), K(0.7, 1), K(total, 1));
            Fade(_pills, K(0, 1), K(0.55, 1), K(0.7, 0, Ease.Linear), K(2.0, 0), K(total, 1));
            Fade(_happy, K(0, 0), K(0.55, 0), K(0.7, 1, Ease.Linear), K(2.0, 1), K(total, 0));
            Track(EyesTx, TranslateTransform.YProperty, false, 0,
                K(0, 0), K(0.7, 0), K(0.82, -3, Ease.QuadOut), K(0.95, 0, Ease.QuadIn), K(total, 0));

            PillTracks(open: 1.0, dur: 0.22, close: 2.0, closeDur: 0.4);
            // tangan bergoyang 3 kali, tiap ayunan 250 ms
            Track(IconRot, RotateTransform.AngleProperty, false, 0,
                K(0, 0), K(1.0, -15, Ease.Linear), K(1.2, -15), K(1.45, 15, Ease.SineInOut), K(1.7, -15, Ease.SineInOut),
                K(1.95, 15, Ease.SineInOut), K(2.2, 0, Ease.SineInOut), K(total, 0));
            Schedule(total, Finish);
        }

        /// <summary>Alarm berbunyi. Getar berulang sampai <see cref="DismissAlarm"/> dipanggil.</summary>
        public void ShowAlarm()
        {
            if (!Acquire(Kind.Alarm, 100)) return;
            SetAccent(Amber);
            ShowIcon(IcoAlarm);

            // terkejut
            Both(ScaleTransform.ScaleYProperty, false, 0, K(0, 1), K(0.12, 1.5, Ease.BackOut), K(0.4, 1.15));
            PillTracks(open: 0.2, dur: 0.22);

            // getar berulang 600 ms, mulai setelah pill terbuka
            const double b = 0.34;
            Track(PillTx, TranslateTransform.XProperty, true, b,
                K(0, 0), K(0.15, -4, Ease.SineInOut), K(0.45, 4, Ease.SineInOut), K(0.6, 0, Ease.SineInOut));
            Track(PillRot, RotateTransform.AngleProperty, true, b,
                K(0, 0), K(0.15, -1.5, Ease.SineInOut), K(0.45, 1.5, Ease.SineInOut), K(0.6, 0, Ease.SineInOut));
            Track(IconRot, RotateTransform.AngleProperty, true, b,
                K(0, 0), K(0.15, -14, Ease.SineInOut), K(0.45, 14, Ease.SineInOut), K(0.6, 0, Ease.SineInOut));
        }

        /// <summary>Alarm dimatikan atau ditunda: mata senang, pill menyusut.</summary>
        public void DismissAlarm()
        {
            if (_kind != Kind.Alarm) return;
            StopAll();
            _kind = Kind.AlarmExit;
            const double total = 0.8;
            Fade(_pills, K(0, 0), K(0.5, 0), K(0.75, 1), K(total, 1));
            Fade(_happy, K(0, 1), K(0.5, 1), K(0.75, 0), K(total, 0));
            PillTracks(open: 0, dur: 0, close: 0.5, closeDur: 0.25, startOpen: true);
            Schedule(total, Finish);
        }

        /// <summary>Pengingat salat (15 detik): mata menutup lembut ‿ ‿, ikon bulan sabit bernapas.</summary>
        public void ShowPrayer()
        {
            if (!Acquire(Kind.Prayer, 80)) return;
            const double total = 16.0;
            SetAccent(Mint);
            if (Glow != null && !Glow.IsFrozen) Glow.Color = Mint;
            ShowIcon(IcoMoon);

            Fade(_pills, K(0, 1), K(0.3, 1), K(0.6, 0), K(15.4, 0), K(total, 1));
            Fade(_calm, K(0, 0), K(0.3, 0), K(0.6, 1), K(15.4, 1), K(total, 0));
            PillTracks(open: 0.45, dur: 0.25, close: 15.7, closeDur: 0.3);

            // napas 3 detik, mulai setelah pill terbuka (0,7 s)
            const double b = 0.7;
            Track(IconBreath, ScaleTransform.ScaleXProperty, true, b, K(0, 1), K(1.5, 1.15, Ease.SineInOut), K(3, 1, Ease.SineInOut));
            Track(IconBreath, ScaleTransform.ScaleYProperty, true, b, K(0, 1), K(1.5, 1.15, Ease.SineInOut), K(3, 1, Ease.SineInOut));
            Track(Glow, DropShadowEffect.OpacityProperty, true, b, K(0, 0.2), K(1.5, 0.6, Ease.SineInOut), K(3, 0.2, Ease.SineInOut));
            Track(EyesTx, TranslateTransform.YProperty, true, b, K(0, 0), K(1.5, 1, Ease.SineInOut), K(3, 0, Ease.SineInOut));
            Schedule(total, Finish);
        }

        /// <summary>Pesan masuk (4 detik). Jika sudah tampil, hanya badge yang berdenyut.</summary>
        public void ShowMessage()
        {
            if (_kind == Kind.Message)
            {
                Poke();
                PulseIcon();
                return;
            }
            if (!Acquire(Kind.Message, 60)) return;
            const double total = 4.2;
            SetAccent(Blue);
            ShowIcon(IcoMsg);

            PillTracks(open: 0, dur: 0.22, close: 3.95, closeDur: 0.25);
            // melirik ke ikon
            Track(EyesTx, TranslateTransform.XProperty, false, 0,
                K(0, 0), K(0.25, 0), K(0.5, 3), K(3.95, 3), K(total, 0));
            // lompat kecil
            Track(EyesTx, TranslateTransform.YProperty, false, 0,
                K(0, 0), K(0.62, 0), K(0.8, -3, Ease.QuadOut), K(1.0, 0, Ease.QuadIn), K(total, 0));
            Both(ScaleTransform.ScaleYProperty, false, 0,
                K(0, 1), K(0.2, 1.06, Ease.CubicOut), K(0.62, 1.06), K(0.8, 1.19, Ease.QuadOut), K(1.0, 1.06, Ease.QuadIn),
                K(3.95, 1.06), K(total, 1));
            Schedule(total, Finish);
        }

        /// <summary>Pengingat minum (8 detik): tetes air muncul, mata mengikuti tetes yang bergoyang.</summary>
        public void ShowDrink()
        {
            if (!Acquire(Kind.Drink, 60)) return;
            const double total = 8.0;
            SetAccent(Aqua);
            ShowIcon(IcoDrop);

            PillTracks(open: 0, dur: 0.22, close: 7.75, closeDur: 0.25);
            // goyang tetes 1,2 detik; mata ikut turun-naik
            Track(IconShift, TranslateTransform.YProperty, true, 0.5, K(0, 0), K(0.6, 2, Ease.SineInOut), K(1.2, 0, Ease.SineInOut));
            Track(EyesTx, TranslateTransform.YProperty, true, 0.5, K(0, 0), K(0.6, 2, Ease.SineInOut), K(1.2, 0, Ease.SineInOut));
            Schedule(total, Finish);
        }

        /// <summary>Pengguna mengetuk "sudah minum": mata senang, tetes terisi penuh, lalu menutup.</summary>
        public void ConfirmDrink()
        {
            if (_kind != Kind.Drink) return;
            StopAll();
            _kind = Kind.DrinkDone;
            const double total = 1.45;
            Fade(_pills, K(0, 1), K(0.15, 0, Ease.Linear), K(1.2, 0), K(total, 1));
            Fade(_happy, K(0, 0), K(0.15, 1, Ease.Linear), K(1.2, 1), K(total, 0));
            Track(DropFill, UIElement.OpacityProperty, false, 0, K(0, 0), K(0.3, 1), K(1.2, 1), K(total, 0));
            PillTracks(open: 0, dur: 0, close: 1.2, closeDur: 0.25, startOpen: true);
            Schedule(total, Finish);
        }

        /// <summary>Notifikasi clipboard (3 detik): salin teks atau tangkapan layar.</summary>
        public void ShowClipboard(ClipboardKind kind)
        {
            if (!Acquire(Kind.Clipboard, 40)) return;
            const double total = 3.25;
            SetAccent(Yellow);

            PillTracks(open: 0, dur: 0.2, close: 3.0, closeDur: 0.25);

            if (kind == ClipboardKind.Text)
            {
                ShowIcon(IcoClip);
                // melirik ke ikon, lalu berkedip puas dan centang muncul
                Track(EyesTx, TranslateTransform.XProperty, false, 0, K(0, 0), K(0.2, 2.5), K(3.0, 2.5), K(total, 0));
                Both(ScaleTransform.ScaleYProperty, false, 0,
                    K(0, 1), K(0.5, 1), K(0.6, 0.18, Ease.CubicIn), K(0.7, 1.1, Ease.CubicOut), K(0.8, 1), K(total, 1));
                Track(ClipCheck, UIElement.OpacityProperty, false, 0,
                    K(0, 0), K(0.5, 0), K(0.6, 1, Ease.Linear), K(3.0, 1), K(total, 0));
            }
            else
            {
                ShowIcon(IcoCamera);
                if (Glow != null && !Glow.IsFrozen) Glow.Color = Colors.White;
                // rana kamera: mata menutup cepat, pill berkilat 120 ms
                Both(ScaleTransform.ScaleYProperty, false, 0,
                    K(0, 1), K(0.08, 0.18, Ease.CubicIn), K(0.2, 1.1, Ease.CubicOut), K(0.3, 1), K(total, 1));
                Track(FlashRing, UIElement.OpacityProperty, false, 0, K(0, 0), K(0.04, 0.8, Ease.Linear), K(0.16, 0, Ease.QuadOut), K(total, 0));
                Track(Glow, DropShadowEffect.OpacityProperty, false, 0, K(0, 0), K(0.04, 0.9, Ease.Linear), K(0.2, 0, Ease.QuadOut), K(total, 0));
            }
            Schedule(total, Finish);
        }

        /// <summary>Musik mulai/berhenti. Saat ada notifikasi tampil, state musik dilanjutkan sesudahnya.</summary>
        public void SetMusicPlaying(bool playing)
        {
            if (playing == _musicOn) return;
            _musicOn = playing;
            if (playing) Poke();
            if (_priority >= 0) return; // notifikasi sedang tampil; Finish() akan memilih state dasar yang benar

            if (playing) EnterMusic();
            else PlayMusicExit();
        }

        /// <summary>Paksa tidur (mis. layar dikunci). Diabaikan jika ada notifikasi atau musik.</summary>
        public void GoToSleep()
        {
            if (_priority >= 0 || _musicOn) return;
            _asleep = true;
            EnterBaseline();
        }

        /// <summary>Bangunkan notch (mis. mouse mendekat). Aman dipanggil kapan saja.</summary>
        public void WakeUp()
        {
            Poke();
            if (_kind != Kind.Sleep) return;
            StopAll();
            _kind = Kind.Waking;
            Both(ScaleTransform.ScaleYProperty, false, 0, K(0, 0.18), K(0.15, 1.25, Ease.BackOut), K(0.4, 1));
            Schedule(0.4, EnterBaseline);
        }

        // =====================================================================================
        //  STATE DASAR
        // =====================================================================================

        private void EnterBaseline()
        {
            StopAll();
            _priority = -1;
            ShowIcon(null);
            ApplyCompactMode();
            if (_musicOn) EnterMusic();
            else if (_asleep) EnterSleep();
            else EnterIdle();
        }

        /// <summary>Idle: kedip, melirik kiri-kanan, sedikit bernapas. Siklus 6 detik.</summary>
        private void EnterIdle()
        {
            _kind = Kind.Idle;
            double glance = IsCompact ? 2.5 : 6.0;
            Track(EyesTx, TranslateTransform.XProperty, true, 0,
                K(0, 0), K(1.5, 0), K(1.8, -glance), K(2.7, -glance), K(3.2, glance), K(3.9, glance), K(4.2, 0), K(6, 0));
            Both(ScaleTransform.ScaleYProperty, true, 0,
                K(0, 1), K(4.3, 1), K(4.38, 0.18, Ease.CubicIn), K(4.45, 1.15, Ease.CubicOut), K(4.6, 1), K(6, 1));
            Track(PillTx, TranslateTransform.YProperty, true, 0,
                K(0, 0), K(3, -1, Ease.SineInOut), K(6, 0, Ease.SineInOut));
        }

        /// <summary>Musik: mengangguk per ketukan, kiri-kanan bergantian, equalizer di kanan.</summary>
        private void EnterMusic()
        {
            _kind = Kind.Music;
            SetAccent(Lime);
            ShowIcon(IcoEq);
            PillTracks(open: 0, dur: 0.25); // tetap terbuka

            double t = 60.0 / Math.Clamp(Bpm, 40, 200); // durasi satu ketukan
            const double b = 0.25;                       // loop mulai setelah pill terbuka

            // mata kiri menyusut di ketukan, mata kanan setengah ketukan kemudian
            Track(LScale, ScaleTransform.ScaleYProperty, true, b, K(0, 1), K(t / 2, 0.7, Ease.SineInOut), K(t, 1, Ease.SineInOut));
            Track(RScale, ScaleTransform.ScaleYProperty, true, b + t / 2, K(0, 1), K(t / 2, 0.7, Ease.SineInOut), K(t, 1, Ease.SineInOut));
            Track(LShift, TranslateTransform.YProperty, true, b, K(0, 0), K(t / 2, 2, Ease.SineInOut), K(t, 0, Ease.SineInOut));
            Track(RShift, TranslateTransform.YProperty, true, b + t / 2, K(0, 0), K(t / 2, 2, Ease.SineInOut), K(t, 0, Ease.SineInOut));
            // pasangan mata miring bergantian dan naik-turun
            Track(EyesRot, RotateTransform.AngleProperty, true, b,
                K(0, 0), K(t * 0.5, -4, Ease.SineInOut), K(t * 1.5, 4, Ease.SineInOut), K(t * 2, 0, Ease.SineInOut));
            Track(EyesTx, TranslateTransform.YProperty, true, b, K(0, 0), K(t / 2, -2, Ease.SineInOut), K(t, 0, Ease.SineInOut));

            // equalizer: tiap bar punya periode berbeda supaya terlihat acak
            Bars(Bar1, b, 0.80, 0.35, 0.90, 0.50, 0.35);
            Bars(Bar2, b, 0.65, 0.70, 0.40, 1.00, 0.70);
            Bars(Bar3, b, 0.90, 0.45, 1.00, 0.35, 0.45);
            Bars(Bar4, b, 0.70, 0.80, 0.50, 0.95, 0.80);
        }

        private void Bars(ScaleTransform bar, double begin, double period, params double[] values)
        {
            var keys = new Key[values.Length];
            for (int i = 0; i < values.Length; i++)
                keys[i] = K(period * i / (values.Length - 1), values[i], Ease.SineInOut);
            Track(bar, ScaleTransform.ScaleYProperty, true, begin, keys);
        }

        private void PlayMusicExit()
        {
            StopAll();
            _kind = Kind.MusicExit;
            PillTracks(open: 0, dur: 0, close: 0, closeDur: 0.25, startOpen: true);
            Schedule(0.3, EnterBaseline);
        }

        /// <summary>Sleep: mata mengantuk lalu jadi garis tipis, pill bernapas, huruf Z melayang.</summary>
        private void EnterSleep()
        {
            _kind = Kind.Sleep;
            SetAccent(Blue);
            Both(ScaleTransform.ScaleYProperty, false, 0,
                K(0, 1), K(0.4, 0.7), K(0.9, 0.7), K(1.2, 0.38), K(1.5, 0.5), K(2.0, 0.18));
            Both(ScaleTransform.ScaleXProperty, false, 0, K(0, 1), K(1.2, 1), K(2.0, 1.2));
            Track(PillTx, TranslateTransform.YProperty, true, 2.0, K(0, 0), K(2, 1, Ease.SineInOut), K(4, 0, Ease.SineInOut));

            SleepZ(Z1, Z1Shift, 1.6);
            SleepZ(Z2, Z2Shift, 2.9);
        }

        private void SleepZ(UIElement z, TranslateTransform shift, double begin)
        {
            Track(z, UIElement.OpacityProperty, true, begin,
                K(0, 0), K(0.5, 1, Ease.Linear), K(2.5, 0.8, Ease.Linear), K(3.5, 0, Ease.Linear), K(4, 0, Ease.Linear));
            Track(shift, TranslateTransform.YProperty, true, begin, K(0, 4), K(3.5, -6, Ease.QuadOut), K(4, -6, Ease.Linear));
        }

        // =====================================================================================
        //  PENOLONG ANIMASI
        // =====================================================================================

        /// <summary>
        /// Pill melebar, slot ikon terbuka, ikon muncul dengan "pop" (0 ke 1,15 ke 1).
        /// open  = detik mulai membuka, dur = lama membuka.
        /// close = detik mulai menutup (-1 = tetap terbuka), closeDur = lama menutup.
        /// startOpen = mulai dari kondisi sudah terbuka (dipakai animasi keluar).
        /// </summary>
        private void PillTracks(double open, double dur, double close = -1, double closeDur = 0.25, bool startOpen = false)
        {
            var w = new List<Key>();
            var box = new List<Key>();
            var op = new List<Key>();
            var sc = new List<Key>();

            if (startOpen)
            {
                w.Add(K(0, _pillOpen));
                box.Add(K(0, _iconBoxOpen));
                op.Add(K(0, 1));
            }
            else
            {
                w.Add(K(0, _pillClosed));
                box.Add(K(0, 0));
                op.Add(K(0, 0));
                sc.Add(K(0, 0));
                if (open > 0)
                {
                    w.Add(K(open, _pillClosed));
                    box.Add(K(open, 0));
                    op.Add(K(open, 0));
                    sc.Add(K(open, 0));
                }
                w.Add(K(open + dur, _pillOpen));
                box.Add(K(open + dur, _iconBoxOpen));
                op.Add(K(open + Math.Min(0.1, dur), 1, Ease.Linear));
                sc.Add(K(open + dur * 0.6, 1.15, Ease.CubicOut));
                sc.Add(K(open + dur, 1));
            }

            if (close >= 0)
            {
                if (close > 0)
                {
                    w.Add(K(close, _pillOpen));
                    box.Add(K(close, _iconBoxOpen));
                    op.Add(K(close, 1));
                }
                w.Add(K(close + closeDur, _pillClosed));
                box.Add(K(close + closeDur, 0));
                op.Add(K(close + closeDur, 0));
            }

            Track(Pill, FrameworkElement.WidthProperty, false, 0, w.ToArray());
            Track(MainGrid, FrameworkElement.WidthProperty, false, 0, w.ToArray());
            Track(IconBox, FrameworkElement.WidthProperty, false, 0, box.ToArray());
            Track(IconInner, UIElement.OpacityProperty, false, 0, op.ToArray());
            if (sc.Count > 1)
            {
                Track(IconScale, ScaleTransform.ScaleXProperty, false, 0, sc.ToArray());
                Track(IconScale, ScaleTransform.ScaleYProperty, false, 0, sc.ToArray());
            }
        }

        private void PulseIcon()
        {
            Track(IconBreath, ScaleTransform.ScaleXProperty, false, 0, K(0, 1), K(0.12, 1.3, Ease.CubicOut), K(0.25, 1, Ease.CubicIn));
            Track(IconBreath, ScaleTransform.ScaleYProperty, false, 0, K(0, 1), K(0.12, 1.3, Ease.CubicOut), K(0.25, 1, Ease.CubicIn));
        }

        /// <summary>Animasi satu properti Double dengan keyframe. Waktu kunci dalam detik, harus menaik.</summary>
        private void Track(IAnimatable target, DependencyProperty prop, bool loop, double begin, params Key[] keys)
        {
            var anim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(Math.Max(keys[^1].T, 0.001)),
                BeginTime = TimeSpan.FromSeconds(begin)
            };
            if (loop) anim.RepeatBehavior = RepeatBehavior.Forever;
            foreach (var k in keys)
                anim.KeyFrames.Add(new EasingDoubleKeyFrame(k.V, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(k.T)), Fn(k.E)));

            target.BeginAnimation(prop, anim);
            _active.Add((target, prop));
        }

        /// <summary>Terapkan keyframe yang sama ke kedua mata (ScaleTransform kiri dan kanan).</summary>
        private void Both(DependencyProperty prop, bool loop, double begin, params Key[] keys)
        {
            Track(LScale, prop, loop, begin, keys);
            Track(RScale, prop, loop, begin, keys);
        }

        private void Fade(UIElement[] elements, params Key[] keys)
        {
            foreach (var e in elements) Track(e, UIElement.OpacityProperty, false, 0, keys);
        }

        private void StopAll()
        {
            foreach (var (target, prop) in _active) target.BeginAnimation(prop, null);
            _active.Clear();
            _endTimer.Stop();
            _onEnd = null;
        }

        private void Schedule(double seconds, Action action)
        {
            _endTimer.Stop();
            _endTimer.Interval = TimeSpan.FromSeconds(seconds);
            _onEnd = action;
            _endTimer.Start();
        }

        private void ShowIcon(UIElement? icon)
        {
            foreach (var i in _icons)
                i.Visibility = ReferenceEquals(i, icon) ? Visibility.Visible : Visibility.Collapsed;
            // tangan melambai berputar di pergelangan (bagian bawah), ikon lain di tengah
            IconInner.RenderTransformOrigin = ReferenceEquals(icon, IcoHand) ? new Point(0.5, 0.9) : new Point(0.5, 0.5);
        }

        private void SetAccent(Color color)
        {
            if (_accent.IsFrozen)
            {
                _accent = new SolidColorBrush(color);
                Resources["AccentBrush"] = _accent;
            }
            else
            {
                _accent.Color = color;
            }
        }

        // =====================================================================================
        //  STATE MACHINE
        // =====================================================================================

        /// <summary>Ambil giliran tampil. False jika notifikasi berprioritas lebih tinggi sedang tampil.</summary>
        private bool Acquire(Kind kind, int priority)
        {
            if (_priority >= 0 && priority < _priority) return false;
            StopAll();
            _priority = priority;
            _kind = kind;
            Poke();
            return true;
        }

        /// <summary>Catat aktivitas: batal tidur dan mulai ulang hitung mundur tidur.</summary>
        private void Poke()
        {
            _asleep = false;
            _sleepTimer.Stop();
            _sleepTimer.Start();
        }

        /// <summary>Notifikasi selesai: kembali ke state dasar (Music, Sleep, atau Idle).</summary>
        private void Finish()
        {
            _priority = -1;
            EnterBaseline();
        }
    }
}
