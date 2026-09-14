using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converter to show MarkdownBlockRenderer for Assistant messages only.
    /// </summary>
    public sealed class RoleToMarkdownBlockVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                return role == ChatMessageRole.Assistant
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
