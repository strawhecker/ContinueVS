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
    /// Tests the gap95 non-debug DTE IDE tools at the ToolService boundary:
    /// present in Agent/Debug (and all-mode tools in Ask/Plan/Reason), default-enabled,
    /// routed to the fake IIdeService, and correct result surfacing.
    /// </summary>
    public class ToolServiceIdeToolsTests
    {
        private readonly Mock<IIdeService> _ideServiceMock;
        private readonly Mock<IConfigService> _configServiceMock;

        public ToolServiceIdeToolsTests()
        {
            _ideServiceMock = new Mock<IIdeService>();
            _configServiceMock = new Mock<IConfigService>();
            _configServiceMock.Setup(s => s.GetEnabledTools())
                .Returns((IEnumerable<ToolDefinition>)BuiltInToolsRegistry.GetAllBuiltInTools());
            _configServiceMock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());
            _configServiceMock.Setup(s => s.GetCurrentConfig()).Returns(new ContinueConfig());
        }

        private ToolService CreateService()
        {
            return new ToolService(_ideServiceMock.Object, _configServiceMock.Object);
        }

        private static readonly string[] AllIdeToolNames =
        {
            "ide_active_document", "ide_open_file", "ide_navigate_to",
            "ide_goto_definition", "ide_find_symbol", "ide_build",
            "ide_build_configuration", "ide_launch_profile", "ide_output_pane"
        };

        // Tools gated to Agent+Debug only (excluded from Ask/Plan/Reason)
        private static readonly string[] AgentDebugGatedIdeToolNames =
        {
            "ide_navigate_to", "ide_goto_definition", "ide_find_symbol",
            "ide_build", "ide_build_configuration", "ide_launch_profile"
        };

        // Tools available in all modes (read-only)
        private static readonly string[] AllModeIdeToolNames =
        {
            "ide_active_document", "ide_open_file", "ide_output_pane"
        };

        [Fact]
        public void GetTool_ReturnsAllIdeTools_DefaultEnabled()
        {
            var service = CreateService();
            foreach (var name in AllIdeToolNames)
            {
                var tool = service.GetTool(name);
                Assert.NotNull(tool);
                Assert.True(tool!.IsEnabled);
            }
        }

        [Fact]
        public void GetAvailableTools_AgentMode_IncludesAllIdeTools()
        {
            var service = CreateService();
            var tools = service.GetAvailableTools(ChatMode.Agent).ToList();
            foreach (var name in AllIdeToolNames)
                Assert.Contains(tools, t => t.Name == name);
        }

        [Fact]
        public void GetAvailableTools_DebugMode_IncludesAllIdeTools()
        {
            var service = CreateService();
            var tools = service.GetAvailableTools(ChatMode.Debug).ToList();
            foreach (var name in AllIdeToolNames)
                Assert.Contains(tools, t => t.Name == name);
        }

        [Fact]
        public void GetAvailableTools_AskPlanReason_ExcludeAgentDebugGatedIdeTools()
        {
            var service = CreateService();
            foreach (var mode in new[] { ChatMode.Ask, ChatMode.Plan, ChatMode.Reason })
            {
                var tools = service.GetAvailableTools(mode).ToList();
                foreach (var name in AgentDebugGatedIdeToolNames)
                    Assert.DoesNotContain(tools, t => t.Name == name);
                foreach (var name in AllModeIdeToolNames)
                    Assert.Contains(tools, t => t.Name == name);
            }
        }

        [Fact]
        public async Task InvokeIdeActiveDocument_RoutesAndSurfacesPath()
        {
            _ideServiceMock
                .Setup(s => s.GetActiveDocumentInfoAsync())
                .ReturnsAsync(new ActiveDocumentInfo { FilePath = @"C:\Foo\Bar.cs", Selection = new Selection { Start = new Location { Line = 5, Column = 3 } } });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_active_document", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains(@"C:\Foo\Bar.cs", result.Output);
            _ideServiceMock.Verify(s => s.GetActiveDocumentInfoAsync(), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeActiveDocument_NoDocument_ReturnsError()
        {
            _ideServiceMock
                .Setup(s => s.GetActiveDocumentInfoAsync())
                .ReturnsAsync((ActiveDocumentInfo?)null);

            var service = CreateService();
            var result = await service.InvokeAsync("ide_active_document", new Dictionary<string, object>());

            Assert.False(result.IsSuccess);
            Assert.Contains("No active document", result.Output);
        }

        [Fact]
        public async Task InvokeIdeOpenFile_RoutesAndSurfacesPath()
        {
            _ideServiceMock
                .Setup(s => s.OpenFileInIdeAsync(It.IsAny<string>()))
                .ReturnsAsync(@"C:\Foo\Bar.cs");

            var service = CreateService();
            var result = await service.InvokeAsync("ide_open_file", new Dictionary<string, object> { { "filepath", @"C:\Foo\Bar.cs" } });

            Assert.True(result.IsSuccess);
            Assert.Contains(@"C:\Foo\Bar.cs", result.Output);
            _ideServiceMock.Verify(s => s.OpenFileInIdeAsync(@"C:\Foo\Bar.cs"), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeNavigateTo_RoutesAndSurfacesPosition()
        {
            _ideServiceMock
                .Setup(s => s.NavigateToAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(true);

            var service = CreateService();
            var result = await service.InvokeAsync("ide_navigate_to", new Dictionary<string, object>
            {
                { "filepath", @"C:\Foo\Bar.cs" }, { "line", 42 }
            });

            Assert.True(result.IsSuccess);
            Assert.Contains(@":42", result.Output);
            _ideServiceMock.Verify(s => s.NavigateToAsync(@"C:\Foo\Bar.cs", 42), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeGotoDefinition_RoutesAndSurfacesLocation()
        {
            _ideServiceMock
                .Setup(s => s.GotoDefinitionAsync())
                .ReturnsAsync(new ActiveDocumentInfo { FilePath = @"C:\Def.cs", Selection = new Selection { Start = new Location { Line = 12 } } });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_goto_definition", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains(@"C:\Def.cs", result.Output);
            _ideServiceMock.Verify(s => s.GotoDefinitionAsync(), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeFindSymbol_RoutesAndSurfacesResolvedLocation()
        {
            _ideServiceMock
                .Setup(s => s.GetActiveDocumentInfoAsync())
                .ReturnsAsync(new ActiveDocumentInfo { FilePath = @"C:\Foo\Bar.cs" });
            _ideServiceMock
                .Setup(s => s.OpenFileInIdeAsync(It.IsAny<string>()))
                .ReturnsAsync(@"C:\Foo\Bar.cs");
            _ideServiceMock
                .Setup(s => s.GotoDefinitionAsync())
                .ReturnsAsync(new ActiveDocumentInfo { FilePath = @"C:\Def.cs", Selection = new Selection { Start = new Location { Line = 7 } } });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_find_symbol", new Dictionary<string, object>
            {
                { "symbol", "MyClass" }, { "filepath", "" }
            });

            Assert.True(result.IsSuccess);
            Assert.Contains("MyClass", result.Output);
            Assert.Contains("C:\\Def.cs:7", result.Output);
        }

        [Fact]
        public async Task InvokeIdeFindSymbol_MissingSymbol_ReturnsError()
        {
            var service = CreateService();
            var result = await service.InvokeAsync("ide_find_symbol", new Dictionary<string, object>
            {
                { "symbol", "" }, { "filepath", "" }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("symbol", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InvokeIdeBuild_RoutesSolutionBuild()
        {
            _ideServiceMock
                .Setup(s => s.BuildSolutionAsync(It.IsAny<string?>()))
                .ReturnsAsync(true);

            var service = CreateService();
            var result = await service.InvokeAsync("ide_build", new Dictionary<string, object> { { "project", "" } });

            Assert.True(result.IsSuccess);
            _ideServiceMock.Verify(s => s.BuildSolutionAsync(null), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeBuildConfiguration_RoutesAndSurfacesConfig()
        {
            _ideServiceMock
                .Setup(s => s.GetActiveBuildConfigurationAsync())
                .ReturnsAsync(new BuildConfigInfo { Name = "Debug", Platform = "Any CPU", IsActive = true });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_build_configuration", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains("Debug", result.Output);
            Assert.Contains("Any CPU", result.Output);
        }

        [Fact]
        public async Task InvokeIdeLaunchProfile_RoutesAndSurfacesProfile()
        {
            _ideServiceMock
                .Setup(s => s.GetLaunchProfileAsync())
                .ReturnsAsync(new LaunchProfileInfo { StartupProject = "MyProj", LaunchProfile = "local" });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_launch_profile", new Dictionary<string, object>());

            Assert.True(result.IsSuccess);
            Assert.Contains("MyProj", result.Output);
            Assert.Contains("local", result.Output);
        }

        [Fact]
        public async Task InvokeIdeOutputPane_RoutesAndSurfacesContent()
        {
            _ideServiceMock
                .Setup(s => s.GetOutputPaneAsync(It.IsAny<string>()))
                .ReturnsAsync(new OutputPaneInfo { Name = "Build", Content = "Build succeeded" });

            var service = CreateService();
            var result = await service.InvokeAsync("ide_output_pane", new Dictionary<string, object> { { "paneName", "Build" } });

            Assert.True(result.IsSuccess);
            Assert.Contains("Build succeeded", result.Output);
            _ideServiceMock.Verify(s => s.GetOutputPaneAsync("Build"), Times.Once);
        }

        [Fact]
        public async Task InvokeIdeOutputPane_MissingPane_ReturnsError()
        {
            _ideServiceMock
                .Setup(s => s.GetOutputPaneAsync(It.IsAny<string>()))
                .ReturnsAsync((OutputPaneInfo?)null);

            var service = CreateService();
            var result = await service.InvokeAsync("ide_output_pane", new Dictionary<string, object> { { "paneName", "Nope" } });

            Assert.False(result.IsSuccess);
            Assert.Contains("Nope", result.Output);
        }
    }
}
