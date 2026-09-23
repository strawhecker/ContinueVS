using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.UI.Renderers;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap88: Controls the unified renderer's visibility per role.
    ///
    /// When the A/B flag is OFF (default), the unified renderer is always
    /// Collapsed — zero behavior change (legacy renderers remain untouched).
    ///
    /// When ON, only the roles the unified renderer actually hosts in
    /// ChatMessageControl are Visible (User, Thinking, Assistant). Tool cards
    /// route through the dedicated ToolInvocationTemplate (gap90b), so Tool stays
    /// Collapsed here — the unified renderer is still available/verbatim for tool
    /// cards wherever the tool template chooses to use it.
    /// </summary>
    public sealed class RoleToRendererVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!UseStreamingMarkdownRenderer.IsEnabled)
                return Visibility.Collapsed;

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
