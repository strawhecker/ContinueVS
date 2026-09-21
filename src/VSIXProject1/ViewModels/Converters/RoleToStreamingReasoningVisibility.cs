using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converter to show StreamingReasoningRenderer for User, Thinking and Tool messages only.
    /// Tool messages reuse the same transparent-background, wrapping, selectable renderer as the
    /// reasoning bubble so tool-call content sizes/surrounds correctly instead of leaving a
    /// yellow WarningBrush bubble empty (see RoleToColorConverter.Tool => Transparent).
    /// </summary>
    public sealed class RoleToStreamingReasoningVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                return (role == ChatMessageRole.User ||
                        role == ChatMessageRole.Thinking ||
                        role == ChatMessageRole.Tool)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
