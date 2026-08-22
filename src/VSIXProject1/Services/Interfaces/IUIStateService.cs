using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for managing UI state and tool policies persisted across sessions.
    /// Provides access to tool execution policies, tool group settings, rule settings, and reasoning configuration.
    /// Thread-safe: uses ConfigService synchronization for all persistence operations.
    /// </summary>
    public interface IUIStateService
    {
        /// <summary>
        /// Gets the execution policy for a specific tool.
        /// If tool not found in UIState, returns AskFirst (safe default).
        /// </summary>
        /// <param name="toolName">The tool identifier (e.g., "read_file", "edit_file").</param>
        /// <returns>The ToolPolicy for this tool (AutoApprove, AskFirst, or Disabled).</returns>
        Task<ToolPolicy> GetToolPolicyAsync(string toolName);

        /// <summary>
        /// Sets the execution policy for a specific tool.
        /// Updates are persisted to ContinueConfig.CustomSettings["ui.state"] immediately.
        /// </summary>
        /// <param name="toolName">The tool identifier.</param>
        /// <param name="policy">The new policy to apply.</param>
        /// <returns>Task representing the async save operation.</returns>
        Task SaveToolPolicyAsync(string toolName, ToolPolicy policy);

        /// <summary>
        /// Gets the enabled state for a tool group.
        /// If group not found, returns true (enabled by default).
        /// </summary>
        /// <param name="groupName">The group identifier (e.g., "file_operations", "web_tools").</param>
        /// <returns>True if group is enabled, false if disabled.</returns>
        Task<bool> GetToolGroupPolicyAsync(string groupName);

        /// <summary>
        /// Sets the enabled state for a tool group.
        /// </summary>
        /// <param name="groupName">The group identifier.</param>
        /// <param name="enabled">Whether the group should be enabled.</param>
        /// <returns>Task representing the async save operation.</returns>
        Task SaveToolGroupPolicyAsync(string groupName, bool enabled);

        /// <summary>
        /// Gets the enabled state for a rule.
        /// If rule not found, returns true (enabled by default).
        /// </summary>
        /// <param name="ruleName">The rule identifier (e.g., "auto_continue").</param>
        /// <returns>True if rule is enabled, false if disabled.</returns>
        Task<bool> IsRuleEnabledAsync(string ruleName);

        /// <summary>
        /// Sets the enabled state for a rule.
        /// </summary>
        /// <param name="ruleName">The rule identifier.</param>
        /// <param name="enabled">Whether the rule should be enabled.</param>
        /// <returns>Task representing the async save operation.</returns>
        Task SaveRuleSettingAsync(string ruleName, bool enabled);

        /// <summary>
        /// Gets all tool policies currently stored in UIState.
        /// </summary>
        /// <returns>Dictionary mapping tool name to ToolPolicy.</returns>
        Task<Dictionary<string, ToolPolicy>> GetAllToolPoliciesAsync();

        /// <summary>
        /// Gets the complete UIState object.
        /// </summary>
        /// <returns>The current UIState, or empty UIState if none exists.</returns>
        Task<UIState> GetUIStateAsync();

        /// <summary>
        /// Saves the complete UIState object.
        /// Overwrites all existing UI state settings.
        /// </summary>
        /// <param name="state">The UIState to persist.</param>
        /// <returns>Task representing the async save operation.</returns>
        Task SaveUIStateAsync(UIState state);
    }
}
