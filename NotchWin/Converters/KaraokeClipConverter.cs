using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NotchWin.Converters
{
    public class KaraokeClipConverter : IMultiValueConverter
    {
        private static readonly RectangleGeometry EmptyClip = CreateClip(0, 0);
        private static readonly ConcurrentDictionary<(string, double, FontWeight, FontStyle), double> _textWidthCache = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 5)
            {
                return EmptyClip;
            }

            TextBlock? tb = values[0] as TextBlock;
            double width = 0.0;
            double height = 0.0;

            int offset = 0;
            if (tb != null)
            {
                width = tb.ActualWidth;
                height = tb.ActualHeight;
                // values: [0]=tb, [1]=isActive, [2]=progress, [3]=karaokeEnabled, [4]=isUntimed
                offset = 1;
            }
            else if (values[0] is double w && values[1] is double h)
            {
                width = w;
                height = h;
                // values: [0]=width, [1]=height, [2]=isActive, [3]=progress, [4]=karaokeEnabled, [5]=isUntimed
                offset = 2;
            }
            else
            {
                return EmptyClip;
            }

            if (values.Length < offset + 4 ||
                values[offset] is not bool isActive ||
                values[offset + 1] is not double progress ||
                values[offset + 2] is not bool karaokeEnabled ||
                values[offset + 3] is not bool isUntimed)
            {
                return EmptyClip;
            }

            if (!isActive || !karaokeEnabled || isUntimed || width <= 0 || height <= 0 ||
                double.IsNaN(width) || double.IsNaN(height) || double.IsNaN(progress) ||
                double.IsInfinity(width) || double.IsInfinity(height) || double.IsInfinity(progress))
            {
                return EmptyClip;
            }

            double clampedProgress = Math.Clamp(progress, 0.0, 1.0);
            if (clampedProgress <= 0.0001)
            {
                return EmptyClip;
            }

            // Calculate exact text bounds and left offset if text is centered inside a wider container
            double leftOffset = 0.0;
            double textSpan = width;

            if (tb != null && !string.IsNullOrEmpty(tb.Text))
            {
                double measuredWidth = GetTextWidth(tb);
                if (measuredWidth > 0 && width > measuredWidth)
                {
                    if (tb.TextAlignment == TextAlignment.Center)
                    {
                        leftOffset = (width - measuredWidth) / 2.0;
                        textSpan = measuredWidth;
                    }
                    else if (tb.TextAlignment == TextAlignment.Right)
                    {
                        leftOffset = width - measuredWidth;
                        textSpan = measuredWidth;
                    }
                }
            }

            double rightEdge = leftOffset + (clampedProgress * textSpan);
            if (clampedProgress >= 0.999)
            {
                rightEdge = width; // Fully reveal to right edge
            }

            return CreateClip(rightEdge, height);
        }

        private static double GetTextWidth(TextBlock tb)
        {
            if (string.IsNullOrEmpty(tb.Text)) return 0;
            var key = (tb.Text, tb.FontSize, tb.FontWeight, tb.FontStyle);
            if (_textWidthCache.TryGetValue(key, out double cached))
            {
                return cached;
            }

            try
            {
                var typeface = new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch);
                var formattedText = new FormattedText(
                    tb.Text,
                    CultureInfo.CurrentCulture,
                    tb.FlowDirection,
                    typeface,
                    tb.FontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(tb).PixelsPerDip);

                double measured = formattedText.WidthIncludingTrailingWhitespace;
                if (_textWidthCache.Count > 500) _textWidthCache.Clear();
                _textWidthCache[key] = measured;
                return measured;
            }
            catch
            {
                return tb.ActualWidth;
            }
        }

        private static RectangleGeometry CreateClip(double width, double height)
        {
            var geometry = new RectangleGeometry(new Rect(0, 0, Math.Max(0, width), Math.Max(0, height)));
            geometry.Freeze();
            return geometry;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
