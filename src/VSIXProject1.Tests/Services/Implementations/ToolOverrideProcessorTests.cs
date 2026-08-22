#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services.Implementations
{
    public class ToolOverrideProcessorTests
    {
        private List<ToolDefinition> CreateSampleTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Read file",
                    Category = "Built-In",
                    Parameters = new List<ParameterDefinition>(),
                    ReturnsDescription = "File content",
                    IsEnabled = true,
                    IsAsync = true,
                    ToolType = "builtin",
                    LastModified = DateTime.UtcNow
                },
                new ToolDefinition
                {
                    Name = "create_new_file",
                    Description = "Create file",
                    Category = "Built-In",
                    Parameters = new List<ParameterDefinition>(),
                    ReturnsDescription = "Confirmation",
                    IsEnabled = true,
                    IsAsync = true,
                    ToolType = "builtin",
                    LastModified = DateTime.UtcNow
                },
                new ToolDefinition
                {
                    Name = "grep_search",
                    Description = "Search files",
                    Category = "Built-In",
                    Parameters = new List<ParameterDefinition>(),
                    ReturnsDescription = "Search results",
                    IsEnabled = true,
                    IsAsync = true,
                    ToolType = "builtin",
                    LastModified = DateTime.UtcNow
                }
            };
        }

        [Fact]
        public void ApplyOverrides_WithNullConfig_ReturnsSameTools()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();

            var result = processor.ApplyOverrides(tools, null).ToList();

            Assert.Equal(tools.Count, result.Count);
            Assert.All(result, tool => Assert.True(tool.IsEnabled));
        }

        [Fact]
        public void ApplyOverrides_WithDisable_MarkToolAsDisabled()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var config = new ToolOverrideConfig
            {
                DisabledTools = new List<string> { "grep_search" }
            };

            var result = processor.ApplyOverrides(tools, config).ToList();

            var grepTool = result.FirstOrDefault(t => t.Name == "grep_search");
            Assert.NotNull(grepTool);
            Assert.False(grepTool!.IsEnabled);
        }

        [Fact]
        public void ApplyOverrides_WithRename_ChangesToolName()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var config = new ToolOverrideConfig
            {
                ToolRenames = new Dictionary<string, string>
                {
                    { "grep_search", "pattern_search" }
                }
            };

            var result = processor.ApplyOverrides(tools, config).ToList();

            var renamedTool = result.FirstOrDefault(t => t.Name == "pattern_search");
            Assert.NotNull(renamedTool);
            Assert.DoesNotContain("grep_search", result.Select(t => t.Name));
        }

        [Fact]
        public void ApplyOverrides_DisablingCriticalTool_ThrowsInvalidOperationException()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var config = new ToolOverrideConfig
            {
                DisabledTools = new List<string> { "read_file" }
            };

            Assert.Throws<InvalidOperationException>(() =>
                processor.ApplyOverrides(tools, config).ToList());
        }

        [Fact]
        public void ApplyOverrides_WithNullToolList_ThrowsArgumentNullException()
        {
            var processor = new ToolOverrideProcessor();
            var config = new ToolOverrideConfig();

            Assert.Throws<ArgumentNullException>(() =>
                processor.ApplyOverrides(null!, config));
        }

        [Fact]
        public void ApplyOverrides_MultipleOverrides_AppliesAllRules()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var config = new ToolOverrideConfig
            {
                DisabledTools = new List<string> { "grep_search" },
                ToolRenames = new Dictionary<string, string>
                {
                    { "create_new_file", "new_file" }
                }
            };

            var result = processor.ApplyOverrides(tools, config).ToList();

            var grepTool = result.FirstOrDefault(t => t.Name == "grep_search");
            Assert.NotNull(grepTool);
            Assert.False(grepTool!.IsEnabled);

            var newFileTool = result.FirstOrDefault(t => t.Name == "new_file");
            Assert.NotNull(newFileTool);
            Assert.True(newFileTool!.IsEnabled);
        }

        [Fact]
        public void ApplyOverrides_WithDuplicateRenames_ThrowsInvalidOperationException()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var config = new ToolOverrideConfig
            {
                ToolRenames = new Dictionary<string, string>
                {
                    { "grep_search", "search_tool" },
                    { "create_new_file", "search_tool" }  // Duplicate target name
                }
            };

            Assert.Throws<InvalidOperationException>(() =>
                processor.ApplyOverrides(tools, config).ToList());
        }

        [Fact]
        public void ApplyOverrides_DoesNotModifyOrigionalTools()
        {
            var processor = new ToolOverrideProcessor();
            var tools = CreateSampleTools();
            var originalCount = tools.Count;
            var originalNames = tools.Select(t => t.Name).ToList();

            var config = new ToolOverrideConfig
            {
                DisabledTools = new List<string> { "grep_search" },
                ToolRenames = new Dictionary<string, string> { { "create_new_file", "new_file" } }
            };

            _ = processor.ApplyOverrides(tools, config).ToList();

            Assert.Equal(originalCount, tools.Count);
            Assert.Equal(originalNames, tools.Select(t => t.Name).ToList());
            Assert.All(tools, t => Assert.True(t.IsEnabled));
        }
    }
}
