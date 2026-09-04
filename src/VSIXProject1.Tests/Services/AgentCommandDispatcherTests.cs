using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Comprehensive xUnit test suite for AgentCommandDispatcher (gap58).
    /// Tests cover: mode policy enforcement, tool routing, error handling, and audit logging.
    /// </summary>
    public class AgentCommandDispatcherTests
    {
        private Mock<IToolService> CreateToolServiceMock()
        {
            return new Mock<IToolService>();
        }

        private Mock<ILlmService> CreateLlmServiceMock()
        {
            return new Mock<ILlmService>();
        }

        private Mock<IModeConfigRegistry> CreateModeConfigRegistryMock()
        {
            return new Mock<IModeConfigRegistry>();
        }

        private Mock<IBridgeLogger> CreateLoggerMock()
        {
            return new Mock<IBridgeLogger>();
        }

        // ====================================================================
        // TEST 1: DispatchAgentCommand_RoutesToToolService_ForReadFileInAgentMode
        // ====================================================================

        [Fact]
        public async Task DispatchAgentCommand_RoutesToToolService_ForReadFileInAgentMode()
        {
            // Arrange
            var mockToolService = CreateToolServiceMock();
            var mockLlmService = CreateLlmServiceMock();
            var mockRegistry = CreateModeConfigRegistryMock();
            var mockLogger = CreateLoggerMock();

            var mockLogger_Setup = mockLogger.Setup(l => l.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var agentModeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                SystemPrompt = "You are an autonomous agent.",
                AllowToolLoop = true,
                AllowWriteTools = true
            };

            mockRegistry.Setup(r => r.GetConfig(ChatMode.Agent)).Returns(agentModeConfig);

            var expectedResult = new ToolResult
            {
                ToolName = "read_file",
                IsSuccess = true,
                Output = "file contents"
            };

            mockToolService.Setup(t => t.InvokeAsync(
                "read_file",
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            var dispatcher = new AgentCommandDispatcher(mockToolService.Object, mockLlmService.Object, 
                mockRegistry.Object, mockLogger.Object);

            var args = new Dictionary<string, object> { { "filepath", "test.txt" } };

            // Act
            var result = await dispatcher.DispatchAgentCommandAsync("read_file", args, ChatMode.Agent);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal("read_file", result.ToolName);
            mockToolService.Verify(t => t.InvokeAsync("read_file", It.IsAny<IDictionary<string, object>>(), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ====================================================================
        // TEST 2: DispatchAgentCommand_ThrowsInvalidOperation_ForWriteFileInAskMode
        // ====================================================================

        [Fact]
        public async Task DispatchAgentCommand_ThrowsInvalidOperation_ForWriteFileInAskMode()
        {
            // Arrange
            var mockToolService = CreateToolServiceMock();
            var mockLlmService = CreateLlmServiceMock();
            var mockRegistry = CreateModeConfigRegistryMock();
            var mockLogger = CreateLoggerMock();

            var mockLogger_Setup = mockLogger.Setup(l => l.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var askModeConfig = new ModeConfig
            {
                Mode = ChatMode.Ask,
                SystemPrompt = "You are a helpful assistant.",
                AllowToolLoop = false,
                AllowWriteTools = false
            };

            mockRegistry.Setup(r => r.GetConfig(ChatMode.Ask)).Returns(askModeConfig);

            var dispatcher = new AgentCommandDispatcher(mockToolService.Object, mockLlmService.Object,
                mockRegistry.Object, mockLogger.Object);

            var args = new Dictionary<string, object> { { "filepath", "test.txt" }, { "contents", "new content" } };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAgentCommandAsync("write_file", args, ChatMode.Ask));

            mockToolService.Verify(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // ====================================================================
        // TEST 3: DispatchAgentCommand_AllowsAllTools_InAgentMode
        // ====================================================================

        [Fact]
        public async Task DispatchAgentCommand_AllowsAllTools_InAgentMode()
        {
            // Arrange
            var mockToolService = CreateToolServiceMock();
            var mockLlmService = CreateLlmServiceMock();
            var mockRegistry = CreateModeConfigRegistryMock();
            var mockLogger = CreateLoggerMock();

            var mockLogger_Setup = mockLogger.Setup(l => l.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var agentModeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                SystemPrompt = "You are an autonomous agent.",
                AllowToolLoop = true,
                AllowWriteTools = true,
                AllowPhaseExecution = true
            };

            mockRegistry.Setup(r => r.GetConfig(ChatMode.Agent)).Returns(agentModeConfig);

            var writeResult = new ToolResult
            {
                ToolName = "write_file",
                IsSuccess = true,
                Output = "file written successfully"
            };

            mockToolService.Setup(t => t.InvokeAsync(
                "write_file",
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(writeResult);

            var dispatcher = new AgentCommandDispatcher(mockToolService.Object, mockLlmService.Object,
                mockRegistry.Object, mockLogger.Object);

            var args = new Dictionary<string, object> { { "filepath", "test.txt" }, { "contents", "new content" } };

            // Act
            var result = await dispatcher.DispatchAgentCommandAsync("write_file", args, ChatMode.Agent);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccess);
            Assert.Equal("write_file", result.ToolName);
            mockToolService.Verify(t => t.InvokeAsync("write_file", It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ====================================================================
        // TEST 4: DispatchAgentCommand_PropagatesToolServiceException_WithFailedResult
        // ====================================================================

        [Fact]
        public async Task DispatchAgentCommand_PropagatesToolServiceException_WithFailedResult()
        {
            // Arrange
            var mockToolService = CreateToolServiceMock();
            var mockLlmService = CreateLlmServiceMock();
            var mockRegistry = CreateModeConfigRegistryMock();
            var mockLogger = CreateLoggerMock();

            var mockLogger_Setup = mockLogger.Setup(l => l.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var agentModeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                SystemPrompt = "You are an autonomous agent.",
                AllowToolLoop = true,
                AllowWriteTools = true
            };

            mockRegistry.Setup(r => r.GetConfig(ChatMode.Agent)).Returns(agentModeConfig);

            mockToolService.Setup(t => t.InvokeAsync(
                "read_file",
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.IO.FileNotFoundException("File not found"));

            var dispatcher = new AgentCommandDispatcher(mockToolService.Object, mockLlmService.Object,
                mockRegistry.Object, mockLogger.Object);

            var args = new Dictionary<string, object> { { "filepath", "nonexistent.txt" } };

            // Act
            var result = await dispatcher.DispatchAgentCommandAsync("read_file", args, ChatMode.Agent);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Equal("read_file", result.ToolName);
            Assert.Contains("Tool execution failed", result.Output);
        }

        // ====================================================================
        // TEST 5: DispatchAgentCommand_LogsDispatchToFileLogger_WithAuditTag
        // ====================================================================

        [Fact]
        public async Task DispatchAgentCommand_LogsDispatchToFileLogger_WithAuditTag()
        {
            // Arrange
            var mockToolService = CreateToolServiceMock();
            var mockLlmService = CreateLlmServiceMock();
            var mockRegistry = CreateModeConfigRegistryMock();
            var mockLogger = CreateLoggerMock();

            mockLogger.Setup(l => l.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()))
                .Returns(Task.CompletedTask);

            var agentModeConfig = new ModeConfig
            {
                Mode = ChatMode.Agent,
                SystemPrompt = "You are an autonomous agent.",
                AllowToolLoop = true,
                AllowWriteTools = true
            };

            mockRegistry.Setup(r => r.GetConfig(ChatMode.Agent)).Returns(agentModeConfig);

            var result = new ToolResult
            {
                ToolName = "read_file",
                IsSuccess = true,
                Output = "file contents"
            };

            mockToolService.Setup(t => t.InvokeAsync(
                "read_file",
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);

            var dispatcher = new AgentCommandDispatcher(mockToolService.Object, mockLlmService.Object,
                mockRegistry.Object, mockLogger.Object);

            var args = new Dictionary<string, object> { { "filepath", "test.txt" } };

            // Act
            await dispatcher.DispatchAgentCommandAsync("read_file", args, ChatMode.Agent);

            // Assert
            mockLogger.Verify(l => l.WriteDebugAsync(It.Is<string>(s => s.Contains("[gap58-dispatch]")), It.IsAny<IReadOnlyDictionary<string, object>>()), 
                Times.AtLeastOnce);
            mockLogger.Verify(l => l.WriteDebugAsync(It.Is<string>(s => s.Contains("read_file")), It.IsAny<IReadOnlyDictionary<string, object>>()), 
                Times.AtLeastOnce);
        }
    }
}
