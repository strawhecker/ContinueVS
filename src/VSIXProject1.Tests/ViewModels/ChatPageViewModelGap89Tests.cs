#nullable enable

using ContinueVS.Core.Types;
using ContinueVS.Tests.Helpers;
using Xunit;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelGap89Tests
    {
        private static ChatMessage MakeMessage(string id, ChatMessageRole role = ChatMessageRole.Assistant)
            => new ChatMessage { Id = id, Role = role, Content = "raw content" };

        [Fact]
        public void ToggleRawView_FromPretty_FlipsToRaw()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            var msg = MakeMessage("m1");
            Assert.Equal(MessageViewMode.Pretty, msg.ViewMode);
            vm.ToggleRawViewCommand.Execute(msg);
            Assert.Equal(MessageViewMode.Raw, msg.ViewMode);
        }

        [Fact]
        public void ToggleRawView_FromRaw_FlipsBackToPretty()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            var msg = MakeMessage("m1");
            msg.ViewMode = MessageViewMode.Raw;
            vm.ToggleRawViewCommand.Execute(msg);
            Assert.Equal(MessageViewMode.Pretty, msg.ViewMode);
        }

        [Fact]
        public void ToggleRawView_ToggleTwice_ReturnsToPretty()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            var msg = MakeMessage("m1");
            vm.ToggleRawViewCommand.Execute(msg);
            vm.ToggleRawViewCommand.Execute(msg);
            Assert.Equal(MessageViewMode.Pretty, msg.ViewMode);
        }

        [Fact]
        public void ToggleRawView_NullMessage_DoesNotThrow()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            vm.ToggleRawViewCommand.Execute(null!);
        }

        [Fact]
        public void ToggleRawView_AppliesToUserReasonResponseAndToolRoles()
        {
            var vm = ChatPageViewModelTestHelper.CreateTestViewModel();
            var roles = new[]
            {
                ChatMessageRole.User,
                ChatMessageRole.Assistant,
                ChatMessageRole.Thinking,
                ChatMessageRole.Tool
            };
            foreach (var role in roles)
            {
                var msg = MakeMessage("id-" + role, role);
                vm.ToggleRawViewCommand.Execute(msg);
                Assert.True(msg.ViewMode == MessageViewMode.Raw, $"{role} should flip to Raw");
            }
        }
    }
}
