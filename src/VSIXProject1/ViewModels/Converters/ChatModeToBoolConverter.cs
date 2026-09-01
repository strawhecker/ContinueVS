using System;
using System.Globalization;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.Services;

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
                if (Enum.TryParse<ChatMode>(paramStr, ignoreCase: true, out var paramMode))
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
            _ = LoggerService.Current.WriteDebugAsync($"[a9-converter-entry] ConvertBack: value={value}, parameter={parameter}");
            if (value is bool isChecked && isChecked && parameter is string paramStr)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[a9-converter-parse] Starting Enum.TryParse for paramStr='{paramStr}'");
                if (Enum.TryParse<ChatMode>(paramStr, ignoreCase: true, out var paramMode))
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[a9-converter-success] Parsed successfully: paramMode={paramMode}");
                    return paramMode;
                }
                _ = LoggerService.Current.WriteDebugAsync($"[a9-converter-fail] Enum.TryParse failed for paramStr='{paramStr}'");
            }
            _ = LoggerService.Current.WriteDebugAsync($"[a9-converter-fallback] Returning ChatMode.Ask (value={value}, isChecked={value is bool && (bool)value}, paramStr check failed)");
            return ChatMode.Ask;
        }
    }
}
