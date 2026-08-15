using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    public sealed class RoleToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                var brush = role switch
                {
                    ChatMessageRole.User => new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    ChatMessageRole.Assistant => new SolidColorBrush(Color.FromRgb(96, 96, 96)),
                    ChatMessageRole.System => new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    ChatMessageRole.Tool => new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    ChatMessageRole.Thinking => new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    _ => new SolidColorBrush(Colors.White)
                };
                System.Diagnostics.Debug.WriteLine($"[a6-converter] RoleToColorConverter.Convert: Role={role}, Color={brush.Color}");
                return brush;
            }

            System.Diagnostics.Debug.WriteLine($"[a6-converter] RoleToColorConverter.Convert: value is null, returning White");
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
