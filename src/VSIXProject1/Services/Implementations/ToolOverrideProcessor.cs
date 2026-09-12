#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Configuration for tool overrides (disable, rename, validate).
    /// </summary>
    public class ToolOverrideConfig
    {
        /// <summary>
        /// Names of tools to disable. Tools in this list will have IsEnabled set to false.
        /// </summary>
        public List<string> DisabledTools { get; set; } = new();

        /// <summary>
        /// Mapping of tool names to their new names. Used to rename tools at retrieval time.
        /// </summary>
        public Dictionary<string, string> ToolRenames { get; set; } = new();
    }

    /// <summary>
    /// Processes tool overrides (disable, rename, validate) at retrieval time.
    /// Applies overrides to tool definitions without modifying the registry.
    /// </summary>
    public class ToolOverrideProcessor
    {
        /// <summary>
        /// Critical tools that must never be disabled (empty set means no critical tools).
        /// </summary>
        private readonly HashSet<string> _criticalTools = new()
        {
            "read_file",
            "create_new_file",
            "run_terminal_command"
        };

        /// <summary>
        /// Apply override rules (disable, rename, validate) to a list of tools.
        /// Returns a new list with overrides applied; does not modify input.
        /// </summary>
        /// <param name="tools">Original list of tool definitions</param>
        /// <param name="config">Override configuration (null means no overrides)</param>
        /// <returns>Filtered/transformed tool list</returns>
        /// <exception cref="ArgumentNullException">If tools is null</exception>
        /// <exception cref="InvalidOperationException">If critical tool is disabled</exception>
        public IEnumerable<ToolDefinition> ApplyOverrides(
            IEnumerable<ToolDefinition> tools,
            ToolOverrideConfig? config = null)
        {
            if (tools == null)
                throw new ArgumentNullException(nameof(tools), "Tool list cannot be null");

            // No configuration means pass through unchanged
            if (config == null)
                return tools.ToList();

            var result = new List<ToolDefinition>();

            foreach (var tool in tools)
            {
                var toolName = tool.Name;

                // Check if tool is critical and disabled
                if (config.DisabledTools.Contains(toolName) && _criticalTools.Contains(toolName))
                {
                    throw new InvalidOperationException(
                        $"Cannot disable critical tool '{toolName}'. Critical tools are: {string.Join(", ", _criticalTools)}");
                }

                // Clone tool definition with potential mutations
                var overriddenTool = CloneToolDefinition(tool);

                // Apply disable override
                if (config.DisabledTools.Contains(toolName))
                {
                    overriddenTool.IsEnabled = false;
                }

                // Apply rename override
                if (config.ToolRenames.TryGetValue(toolName, out var newName))
                {
                    overriddenTool.Name = newName;
                }

                result.Add(overriddenTool);
            }

            // Validate: All tools after overrides
            ValidateToolList(result, config);

            return result;
        }

        /// <summary>
        /// Clone a tool definition for safe mutation.
        /// </summary>
        private ToolDefinition CloneToolDefinition(ToolDefinition original)
        {
            return new ToolDefinition
            {
                Name = original.Name,
                Description = original.Description,
                Category = original.Category,
                Parameters = original.Parameters != null
                    ? new List<ParameterDefinition>(original.Parameters)
                    : new List<ParameterDefinition>(),
                ReturnsDescription = original.ReturnsDescription,
                IsEnabled = original.IsEnabled,
                IsAsync = original.IsAsync,
                ToolType = original.ToolType,
                LastModified = original.LastModified
            };
        }

        /// <summary>
        /// Validate the final tool list against override rules.
        /// </summary>
        private void ValidateToolList(List<ToolDefinition> tools, ToolOverrideConfig config)
        {
            // Check that renamed tools don't create duplicates
            var toolNames = new HashSet<string>();
            foreach (var tool in tools)
            {
                if (!toolNames.Add(tool.Name))
                {
                    throw new InvalidOperationException(
                        $"Tool rename configuration created duplicate tool name '{tool.Name}'. Ensure renames are unique.");
                }
            }

            // Check that no critical disabled tools exist
            var disabledCritical = tools
                .Where(t => _criticalTools.Contains(t.Name) && !t.IsEnabled)
                .Select(t => t.Name)
                .ToList();

            if (disabledCritical.Any())
            {
                throw new InvalidOperationException(
                    $"Critical tools cannot be disabled: {string.Join(", ", disabledCritical)}");
            }
        }
    }
}
