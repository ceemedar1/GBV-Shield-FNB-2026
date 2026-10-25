using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace GBV_Shield.Converters
{
    public sealed class MessageBubbleAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isSelf = value is bool b && b;
            return isSelf ? LayoutOptions.End : LayoutOptions.Start;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
