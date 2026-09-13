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
    public class ToolServiceTests
    {
        private Mock<IIdeService> CreateMockIdeService()
        {
            var mock = new Mock<IIdeService>();
            mock.Setup(s => s.ReadFileAsync(It.IsAny<string>()))
                .ReturnsAsync("file content");
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(s => s.GetEnabledTools())
                .Returns(BuiltInToolsRegistry.GetAllBuiltInTools() as IEnumerable<ToolDefinition>);
            return mock;
        }

        [Fact]
        public void Constructor_WithValidDependencies_Succeeds()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();

            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithNullIdeService_ThrowsArgumentNullException()
        {
            var configServiceMock = CreateMockConfigService();

            Assert.Throws<ArgumentNullException>(() =>
                new ToolService(null, configServiceMock.Object));
        }

        [Fact]
        public void Constructor_WithNullConfigService_ThrowsArgumentNullException()
        {
            var ideServiceMock = CreateMockIdeService();

            Assert.Throws<ArgumentNullException>(() =>
                new ToolService(ideServiceMock.Object, null));
        }

        [Fact]
        public void GetAvailableTools_ReturnsAllBuiltInTools()
        {
            var ideServiceMock = CreateMockIdeService();
                var configServiceMock = CreateMockConfigService();
                var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

                var tools = service.GetAvailableTools().ToList();

                Assert.NotEmpty(tools);
                // Now 19 tools (git tools disabled by default in UserSettings)
                Assert.Equal(19, tools.Count);
            }

        [Fact]
        public void GetAvailableTools_ContainsExpectedToolNames()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("read_file", toolNames);
            Assert.Contains("create_new_file", toolNames);
            Assert.Contains("run_terminal_command", toolNames);
            Assert.Contains("file_glob_search", toolNames);
            Assert.Contains("view_diff", toolNames);
            Assert.Contains("read_currently_open_file", toolNames);
            Assert.Contains("ls", toolNames);
            Assert.Contains("edit_file", toolNames);
            Assert.Contains("search_codebase", toolNames);
        }

        [Fact]
        public void GetTool_WithValidName_ReturnsTool()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool("read_file");

            Assert.NotNull(tool);
            Assert.Equal("read_file", tool.Name);
        }

        [Fact]
        public void GetTool_WithInvalidName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool("nonexistent_tool");

            Assert.Null(tool);
        }

        [Fact]
        public void GetTool_WithNullName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool(null);

            Assert.Null(tool);
        }

        [Fact]
        public void GetTool_WithEmptyName_ReturnsNull()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tool = service.GetTool(string.Empty);

            Assert.Null(tool);
        }

        [Fact]
        public async Task InvokeAsync_WithInvalidToolName_ReturnsErrorResult()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync("nonexistent", new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
            Assert.Contains("not found", result.Output);
        }

        [Fact]
        public async Task InvokeAsync_WithNullToolName_ReturnsErrorResult()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var result = await service.InvokeAsync(null, new Dictionary<string, object>());

            Assert.NotNull(result);
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void GetAvailableTools_AllToolsHaveBuiltInCategory()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools();

            foreach (var tool in tools)
            {
                Assert.Equal("Built-In", tool.Category);
            }
        }

        [Fact]
        public void GetAvailableTools_AllToolsHaveBuiltInType()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools();

            foreach (var tool in tools)
            {
                Assert.Equal("builtin", tool.ToolType);
            }
        }

        [Fact]
        public void GetAvailableTools_WithAskMode_ReturnsReadOnlyTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Ask).ToList();

            // Ask mode should have all read-only tools for context inspection
            var enabledTools = tools.Where(t => t.IsEnabled).ToList();
            Assert.NotEmpty(enabledTools);
            // Should include core read-only tools (git tools are disabled by default)
            var readOnlyToolNames = new[] { "read_file", "file_glob_search", "search_codebase", "grep_search" };
            foreach (var toolName in readOnlyToolNames)
            {
                Assert.NotNull(enabledTools.FirstOrDefault(t => t.Name == toolName));
            }
        }

        [Fact]
        public void GetAvailableTools_WithPlanMode_ReturnsOnlyReadTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Plan).ToList();

            Assert.NotEmpty(tools);
            var toolNames = tools.Select(t => t.Name).ToList();
            // Should include read-only tools
            Assert.Contains("read_file", toolNames);
            Assert.Contains("search_codebase", toolNames);
            Assert.Contains("file_glob_search", toolNames);
            // Should NOT include write tools
            Assert.DoesNotContain("edit_file", toolNames);
            Assert.DoesNotContain("create_new_file", toolNames);
            Assert.DoesNotContain("run_terminal_command", toolNames);
        }

        [Fact]
        public void GetAvailableTools_WithAgentMode_ReturnsAllWriteTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Agent).ToList();

            Assert.NotEmpty(tools);
            var toolNames = tools.Select(t => t.Name).ToList();
            // Verify Agent mode includes both read and write tools
            Assert.Contains("read_file", toolNames);
            Assert.Contains("search_codebase", toolNames);
            Assert.Contains("create_new_file", toolNames);
            Assert.Contains("edit_file", toolNames);
            Assert.Contains("run_terminal_command", toolNames);
        }

        [Fact]
        public void GetAvailableTools_WithDebugMode_ReturnsAllWriteTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Debug).ToList();

            Assert.NotEmpty(tools);
            var toolNames = tools.Select(t => t.Name).ToList();
            // Verify Debug mode includes both read and write tools
            Assert.Contains("read_file", toolNames);
            Assert.Contains("create_new_file", toolNames);
            Assert.Contains("edit_file", toolNames);
        }

        [Fact]
        public void GetAvailableTools_WithReasonMode_ReturnsOnlyReadOnlyTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Reason).ToList();

            Assert.NotEmpty(tools);
            var toolNames = tools.Select(t => t.Name).ToList();
            // Verify Reason mode includes only read-only tools, not write tools
            Assert.Contains("read_file", toolNames);
            Assert.DoesNotContain("create_new_file", toolNames);
            Assert.DoesNotContain("edit_file", toolNames);
        }

        [Fact]
        public void GetAvailableTools_WithoutMode_ReturnsAllTools_BackwardCompatibility()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();
            var agentTools = service.GetAvailableTools(ChatMode.Agent).ToList();

            // Without mode parameter, should return all tools (backward compatibility)
            // Now 19 tools (git tools disabled by default in UserSettings)
            Assert.Equal(19, tools.Count);
            Assert.Equal(tools.Count, agentTools.Count);
        }

        [Fact]
        public void GetAvailableTools_PlanMode_ToolCountIsGreaterThanOne()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools(ChatMode.Plan).ToList();

            // Plan mode should have at least 2 read-only tools (read_file, search_codebase)
            Assert.True(tools.Count >= 2);
        }

        [Fact]
        public void GetAvailableTools_PlanMode_ExcludesWriteTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var planModeTools = service.GetAvailableTools(ChatMode.Plan).ToList();

            // Plan mode should NOT have any write tools
            var writeToolNames = new[] 
            { 
                "edit_file", "create_new_file", "create_folder", "run_terminal_command",
                "git_commit", "single_find_and_replace", "run_pytest"
            };

            foreach (var toolName in writeToolNames)
            {
                Assert.DoesNotContain(planModeTools, t => t.Name == toolName);
            }
        }

        [Fact]
        public void GetAvailableTools_PlanMode_IncludesReadOnlyTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = CreateMockConfigService();
            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var planModeTools = service.GetAvailableTools(ChatMode.Plan).ToList();

            // Plan mode SHOULD have core read-only tools (git tools are disabled by default)
            var readOnlyToolNames = new[] 
            { 
                "read_file", "search_codebase", "grep_search", "file_glob_search",
                "view_diff", "get_problems"
            };

            foreach (var toolName in readOnlyToolNames)
            {
                Assert.Contains(planModeTools, t => t.Name == toolName);
            }
        }

        [Fact]
        public void GetAvailableTools_WithGitToolsDisabledInUserSettings_ExcludesAllGitTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = new Mock<IConfigService>();

            // Create a config with all git tools disabled
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Tool_GitStatusEnabled, false },
                    { UserSettings.Tool_GitDiffEnabled, false },
                    { UserSettings.Tool_GitLogEnabled, false },
                    { UserSettings.Tool_GitCommitEnabled, false }
                }
            };

            configServiceMock.Setup(s => s.GetCurrentConfig()).Returns(config);
            configServiceMock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());

            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();
            var gitToolNames = new[] { "git_status", "git_diff", "git_log", "git_commit" };

            // All git tools should be filtered out
            foreach (var toolName in gitToolNames)
            {
                Assert.DoesNotContain(tools, t => t.Name == toolName);
            }
        }

        [Fact]
        public void GetAvailableTools_WithSpecificToolDisabledInUserSettings_ExcludesTool()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = new Mock<IConfigService>();

            // Disable only grep_search
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Tool_GrepSearchEnabled, false }
                }
            };

            configServiceMock.Setup(s => s.GetCurrentConfig()).Returns(config);
            configServiceMock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());

            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var tools = service.GetAvailableTools().ToList();

            // grep_search should be filtered, but read_file should still be available
            Assert.DoesNotContain(tools, t => t.Name == "grep_search");
            Assert.Contains(tools, t => t.Name == "read_file");
        }

        [Fact]
        public void GetAvailableTools_PlanModeWithGitToolsDisabledGlobally_ExcludesGitTools()
        {
            var ideServiceMock = CreateMockIdeService();
            var configServiceMock = new Mock<IConfigService>();

            // Disable all git tools globally
            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Tool_GitStatusEnabled, false },
                    { UserSettings.Tool_GitDiffEnabled, false },
                    { UserSettings.Tool_GitLogEnabled, false }
                }
            };

            configServiceMock.Setup(s => s.GetCurrentConfig()).Returns(config);
            configServiceMock.Setup(s => s.GetToolOverrideConfig()).Returns(new ToolOverrideConfig());

            var service = new ToolService(ideServiceMock.Object, configServiceMock.Object);

            var planModeTools = service.GetAvailableTools(ChatMode.Plan).ToList();

            // git_status, git_diff, git_log should be filtered from Plan mode
            Assert.DoesNotContain(planModeTools, t => t.Name == "git_status");
            Assert.DoesNotContain(planModeTools, t => t.Name == "git_diff");
            Assert.DoesNotContain(planModeTools, t => t.Name == "git_log");
        }
    }
}
