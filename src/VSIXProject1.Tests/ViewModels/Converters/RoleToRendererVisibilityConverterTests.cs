using System.Globalization;
using System.Windows;
using ContinueVS.Core.Types;
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
        public void Convert_HostedRolesAlwaysVisible(ChatMessageRole role)
        {
            // The unified renderer is the single renderer — no A/B flag. Hosted roles
            // (user/thinking/assistant) are unconditionally visible.
            var result = _converter.Convert(role, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void Convert_ToolRoleCollapsed()
        {
            // Tool cards route through the dedicated ToolInvocationTemplate, so the
            // unified renderer stays collapsed here.
            Assert.Equal(Visibility.Collapsed,
                _converter.Convert(ChatMessageRole.Tool, typeof(Visibility), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ConvertBack_ReturnsUnsetValue()
        {
            var result = _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.Equal(DependencyProperty.UnsetValue, result);
        }
    }
}
