#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelDeleteMessageTests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            var mock = new Mock<ILlmService>();
            return mock;
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
            var mock = new Mock<IToolService>();
            return mock;
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.DeleteMessageAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
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
                    new ModelInfo 
                    { 
                        Name = "Llama 3.1",
                        Provider = "ollama",
                        BaseUrl = "http://localhost:11434"
                    }
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
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("Test system prompt");
            return mock;
        }

        [Fact]
        public void DeleteMessageCommand_RemovesMessageFromCollection()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);

            // Add 3 messages to the collection
            var msg1 = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Message 1" };
            var msg2 = new ChatMessage { Id = "2", Role = ChatMessageRole.User, Content = "Message 2" };
            var msg3 = new ChatMessage { Id = "3", Role = ChatMessageRole.User, Content = "Message 3" };

            viewModel.Messages.Add(msg1);
            viewModel.Messages.Add(msg2);
            viewModel.Messages.Add(msg3);

            Assert.Equal(3, viewModel.Messages.Count);

            // Act
            viewModel.DeleteMessageCommand.Execute("2");

            // Assert
            Assert.Equal(2, viewModel.Messages.Count);
            Assert.DoesNotContain(msg2, viewModel.Messages);
            Assert.Contains(msg1, viewModel.Messages);
            Assert.Contains(msg3, viewModel.Messages);
        }

        [Fact]
        public void DeleteMessageCommand_CallsSessionServiceDeleteMessageAsync()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);

            var messageId = "test-message-id";
            var msg = new ChatMessage { Id = messageId, Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            // Act
            viewModel.DeleteMessageCommand.Execute(messageId);

            // Give async operation time to complete
            System.Threading.Thread.Sleep(100);

            // Assert
            sessionService.Verify(x => x.DeleteMessageAsync(messageId), Times.Once);
        }

        [Fact]
        public void DeleteMessageCommand_HandlesServiceErrorByRollingBackMessage()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            // Mock service to throw error
            sessionService.Setup(x => x.DeleteMessageAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Delete failed"));

            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);

            var messageId = "test-message-id";
            var msg = new ChatMessage { Id = messageId, Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            // Act
            viewModel.DeleteMessageCommand.Execute(messageId);

            // Give async operation time to complete
            System.Threading.Thread.Sleep(200);

            // Assert - message should be re-added to collection
            Assert.Single(viewModel.Messages);
            Assert.Contains(msg, viewModel.Messages);

            // Notification service should have been called with error
            notificationService.Verify(
                x => x.ShowNotificationAsync(
                    "Delete Failed",
                    It.Is<string>(s => s.Contains("Delete failed")),
                    NotificationType.Error),
                Times.Once);
        }

        [Fact]
        public void DeleteMessageCommand_IgnoresNullOrEmptyMessageId()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            // Act - pass null/empty message ID
            viewModel.DeleteMessageCommand.Execute(null);
            viewModel.DeleteMessageCommand.Execute("");
            viewModel.DeleteMessageCommand.Execute("   ");

            // Assert - message should remain
            Assert.Single(viewModel.Messages);
            Assert.Contains(msg, viewModel.Messages);

            // Service should never be called
            sessionService.Verify(x => x.DeleteMessageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void DeleteMessageCommand_IgnoresNonExistentMessageId()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object);

            var msg = new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(msg);

            // Act - try to delete non-existent message
            viewModel.DeleteMessageCommand.Execute("non-existent-id");

            // Assert - message should remain
            Assert.Single(viewModel.Messages);
            Assert.Contains(msg, viewModel.Messages);

            // Service should not be called for non-existent message
            sessionService.Verify(x => x.DeleteMessageAsync(It.IsAny<string>()), Times.Never);
        }
    }
}
