using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.UI.Renderers;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// Controls the unified renderer's visibility per role.
    ///
    /// The unified <see cref="StreamingMarkdownRenderer"/> is the single renderer for
    /// user, thinking, and assistant cards, so those roles are always Visible. Tool
    /// cards route through the dedicated ToolInvocationTemplate (gap90b), so Tool stays
    /// Collapsed here — the unified renderer is still available/verbatim for tool
    /// cards wherever the tool template chooses to use it.
    /// </summary>
    public sealed class RoleToRendererVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                switch (role)
                {
                    case ChatMessageRole.User:
                    case ChatMessageRole.Thinking:
                    case ChatMessageRole.Assistant:
                        return Visibility.Visible;
                    default:
                        return Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
