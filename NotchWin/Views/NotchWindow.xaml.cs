using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchWin.Interop;
using NotchWin.Models;
using NotchWin.ViewModels;
using Serilog;

namespace NotchWin.Views
{
    public partial class NotchWindow : Window
    {
        public NotchViewModel ViewModel { get; }

        public static readonly DependencyProperty PillCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(PillCornerRadius),
                typeof(double),
                typeof(NotchWindow),
                new PropertyMetadata(NotchViewModel.CollapsedCornerRadius, (d, e) =>
                {
                    if (d is NotchWindow w && w.PillBorder != null)
                    {
                        double r = (double)e.NewValue;
                        w.PillBorder.CornerRadius = new CornerRadius(0, 0, r, r);
                    }
                }));

        public double PillCornerRadius
        {
            get => (double)GetValue(PillCornerRadiusProperty);
            set => SetValue(PillCornerRadiusProperty, value);
        }

        public NotchWindow()
        {
            InitializeComponent();
            ViewModel = new NotchViewModel();
            DataContext = ViewModel;

            ViewModel.RequestCloseEvent += (_, _) =>
            {
                Log.Information("Close requested from ViewModel");
                Close();
            };

            ViewModel.StateAnimationRequested += (_, _) => AnimateToCurrentState();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PositionAtTopCenter();

            var hwnd = new WindowInteropHelper(this).Handle;
            WindowStyleHelper.ApplyNotchWindowStyles(hwnd);

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            Log.Information("NotchWindow Loaded. ScreenWidth={ScreenWidth}, ScreenHeight={ScreenHeight}, Left={Left}, Top={Top}",
                SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight, Left, Top);
        }

        private void PositionAtTopCenter()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = Math.Round((screenWidth - Width) / 2.0);
            Top = 0;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(NativeMethods.MA_NOACTIVATE);
            }

            if (msg == NativeMethods.WM_NCHITTEST)
            {
                if (PillBorder != null && IsLoaded)
                {
                    try
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        Point screenPt = new Point(x, y);
                        Point windowPt = PointFromScreen(screenPt);

                        GeneralTransform transform = PillBorder.TransformToVisual(this);
                        Rect pillRect = transform.TransformBounds(new Rect(0, 0, PillBorder.ActualWidth, PillBorder.ActualHeight));

                        if (pillRect.Contains(windowPt))
                        {
                            handled = true;
                            return new IntPtr(NativeMethods.HTCLIENT);
                        }
                        else
                        {
                            handled = true;
                            return new IntPtr(NativeMethods.HTTRANSPARENT);
                        }
                    }
                    catch
                    {
                        // Fallback
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void AnimateToCurrentState()
        {
            bool isExpanding = ViewModel.CurrentState == NotchState.Expanded;
            Log.Debug("AnimateToCurrentState: IsExpanding={IsExpanding}", isExpanding);

            double targetW = isExpanding ? NotchViewModel.ExpandedWidth : NotchViewModel.CollapsedWidth;
            double targetH = isExpanding ? NotchViewModel.ExpandedHeight : NotchViewModel.CollapsedHeight;
            double targetR = isExpanding ? NotchViewModel.ExpandedCornerRadius : NotchViewModel.CollapsedCornerRadius;

            var duration = TimeSpan.FromMilliseconds(isExpanding ? 320 : 250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var widthAnim = new DoubleAnimation(targetW, duration) { EasingFunction = ease };
            PillBorder.BeginAnimation(WidthProperty, widthAnim);

            var heightAnim = new DoubleAnimation(targetH, duration) { EasingFunction = ease };
            PillBorder.BeginAnimation(HeightProperty, heightAnim);

            var cornerAnim = new DoubleAnimation(targetR, duration) { EasingFunction = ease };
            this.BeginAnimation(PillCornerRadiusProperty, cornerAnim);

            if (isExpanding)
            {
                var fadeOutCollapsed = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(100));
                CollapsedContentGrid.BeginAnimation(OpacityProperty, fadeOutCollapsed);

                ExpandedContentGrid.Visibility = Visibility.Visible;
                var fadeInExpanded = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    BeginTime = TimeSpan.FromMilliseconds(120),
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };
                ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeInExpanded);
            }
            else
            {
                var fadeOutExpanded = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(90));
                fadeOutExpanded.Completed += (_, _) =>
                {
                    if (ViewModel.CurrentState == NotchState.Collapsed)
                    {
                        ExpandedContentGrid.Visibility = Visibility.Collapsed;
                    }
                };
                ExpandedContentGrid.BeginAnimation(OpacityProperty, fadeOutExpanded);

                var fadeInCollapsed = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    BeginTime = TimeSpan.FromMilliseconds(100),
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = ease
                };
                CollapsedContentGrid.BeginAnimation(OpacityProperty, fadeInCollapsed);
            }
        }

        private void PillBorder_MouseEnter(object sender, MouseEventArgs e)
        {
            ViewModel.OnMouseEnter();
        }

        private void PillBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            ViewModel.OnMouseLeave();
        }

        private void PillBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.OnPillClicked();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Log.Information("Escape key pressed -> Closing NotchWin");
                Close();
            }
        }

        private void OnTogglePinClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.OnPillClicked();
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Log.Information("Context menu exit clicked -> Closing NotchWin");
            Close();
        }
    }
}
