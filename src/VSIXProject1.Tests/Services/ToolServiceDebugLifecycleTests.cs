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
    /// Tests the gap94 debug-session lifecycle tools at the ToolService boundary:
    /// present in Debug mode, default-enabled, routed to the fake IDebuggerService, surfaced
    /// session handle, absent-service -> unavailable, and correctness of each routed action.
    /// </summary>
    public class ToolServiceDebugLifecycleTests
    {
        private readonly Mock<IIdeService> _ideServiceMock;
        private readonly Mock<IConfigService> _configServiceMock;
        private readonly Mock<IDebuggerService> _debuggerMock;

        public ToolServiceDebugLifecycleTests()
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

        private static readonly string[] LifecycleToolNames =
        {
            "debug_start", "debug_stop", "debug_restart",
            "ide_attach_to_process", "debug_select_session"
        };

        [Fact]
        public void GetTool_ReturnsLifecycleTools_DebugOnly_Enabled()
        {
            var service = CreateService();
            foreach (var name in LifecycleToolNames)
            {
                var tool = service.GetTool(name);
                Assert.NotNull(tool);
                Assert.True(tool!.IsEnabled);
                Assert.Contains(ChatMode.Debug, tool.SupportedModes);
                Assert.DoesNotContain(ChatMode.Agent, tool.SupportedModes);
            }
        }

        [Fact]
        public void GetAvailableTools_DebugMode_IncludesLifecycleTools()
        {
            var service = CreateService();
            var tools = service.GetAvailableTools(ChatMode.Debug).ToList();
            foreach (var name in LifecycleToolNames)
                Assert.Contains(tools, t => t.Name == name);
        }

        [Fact]
        public void GetAvailableTools_AgentMode_ExcludesLifecycleTools()
        {
            var service = CreateService();
            var tools = service.GetAvailableTools(ChatMode.Agent).ToList();
            foreach (var name in LifecycleToolNames)
                Assert.DoesNotContain(tools, t => t.Name == name);
        }

        [Fact]
        public async Task InvokeDebugStart_RoutesAndSurfacesSessionHandle()
        {
            _debuggerMock
                .Setup(s => s.StartDebuggingAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DebugSessionInfo { SessionId = "proc:11", ProcessId = 11, ProcessName = "app.exe", Mode = "run" });

            var service = CreateService();
            var result = await service.InvokeAsync("debug_start", new Dictionary<string, object>
            {
                { "project", "MyProj" }, { "launchProfile", "local" }
            });

            Assert.True(result.IsSuccess);
            Assert.Contains("proc:11", result.Output);
            Assert.Contains("run", result.Output);
            _debuggerMock.Verify(s => s.StartDebuggingAsync("MyProj", "local", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeDebugStart_NoStartupProject_ReturnsError()
        {
            _debuggerMock
                .Setup(s => s.StartDebuggingAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DebugSessionInfo?)null);

            var service = CreateService();
            var result = await service.InvokeAsync("debug_start", new Dictionary<string, object>());

            Assert.False(result.IsSuccess);
            Assert.Contains("start", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvokeDebugStop_RoutesAndSurfacesSessionHandle()
        {
            _debuggerMock
                .Setup(s => s.StopDebuggingAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DebugSessionInfo { SessionId = "proc:2", ProcessId = 2, ProcessName = "x.exe", Mode = "break" });

            var service = CreateService();
            var result = await service.InvokeAsync("debug_stop", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains("proc:2", result.Output);
            _debuggerMock.Verify(s => s.StopDebuggingAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeDebugRestart_RoutesAndSurfacesSessionHandle()
        {
            _debuggerMock
                .Setup(s => s.RestartDebuggingAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DebugSessionInfo { SessionId = "proc:3", ProcessId = 3, ProcessName = "y.exe", Mode = "run" });

            var service = CreateService();
            var result = await service.InvokeAsync("debug_restart", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains("proc:3", result.Output);
            _debuggerMock.Verify(s => s.RestartDebuggingAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeAttachToProcess_RoutesAndSurfacesSessionHandle()
        {
            _debuggerMock
                .Setup(s => s.AttachToProcessAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DebugSessionInfo { SessionId = "proc:99", ProcessId = 99, ProcessName = "z.exe", Mode = "run" });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_attach_to_process", new Dictionary<string, object> { { "processId", 99 } });

            Assert.True(result.IsSuccess);
            Assert.Contains("proc:99", result.Output);
            _debuggerMock.Verify(s => s.AttachToProcessAsync(99, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeDebugSelectSession_BindsAndSurfacesHandle()
        {
            _debuggerMock
                .Setup(s => s.SelectSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DebugSessionInfo { SessionId = "proc:5", ProcessId = 5, ProcessName = "app", Mode = "break" });

            var service = CreateService();
            var result = await service.InvokeAsync("debug_select_session", new Dictionary<string, object> { { "sessionId", "proc:5" } });

            Assert.True(result.IsSuccess);
            Assert.Contains("proc:5", result.Output);
            _debuggerMock.Verify(s => s.SelectSessionAsync("proc:5", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvokeDebugSelectSession_UnknownId_ReturnsError()
        {
            _debuggerMock
                .Setup(s => s.SelectSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((DebugSessionInfo?)null);

            var service = CreateService();
            var result = await service.InvokeAsync("debug_select_session", new Dictionary<string, object> { { "sessionId", "proc:999" } });

            Assert.False(result.IsSuccess);
            Assert.Contains("Unknown", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvokeDebugStart_NullDebugger_ReturnsUnavailable()
        {
            var service = CreateService(withDebugger: false);
            var result = await service.InvokeAsync("debug_start", new Dictionary<string, object>());
            Assert.False(result.IsSuccess);
            Assert.Contains("Debugger service not available", result.Output);
        }
    }
}
