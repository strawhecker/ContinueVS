using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service implementation for managing UI state and tool policies.
    /// Delegates persistence to IConfigService, which manages serialization and disk I/O.
    /// All policy updates are immediately persisted to disk.
    /// Thread-safe through ConfigService synchronization.
    /// </summary>
    public class UIStateService : IUIStateService
    {
        private readonly IConfigService _configService;

        public UIStateService(IConfigService configService)
        {
            if (configService == null)
            {
                throw new ArgumentNullException(nameof(configService));
            }
            _configService = configService;
        }

        /// <summary>
        /// Gets the execution policy for a specific tool.
        /// If tool not found in UIState, returns AskFirst (safe default).
        /// </summary>
        public async Task<ToolPolicy> GetToolPolicyAsync(string toolName)
        {
            if (toolName == null)
            {
                throw new ArgumentNullException(nameof(toolName));
            }

            var uiState = await _configService.GetUIStateAsync();
            if (uiState.ToolSettings.TryGetValue(toolName, out var policy))
            {
                return policy;
            }

            // Default to AskFirst if tool not found (safe mode)
            return ToolPolicy.AskFirst;
        }

        /// <summary>
        /// Sets the execution policy for a specific tool.
        /// Updates are persisted immediately to disk.
        /// </summary>
        public async Task SaveToolPolicyAsync(string toolName, ToolPolicy policy)
        {
            if (toolName == null)
            {
                throw new ArgumentNullException(nameof(toolName));
            }

            var uiState = await _configService.GetUIStateAsync();
            uiState.ToolSettings[toolName] = policy;
            uiState.LastModified = DateTime.UtcNow;
            await _configService.SaveUIStateAsync(uiState);
        }

        /// <summary>
        /// Gets the enabled state for a tool group.
        /// If group not found, returns true (enabled by default).
        /// </summary>
        public async Task<bool> GetToolGroupPolicyAsync(string groupName)
        {
            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var uiState = await _configService.GetUIStateAsync();
            if (uiState.ToolGroupSettings.TryGetValue(groupName, out var enabled))
            {
                return enabled;
            }

            // Default to enabled if group not found
            return true;
        }

        /// <summary>
        /// Sets the enabled state for a tool group.
        /// Updates are persisted immediately to disk.
        /// </summary>
        public async Task SaveToolGroupPolicyAsync(string groupName, bool enabled)
        {
            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var uiState = await _configService.GetUIStateAsync();
            uiState.ToolGroupSettings[groupName] = enabled;
            uiState.LastModified = DateTime.UtcNow;
            await _configService.SaveUIStateAsync(uiState);
        }

        /// <summary>
        /// Gets the enabled state for a rule.
        /// If rule not found, returns true (enabled by default).
        /// </summary>
        public async Task<bool> IsRuleEnabledAsync(string ruleName)
        {
            if (ruleName == null)
            {
                throw new ArgumentNullException(nameof(ruleName));
            }

            var uiState = await _configService.GetUIStateAsync();
            if (uiState.RuleSettings.TryGetValue(ruleName, out var enabled))
            {
                return enabled;
            }

            // Default to enabled if rule not found
            return true;
        }

        /// <summary>
        /// Sets the enabled state for a rule.
        /// Updates are persisted immediately to disk.
        /// </summary>
        public async Task SaveRuleSettingAsync(string ruleName, bool enabled)
        {
            if (ruleName == null)
            {
                throw new ArgumentNullException(nameof(ruleName));
            }

            var uiState = await _configService.GetUIStateAsync();
            uiState.RuleSettings[ruleName] = enabled;
            uiState.LastModified = DateTime.UtcNow;
            await _configService.SaveUIStateAsync(uiState);
        }

        /// <summary>
        /// Gets all tool policies currently stored in UIState.
        /// </summary>
        public async Task<Dictionary<string, ToolPolicy>> GetAllToolPoliciesAsync()
        {
            var uiState = await _configService.GetUIStateAsync();
            return new Dictionary<string, ToolPolicy>(uiState.ToolSettings);
        }

        /// <summary>
        /// Gets the complete UIState object.
        /// </summary>
        public async Task<UIState> GetUIStateAsync()
        {
            return await _configService.GetUIStateAsync();
        }

        /// <summary>
        /// Saves the complete UIState object.
        /// Overwrites all existing UI state settings.
        /// </summary>
        public async Task SaveUIStateAsync(UIState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            state.LastModified = DateTime.UtcNow;
            await _configService.SaveUIStateAsync(state);
        }
    }
}
