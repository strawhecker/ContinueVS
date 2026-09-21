using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Converter to show StreamingReasoningRenderer for User and Thinking messages only.
    /// Tool messages are intentionally excluded: tool calls render through the dedicated
    /// ToolInvocationTemplate (not this renderer). Routing Tool here produced an unsized,
    /// black/transparent bubble (StreamingReasoningRenderer latches its width from a
    /// PageWidth/SizeChanged and was not built for tool bubbles), so Tool is reverted out.
    /// </summary>
    public sealed class RoleToStreamingReasoningVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                return (role == ChatMessageRole.User ||
                        role == ChatMessageRole.Thinking)
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
