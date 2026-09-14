using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converter to show StreamingReasoningRenderer for User and Thinking messages only.
    /// </summary>
    public sealed class RoleToStreamingReasoningVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                return (role == ChatMessageRole.User || role == ChatMessageRole.Thinking)
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
