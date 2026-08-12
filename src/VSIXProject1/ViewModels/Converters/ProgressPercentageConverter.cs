using System;
using System.Globalization;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts a numeric progress value (0–100 or 0.0–1.0) to a formatted percentage string.
    /// Supports both integer and decimal progress representations.
    /// </summary>
    public class ProgressPercentageConverter : IValueConverter
    {
        /// <summary>
        /// Converts a numeric value to a percentage string.
        /// Handles both 0–100 and 0.0–1.0 ranges.
        /// </summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return "0%";
            }

            try
            {
                double numValue = System.Convert.ToDouble(value, culture);

                // Normalize to 0–100 range if input is 0.0–1.0
                if (numValue <= 1.0 && numValue >= 0.0)
                {
                    numValue *= 100.0;
                }

                // Clamp to 0–100
                numValue = Math.Max(0, Math.Min(100, numValue));

                return $"{numValue:F0}%";
            }
            catch
            {
                return "0%";
            }
        }

        /// <summary>
        /// Converts a percentage string back to a numeric value (0–1.0 range).
        /// </summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                stringValue = stringValue.TrimEnd('%').Trim();
                if (double.TryParse(stringValue, NumberStyles.Number, culture, out var numValue))
                {
                    // Return as 0–1.0 range
                    return Math.Max(0, Math.Min(1.0, numValue / 100.0));
                }
            }

            return 0.0;
        }
    }
}
