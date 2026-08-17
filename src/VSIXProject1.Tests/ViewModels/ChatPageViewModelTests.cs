#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
        private Mock<IConfigService> CreateConfigServiceMock()
        {
            var mock = CreateLooseMock<IConfigService>();
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Model 1", Provider = "ollama", BaseUrl = "http://localhost:11434" },
                    new ModelInfo { Name = "Model 2", Provider = "openai", BaseUrl = "https://api.openai.com" }
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
        public void Constructor_WithValidDependencies_InitializesCollections()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`n var mockSystemPromptService = CreateSystemPromptServiceMock();

            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockSystemPromptService.Object);

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.Messages);
            Assert.NotNull(viewModel.SelectedContext);
            Assert.NotNull(viewModel.AvailableModels);
            Assert.IsType<ObservableCollection<ChatMessage>>(viewModel.Messages);
            Assert.IsType<ObservableCollection<ContextItem>>(viewModel.SelectedContext);
            Assert.IsType<ObservableCollection<ModelInfo>>(viewModel.AvailableModels);
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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`n var mockSystemPromptService = CreateSystemPromptServiceMock();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ChatPageViewModel(
                    null!,
                    mockContextService.Object,
                    mockToolService.Object,
                    mockSessionService.Object,
                    mockNotificationService.Object,
                    mockConfigService.Object,
                    mockSystemPromptService.Object));
        }

        [Fact]
        public async Task LoadModelsAsync_PopulatesAvailableModels()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();
            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockSystemPromptService.Object);

            // Wait for async initialization
            await Task.Delay(100);

            // Assert
            Assert.NotEmpty(viewModel.AvailableModels);
            Assert.Equal(2, viewModel.AvailableModels.Count);

            Assert.Equal("Model 2", viewModel.AvailableModels[1].Name);
        }

        [Fact]
        public async Task SelectedModel_DefaultsToFirstModel()
        {
            // Arrange
            var mockLlmService = CreateLooseMock<ILlmService>();
            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();
            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

            await Task.Delay(100);

            // Assert
            Assert.NotNull(viewModel.SelectedModel);
            Assert.Equal("Model 1", viewModel.SelectedModel.Name);
        }

        [Fact]
        public async Task SelectedModel_CanBeChanged()
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
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

            await Task.Delay(100);

            // Act
            viewModel.SelectedModel = viewModel.AvailableModels[1];

            // Assert
            Assert.Equal("Model 2", viewModel.SelectedModel.Name);
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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();
            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();
            // Act
            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,
                mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

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
            var mockConfigService = CreateConfigServiceMock();`n            var mockSystemPromptService = CreateSystemPromptServiceMock();`nvar viewModel = new ChatPageViewModel(

            var mockSystemPromptService = CreateSystemPromptServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object,`n`t mockSystemPromptService.Object);

            // Act
            viewModel.SetModeCommand.Execute(ChatMode.Plan);

            // Assert
            Assert.Equal(ChatMode.Plan, viewModel.CurrentMode);
        }
    }
}







