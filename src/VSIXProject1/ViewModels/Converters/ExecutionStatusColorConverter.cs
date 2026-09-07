using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts ExecutionStatus enum values to SolidColorBrush for status display.
    /// Maps: Succeeded=Green, Running=Blue, Failed=Red, Skipped/Pending=Gray, Cancelled=Orange.
    /// Used for gap69 execution impact status badges.
    /// </summary>
    [ValueConversion(typeof(ExecutionStatus), typeof(Brush))]
    public class ExecutionStatusColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts ExecutionStatus to corresponding Brush color.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ExecutionStatus status)
            {
                return status switch
                {
                    ExecutionStatus.Succeeded => new SolidColorBrush(Color.FromArgb(255, 33, 161, 33)),      // Green #21A121
                    ExecutionStatus.Running => new SolidColorBrush(Color.FromArgb(255, 14, 99, 156)),       // Blue #0E639C
                    ExecutionStatus.Failed => new SolidColorBrush(Color.FromArgb(255, 232, 17, 35)),        // Red #E81123
                    ExecutionStatus.Cancelled => new SolidColorBrush(Color.FromArgb(255, 255, 165, 0)),     // Orange #FFA500
                    ExecutionStatus.Skipped => new SolidColorBrush(Color.FromArgb(255, 107, 107, 107)),     // Gray #6B6B6B
                    ExecutionStatus.Pending => new SolidColorBrush(Color.FromArgb(255, 107, 107, 107)),     // Gray #6B6B6B
                    _ => new SolidColorBrush(Color.FromArgb(255, 204, 204, 204))                            // Light gray fallback
                };
            }

            return new SolidColorBrush(Color.FromArgb(255, 204, 204, 204));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ExecutionStatusColorConverter is one-way only.");
        }
    }
}
