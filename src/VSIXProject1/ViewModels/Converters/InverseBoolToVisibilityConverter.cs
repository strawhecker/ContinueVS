using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap85: Converts a boolean to a Visibility, inverted from
    /// <see cref="BooleanToVisibilityConverter"/>: true → Collapsed, false → Visible.
    /// Used to show the delete button only while a message is NOT deleted, and to
    /// collapse a message's body only while it IS minimized.
    /// </summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return true;
        }
    }
}
