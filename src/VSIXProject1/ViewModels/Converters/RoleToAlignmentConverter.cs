using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.Services;

namespace ContinueVS.ViewModels.Converters
{
    public sealed class RoleToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                var alignment = role switch
                {
                    ChatMessageRole.User => HorizontalAlignment.Right,
                    ChatMessageRole.Assistant => HorizontalAlignment.Left,
                    ChatMessageRole.System => HorizontalAlignment.Stretch,
                    ChatMessageRole.Tool => HorizontalAlignment.Stretch,
                    ChatMessageRole.Thinking => HorizontalAlignment.Stretch,
                    _ => HorizontalAlignment.Stretch
                };
                _ = LoggerService.Current.WriteDebugAsync($"[a6-converter] RoleToAlignmentConverter.Convert: Role={role}, Alignment={alignment}");
                return alignment;
            }

            _ = LoggerService.Current.WriteDebugAsync($"[a6-converter] RoleToAlignmentConverter.Convert: value is null, returning Stretch");
            return HorizontalAlignment.Stretch;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
