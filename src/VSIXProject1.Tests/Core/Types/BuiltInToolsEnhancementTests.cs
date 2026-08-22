#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using Xunit;

namespace ContinueVS.Tests.Core.Types
{
    public class BuiltInToolsEnhancementTests
    {
        [Fact]
        public void GetReadFileRangeTool_HasCorrectName()
        {
            var tool = BuiltInToolsRegistry.GetReadFileRangeTool();
            Assert.Equal("read_file_range", tool.Name);
        }

        [Fact]
        public void GetReadFileRangeTool_HasRequiredParameters()
        {
            var tool = BuiltInToolsRegistry.GetReadFileRangeTool();
            var paramNames = tool.Parameters.Select(p => p.Name).ToList();
            Assert.Contains("filepath", paramNames);
            Assert.Contains("startLine", paramNames);
            Assert.Contains("endLine", paramNames);
            Assert.Equal(3, tool.Parameters.Count);
        }

        [Fact]
        public void GetGrepSearchTool_HasCorrectName()
        {
            var tool = BuiltInToolsRegistry.GetGrepSearchTool();
            Assert.Equal("grep_search", tool.Name);
        }

        [Fact]
        public void GetGrepSearchTool_HasOptionalFilePattern()
        {
            var tool = BuiltInToolsRegistry.GetGrepSearchTool();
            var filePatternParam = tool.Parameters.FirstOrDefault(p => p.Name == "filePattern");
            Assert.NotNull(filePatternParam);
            Assert.False(filePatternParam!.IsRequired);
            Assert.Equal("*", filePatternParam.DefaultValue);
        }

        [Fact]
        public void GetSingleFindAndReplaceTool_HasCorrectName()
        {
            var tool = BuiltInToolsRegistry.GetSingleFindAndReplaceTool();
            Assert.Equal("single_find_and_replace", tool.Name);
        }

        [Fact]
        public void GetSingleFindAndReplaceTool_HasFlagsParameter()
        {
            var tool = BuiltInToolsRegistry.GetSingleFindAndReplaceTool();
            var flagsParam = tool.Parameters.FirstOrDefault(p => p.Name == "flags");
            Assert.NotNull(flagsParam);
            Assert.False(flagsParam!.IsRequired);
            Assert.Equal("", flagsParam.DefaultValue);
        }

        [Fact]
        public void GetGitDiffTool_HasStagedParameter()
        {
            var tool = BuiltInToolsRegistry.GetGitDiffTool();
            var stagedParam = tool.Parameters.FirstOrDefault(p => p.Name == "staged");
            Assert.NotNull(stagedParam);
            Assert.False(stagedParam!.IsRequired);
            Assert.Equal("boolean", stagedParam.Type);
            Assert.Equal(false, stagedParam.DefaultValue);
        }

        [Fact]
        public void GetGitDiffTool_HasCommitRangeParameter()
        {
            var tool = BuiltInToolsRegistry.GetGitDiffTool();
            var commitRangeParam = tool.Parameters.FirstOrDefault(p => p.Name == "commitRange");
            Assert.NotNull(commitRangeParam);
            Assert.False(commitRangeParam!.IsRequired);
            Assert.Equal("string", commitRangeParam.Type);
        }

        [Fact]
        public void GetAllBuiltInTools_IncludesNewTools()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            var toolNames = tools.Select(t => t.Name).ToList();
            Assert.Contains("read_file_range", toolNames);
            Assert.Contains("grep_search", toolNames);
            Assert.Contains("single_find_and_replace", toolNames);
        }

        [Fact]
        public void GetAllBuiltInTools_ReturnsCorrectCount()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            Assert.Equal(22, tools.Count);
        }

        [Fact]
        public void GetAllBuiltInTools_AllToolsEnabledExceptRuleBlock()
        {
            var tools = BuiltInToolsRegistry.GetAllBuiltInTools().ToList();
            var disabledTools = tools.Where(t => !t.IsEnabled).ToList();

            // create_rule_block and create_snippet should be disabled  
            Assert.Equal(2, disabledTools.Count);
            var disabledNames = disabledTools.Select(t => t.Name).ToList();
            Assert.Contains("create_rule_block", disabledNames);
            Assert.Contains("create_snippet", disabledNames);
        }
    }
}
