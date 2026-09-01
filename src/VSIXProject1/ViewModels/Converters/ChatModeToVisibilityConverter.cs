using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.Services;

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
            _ = LoggerService.Current.WriteDebugAsync($"[gap10-converter-enter] value={value}, targetType={targetType.Name}");
            if (value is ChatMode mode)
            {
                bool isAsk = mode == ChatMode.Ask;
                _ = LoggerService.Current.WriteDebugAsync($"[gap10-converter-logic] ChatMode={mode}, isAsk={isAsk}");
                Visibility result = isAsk ? Visibility.Visible : Visibility.Collapsed;
                _ = LoggerService.Current.WriteDebugAsync($"[gap10-converter-result] Returning {result}");
                return result;
            }
            _ = LoggerService.Current.WriteDebugAsync($"[gap10-converter-invalid-type] value is not ChatMode, type={value?.GetType().Name ?? "null"}");
            return Visibility.Collapsed;
        }

        /// <summary>
        /// ConvertBack is not supported.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            _ = LoggerService.Current.WriteDebugAsync($"[gap10-converter-convertback] Unexpected ConvertBack called with value={value}");
            throw new NotSupportedException("ChatModeToVisibilityConverter does not support ConvertBack.");
        }
    }
}
