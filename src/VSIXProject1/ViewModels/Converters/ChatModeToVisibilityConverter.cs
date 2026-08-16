using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts ChatMode to Visibility for conditional UI display.
    /// Returns Visible only in Ask mode; Collapsed for Agent and Plan modes.
    /// </summary>
    public class ChatModeToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts ChatMode to Visibility.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMode mode)
            {
                return mode == ChatMode.Ask ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// ConvertBack is not supported.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ChatModeToVisibilityConverter does not support ConvertBack.");
        }
    }
}
