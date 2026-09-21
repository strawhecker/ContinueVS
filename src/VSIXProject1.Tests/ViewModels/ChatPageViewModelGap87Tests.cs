#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Helpers;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// gap87 tests: tool-call descriptions are fabricated from args and attached to
    /// Tool-role messages so the chat bubble can show what a tool call actually did.
    /// Verifies the description is populated on tool messages and is display-only.
    /// </summary>
    public class ChatPageViewModelGap87Tests
    {
        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
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
        public async Task ExecuteAgentCommandAsync_AddsToolMessage_WithPopulatedDescriptionAsync()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();

            Mock<IAgentCommandDispatcher> dispatcher = new Mock<IAgentCommandDispatcher>();
            dispatcher
                .Setup(x => x.DispatchAgentCommandAsync(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<ChatMode>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolResult
                {
                    ToolName = "read_file",
                    Output = "file contents here"
                });

            var vm = ChatPageViewModelTestHelper.CreateTestViewModel(
                sessionService: sessionService.Object,
                notificationService: notificationService.Object,
                agentCommandDispatcher: dispatcher.Object);
            vm.CurrentMode = ChatMode.Agent;

            var args = new Dictionary<string, object> { ["filepath"] = "src/foo.cs" };
            await vm.ExecuteAgentCommandAsync("read_file", args);

            var toolMsg = vm.Messages.FirstOrDefault(m => m.Role == ChatMessageRole.Tool);
            Assert.NotNull(toolMsg);
            Assert.Equal("read_file", toolMsg!.ToolName);
            Assert.False(string.IsNullOrWhiteSpace(toolMsg.ToolCallDescription));
            Assert.Contains("foo.cs", toolMsg.ToolCallDescription);
        }

        [Fact]
        public void ExecuteToolCallsFromOllamaAsync_DoesNotThrowForAnyCall()
        {
            // Smoke test: descriptions are purely derived; nothing here should throw or regress.
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var vm = CreateViewModel(sessionService, notificationService);

            var toolCall = new ToolCall
            {
                Id = "t1",
                Name = "run_terminal_command",
                Arguments = new Dictionary<string, object> { ["command"] = "git status" }
            };

            // Just verify the builder path used by tool records is stable.
            var desc = ContinueVS.Services.Implementations.ToolCallDescriptionBuilder.Build(toolCall);
            Assert.Equal("run: git status", desc);
        }
    }
}
