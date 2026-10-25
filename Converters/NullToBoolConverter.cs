using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace GBV_Shield.Converters
{
    // Returns true if value is NOT null/empty -> useful for IsVisible
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return false;

            if (value is string s)
                return !string.IsNullOrWhiteSpace(s);

            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
