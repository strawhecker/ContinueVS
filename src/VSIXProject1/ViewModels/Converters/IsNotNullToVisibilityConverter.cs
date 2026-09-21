using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap85: Converts a non-null/non-empty reference (string) to Visibility.
    /// Non-whitespace value → Visible; null/empty/whitespace → Collapsed.
    /// Used to show the "file used" line on tool-call bubbles only when a file is present.
    /// </summary>
    public sealed class IsNotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is string s && !string.IsNullOrWhiteSpace(s)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility == Visibility.Visible;
            return false;
        }
    }
}
