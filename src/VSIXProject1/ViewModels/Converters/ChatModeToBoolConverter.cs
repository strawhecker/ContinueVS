using System;
using System.Globalization;
using System.Windows.Data;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converts ChatMode to a boolean value for ToggleButton binding.
    /// Used with ConverterParameter to check if CurrentMode matches the specified mode.
    /// </summary>
    public class ChatModeToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts ChatMode to boolean based on ConverterParameter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMode mode && parameter is string paramStr)
            {
                if (Enum.TryParse<ChatMode>(paramStr, out var paramMode))
                {
                    return mode == paramMode;
                }
            }
            return false;
        }

        /// <summary>
        /// ConvertBack converts boolean back to ChatMode based on ConverterParameter.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string paramStr)
            {
                if (Enum.TryParse<ChatMode>(paramStr, out var paramMode))
                {
                    return paramMode;
                }
            }
            return ChatMode.Ask;
        }
    }
}
