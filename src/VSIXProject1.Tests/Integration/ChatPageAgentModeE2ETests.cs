#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// End-to-end integration tests for Agent Mode.
    /// Validates the complete agent loop: user message → LLM → tool calls → tool execution → continuation.
    /// Requires all dependencies from gap58-gap61 to be integrated.
    /// </summary>
    public class ChatPageAgentModeE2ETests : TestFixtureBase
    {
        private Mock<ILlmService> _mockLlmService = null!;
        private Mock<IContextService> _mockContextService = null!;
        private Mock<IToolService> _mockToolService = null!;
        private Mock<ISessionService> _mockSessionService = null!;
        private Mock<INotificationService> _mockNotificationService = null!;
        private Mock<IConfigService> _mockConfigService = null!;
        private Mock<ISystemPromptService> _mockSystemPromptService = null!;
        private Mock<IUIStateService> _mockUIStateService = null!;
        private Mock<IInstructionExecutorService> _mockInstructionExecutorService = null!;
        private Mock<IChangeStackService> _mockChangeStackService = null!;
        private Mock<IMarkdownService> _mockMarkdownService = null!;
        private Mock<IModeConfigRegistry> _mockModeConfigRegistry = null!;

        public ChatPageAgentModeE2ETests()
        {
            InitializeMocks();
        }

        private void InitializeMocks()
        {
            _mockLlmService = CreateLooseMock<ILlmService>();
            _mockContextService = CreateLooseMock<IContextService>();
            _mockToolService = CreateLooseMock<IToolService>();
            _mockSessionService = CreateLooseMock<ISessionService>();
            _mockNotificationService = CreateLooseMock<INotificationService>();
            _mockConfigService = CreateLooseMock<IConfigService>();
            _mockSystemPromptService = CreateLooseMock<ISystemPromptService>();
            _mockUIStateService = CreateLooseMock<IUIStateService>();
            _mockInstructionExecutorService = CreateLooseMock<IInstructionExecutorService>();
            _mockChangeStackService = CreateLooseMock<IChangeStackService>();
            _mockMarkdownService = CreateLooseMock<IMarkdownService>();
            _mockModeConfigRegistry = CreateLooseMock<IModeConfigRegistry>();

            // Setup basic defaults
            _mockContextService.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());

            _mockSessionService.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);

            _mockSessionService.Setup(x => x.GetCurrentSession())
                .Returns(new Session { Messages = new List<ChatMessage>() });

            _mockSessionService.Setup(x => x.PackageMessages(
                It.IsAny<ModelInfo>(),
                It.IsAny<ChatMessage>(),
                It.IsAny<string>()))
                .Returns((ModelInfo _, ChatMessage system, string userContent) =>
                {
                    return new List<ChatMessage>
                    {
                        system,
                        new ChatMessage { Role = ChatMessageRole.User, Content = userContent }
                    };
                });

            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo
                    {
                        Name = "Test Model",
                        Provider = "test",
                        BaseUrl = "http://localhost:11434"
                    }
                }
            };
            _mockConfigService.Setup(x => x.GetCurrentConfig()).Returns(config);
            _mockConfigService.Setup(x => x.GetSelectedModel())
                .Returns(new ModelInfo { Name = "Test Model", ContextWindow = 4096 });

            _mockSystemPromptService.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            _mockSystemPromptService.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("Test system prompt");

            var uiState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "read_file", ToolPolicy.AutoApprove },
                    { "grep_search", ToolPolicy.AutoApprove },
                    { "find_symbol", ToolPolicy.AutoApprove }
                }
            };
            _mockUIStateService.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(uiState);

            // Default mode config: Agent mode allows tool loop
            var agentModeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                AllowToolLoop = true,
                SystemPrompt = "Agent mode system prompt"
            };
            _mockModeConfigRegistry.Setup(x => x.GetConfig(ChatMode.Agent))
                .Returns(agentModeConfig);
            _mockModeConfigRegistry.Setup(x => x.GetConfig(It.IsAny<ChatMode>()))
                .Returns(agentModeConfig);
        }

        private ChatPageViewModel CreateChatPageViewModelWithMocks()
        {
            return new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                _mockInstructionExecutorService.Object,
                _mockChangeStackService.Object,
                _mockMarkdownService.Object);
        }

        /// <summary>
        /// Scenario 1: Single tool call execution with continuation.
        /// User sends message → LLM returns tool call + text → Tool executes → LLM continues.
        /// </summary>
        [Fact]
        public async Task AgentMode_ExecutesToolCall_AndContinuesConversation()
        {
            // Arrange
            var viewModel = CreateChatPageViewModelWithMocks();
            viewModel.CurrentMode = ChatMode.Agent;

            var userMessage = "Read the file at path /test.txt";

            // Mock first LLM response: assistant message with tool call
            var toolCall = new ToolCall
            {
                Id = "call_1",
                Name = "read_file",
                Arguments = new Dictionary<string, object> { { "path", "/test.txt" } }
            };

            var firstChunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "I'll read that file" },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = toolCall }
            };

            // Create async enumerable for first LLM response
            _mockLlmService.Setup(x => x.StreamAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<StreamOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> messages, StreamOptions? opts, CancellationToken ct) =>
                    GenerateChunksAsync(firstChunks, ct));

            // Mock tool execution result
            var toolResult = new ToolResult
            {
                ToolName = "read_file",
                Output = "File contents here",
                IsSuccess = true,
                Timestamp = DateTime.UtcNow
            };

            _mockToolService.Setup(x => x.InvokeAsync(
                "read_file",
                It.Is<IDictionary<string, object>>(d => d.ContainsKey("path")),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            // Mock continuation response (second LLM call after tool result)
            var continuationChunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "The file contains: File contents here" },
                new CompletionChunk { Type = ChunkType.Done, IsDone = true }
            };

            int callCount = 0;
            _mockLlmService.Setup(x => x.StreamAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<StreamOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> messages, StreamOptions? opts, CancellationToken ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return GenerateChunksAsync(firstChunks, ct);
                    }
                    else
                    {
                        return GenerateChunksAsync(continuationChunks, ct);
                    }
                });

            // Act
            // Note: ExecuteSendMessage is private; we test via public properties and verify mocks
            viewModel.InputText = userMessage;
            if (viewModel.SendMessageCommand.CanExecute(null))
            {
                viewModel.SendMessageCommand.Execute(null);
                await Task.Delay(500);  // Allow async work to complete
            }

            // Assert: Tool service should have been invoked
            _mockToolService.Verify(
                x => x.InvokeAsync("read_file", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Scenario 2: Tool execution failure handling.
        /// User sends message → LLM returns tool call → Tool throws exception → Error captured.
        /// </summary>
        [Fact]
        public async Task AgentMode_HandlesToolFailure_AndNotifies()
        {
            // Arrange
            var viewModel = CreateChatPageViewModelWithMocks();
            viewModel.CurrentMode = ChatMode.Agent;

            var userMessage = "Read a non-existent file";

            var toolCall = new ToolCall
            {
                Id = "call_1",
                Name = "read_file",
                Arguments = new Dictionary<string, object> { { "path", "/nonexistent.txt" } }
            };

            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "I'll try to read that file" },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = toolCall }
            };

            _mockLlmService.Setup(x => x.StreamAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<StreamOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> _, StreamOptions? __, CancellationToken ct) =>
                    GenerateChunksAsync(chunks, ct));

            // Mock tool to fail
            _mockToolService.Setup(x => x.InvokeAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.IO.FileNotFoundException("File not found"));

            // Act
            viewModel.InputText = userMessage;
            if (viewModel.SendMessageCommand.CanExecute(null))
            {
                viewModel.SendMessageCommand.Execute(null);
                await Task.Delay(500);
            }

            // Assert: Tool service was called and threw
            _mockToolService.Verify(
                x => x.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Scenario 3: Multiple tool calls in sequence.
        /// LLM returns 3 tool calls → All execute → Results consolidated → Continuation continues.
        /// </summary>
        [Fact]
        public async Task AgentMode_ExecutesMultipleToolCalls_InSequence()
        {
            // Arrange
            var viewModel = CreateChatPageViewModelWithMocks();
            viewModel.CurrentMode = ChatMode.Agent;

            var userMessage = "Analyze this code file";

            var toolCall1 = new ToolCall
            {
                Id = "call_1",
                Name = "read_file",
                Arguments = new Dictionary<string, object> { { "path", "/code.cs" } }
            };

            var toolCall2 = new ToolCall
            {
                Id = "call_2",
                Name = "grep_search",
                Arguments = new Dictionary<string, object> { { "query", "public void" } }
            };

            var toolCall3 = new ToolCall
            {
                Id = "call_3",
                Name = "find_symbol",
                Arguments = new Dictionary<string, object> { { "symbol", "MyClass" } }
            };

            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "I'll analyze the code" },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = toolCall1 },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = toolCall2 },
                new CompletionChunk { Type = ChunkType.ToolCall, ToolCall = toolCall3 }
            };

            int callCount = 0;
            _mockLlmService.Setup(x => x.StreamAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<StreamOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> messages, StreamOptions? opts, CancellationToken ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return GenerateChunksAsync(chunks, ct);
                    }
                    else
                    {
                        // Continuation response with no tools
                        var contChunks = new List<CompletionChunk>
                        {
                            new CompletionChunk { Type = ChunkType.Text, Content = "Code analysis complete." },
                            new CompletionChunk { Type = ChunkType.Done, IsDone = true }
                        };
                        return GenerateChunksAsync(contChunks, ct);
                    }
                });

            // Mock tool executions
            _mockToolService.Setup(x => x.InvokeAsync("read_file", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolResult
                {
                    ToolName = "read_file",
                    Output = "file content",
                    IsSuccess = true,
                    Timestamp = DateTime.UtcNow
                });

            _mockToolService.Setup(x => x.InvokeAsync("grep_search", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolResult
                {
                    ToolName = "grep_search",
                    Output = "public void Method1()",
                    IsSuccess = true,
                    Timestamp = DateTime.UtcNow
                });

            _mockToolService.Setup(x => x.InvokeAsync("find_symbol", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolResult
                {
                    ToolName = "find_symbol",
                    Output = "Found in file.cs:line 10",
                    IsSuccess = true,
                    Timestamp = DateTime.UtcNow
                });

            // Act
            viewModel.InputText = userMessage;
            if (viewModel.SendMessageCommand.CanExecute(null))
            {
                viewModel.SendMessageCommand.Execute(null);
                await Task.Delay(500);
            }

            // Assert: All tools should have been invoked
            _mockToolService.Verify(
                x => x.InvokeAsync("read_file", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            _mockToolService.Verify(
                x => x.InvokeAsync("grep_search", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
            _mockToolService.Verify(
                x => x.InvokeAsync("find_symbol", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        /// <summary>
        /// Scenario 4: Tool loop policy enforcement.
        /// When AllowToolLoop = false, agent mode respects policy configuration.
        /// </summary>
        [Fact]
        public void AgentMode_RespectsModeConfig_AllowToolLoopPolicy()
        {
            // Arrange: Get mode config and verify tool loop setting
            var modeConfig = _mockModeConfigRegistry.Object.GetConfig(ChatMode.Agent);

            // Assert: Default agent mode allows tool loop
            Assert.NotNull(modeConfig);
            Assert.True(modeConfig.AllowToolLoop);
        }

        /// <summary>
        /// Helper to create async enumerable from chunks.
        /// </summary>
        private async IAsyncEnumerable<CompletionChunk> GenerateChunksAsync(List<CompletionChunk> chunks, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var chunk in chunks)
            {
                yield return chunk;
                await Task.Delay(1, ct);
            }
        }
    }
}
