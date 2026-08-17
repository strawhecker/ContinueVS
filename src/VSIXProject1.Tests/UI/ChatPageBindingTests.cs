#nullable enable

using System.Collections.ObjectModel;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.UI
{
    public class ChatPageBindingTests : DataBindingTestBase
    {
        private Mock<IConfigService> CreateConfigServiceMock()
        {
            var mock = CreateLooseMock<IConfigService>();
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Default Model", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            mock.Setup(m => m.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = CreateLooseMock<ISystemPromptService>();
            mock.Setup(m => m.GetPromptForMode(It.IsAny<string>())).Returns("Test system prompt");
            return mock;
        }

        [Fact]
        public void InputText_PropertyChanged_FiresNotification()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.InputText = "Hello, world!";

            // Assert
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.InputText));
            Assert.Equal("Hello, world!", viewModel.InputText);
        }

        [Fact]
        public void IsStreaming_PropertyChanged_FiresNotification()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.IsStreaming = true;

            // Assert
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.IsStreaming));
            Assert.True(viewModel.IsStreaming);
        }

        [Fact]
        public void StreamingResponse_PropertyChanged_FiresNotification()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.StreamingResponse = "Response text";

            // Assert
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.StreamingResponse));
            Assert.Equal("Response text", viewModel.StreamingResponse);
        }

        [Fact]
        public void Messages_CollectionChanged_FiresNotificationOnAdd()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var collectionTracker = new CollectionChangeTracker(viewModel.Messages);

            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };

            // Act
            viewModel.Messages.Add(message);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.Messages);
            Assert.Contains(message, viewModel.Messages);
        }

        [Fact]
        public void Messages_CollectionChanged_FiresNotificationOnRemove()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            v
                mockSystemPromptService.Object);ar message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };
            viewModel.Messages.Add(message);

            using var collectionTracker = new CollectionChangeTracker(viewModel.Messages);

            // Act
            viewModel.Messages.Remove(message);

            // Assert
            AssertCollectionRemoved(collectionTracker, count: 1);
            Assert.Empty(viewModel.Messages);
        }

        [Fact]
        public void SelectedContext_CollectionChanged_FiresNotificationOnAdd()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var collectionTracker = new CollectionChangeTracker(viewModel.SelectedContext);

            var contextItem = new ContextItem { FilePath = "test.cs", Type = ContextItemType.File };

            // Act
            viewModel.SelectedContext.Add(contextItem);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.SelectedContext);
            Assert.Contains(contextItem, viewModel.SelectedContext);
        }

        [Fact]
        public void SendMessageCommand_CanBeExecuted_WithInputText()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            /
                mockSystemPromptService.Object);/ Act
            viewModel.InputText = "Test message";

            // Assert
            Assert.NotNull(viewModel.SendMessageCommand);
            Assert.True(viewModel.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public void SendMessageCommand_CannotBeExecuted_WithoutInputText()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            /
                mockSystemPromptService.Object);/ Act & Assert
            Assert.NotNull(viewModel.SendMessageCommand);
            Assert.False(viewModel.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public void CancelCommand_CanBeExecutedWhenStreaming()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            /
                mockSystemPromptService.Object);/ Act
            viewModel.IsStreaming = true;

            // Assert
            Assert.NotNull(viewModel.CancelCommand);
            Assert.True(viewModel.CancelCommand.CanExecute(null));
        }

        [Fact]
        public void CancelCommand_CannotBeExecutedWhenNotStreaming()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            /
                mockSystemPromptService.Object);/ Act & Assert
            Assert.NotNull(viewModel.CancelCommand);
            Assert.False(viewModel.CancelCommand.CanExecute(null));
        }

        [Fact]
        public void AddContextCommand_CanBeExecuted()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            /
                mockSystemPromptService.Object);/ Act & Assert
            Assert.NotNull(viewModel.AddContextCommand);
            Assert.True(viewModel.AddContextCommand.CanExecute("test"));
        }

        [Fact]
        public void MultiplePropertyChanges_AllFireNotifications()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,

            u
                mockSystemPromptService.Object);sing var tracker = new PropertyChangedTracker(viewModel);

            // Act
            viewModel.InputText = "Message 1";
            viewModel.IsStreaming = true;
            viewModel.StreamingResponse = "Response 1";

            // Assert
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.InputText));
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.IsStreaming));
            AssertPropertyChanged(tracker, nameof(ChatPageViewModel.StreamingResponse));
        }
    }
}






