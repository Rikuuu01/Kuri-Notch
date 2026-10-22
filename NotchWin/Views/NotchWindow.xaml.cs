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

        public const double EarRadius = 12.0;

        #region Dependency Properties for Smooth Animation

        public static readonly DependencyProperty NotchWidthProperty =
            DependencyProperty.Register(
                nameof(NotchWidth),
                typeof(double),
                typeof(NotchWindow),
                new PropertyMetadata(NotchViewModel.CollapsedWidth, OnGeometryChanged));

        public static readonly DependencyProperty NotchHeightProperty =
            DependencyProperty.Register(
                nameof(NotchHeight),
                typeof(double),
                typeof(NotchWindow),
                new PropertyMetadata(NotchViewModel.CollapsedHeight, OnGeometryChanged));

        public static readonly DependencyProperty NotchBottomRadiusProperty =
            DependencyProperty.Register(
                nameof(NotchBottomRadius),
                typeof(double),
                typeof(NotchWindow),
                new PropertyMetadata(NotchViewModel.CollapsedCornerRadius, OnGeometryChanged));

        public double NotchWidth
        {
            get => (double)GetValue(NotchWidthProperty);
            set => SetValue(NotchWidthProperty, value);
        }

        public double NotchHeight
        {
            get => (double)GetValue(NotchHeightProperty);
            set => SetValue(NotchHeightProperty, value);
        }

        public double NotchBottomRadius
        {
            get => (double)GetValue(NotchBottomRadiusProperty);
            set => SetValue(NotchBottomRadiusProperty, value);
        }

        private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotchWindow window)
            {
                window.UpdateNotchGeometry();
            }
        }

        #endregion

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
            ViewModel.Media.ActiveLyricChanged += Media_ActiveLyricChanged;

            UpdateNotchGeometry();
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

        private void UpdateNotchGeometry()
        {
            if (NotchBackgroundPath == null || NotchStrokePath == null || PillContainer == null || ContentHost == null)
                return;

            double w = NotchWidth;
            double h = NotchHeight;
            double earR = EarRadius;
            double botR = NotchBottomRadius;

            if (w <= 0 || h <= 0) return;

            // Update Geometries
            NotchBackgroundPath.Data = BuildFillGeometry(w, h, earR, botR);
            NotchStrokePath.Data = BuildStrokeGeometry(w, h, earR, botR);

            // Container size accommodates the main notch body plus the outward ears on both sides
            double totalWidth = w + 2.0 * earR;
            PillContainer.Width = totalWidth;
            PillContainer.Height = h;

            // Content sits inside the main body
            ContentHost.Width = w;
            ContentHost.Height = h;

            // Clip content to body shape with rounded bottom corners
            ContentHost.Clip = new RectangleGeometry(new Rect(0, 0, w, h), botR, botR);
        }

        private static Geometry BuildFillGeometry(double bodyWidth, double height, double earRadius, double bottomRadius)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);

                // 1. Left outward ear: concave arc curving from top bezel down to side wall
                ctx.ArcTo(
                    new Point(earRadius, earRadius),
                    new Size(earRadius, earRadius),
                    0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true
                );

                // 2. Left side wall
                ctx.LineTo(new Point(earRadius, height - bottomRadius), isStroked: true, isSmoothJoin: true);

                // 3. Bottom-left corner
                ctx.ArcTo(
                    new Point(earRadius + bottomRadius, height),
                    new Size(bottomRadius, bottomRadius),
                    0, false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true
                );

                // 4. Bottom horizontal edge
                ctx.LineTo(new Point(earRadius + bodyWidth - bottomRadius, height), isStroked: true, isSmoothJoin: true);

                // 5. Bottom-right corner
                ctx.ArcTo(
                    new Point(earRadius + bodyWidth, height - bottomRadius),
                    new Size(bottomRadius, bottomRadius),
                    0, false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true
                );

                // 6. Right side wall
                ctx.LineTo(new Point(earRadius + bodyWidth, earRadius), isStroked: true, isSmoothJoin: true);

                // 7. Right outward ear: concave arc curving up to top bezel
                ctx.ArcTo(
                    new Point(bodyWidth + 2.0 * earRadius, 0),
                    new Size(earRadius, earRadius),
                    0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true
                );

                // 8. Top edge along screen bezel (flush)
                ctx.LineTo(new Point(0, 0), isStroked: false, isSmoothJoin: true);
            }
            geometry.Freeze();
            return geometry;
        }

        private static Geometry BuildStrokeGeometry(double bodyWidth, double height, double earRadius, double bottomRadius)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // Stroke is open along the top bezel so the black color fuses into the physical monitor bezel
                ctx.BeginFigure(new Point(0, 0), isFilled: false, isClosed: false);

                // 1. Left outward ear
                ctx.ArcTo(
                    new Point(earRadius, earRadius),
                    new Size(earRadius, earRadius),
                    0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true
                );

                // 2. Left side wall
                ctx.LineTo(new Point(earRadius, height - bottomRadius), isStroked: true, isSmoothJoin: true);

                // 3. Bottom-left corner
                ctx.ArcTo(
                    new Point(earRadius + bottomRadius, height),
                    new Size(bottomRadius, bottomRadius),
                    0, false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true
                );

                // 4. Bottom edge
                ctx.LineTo(new Point(earRadius + bodyWidth - bottomRadius, height), isStroked: true, isSmoothJoin: true);

                // 5. Bottom-right corner
                ctx.ArcTo(
                    new Point(earRadius + bodyWidth, height - bottomRadius),
                    new Size(bottomRadius, bottomRadius),
                    0, false, SweepDirection.Counterclockwise, isStroked: true, isSmoothJoin: true
                );

                // 6. Right side wall
                ctx.LineTo(new Point(earRadius + bodyWidth, earRadius), isStroked: true, isSmoothJoin: true);

                // 7. Right outward ear
                ctx.ArcTo(
                    new Point(bodyWidth + 2.0 * earRadius, 0),
                    new Size(earRadius, earRadius),
                    0, false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true
                );
            }
            geometry.Freeze();
            return geometry;
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
                if (PillContainer != null && IsLoaded && NotchBackgroundPath?.Data != null)
                {
                    try
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        Point screenPt = new Point(x, y);
                        Point windowPt = PointFromScreen(screenPt);

                        double containerLeft = (Width - PillContainer.ActualWidth) / 2.0;
                        Point localPt = new Point(windowPt.X - containerLeft, windowPt.Y);

                        if (NotchBackgroundPath.Data.FillContains(localPt))
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

            double targetW = ViewModel.TargetWidth;
            double targetH = ViewModel.TargetHeight;
            double targetR = ViewModel.TargetCornerRadius;

            var duration = TimeSpan.FromMilliseconds(isExpanding ? 320 : 250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // 1. Animate Width
            var widthAnim = new DoubleAnimation(targetW, duration) { EasingFunction = ease };
            BeginAnimation(NotchWidthProperty, widthAnim);

            // 2. Animate Height
            var heightAnim = new DoubleAnimation(targetH, duration) { EasingFunction = ease };
            BeginAnimation(NotchHeightProperty, heightAnim);

            // 3. Animate Bottom Corner Radius
            var cornerAnim = new DoubleAnimation(targetR, duration) { EasingFunction = ease };
            BeginAnimation(NotchBottomRadiusProperty, cornerAnim);

            // 4. Smooth content crossfade
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

            // 5. Smooth Lyrics Panel Unfold / Fold
            bool isLyricsOpen = ViewModel.Media.IsLyricsExpanded && isExpanding;
            double targetLyricsH = isLyricsOpen ? 108.0 : 0.0;
            double targetLyricsO = isLyricsOpen ? 1.0 : 0.0;
            var targetLyricsMargin = isLyricsOpen ? new Thickness(0, 8, 0, 0) : new Thickness(0, 0, 0, 0);

            if (LyricsPanelBorder != null)
            {
                if (isLyricsOpen)
                {
                    UpdateLyricsTranslate(ViewModel.Media.ActiveLyricIndex, animate: false);
                }

                var lyricsHAnim = new DoubleAnimation(targetLyricsH, duration) { EasingFunction = ease };
                var lyricsOAnim = new DoubleAnimation(targetLyricsO, duration) { EasingFunction = ease };
                var lyricsMAnim = new ThicknessAnimation(targetLyricsMargin, duration) { EasingFunction = ease };

                LyricsPanelBorder.BeginAnimation(HeightProperty, lyricsHAnim);
                LyricsPanelBorder.BeginAnimation(OpacityProperty, lyricsOAnim);
                LyricsPanelBorder.BeginAnimation(MarginProperty, lyricsMAnim);
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

        #region Interactive Seekbar Slider Handling

        private void MediaSeekSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.Media.IsSeeking = true;
        }

        private void MediaSeekSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel.Media.IsSeeking = false;
            if (MediaSeekSlider != null)
            {
                _ = ViewModel.Media.SeekToSecondsAsync(MediaSeekSlider.Value);
            }
        }

        #endregion
        #region Lyrics Handling

        private void UpdateLyricsTranslate(int index, bool animate = true)
        {
            Dispatcher.Invoke(() =>
            {
                if (LyricsTranslateTransform != null && ViewModel.Media.LyricsLines.Count > 0)
                {
                    int targetIdx = Math.Clamp(index, 0, ViewModel.Media.LyricsLines.Count - 1);
                    double slotHeight = 36.0;
                    double targetY = (1 - targetIdx) * slotHeight;

                    if (animate)
                    {
                        var anim = new DoubleAnimation
                        {
                            To = targetY,
                            Duration = TimeSpan.FromMilliseconds(320),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        LyricsTranslateTransform.BeginAnimation(TranslateTransform.YProperty, anim);
                    }
                    else
                    {
                        LyricsTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
                        LyricsTranslateTransform.Y = targetY;
                    }
                }
            });
        }

        private void Media_ActiveLyricChanged(object? sender, int index)
        {
            UpdateLyricsTranslate(index, animate: true);
        }

        private void LyricLine_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LyricsLineViewModel line)
            {
                _ = ViewModel.Media.SeekToLyricAsync(line);
            }
        }

        private void LyricsBorder_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel.Media.HasLyrics && ViewModel.Media.IsSynced && ViewModel.Media.LyricsLines.Count > 0)
            {
                e.Handled = true;
                int current = Math.Max(0, ViewModel.Media.ActiveLyricIndex);
                if (e.Delta < 0)
                {
                    int next = Math.Min(ViewModel.Media.LyricsLines.Count - 1, current + 1);
                    _ = ViewModel.Media.SeekToLyricAsync(ViewModel.Media.LyricsLines[next]);
                }
                else if (e.Delta > 0)
                {
                    int prev = Math.Max(0, current - 1);
                    _ = ViewModel.Media.SeekToLyricAsync(ViewModel.Media.LyricsLines[prev]);
                }
            }
        }

        #endregion
    }
}
