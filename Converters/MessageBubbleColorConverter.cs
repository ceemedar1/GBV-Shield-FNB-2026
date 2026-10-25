using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace GBV_Shield.Converters
{
    public sealed class MessageBubbleColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isSelf = value is bool b && b;
            return isSelf ? Color.FromArgb("#6D28D9") : Color.FromArgb("#3B0764");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
