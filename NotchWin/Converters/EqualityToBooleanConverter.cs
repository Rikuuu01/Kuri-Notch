using System;
using System.Globalization;
using System.Windows.Data;

namespace NotchWin.Converters
{
    public class EqualityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int intVal)) return intVal;
                return parameter.ToString()!;
            }
            return Binding.DoNothing;
        }
    }
}
