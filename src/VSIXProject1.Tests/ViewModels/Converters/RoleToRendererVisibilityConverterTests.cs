using System.Globalization;
using System.Windows;
using ContinueVS.Core.Types;
using ContinueVS.UI.Renderers;
using ContinueVS.ViewModels.Converters;
using Xunit;

namespace ContinueVS.Tests.ViewModels.Converters
{
    public class RoleToRendererVisibilityConverterTests
    {
        private readonly RoleToRendererVisibilityConverter _converter = new RoleToRendererVisibilityConverter();

        [Theory]
        [InlineData(ChatMessageRole.User)]
        [InlineData(ChatMessageRole.Thinking)]
        [InlineData(ChatMessageRole.Assistant)]
        public void Convert_WhenFlagOff_ReturnsCollapsed(ChatMessageRole role)
        {
            // A/B flag default OFF => unified renderer hidden, zero behavior change.
            bool previous = UseStreamingMarkdownRenderer.IsEnabled;
            try
            {
                UseStreamingMarkdownRenderer.IsEnabled = false;
                var result = _converter.Convert(role, typeof(Visibility), null, CultureInfo.InvariantCulture);
                Assert.Equal(Visibility.Collapsed, result);
            }
            finally
            {
                UseStreamingMarkdownRenderer.IsEnabled = previous;
            }
        }

        [Fact]
        public void Convert_WhenFlagOn_HostedRolesVisible()
        {
            bool previous = UseStreamingMarkdownRenderer.IsEnabled;
            try
            {
                UseStreamingMarkdownRenderer.IsEnabled = true;
                Assert.Equal(Visibility.Visible,
                    _converter.Convert(ChatMessageRole.User, typeof(Visibility), null, CultureInfo.InvariantCulture));
                Assert.Equal(Visibility.Visible,
                    _converter.Convert(ChatMessageRole.Thinking, typeof(Visibility), null, CultureInfo.InvariantCulture));
                Assert.Equal(Visibility.Visible,
                    _converter.Convert(ChatMessageRole.Assistant, typeof(Visibility), null, CultureInfo.InvariantCulture));
                Assert.Equal(Visibility.Collapsed,
                    _converter.Convert(ChatMessageRole.Tool, typeof(Visibility), null, CultureInfo.InvariantCulture));
            }
            finally
            {
                UseStreamingMarkdownRenderer.IsEnabled = previous;
            }
        }

        [Fact]
        public void ConvertBack_ReturnsUnsetValue()
        {
            var result = _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.Equal(DependencyProperty.UnsetValue, result);
        }
    }
}
