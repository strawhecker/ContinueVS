using System.Globalization;
using ContinueVS.Core.Types;
using ContinueVS.UI.Renderers;
using ContinueVS.ViewModels.Converters;
using Xunit;

namespace ContinueVS.Tests.ViewModels.Converters
{
    public class RoleToRendererModeConverterTests
    {
        private readonly RoleToRendererModeConverter _converter = new RoleToRendererModeConverter();

        [Theory]
        [InlineData(ChatMessageRole.Tool, MarkdownMode.Verbatim)]
        [InlineData(ChatMessageRole.User, MarkdownMode.Verbatim)]
        [InlineData(ChatMessageRole.Assistant, MarkdownMode.Markdown)]
        [InlineData(ChatMessageRole.Thinking, MarkdownMode.Markdown)]
        public void Convert_MapsRoleToContentKind(ChatMessageRole role, MarkdownMode expected)
        {
            var result = _converter.Convert(role, typeof(MarkdownMode), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_NullValue_ReturnsVerbatim()
        {
            var result = _converter.Convert(null, typeof(MarkdownMode), null, CultureInfo.InvariantCulture);
            Assert.Equal(MarkdownMode.Verbatim, result);
        }

        [Fact]
        public void ConvertBack_ReturnsUnsetValue()
        {
            var result = _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.Equal(System.Windows.DependencyProperty.UnsetValue, result);
        }
    }
}
