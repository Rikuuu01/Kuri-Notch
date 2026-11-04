using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using NotchWin.Widgets;

namespace NotchWin.Converters
{
    public class WidgetTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WidgetType currentType && parameter is string targetStr)
            {
                bool invert = false;
                if (targetStr.StartsWith("!"))
                {
                    invert = true;
                    targetStr = targetStr.Substring(1);
                }

                if (Enum.TryParse<WidgetType>(targetStr, true, out var target))
                {
                    bool match = currentType == target;
                    if (invert) match = !match;
                    return match ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
