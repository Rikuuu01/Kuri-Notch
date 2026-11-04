using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NotchWin.Views;
using Serilog;

namespace NotchWin.Services
{
    public static class FontScaleService
    {
        private static readonly DependencyProperty BaseFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "BaseFontSize",
                typeof(double?),
                typeof(FontScaleService),
                new PropertyMetadata(null));

        private static readonly DependencyProperty BaseLineHeightProperty =
            DependencyProperty.RegisterAttached(
                "BaseLineHeight",
                typeof(double?),
                typeof(FontScaleService),
                new PropertyMetadata(null));

        private static double _currentScale = 1.0;
        private static bool _initialized;

        public static double CurrentScale => _currentScale;

        public static void Initialize(double initialScale)
        {
            _currentScale = Math.Clamp(initialScale > 0 ? initialScale : 1.0, 0.70, 1.60);

            if (_initialized) return;
            _initialized = true;

            // Automatically scale TextBlocks when they load in NotchWindow
            EventManager.RegisterClassHandler(
                typeof(TextBlock),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnTextBlockLoaded));

            // Automatically scale Controls when they load in NotchWindow
            EventManager.RegisterClassHandler(
                typeof(Control),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnControlLoaded));

            Log.Information("FontScaleService initialized with scale {Scale:P0}", _currentScale);
        }

        private static bool ShouldScale(DependencyObject elem)
        {
            if (elem == null) return false;

            var win = Window.GetWindow(elem);
            if (win is NotchWindow) return true;

            DependencyObject? current = elem;
            while (current != null)
            {
                if (current is NotchWindow) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private static void OnTextBlockLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb && ShouldScale(tb))
            {
                ApplyToTextBlock(tb, _currentScale);
            }
        }

        private static void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Control c && ShouldScale(c))
            {
                ApplyToControl(c, _currentScale);
            }
        }

        public static void ApplyToTextBlock(TextBlock tb, double scale)
        {
            if (tb == null) return;

            var baseObj = tb.GetValue(BaseFontSizeProperty);
            double baseSize;
            if (baseObj is double d)
            {
                baseSize = d;
            }
            else
            {
                var local = tb.ReadLocalValue(TextBlock.FontSizeProperty);
                baseSize = (local is double l && l > 0) ? l : tb.FontSize;
                if (baseSize <= 0) baseSize = 12.0;
                tb.SetValue(BaseFontSizeProperty, baseSize);
            }

            // Force HD ClearType rendering and display grid formatting on every text element
            TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(tb, ClearTypeHint.Enabled);

            if (tb.LineHeight > 0)
            {
                var baseLhObj = tb.GetValue(BaseLineHeightProperty);
                double baseLh;
                if (baseLhObj is double dlh)
                {
                    baseLh = dlh;
                }
                else
                {
                    baseLh = tb.LineHeight;
                    tb.SetValue(BaseLineHeightProperty, baseLh);
                }
                tb.LineHeight = Math.Max(1.0, Math.Round(baseLh * scale * 2.0) / 2.0);
            }

            // Snap to half-point increments (e.g. 8.5, 9.0, 9.5, 10.0) to prevent sub-pixel glyph stem blur
            double target = Math.Max(7.0, Math.Round(baseSize * scale * 2.0) / 2.0);
            if (Math.Abs(tb.FontSize - target) > 0.05)
            {
                tb.FontSize = target;
            }
        }

        public static void ApplyToControl(Control c, double scale)
        {
            if (c == null) return;

            var baseObj = c.GetValue(BaseFontSizeProperty);
            double baseSize;
            if (baseObj is double d)
            {
                baseSize = d;
            }
            else
            {
                var local = c.ReadLocalValue(Control.FontSizeProperty);
                baseSize = (local is double l && l > 0) ? l : c.FontSize;
                if (baseSize <= 0) baseSize = 12.0;
                c.SetValue(BaseFontSizeProperty, baseSize);
            }

            TextOptions.SetTextFormattingMode(c, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(c, TextRenderingMode.ClearType);
            RenderOptions.SetClearTypeHint(c, ClearTypeHint.Enabled);

            double target = Math.Max(7.0, Math.Round(baseSize * scale * 2.0) / 2.0);
            if (Math.Abs(c.FontSize - target) > 0.05)
            {
                c.FontSize = target;
            }
        }

        public static void SetScale(double scale)
        {
            scale = Math.Clamp(scale > 0 ? scale : 1.0, 0.70, 1.60);
            if (Math.Abs(_currentScale - scale) < 0.005) return;

            _currentScale = scale;
            Log.Debug("FontScaleService: Scale updated to {Scale:P0}", _currentScale);

            if (Application.Current == null) return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is NotchWindow notchWin && notchWin.IsLoaded)
                    {
                        ApplyScaleToWindow(notchWin, _currentScale);
                        notchWin.InvalidateNavFullWidths();
                    }
                }
            }, DispatcherPriority.Render);
        }

        public static void ApplyScaleToWindow(Window win, double scale)
        {
            if (win == null) return;
            UpdateVisualTree(win, scale);
        }

        private static void UpdateVisualTree(DependencyObject parent, double scale)
        {
            if (parent == null) return;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is TextBlock tb)
                {
                    ApplyToTextBlock(tb, scale);
                }
                else if (child is Control c)
                {
                    ApplyToControl(c, scale);
                }

                UpdateVisualTree(child, scale);
            }
        }
    }
}
