using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for gap69: Execution impact message creation and display.
    /// Validates that execution impact messages are created after tool execution.
    /// </summary>
    public class ChatPageViewModelExecutionImpactTests
    {
        private readonly Mock<ILlmService> _mockLlmService;
        private readonly Mock<IContextService> _mockContextService;
        private readonly Mock<IToolService> _mockToolService;
        private readonly Mock<ISessionService> _mockSessionService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IConfigService> _mockConfigService;
        private readonly Mock<ISystemPromptService> _mockSystemPromptService;
        private readonly Mock<IUIStateService> _mockUIStateService;
        private readonly Mock<IInstructionExecutorService> _mockInstructionExecutorService;
        private readonly Mock<IChangeStackService> _mockChangeStackService;
        private readonly Mock<IMarkdownService> _mockMarkdownService;
        private readonly Mock<IModeConfigRegistry> _mockModeConfigRegistry;
        private readonly ChatPageViewModel _viewModel;

        public ChatPageViewModelExecutionImpactTests()
        {
            _mockLlmService = new Mock<ILlmService>();
            _mockContextService = new Mock<IContextService>();
            _mockToolService = new Mock<IToolService>();
            _mockSessionService = new Mock<ISessionService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockConfigService = new Mock<IConfigService>();
            _mockSystemPromptService = new Mock<ISystemPromptService>();
            _mockUIStateService = new Mock<IUIStateService>();
            _mockInstructionExecutorService = new Mock<IInstructionExecutorService>();
            _mockChangeStackService = new Mock<IChangeStackService>();
            _mockMarkdownService = new Mock<IMarkdownService>();
            _mockModeConfigRegistry = new Mock<IModeConfigRegistry>();

            // Setup mock dependencies
            var mockAgentCommandDispatcher = new Mock<IAgentCommandDispatcher>();
            var mockLlmQuestionService = new Mock<ILlmQuestionService>();

            // Mock session to capture added messages
            var messages = new List<ChatMessage>();
            _mockSessionService.Setup(s => s.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Callback<ChatMessage>(msg => messages.Add(msg))
                .Returns(Task.CompletedTask);

            _mockSessionService.Setup(s => s.GetCurrentSession())
                .Returns(new Session { Messages = messages });

            _mockConfigService.Setup(c => c.GetCurrentConfig())
                .Returns(new ContinueConfig { Models = new List<ModelInfo>() });

            _viewModel = new ChatPageViewModel(
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
                _mockMarkdownService.Object,
                mockLlmQuestionService.Object,
                null,
                null,
                null,
                _mockModeConfigRegistry.Object);
        }

        [Fact]
        public async Task ExecuteToolCallsAsync_EmitsExecutionImpactMessage_AfterCompletion()
        {
            // Arrange
            var toolCalls = new List<ToolCallSchema>
            {
                new ToolCallSchema
                {
                    Id = "call_1",
                    Function = new ToolCallFunction { Name = "read_file", Arguments = "{\"path\": \"/test.txt\"}" }
                }
            };

            var toolResult = new ToolResult
            {
                ToolName = "read_file",
                Output = "file contents",
                ToolCallId = "call_1"
            };

            _mockToolService.Setup(ts => ts.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            var messages = new List<ChatMessage>();
            var addMessageCalls = 0;
            _mockSessionService.Setup(s => s.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Callback<ChatMessage>(msg =>
                {
                    messages.Add(msg);
                    addMessageCalls++;
                })
                .Returns(Task.CompletedTask);

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(toolCalls, CancellationToken.None);

            // Assert
            Assert.NotEmpty(results);
            Assert.Single(results);
            Assert.Equal("read_file", results[0].ToolName);
        }

        [Fact]
        public void ExecutionImpactMessage_CreationWithPhases()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var phases = new List<PhaseExecutionResult>
            {
                new PhaseExecutionResult
                {
                    PhaseId = "tool_0",
                    Status = ExecutionStatus.Succeeded,
                    Evidence = "read_file",
                    StartTime = startTime,
                    EndTime = startTime.AddSeconds(1)
                },
                new PhaseExecutionResult
                {
                    PhaseId = "tool_1",
                    Status = ExecutionStatus.Succeeded,
                    Evidence = "write_file",
                    StartTime = startTime.AddSeconds(1),
                    EndTime = startTime.AddSeconds(2)
                }
            };

            // Act
            var message = new ExecutionImpactMessage();
            message.Initialize(phases);

            // Assert
            Assert.Equal(ChatMessageRole.System, message.Role);
            Assert.Equal(2, message.ToolsRun);
            Assert.Equal(ExecutionStatus.Succeeded, message.Status);
        }

        [Fact]
        public void ExecutionImpactMessage_SummaryTextFormatted()
        {
            // Arrange
            var message = new ExecutionImpactMessage();
            message.FilesChanged = 3;
            message.ToolsRun = 2;
            message.DurationMs = 5000;
            message.Status = ExecutionStatus.Succeeded;

            // Act
            var summary = message.GetSummaryText();

            // Assert
            Assert.Contains("✓", summary);
            Assert.Contains("3 files modified", summary);
            Assert.Contains("2 tools executed", summary);
            Assert.Contains("5.0s", summary);
        }

        [Fact]
        public void ExecutionImpactMessage_FailureIndicator()
        {
            // Arrange
            var phases = new List<PhaseExecutionResult>
            {
                new PhaseExecutionResult { Status = ExecutionStatus.Failed, PhaseId = "phase1", StartTime = DateTime.UtcNow }
            };

            var message = new ExecutionImpactMessage();

            // Act
            message.Initialize(phases);

            // Assert
            Assert.Equal(ExecutionStatus.Failed, message.Status);
            Assert.Contains("✗", message.GetSummaryText());
        }
    }
}
