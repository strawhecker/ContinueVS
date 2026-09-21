using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ContinueVS.Core.Types;
using ContinueVS.Services;

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
                    ChatMessageRole.User => TryGetResourceBrush("AccentBrush") ?? new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    ChatMessageRole.Assistant => TryGetResourceBrush("SecondaryTextBrush") ?? new SolidColorBrush(Color.FromRgb(96, 96, 96)),
                    ChatMessageRole.Thinking => TryGetResourceBrush("InfoBrush") ?? new SolidColorBrush(Color.FromRgb(0, 90, 120)),
                    ChatMessageRole.System => TryGetResourceBrush("SecondaryTextBrush") ?? new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    // Tool bubbles render content through StreamingReasoningRenderer (transparent
                    // background, VsBrush.WindowText foreground). Painting the bubble yellow here left
                    // an empty yellow bar (no renderer was visible for Tool) — make it transparent so
                    // the tool-call description/text shows on the renderer, matching the reasoning
                    // bubble pattern.
                    ChatMessageRole.Tool => Brushes.Transparent,
                    _ => new SolidColorBrush(Colors.White)
                };
                LoggerService.Current.WriteDebug($"[a6-converter] RoleToColorConverter.Convert: Role={role}");
                return brush;
            }

            LoggerService.Current.WriteDebug("[a6-converter] RoleToColorConverter.Convert: value is null, returning White");
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }

        private static Brush? TryGetResourceBrush(string key)
        {
            try
            {
                var resource = Application.Current.Resources[key];
                return resource as Brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
