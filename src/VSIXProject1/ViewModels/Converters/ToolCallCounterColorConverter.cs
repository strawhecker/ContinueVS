using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts a tool call percentage value (0-100) to a dynamic Brush color.
    /// - Gray (< 80%): Normal state
    /// - Orange (80-99%): Warning state
    /// - Red (100%): Error state
    /// Used for gap23_4_5 tool call counter display.
    /// </summary>
    [ValueConversion(typeof(double), typeof(Brush))]
    public class ToolCallCounterColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a nullable double percentage to a Brush based on thresholds.
        /// </summary>
        /// <param name="value">The percentage value (0-100) or null.</param>
        /// <param name="targetType">Ignored.</param>
        /// <param name="parameter">Ignored.</param>
        /// <param name="culture">Ignored.</param>
        /// <returns>Brush: PrimaryTextBrush for < 80%, WarningBrush for 80-99%, ErrorBrush for 100%.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                if (percentage >= 100.0)
                {
                    return TryGetResource("ErrorBrush") ?? Brushes.Red;
                }
                else if (percentage >= 80.0)
                {
                    return TryGetResource("WarningBrush") ?? Brushes.Orange;
                }
            }

            return TryGetResource("PrimaryTextBrush") ?? Brushes.Gray;
        }

        private Brush? TryGetResource(string resourceKey)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    return (Brush?)app.TryFindResource(resourceKey);
                }
            }
            catch
            {
                // Suppress exceptions during resource lookup
            }

            return null;
        }


        /// <summary>
        /// Not implemented for this one-way converter.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ToolCallCounterColorConverter is a one-way converter.");
        }
    }
}
