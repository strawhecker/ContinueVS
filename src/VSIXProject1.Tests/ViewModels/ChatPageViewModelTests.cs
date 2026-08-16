#nullable enable

using System;
using System.Collections.ObjectModel;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelTests : TestFixtureBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_InitializesCollections()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.Messages);
            Assert.NotNull(viewModel.SelectedContext);
            Assert.IsType<ObservableCollection<ChatMessage>>(viewModel.Messages);
            Assert.IsType<ObservableCollection<ContextItem>>(viewModel.SelectedContext);
            Assert.False(viewModel.IsStreaming);
        }

        [Fact]
        public void Constructor_WithNullLlmService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ChatPageViewModel(
                    null!,
                    mockContextService.Object,
                    mockToolService.Object,
                    mockSessionService.Object,
                    mockNotificationService.Object));
        }

        [Fact]
        public void InputText_CanBeSet()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            const string testInput = "Hello, AI!";

            // Act
            viewModel.InputText = testInput;

            // Assert
            Assert.Equal(testInput, viewModel.InputText);
        }

        [Fact]
        public void IsStreaming_CanBeSet()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Act
            viewModel.IsStreaming = true;

            // Assert
            Assert.True(viewModel.IsStreaming);
        }

        [Fact]
        public void StreamingResponse_CanBeSet()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            const string testResponse = "This is a test response.";

            // Act
            viewModel.StreamingResponse = testResponse;

            // Assert
            Assert.Equal(testResponse, viewModel.StreamingResponse);
        }

        [Fact]
        public void Commands_AreNotNull()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Assert
            Assert.NotNull(viewModel.SendMessageCommand);
            Assert.NotNull(viewModel.CancelCommand);
            Assert.NotNull(viewModel.AddContextCommand);
        }

        [Fact]
        public void CanAddMessage_ToMessages()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            var testMessage = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Test message"
            };

            // Act
            viewModel.Messages.Add(testMessage);

            // Assert
            Assert.Single(viewModel.Messages);
            Assert.Equal("Test message", viewModel.Messages[0].Content);
        }

        [Fact]
        public void CurrentMode_Default_IsAsk()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Assert
            Assert.Equal(ChatMode.Ask, viewModel.CurrentMode);
        }

        [Fact]
        public void SetModeCommand_ChangeToAgent_UpdatesProperty()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Act
            viewModel.SetModeCommand.Execute(ChatMode.Agent);

            // Assert
            Assert.Equal(ChatMode.Agent, viewModel.CurrentMode);
        }

        [Fact]
        public void SetModeCommand_ChangeToPlan_UpdatesProperty()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object);

            // Act
            viewModel.SetModeCommand.Execute(ChatMode.Plan);

            // Assert
            Assert.Equal(ChatMode.Plan, viewModel.CurrentMode);
        }
    }
}
