using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Moq;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for the New Chat command (gap47).
    /// Verifies CreateNewSessionAsync is called, UI state is cleared, and
    /// the command is disabled while streaming.
    /// </summary>
    public class ChatPageViewModelNewChatTests
    {
        private Mock<ILlmService> CreateMockLlmService()
        {
            var mock = new Mock<ILlmService>();
            mock.Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<StreamOptions>(), It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncEnumerableAsync());
            return mock;
        }

        private async IAsyncEnumerable<CompletionChunk> CreateAsyncEnumerableAsync()
        {
            yield return new CompletionChunk { Type = ChunkType.Text, Content = "Test response" };
            await Task.Yield();
        }

        private Mock<IContextService> CreateMockContextService()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private Mock<IToolService> CreateMockToolService()
        {
            return new Mock<IToolService>();
        }

        private Mock<ISessionService> CreateMockSessionService()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.GetCurrentSession())
                .Returns(new Session { Id = "test-session", Messages = new List<ChatMessage>(), ToolCallsExecuted = 0 });
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.CreateNewSessionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private Mock<INotificationService> CreateMockNotificationService()
        {
            return new Mock<INotificationService>();
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "test-model", Provider = "ollama", ContextWindow = 8192 }
                },
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            mock.Setup(x => x.GetSelectedModel()).Returns(config.Models[0]);
            mock.Setup(x => x.GetDefaultModeAsync()).ReturnsAsync(0);
            mock.Setup(x => x.GetDefaultPolicyAsync()).ReturnsAsync(ContinuationPolicy.Interactive);
            return mock;
        }

        private Mock<ISystemPromptService> CreateMockSystemPromptService()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("Test prompt");
            return mock;
        }

        private Mock<IUIStateService> CreateMockUIStateService()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(new UIState());
            return mock;
        }

        private Mock<IChangeStackService> CreateMockChangeStackService()
        {
            var mock = new Mock<IChangeStackService>();
            mock.Setup(x => x.CreateChangeStack()).Returns(Guid.NewGuid().ToString());
            return mock;
        }

        [Fact(DisplayName = "gap47: NewChatCommand calls CreateNewSessionAsync when not streaming")]
        public async Task NewChatCommand_WhenNotStreaming_CallsCreateNewSessionAsync()
        {
            // Arrange
            var mockSession = CreateMockSessionService();
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                mockSession.Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IInstructionExecutorService>().Object,
                CreateMockChangeStackService().Object,
                new Mock<IMarkdownService>().Object
            );

            // Act
            viewModel.NewChatCommand.Execute(null);
            await Task.Delay(50); // allow fire-and-forget to complete

            // Assert
            mockSession.Verify(x => x.CreateNewSessionAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact(DisplayName = "gap47: NewChatCommand clears InputText, SelectedContext, and Messages")]
        public async Task NewChatCommand_WhenNotStreaming_ClearsInputTextAndContext()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IInstructionExecutorService>().Object,
                CreateMockChangeStackService().Object,
                new Mock<IMarkdownService>().Object
            );
            viewModel.InputText = "some prior message";
            viewModel.SelectedContext.Add(new ContextItem { Type = ContextItemType.File, FilePath = "foo.cs" });
            viewModel.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "hello" });

            // Act
            viewModel.NewChatCommand.Execute(null);
            await Task.Delay(50); // allow fire-and-forget to complete

            // Assert
            Assert.Equal(string.Empty, viewModel.InputText);
            Assert.Empty(viewModel.SelectedContext);
            Assert.Empty(viewModel.Messages);
        }

        [Fact(DisplayName = "gap47: NewChatCommand CanExecute returns false when streaming")]
        public void NewChatCommand_WhenStreaming_CannotExecute()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IInstructionExecutorService>().Object,
                CreateMockChangeStackService().Object,
                new Mock<IMarkdownService>().Object
            );

            // Act
            viewModel.IsStreaming = true;

            // Assert
            Assert.False(viewModel.NewChatCommand.CanExecute(null));
        }
    }
}
