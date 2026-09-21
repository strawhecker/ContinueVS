#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Helpers;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Converters;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// gap85 tests: Chat Maximize/Minimize and Chat Delete/Undelete toggles,
    /// applied uniformly to user, reason, response, and tool-call entries.
    /// Verifies:
    ///   - ToggleMinimizeMessageCommand flips IsMinimized (UI visibility, not a tombstone).
    ///   - Soft-delete/undelete toggle semantics (✕ -> ↺) reuse the gap81 tombstone.
    ///   - Tool-call display helpers (ToolCallLabel / ToolFileName) render compact, limited text.
    /// </summary>
    public class ChatPageViewModelGap85Tests
    {
        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.SoftDeleteMessageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.UndeleteMessageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            mock.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static ChatPageViewModel CreateViewModel(
            Mock<ISessionService> sessionService,
            Mock<INotificationService> notificationService)
        {
            return ChatPageViewModelTestHelper.CreateTestViewModel(
                sessionService: sessionService.Object,
                notificationService: notificationService.Object);
        }

        [Fact]
        public void ToggleMinimizeMessageCommand_FalseToTrue_BecomesMinimized()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Hello" };
            vm.Messages.Add(msg);

            vm.ToggleMinimizeMessageCommand.Execute("1");

            Assert.True(msg.IsMinimized, "IsMinimized should flip to true (minimize toggle).");
        }

        [Fact]
        public void ToggleMinimizeMessageCommand_TrueToFalse_BecomesExpanded()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.Assistant, Content = "Response", IsMinimized = true };
            vm.Messages.Add(msg);

            vm.ToggleMinimizeMessageCommand.Execute("1");

            Assert.False(msg.IsMinimized, "IsMinimized should flip back to false (maximize toggle).");
        }

        [Fact]
        public void ToggleMinimizeMessageCommand_IgnoresNullOrMissingId()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "x" };
            vm.Messages.Add(msg);

            vm.ToggleMinimizeMessageCommand.Execute(null);
            vm.ToggleMinimizeMessageCommand.Execute("");
            vm.ToggleMinimizeMessageCommand.Execute("missing-id");

            Assert.False(msg.IsMinimized, "No change for null/empty/missing ids.");
        }

        [Fact]
        public void ToggleMinimizeMessageCommand_AppliesToAllRoles()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var user = new ChatMessage { Id = "u", Role = ChatMessageRole.User, Content = "hi" };
            var reason = new ChatMessage { Id = "r", Role = ChatMessageRole.Thinking, Content = "thinking" };
            var response = new ChatMessage { Id = "a", Role = ChatMessageRole.Assistant, Content = "answer" };
            var tool = new ChatMessage { Id = "t", Role = ChatMessageRole.Tool, Content = "result", ToolName = "read_file" };
            vm.Messages.Add(user);
            vm.Messages.Add(reason);
            vm.Messages.Add(response);
            vm.Messages.Add(tool);

            foreach (var id in new[] { "u", "r", "a", "t" })
                vm.ToggleMinimizeMessageCommand.Execute(id);

            Assert.True(user.IsMinimized);
            Assert.True(reason.IsMinimized);
            Assert.True(response.IsMinimized);
            Assert.True(tool.IsMinimized);
        }

        [Fact]
        public void DeleteUndeleteToggle_SetsThenClearsTombstone()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Hello" };
            vm.Messages.Add(msg);

            // State A -> delete (soft-delete => tombstone set).
            vm.SoftDeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(100);
            Assert.True(msg.IsDeleted, "Delete toggle should mark IsDeleted=true.");

            // State B -> undelete (tombstone cleared).
            vm.UndeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(100);
            Assert.False(msg.IsDeleted, "Undelete toggle should clear IsDeleted=false.");

            // The element is retained the whole time (soft-delete, not hard-remove).
            Assert.Single(vm.Messages);
        }

        [Fact]
        public void ToolCallLabel_UsesExplicitToolName()
        {
            var tool = new ChatMessage { Role = ChatMessageRole.Tool, ToolName = "read_file" };
            Assert.Equal("read_file", tool.ToolCallLabel);
        }

        [Fact]
        public void ToolCallLabel_FallsBackToFirstRequestToolName()
        {
            var tool = new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                ToolCalls = new List<ToolCall> { new ToolCall { Name = "grep_search" } }
            };
            Assert.Equal("grep_search", tool.ToolCallLabel);
        }

        [Fact]
        public void ToolCallLabel_DefaultsToToolWhenUnknown()
        {
            var tool = new ChatMessage { Role = ChatMessageRole.Tool, Content = "some result" };
            Assert.Equal("tool", tool.ToolCallLabel);
        }

        [Fact]
        public void ToolFileName_ExtractsBasenameFromArguments()
        {
            var tool = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = "read_file",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Name = "read_file",
                        Arguments = new Dictionary<string, object> { ["filepath"] = @"C:\repo\src\Program.cs" }
                    }
                }
            };
            Assert.Equal("Program.cs", tool.ToolFileName);
        }

        [Fact]
        public void ToolFileName_ReturnsNullWhenNoFileReference()
        {
            var tool = new ChatMessage { Role = ChatMessageRole.Tool, ToolName = "run_pytest" };
            Assert.Null(tool.ToolFileName);
        }

        [Fact]
        public void InverseBoolToVisibilityConverter_Inverts()
        {
            var conv = new InverseBoolToVisibilityConverter();
            Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(true, typeof(object), null!, null!));
            Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(false, typeof(object), null!, null!));
        }

        [Fact]
        public void IsNotNullToVisibilityConverter_HidesEmptyString()
        {
            var conv = new IsNotNullToVisibilityConverter();
            Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert("   ", typeof(object), null!, null!));
            Assert.Equal(System.Windows.Visibility.Visible, conv.Convert("Program.cs", typeof(object), null!, null!));
        }
    }
}
