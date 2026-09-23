using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ContinueVS.Core.Types;
using ContinueVS.UI.Renderers;

namespace ContinueVS.ViewModels.Converters
{
    /// <summary>
    /// gap88: Maps a <see cref="ChatMessageRole"/> to the unified renderer's
    /// <see cref="MarkdownMode"/> content kind. User and Tool are verbatim; the
    /// (reasoning/response) "Thinking"/"Assistant" cards are markdown-guaranteed.
    /// Only consulted when the unified renderer is active (feature-flagged A/B).
    /// </summary>
    public sealed class RoleToRendererModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ChatMessageRole role)
            {
                return role switch
                {
                    // Tool & User carry arbitrary source/paste text: verbatim,
                    // never run through the markdig pipeline (gap88).
                    ChatMessageRole.Tool => MarkdownMode.Verbatim,
                    ChatMessageRole.User => MarkdownMode.Verbatim,
                    // Reasoning/response are markdown-guaranteed.
                    _ => MarkdownMode.Markdown
                };
            }
            return MarkdownMode.Verbatim;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
