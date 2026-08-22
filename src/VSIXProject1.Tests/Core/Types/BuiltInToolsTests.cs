using System;
using System.Linq;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.Core.Types
{
    public class BuiltInToolsTests
    {
        [Fact]
        public void GetReadFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetReadFileTool();

            Assert.NotNull(tool);
            Assert.Equal("read_file", tool.Name);
            Assert.NotNull(tool.Description);
            Assert.Single(tool.Parameters);
            Assert.Equal("filepath", tool.Parameters.First().Name);
            Assert.True(tool.Parameters.First().IsRequired);
            Assert.Equal("Built-In", tool.Category);
            Assert.True(tool.IsEnabled);
            Assert.Equal("builtin", tool.ToolType);
        }

        [Fact]
        public void GetCreateNewFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetCreateNewFileTool();

            Assert.NotNull(tool);
            Assert.Equal("create_new_file", tool.Name);
            Assert.NotNull(tool.Description);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.Equal("filepath", tool.Parameters[0].Name);
            Assert.Equal("contents", tool.Parameters[1].Name);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetRunTerminalCommandTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetRunTerminalCommandTool();

            Assert.NotNull(tool);
            Assert.Equal("run_terminal_command", tool.Name);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetFileGlobSearchTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetFileGlobSearchTool();

            Assert.NotNull(tool);
            Assert.Equal("file_glob_search", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.Equal("pattern", tool.Parameters.First().Name);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetViewDiffTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetViewDiffTool();

            Assert.NotNull(tool);
            Assert.Equal("view_diff", tool.Name);
            Assert.Empty(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetReadCurrentlyOpenFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetReadCurrentlyOpenFileTool();

            Assert.NotNull(tool);
            Assert.Equal("read_currently_open_file", tool.Name);
            Assert.Empty(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetListDirectoryTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetListDirectoryTool();

            Assert.NotNull(tool);
            Assert.Equal("ls", tool.Name);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetCreateRuleBlockTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetCreateRuleBlockTool();

            Assert.NotNull(tool);
            Assert.Equal("create_rule_block", tool.Name);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.False(tool.IsEnabled);
        }

        [Fact]
        public void GetEditFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetEditFileTool();

            Assert.NotNull(tool);
            Assert.Equal("edit_file", tool.Name);
            Assert.Equal(3, tool.Parameters.Count);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetSearchCodebaseTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetSearchCodebaseTool();

            Assert.NotNull(tool);
            Assert.Equal("search_codebase", tool.Name);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetRunPytestTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetRunPytestTool();

            Assert.NotNull(tool);
            Assert.Equal("run_pytest", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetGetProblemsTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetGetProblemsTool();

            Assert.NotNull(tool);
            Assert.Equal("get_problems", tool.Name);
            Assert.Empty(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetViewFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetViewFileTool();

            Assert.NotNull(tool);
            Assert.Equal("view_file", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetOpenFileTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetOpenFileTool();

            Assert.NotNull(tool);
            Assert.Equal("open_file", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetGitStatusTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetGitStatusTool();

            Assert.NotNull(tool);
            Assert.Equal("git_status", tool.Name);
            Assert.Empty(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetGitDiffTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetGitDiffTool();

            Assert.NotNull(tool);
            Assert.Equal("git_diff", tool.Name);
            Assert.Equal(3, tool.Parameters.Count);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetGitLogTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetGitLogTool();

            Assert.NotNull(tool);
            Assert.Equal("git_log", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetGitCommitTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetGitCommitTool();

            Assert.NotNull(tool);
            Assert.Equal("git_commit", tool.Name);
            Assert.Single(tool.Parameters);
            Assert.True(tool.IsEnabled);
        }

        [Fact]
        public void GetCreateSnippetTool_ReturnsValid()
        {
            var tool = BuiltInToolsRegistry.GetCreateSnippetTool();

            Assert.NotNull(tool);
            Assert.Equal("create_snippet", tool.Name);
            Assert.Equal(2, tool.Parameters.Count);
            Assert.False(tool.IsEnabled);
        }

        [Fact]
        public void GetAllBuiltInTools_Returns19Tools()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools();

            Assert.NotNull(tools);
            Assert.Equal(22, tools.Count());
        }

        [Fact]
        public void GetAllBuiltInTools_ContainsExpectedToolNames()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("read_file", toolNames);
            Assert.Contains("create_new_file", toolNames);
            Assert.Contains("run_terminal_command", toolNames);
            Assert.Contains("file_glob_search", toolNames);
            Assert.Contains("view_diff", toolNames);
            Assert.Contains("read_currently_open_file", toolNames);
            Assert.Contains("ls", toolNames);
            Assert.Contains("create_rule_block", toolNames);
            Assert.Contains("edit_file", toolNames);
            Assert.Contains("search_codebase", toolNames);
            Assert.Contains("run_pytest", toolNames);
            Assert.Contains("get_problems", toolNames);
            Assert.Contains("view_file", toolNames);
            Assert.Contains("open_file", toolNames);
            Assert.Contains("git_status", toolNames);
            Assert.Contains("git_diff", toolNames);
            Assert.Contains("git_log", toolNames);
            Assert.Contains("git_commit", toolNames);
            Assert.Contains("create_snippet", toolNames);
        }

        [Fact]
        public void GetAllBuiltInTools_AllHaveValidCategory()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools();

            foreach (var tool in tools)
            {
                Assert.Equal("Built-In", tool.Category);
            }
        }

        [Fact]
        public void GetAllBuiltInTools_AllHaveValidToolType()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools();

            foreach (var tool in tools)
            {
                Assert.Equal("builtin", tool.ToolType);
            }
        }

        [Fact]
        public void GetAllBuiltInTools_NoNullNames()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools();

            foreach (var tool in tools)
            {
                Assert.NotNull(tool.Name);
                Assert.NotEmpty(tool.Name);
            }
        }

        [Fact]
        public void GetAllBuiltInTools_NoDuplicateToolNames()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            var toolNames = tools.Select(t => t.Name).ToList();
            var uniqueNames = toolNames.Distinct().ToList();

            Assert.Equal(toolNames.Count, uniqueNames.Count);
        }

        [Fact]
        public void ParameterDefinitions_HaveDescriptions()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools();

            foreach (var tool in tools)
            {
                foreach (var param in tool.Parameters)
                {
                    Assert.NotNull(param.Description);
                    Assert.NotEmpty(param.Description);
                }
            }
        }
    }
}
