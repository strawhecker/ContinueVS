#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelGap81PruneTests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            return new Mock<ILlmService>();
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            return new Mock<IToolService>();
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.DeleteMessageAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
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

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Llama 3.1", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("Test system prompt");
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() });
            return mock;
        }

        private static ChatPageViewModel CreateViewModel(
            Mock<ISessionService> sessionService,
            Mock<INotificationService> notificationService)
        {
            return new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                sessionService.Object,
                notificationService.Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                new Mock<ILlmQuestionService>().Object);
        }

        [Fact]
        public void SoftDeleteCommand_MarksMessageDeletedInPlace()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Message" };
            viewModel.Messages.Add(msg);

            viewModel.SoftDeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(100);

            Assert.Single(viewModel.Messages);
            Assert.True(msg.IsDeleted, "Message should be marked IsDeleted in place.");
            sessionService.Verify(x => x.SoftDeleteMessageAsync("1"), Times.Once);
        }

        [Fact]
        public void SoftDeleteCommand_IgnoresNullOrMissingId()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            viewModel.SoftDeleteMessageCommand.Execute(null);
            viewModel.SoftDeleteMessageCommand.Execute("");
            viewModel.SoftDeleteMessageCommand.Execute("missing-id");

            Assert.Single(viewModel.Messages);
            Assert.False(msg.IsDeleted);
            sessionService.Verify(x => x.SoftDeleteMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void SoftDeleteCommand_ErrorRollsBackAndNotifies()
        {
            var sessionService = CreateSessionServiceMock();
            sessionService.Setup(x => x.SoftDeleteMessageAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Soft delete failed"));
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            viewModel.SoftDeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(200);

            Assert.False(msg.IsDeleted, "Tombstone should roll back to false on service error.");
            Assert.Single(viewModel.Messages);
            notificationService.Verify(
                x => x.ShowNotificationAsync(
                    "Prune Failed",
                    It.Is<string>(s => s.Contains("Soft delete failed")),
                    NotificationType.Error),
                Times.Once);
        }

        [Fact]
        public void UndeleteCommand_RestoresMessageVisibility()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test", IsDeleted = true };
            viewModel.Messages.Add(msg);

            viewModel.UndeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(100);

            Assert.False(msg.IsDeleted, "Undelete should clear the tombstone.");
            Assert.Single(viewModel.Messages);
            sessionService.Verify(x => x.UndeleteMessageAsync("1"), Times.Once);
        }

        [Fact]
        public void UndeleteCommand_IgnoresNullOrMissingId()
        {
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test", IsDeleted = true };
            viewModel.Messages.Add(msg);

            viewModel.UndeleteMessageCommand.Execute(null);
            viewModel.UndeleteMessageCommand.Execute("missing-id");

            Assert.True(msg.IsDeleted, "No change when id invalid/missing.");
            sessionService.Verify(x => x.UndeleteMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void UndeleteCommand_ErrorRollsBackAndNotifies()
        {
            var sessionService = CreateSessionServiceMock();
            sessionService.Setup(x => x.UndeleteMessageAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Undelete failed"));
            var notificationService = CreateNotificationServiceMock();
            var viewModel = CreateViewModel(sessionService, notificationService);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test", IsDeleted = true };
            viewModel.Messages.Add(msg);

            viewModel.UndeleteMessageCommand.Execute("1");
            System.Threading.Thread.Sleep(200);

            Assert.True(msg.IsDeleted, "Tombstone should remain true on undelete service error.");
            Assert.Single(viewModel.Messages);
            notificationService.Verify(
                x => x.ShowNotificationAsync(
                    "Undelete Failed",
                    It.Is<string>(s => s.Contains("Undelete failed")),
                    NotificationType.Error),
                Times.Once);
        }
    }
}
