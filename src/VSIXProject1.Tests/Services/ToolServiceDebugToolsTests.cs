#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests the gap92_3 Tier-2 debug evaluate/mutate tools at the ToolService boundary:
    /// default-disabled (absent from GetAvailableTools), routed to a fake IDebuggerService
    /// when invoked, and surfaced state-echo/rejection results.
    /// </summary>
    public class ToolServiceDebugToolsTests
    {
        private readonly Mock<IIdeService> _ideServiceMock;
        private readonly Mock<IConfigService> _configServiceMock;
        private readonly Mock<IDebuggerService> _debuggerMock;

        public ToolServiceDebugToolsTests()
        {
            _ideServiceMock = new Mock<IIdeService>();
            _configServiceMock = new Mock<IConfigService>();
            _configServiceMock.Setup(s => s.GetEnabledTools())
                .Returns((IEnumerable<ToolDefinition>)BuiltInToolsRegistry.GetAllBuiltInTools());
            _configServiceMock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());
            _configServiceMock.Setup(s => s.GetCurrentConfig()).Returns(new ContinueConfig());
            _debuggerMock = new Mock<IDebuggerService>();
        }

        private ToolService CreateService(bool withDebugger = true)
        {
            return new ToolService(
                _ideServiceMock.Object,
                _configServiceMock.Object,
                debuggerService: withDebugger ? _debuggerMock.Object : null);
        }

        private static readonly string[] DebugToolNames =
        {
            "debug_evaluate", "debug_set_value", "debug_memory_read",
            "debug_memory_write", "debug_run_to_cursor", "debug_thread_set_state"
        };

        [Theory]
        [InlineData(ChatMode.Agent)]
        [InlineData(ChatMode.Debug)]
        [InlineData(ChatMode.Plan)]
        [InlineData(ChatMode.Ask)]
        [InlineData(ChatMode.Reason)]
        public void GetAvailableTools_NeverExposesDebugTools(ChatMode mode)
        {
            var service = CreateService();
            var tools = service.GetAvailableTools(mode).ToList();
            foreach (var name in DebugToolNames)
                Assert.DoesNotContain(tools, t => t.Name == name);
        }

        [Fact]
        public void GetTool_ReturnsDebugTool_DebugModeOnly_Disabled()
        {
            var service = CreateService();

            foreach (var name in DebugToolNames)
            {
                var tool = service.GetTool(name);
                Assert.NotNull(tool);
                Assert.False(tool!.IsEnabled);
                Assert.Contains(ChatMode.Debug, tool.SupportedModes);
                Assert.DoesNotContain(ChatMode.Agent, tool.SupportedModes);
            }
        }

        [Fact]
        public async Task InvokeDebugEvaluate_RoutesToDebuggerService()
        {
            _debuggerMock
                .Setup(s => s.EvaluateAsync(It.IsAny<DebugSessionState>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DebugInspectionResult<VariableInfo>.Success(
                    new DebugSessionState { Mode = "break", IsDebuggerActive = true },
                    new VariableInfo { Name = "x", Type = "int", Value = "int = 42" }));

            var service = CreateService();
            var result = await service.InvokeAsync("debug_evaluate", new Dictionary<string, object>
            {
                { "threadId", 5 }, { "frameIndex", 0 }, { "expression", "x" }
            });

            Assert.True(result.IsSuccess);
            Assert.Contains("42", result.Output);
            _debuggerMock.Verify(
                s => s.EvaluateAsync(It.IsAny<DebugSessionState>(), 5, 0, "x", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeDebugEvaluate_Rejected_ReturnsStateEchoReason()
        {
            _debuggerMock
                .Setup(s => s.EvaluateAsync(It.IsAny<DebugSessionState>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DebugInspectionResult<VariableInfo>.Rejected(
                    new DebugSessionState { Mode = "run", IsDebuggerActive = true }, "requires-break-mode"));

            var service = CreateService();
            var result = await service.InvokeAsync("debug_evaluate", new Dictionary<string, object>
            {
                { "threadId", 5 }, { "frameIndex", 0 }, { "expression", "x" }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("requires-break-mode", result.Output);
            Assert.Contains("run", result.Output);
        }

        [Fact]
        public async Task InvokeDebugSetValue_RoutesToDebuggerService()
        {
            _debuggerMock
                .Setup(s => s.SetValueAsync(It.IsAny<DebugSessionState>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DebugInspectionResult<bool>.Success(
                    new DebugSessionState { Mode = "break", IsDebuggerActive = true }, true));

            var service = CreateService();
            var result = await service.InvokeAsync("debug_set_value", new Dictionary<string, object>
            {
                { "threadId", 5 }, { "frameIndex", 0 }, { "name", "x" }, { "value", "42" }
            });

            Assert.True(result.IsSuccess);
            _debuggerMock.Verify(
                s => s.SetValueAsync(It.IsAny<DebugSessionState>(), 5, 0, "x", "42", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task InvokeDebugMemoryRead_ReportsNotExposed()
        {
            _debuggerMock
                .Setup(s => s.MemoryReadAsync(It.IsAny<DebugSessionState>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DebugInspectionResult<string>.Rejected(
                    new DebugSessionState { Mode = "break", IsDebuggerActive = true }, "not-exposed-by-dte"));

            var service = CreateService();
            var result = await service.InvokeAsync("debug_memory_read", new Dictionary<string, object>
            {
                { "address", "0x1234" }, { "length", 16 }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("not-exposed-by-dte", result.Output);
        }

        [Fact]
        public async Task InvokeDebugThreadSetState_Thaw_RoutesToThaw()
        {
            _debuggerMock
                .Setup(s => s.ThawThreadAsync(It.IsAny<DebugSessionState>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DebugInspectionResult<bool>.Success(
                    new DebugSessionState { Mode = "break", IsDebuggerActive = true }, true));

            var service = CreateService();
            var result = await service.InvokeAsync("debug_thread_set_state", new Dictionary<string, object>
            {
                { "threadId", 9 }, { "action", "thaw" }
            });

            Assert.True(result.IsSuccess);
            _debuggerMock.Verify(
                s => s.ThawThreadAsync(It.IsAny<DebugSessionState>(), 9, It.IsAny<CancellationToken>()),
                Times.Once);
            _debuggerMock.Verify(
                s => s.FreezeThreadAsync(It.IsAny<DebugSessionState>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task InvokeDebugRunToCursor_NullDebugger_ReturnsUnavailable()
        {
            var service = CreateService(withDebugger: false);
            var result = await service.InvokeAsync("debug_run_to_cursor", new Dictionary<string, object>());
            Assert.False(result.IsSuccess);
            Assert.Contains("Debugger service not available", result.Output);
        }
    }
}
