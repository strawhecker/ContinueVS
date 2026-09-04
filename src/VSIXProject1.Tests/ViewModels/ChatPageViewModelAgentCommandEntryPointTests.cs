#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// gap60: Tests for ExecuteAgentCommandAsync() public entry point.
    /// Validates mode checks, command validation, dispatcher dispatch, 
    /// session message addition, and error handling.
    /// </summary>
    public class ChatPageViewModelAgentCommandEntryPointTests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            return new Mock<ILlmService>();
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new System.Collections.Generic.List<ContextItem>());
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            return new Mock<IToolService>();
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            return new Mock<INotificationService>();
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new System.Collections.Generic.List<ModelInfo>
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

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var uiState = new UIState
            {
                ToolSettings = new System.Collections.Generic.Dictionary<string, ToolPolicy>()
            };
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(uiState);
            return mock;
        }

        /// <summary>
        /// Test 1: ExecuteAgentCommandAsync_ThrowsInvalidOperation_IfNotInAgentMode
        /// Validates that calling ExecuteAgentCommandAsync in non-Agent mode throws InvalidOperationException
        /// </summary>
        [Fact]
        public async Task ExecuteAgentCommandAsync_ThrowsInvalidOperation_IfNotInAgentMode()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                agentCommandDispatcher: new Mock<IAgentCommandDispatcher>().Object);

            // Set to Ask mode (not Agent)
            viewModel.CurrentMode = ChatMode.Ask;

            var commandName = "test_command";
            var commandArgs = new Dictionary<string, object> { { "arg1", "value1" } };
            var ct = CancellationToken.None;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => viewModel.ExecuteAgentCommandAsync(commandName, commandArgs, ct));
            Assert.Contains("Agent commands only valid in Agent mode", ex.Message);
        }

        /// <summary>
        /// Test 2: ExecuteAgentCommandAsync_ThrowsArgumentException_IfCommandNameEmpty
        /// Validates that empty command name throws ArgumentException
        /// </summary>
        [Fact]
        public async Task ExecuteAgentCommandAsync_ThrowsArgumentException_IfCommandNameEmpty()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                agentCommandDispatcher: new Mock<IAgentCommandDispatcher>().Object);

            // Set to Agent mode
            viewModel.CurrentMode = ChatMode.Agent;

            var commandName = "";
            var commandArgs = new Dictionary<string, object>();
            var ct = CancellationToken.None;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => viewModel.ExecuteAgentCommandAsync(commandName, commandArgs, ct));
            Assert.Equal("commandName", ex.ParamName);
        }

        /// <summary>
        /// Test 3: ExecuteAgentCommandAsync_CallsDispatcher_WithCorrectParameters
        /// Validates that the dispatcher is invoked with correct command name, args, mode, and cancellation token
        /// </summary>
        [Fact]
        public async Task ExecuteAgentCommandAsync_CallsDispatcher_WithCorrectParameters()
        {
            // Arrange
            var mockDispatcher = new Mock<IAgentCommandDispatcher>();
            var dispatchResult = new ToolResult { Output = "Command executed" };
            mockDispatcher.Setup(x => x.DispatchAgentCommandAsync(
                    It.IsAny<string>(), 
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<ChatMode>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(dispatchResult);

            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                agentCommandDispatcher: mockDispatcher.Object);

            viewModel.CurrentMode = ChatMode.Agent;
            var commandName = "test_command";
            var commandArgs = new Dictionary<string, object> { { "arg1", "value1" } };
            var ct = CancellationToken.None;

            // Act
            await viewModel.ExecuteAgentCommandAsync(commandName, commandArgs, ct);

            // Assert
            mockDispatcher.Verify(
                x => x.DispatchAgentCommandAsync(
                    "test_command",
                    It.Is<IDictionary<string, object>>(d => d.Count == 1),
                    ChatMode.Agent,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Test 4: ExecuteAgentCommandAsync_AddsToolResultToSession
        /// Validates that successful command result is added to session and UI messages
        /// </summary>
        [Fact]
        public async Task ExecuteAgentCommandAsync_AddsToolResultToSession()
        {
            // Arrange
            var mockDispatcher = new Mock<IAgentCommandDispatcher>();
            var dispatchResult = new ToolResult { Output = "Test output" };
            mockDispatcher.Setup(x => x.DispatchAgentCommandAsync(
                    It.IsAny<string>(), 
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<ChatMode>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(dispatchResult);

            var mockSessionService = CreateSessionServiceMock();
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                mockSessionService.Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                agentCommandDispatcher: mockDispatcher.Object);

            viewModel.CurrentMode = ChatMode.Agent;
            var commandName = "test_command";
            var commandArgs = new Dictionary<string, object>();
            var ct = CancellationToken.None;

            // Act
            await viewModel.ExecuteAgentCommandAsync(commandName, commandArgs, ct);

            // Assert
            mockSessionService.Verify(
                x => x.AddMessageAsync(It.Is<ChatMessage>(m =>
                    m.Role == ChatMessageRole.Tool &&
                    m.Content == "Test output")),
                Times.Once);

            // Verify message added to UI
            Assert.Single(viewModel.Messages);
            var uiMessage = viewModel.Messages[0];
            Assert.Equal(ChatMessageRole.Tool, uiMessage.Role);
            Assert.Equal("Test output", uiMessage.Content);
        }

        /// <summary>
        /// Test 5: ExecuteAgentCommandAsync_HandlesErrorResponse
        /// Validates that error responses in command output are handled correctly
        /// </summary>
        [Fact]
        public async Task ExecuteAgentCommandAsync_HandlesErrorResponse()
        {
            // Arrange
            var mockDispatcher = new Mock<IAgentCommandDispatcher>();
            var dispatchResult = new ToolResult { Output = "Error: Command failed" };
            mockDispatcher.Setup(x => x.DispatchAgentCommandAsync(
                    It.IsAny<string>(), 
                    It.IsAny<IDictionary<string, object>>(),
                    It.IsAny<ChatMode>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(dispatchResult);

            var mockSessionService = CreateSessionServiceMock();
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                mockSessionService.Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                agentCommandDispatcher: mockDispatcher.Object);

            viewModel.CurrentMode = ChatMode.Agent;
            var commandName = "test_command";
            var commandArgs = new Dictionary<string, object>();
            var ct = CancellationToken.None;

            // Act
            await viewModel.ExecuteAgentCommandAsync(commandName, commandArgs, ct);

            // Assert
            mockSessionService.Verify(
                x => x.AddMessageAsync(It.Is<ChatMessage>(m =>
                    m.Role == ChatMessageRole.Tool)),
                Times.Once);

            var uiMessage = viewModel.Messages[0];
            Assert.Equal(ToolInvocationStatus.Failed, uiMessage.InvocationStatus);
        }
    }
}

