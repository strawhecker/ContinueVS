using System;
using System.Globalization;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts a boolean value to its inverse (negated) boolean value.
    /// true → false, false → true
    /// Useful for inverting UI states (e.g., "Enable when NOT streaming").
    /// </summary>
    public class InverseBooleanConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to its inverse.
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }

        /// <summary>
        /// Converts a boolean back to its inverse.
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return true;
        }
    }
}
