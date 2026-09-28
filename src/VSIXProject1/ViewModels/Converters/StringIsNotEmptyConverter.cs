#nullable enable

using System;
using System.Globalization;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap86_1: Converts a string to a bool indicating whether it is non-empty (and not
    /// whitespace). Used to enable the "Use text below as answer" claim button only while the
    /// main composer has draft text — the single source that the claim copies from.
    /// </summary>
    public sealed class StringIsNotEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => !string.IsNullOrWhiteSpace(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException("StringIsNotEmptyConverter is one-way only.");
    }
}
