using System;
using System.Globalization;
using System.Windows.Data;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts ExecutionStatus enum values to display symbols/icons.
    /// Maps: Succeeded=✓, Running=⊙, Failed=✗, Skipped=○, Pending=○, Cancelled=⊗.
    /// Used for gap69 execution impact status display.
    /// </summary>
    [ValueConversion(typeof(ExecutionStatus), typeof(string))]
    public class ExecutionStatusIconConverter : IValueConverter
    {
        /// <summary>
        /// Converts ExecutionStatus to corresponding Unicode symbol.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ExecutionStatus status)
            {
                return status switch
                {
                    ExecutionStatus.Succeeded => "✓",   // Check mark
                    ExecutionStatus.Running => "⊙",     // Circle with dot (in-progress)
                    ExecutionStatus.Failed => "✗",      // X mark
                    ExecutionStatus.Cancelled => "⊗",   // Circled X
                    ExecutionStatus.Skipped => "○",     // Empty circle
                    ExecutionStatus.Pending => "○",     // Empty circle
                    _ => "?"                            // Unknown
                };
            }

            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ExecutionStatusIconConverter is one-way only.");
        }
    }
}
