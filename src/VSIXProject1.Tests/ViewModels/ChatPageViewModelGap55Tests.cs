#nullable enable

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
using Newtonsoft.Json;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for gap55_4: Tool call routing and execution flow (Ollama ToolCallSchema handling).
    /// </summary>
    public class ChatPageViewModelGap55Tests
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

        public ChatPageViewModelGap55Tests()
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

            // Default setup for mode config registry
            var defaultModeConfig = new ModeConfig
            {
                AllowToolLoop = true,
                AllowWriteTools = true,
                ExportsPlanFile = false,
                AllowPhaseExecution = false
            };
            _mockModeConfigRegistry
                .Setup(x => x.GetConfig(It.IsAny<ChatMode>()))
                .Returns(defaultModeConfig);

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
                null,
                null,
                null,
                null,
                _mockModeConfigRegistry.Object,
                null);
        }

        [Fact]
        public async Task ExecuteToolCallsFromOllamaAsync_DeserializesJsonArguments()
        {
            // Arrange
            var toolCallId = "call_123";
            var toolName = "test_tool";
            var argsDict = new Dictionary<string, object> { { "param1", "value1" } };
            var argsJson = JsonConvert.SerializeObject(argsDict);

            var toolCall = new ToolCallSchema
            {
                Id = toolCallId,
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = toolName,
                    Arguments = argsJson
                }
            };

            var expectedResult = new ToolResult
            {
                ToolName = toolName,
                Output = "Success",
                IsSuccess = true
            };

            _mockToolService
                .Setup(x => x.InvokeAsync(toolName, It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(
                new List<ToolCallSchema> { toolCall },
                CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal(toolCallId, results[0].ToolCallId);
            Assert.Equal(toolName, results[0].ToolName);
            _mockToolService.Verify(
                x => x.InvokeAsync(
                    toolName,
                    It.Is<IDictionary<string, object>>(d => d["param1"].ToString() == "value1"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteToolCallsFromOllamaAsync_HandleTimeout()
        {
            // Arrange
            var toolName = "slow_tool";
            var toolCall = new ToolCallSchema
            {
                Id = "call_timeout",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = toolName,
                    Arguments = "{}"
                }
            };

            _mockToolService
                .Setup(x => x.InvokeAsync(toolName, It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(
                new List<ToolCallSchema> { toolCall },
                CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal("call_timeout", results[0].ToolCallId);
            Assert.Contains("timed out", results[0].Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteToolCallsFromOllamaAsync_ErrorRecovery()
        {
            // Arrange
            var failingTool = new ToolCallSchema
            {
                Id = "call_fail",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "failing_tool",
                    Arguments = "{}"
                }
            };

            var successTool = new ToolCallSchema
            {
                Id = "call_success",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "working_tool",
                    Arguments = "{}"
                }
            };

            _mockToolService
                .Setup(x => x.InvokeAsync("failing_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Tool failed"));

            _mockToolService
                .Setup(x => x.InvokeAsync("working_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ToolResult { ToolName = "working_tool", Output = "Success", IsSuccess = true });

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(
                new List<ToolCallSchema> { failingTool, successTool },
                CancellationToken.None);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains("Error", results[0].Output);  // First tool failed
            Assert.Equal("Success", results[1].Output);   // Second tool succeeded
        }

        [Fact]
        public void GetAvailableToolsForCurrentMode_AskModeExcludesWriteTools()
        {
            // Arrange
            var tools = new List<ToolDefinition>
            {
                new ToolDefinition { Name = "read_file" },
                new ToolDefinition { Name = "list_files" },
                new ToolDefinition { Name = "search_code" },
                new ToolDefinition { Name = "write_files" },
                new ToolDefinition { Name = "delete_file" },
                new ToolDefinition { Name = "run_command" }
            };

            _mockToolService
                .Setup(x => x.GetAvailableTools())
                .Returns(tools);

            _viewModel.CurrentMode = ChatMode.Ask;

            // Act
            var result = _viewModel.GetAvailableToolsForCurrentMode();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains(result, t => t.Name == "read_file");
            Assert.Contains(result, t => t.Name == "list_files");
            Assert.Contains(result, t => t.Name == "search_code");
            Assert.DoesNotContain(result, t => t.Name == "write_files");
            Assert.DoesNotContain(result, t => t.Name == "delete_file");
            Assert.DoesNotContain(result, t => t.Name == "run_command");
        }

        [Fact]
        public void GetAvailableToolsForCurrentMode_AgentModeIncludesAll()
        {
            // Arrange
            var tools = new List<ToolDefinition>
            {
                new ToolDefinition { Name = "read_file" },
                new ToolDefinition { Name = "write_files" },
                new ToolDefinition { Name = "run_command" }
            };

            _mockToolService
                .Setup(x => x.GetAvailableTools())
                .Returns(tools);

            _viewModel.CurrentMode = ChatMode.Agent;

            // Act
            var result = _viewModel.GetAvailableToolsForCurrentMode();

            // Assert
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public async Task ExecuteToolCallsFromOllamaAsync_ToolResultLinkedWithToolCallId()
        {
            // Arrange
            var toolCallId = "call_789";
            var toolCall = new ToolCallSchema
            {
                Id = toolCallId,
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "test_tool",
                    Arguments = "{}"
                }
            };

            var toolResult = new ToolResult
            {
                ToolName = "test_tool",
                Output = "Test result",
                IsSuccess = true
            };

            _mockToolService
                .Setup(x => x.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(toolResult);

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(
                new List<ToolCallSchema> { toolCall },
                CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal(toolCallId, results[0].ToolCallId);
        }

        [Fact]
        public async Task ExecuteToolCallsFromOllamaAsync_EmptyArgumentsHandledGracefully()
        {
            // Arrange
            var toolCall = new ToolCallSchema
            {
                Id = "call_empty",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "no_args_tool",
                    Arguments = null
                }
            };

            var result = new ToolResult
            {
                ToolName = "no_args_tool",
                Output = "Success",
                IsSuccess = true
            };

            _mockToolService
                .Setup(x => x.InvokeAsync("no_args_tool", It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            // Act
            var results = await _viewModel.ExecuteToolCallsFromOllamaAsync(
                new List<ToolCallSchema> { toolCall },
                CancellationToken.None);

            // Assert
            Assert.Single(results);
            Assert.Equal("call_empty", results[0].ToolCallId);
            _mockToolService.Verify(
                x => x.InvokeAsync(
                    "no_args_tool",
                    It.Is<IDictionary<string, object>>(d => d.Count == 0),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
