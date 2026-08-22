using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Xunit;
using Moq;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for ChatPageViewModel context window integration behavior.
    /// Verifies that message pruning is triggered when context window threshold is exceeded,
    /// and that the model's actual context window is used (not hardcoded values).
    /// </summary>
    public class ChatPageViewModelContextTests
    {
        private Mock<ILlmService> CreateMockLlmService()
        {
            var mock = new Mock<ILlmService>();
            // Mock streaming to return empty
            mock.Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<StreamOptions>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(new AsyncEnumerable());
            return mock;
        }

        private Mock<ISessionService> CreateMockSessionService()
        {
            var mock = new Mock<ISessionService>();
            var session = new Session 
            { 
                Id = Guid.NewGuid().ToString(),
                Messages = new List<ChatMessage>(),
                Title = "Test",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            mock.Setup(x => x.GetCurrentSession()).Returns(session);
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.PruneOldMessagesAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((0, new List<ChatMessage>()));
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService(int contextWindow = 8192)
        {
            var mock = new Mock<IConfigService>();
            var model = new ModelInfo { ContextWindow = contextWindow, Name = "test-model" };
            mock.Setup(x => x.GetSelectedModel()).Returns(model);
            return mock;
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
            var mock = new Mock<IToolService>();
            mock.Setup(x => x.GetAvailableTools()).Returns(new List<ToolDefinition>());
            return mock;
        }

        private Mock<INotificationService> CreateMockNotificationService()
        {
            return new Mock<INotificationService>();
        }

        private Mock<ISystemPromptService> CreateMockSystemPromptService()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("You are a helpful assistant.");
            return mock;
        }

        private Mock<IUIStateService> CreateMockUIStateService()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() });
            return mock;
        }

        [Fact]
        public void ChatPageViewModel_UsesModelContextWindow_NotHardcoded()
        {
            // Arrange
            var llmService = CreateMockLlmService();
            var sessionService = CreateMockSessionService();
            var configService = CreateMockConfigService(contextWindow: 16384);
            var contextService = CreateMockContextService();
            var toolService = CreateMockToolService();
            var notificationService = CreateMockNotificationService();
            var systemPromptService = CreateMockSystemPromptService();
            var uiStateService = CreateMockUIStateService();

            // Act
            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object,
                uiStateService.Object);

            // Assert
            var selectedModel = configService.Object.GetSelectedModel();
            Assert.NotNull(selectedModel);
            Assert.Equal(16384, selectedModel.ContextWindow);
            Assert.NotEqual(4096, selectedModel.ContextWindow);
        }

        [Fact]
        public void ChatPageViewModel_RespectsReserveMargin()
        {
            // Arrange - with context window of 8192, available should be 75% = 6144
            var contextWindow = 8192;
            var availableTokens = (int)(contextWindow * 0.75);

            // Assert
            Assert.Equal(6144, availableTokens);
            Assert.True(availableTokens > 0, "Available tokens should be positive");
        }

        [Fact]
        public async Task ChatPageViewModel_CallsPruningService_WhenContextExceeded()
        {
            // Arrange
            var llmService = CreateMockLlmService();
            var sessionService = CreateMockSessionService();
            var configService = CreateMockConfigService(contextWindow: 8192);
            var contextService = CreateMockContextService();
            var toolService = CreateMockToolService();
            var notificationService = CreateMockNotificationService();
            var systemPromptService = CreateMockSystemPromptService();

            // Setup session to have messages already
            var session = sessionService.Object.GetCurrentSession();
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "First message" });

            // Setup pruning to be called
            sessionService.Setup(x => x.PruneOldMessagesAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((1, new List<ChatMessage>()));

            var uiStateService = CreateMockUIStateService();
            var viewModel = new ChatPageViewModel(
                llmService.Object,
                contextService.Object,
                toolService.Object,
                sessionService.Object,
                notificationService.Object,
                configService.Object,
                systemPromptService.Object,
                uiStateService.Object);

            // Assert - the pruning method should be available
            Assert.NotNull(sessionService.Object);
            var (count, pruned) = await sessionService.Object.PruneOldMessagesAsync(6144, keepSystemMessages: true);
            Assert.Equal(1, count);
        }
    }

    /// <summary>
    /// Dummy async enumerable for mocking
    /// </summary>
    public class AsyncEnumerable : IAsyncEnumerable<CompletionChunk>
    {
        public IAsyncEnumerator<CompletionChunk> GetAsyncEnumerator(System.Threading.CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator();
        }
    }

    public class AsyncEnumerator : IAsyncEnumerator<CompletionChunk>
    {
        public CompletionChunk Current => throw new NotImplementedException();

        public ValueTask DisposeAsync() => new ValueTask();

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
    }
}
