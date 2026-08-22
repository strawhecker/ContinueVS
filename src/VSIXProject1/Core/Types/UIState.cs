using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Tool execution policy enumeration mirroring Continue.js Redux uiSlice.ts.
    /// Determines how tools are handled during Agent mode execution.
    /// </summary>
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public enum ToolPolicy
    {
        /// <summary>
        /// Tool automatically approved and executed without user confirmation.
        /// </summary>
        [JsonProperty("auto_approve")]
        AutoApprove,

        /// <summary>
        /// Tool requires user confirmation before execution (default safe mode).
        /// </summary>
        [JsonProperty("ask_first")]
        AskFirst,

        /// <summary>
        /// Tool is disabled and cannot be executed.
        /// </summary>
        [JsonProperty("disabled")]
        Disabled
    }

    /// <summary>
    /// Represents reasoning settings for Agent mode.
    /// Maps Continue.js reasoning configuration (enabled/budget).
    /// </summary>
    public class ReasoningSettings
    {
        /// <summary>
        /// Whether reasoning is enabled for this session.
        /// </summary>
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Maximum token budget for reasoning steps (optional).
        /// </summary>
        [JsonProperty("budget")]
        public int? Budget { get; set; }
    }

    /// <summary>
    /// UI State persisted across sessions, mirroring Continue.js Redux uiSlice.ts.
    /// Contains tool policies, rule settings, reasoning configuration, and dialog visibility.
    /// Stored in ContinueConfig.CustomSettings["ui.state"] as JSON string.
    /// </summary>
    public class UIState
    {
        /// <summary>
        /// Per-tool execution policies: tool name → policy (auto_approve/ask_first/disabled).
        /// If a tool is not present, defaults to AskFirst (safe default).
        /// </summary>
        [JsonProperty("toolSettings")]
        public Dictionary<string, ToolPolicy> ToolSettings { get; set; } = new Dictionary<string, ToolPolicy>();

        /// <summary>
        /// Tool group policies: group name (e.g., "file_operations", "web_tools") → enabled.
        /// </summary>
        [JsonProperty("toolGroupSettings")]
        public Dictionary<string, bool> ToolGroupSettings { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Rule settings: rule name → enabled.
        /// Rules are high-level behavior toggles (e.g., "auto_continue", "streaming_enabled").
        /// </summary>
        [JsonProperty("ruleSettings")]
        public Dictionary<string, bool> RuleSettings { get; set; } = new Dictionary<string, bool>();

        /// <summary>
        /// Reasoning mode settings: reasoning type name → ReasoningSettings.
        /// E.g., "deep_research" → {enabled: true, budget: 5000}.
        /// </summary>
        [JsonProperty("reasoningSettings")]
        public Dictionary<string, ReasoningSettings> ReasoningSettings { get; set; } = new Dictionary<string, ReasoningSettings>();

        /// <summary>
        /// Onboarding card visibility state.
        /// </summary>
        [JsonProperty("onboardingCardVisible")]
        public bool OnboardingCardVisible { get; set; } = true;

        /// <summary>
        /// Explore dialog open state.
        /// </summary>
        [JsonProperty("exploreDialogOpen")]
        public bool ExploreDialogOpen { get; set; } = false;

        /// <summary>
        /// Text-to-speech active state.
        /// </summary>
        [JsonProperty("TTSActive")]
        public bool TTSActive { get; set; } = false;

        /// <summary>
        /// File editing mode state.
        /// </summary>
        [JsonProperty("fileEditingMode")]
        public bool FileEditingMode { get; set; } = false;

        /// <summary>
        /// Timestamp when UIState was last modified.
        /// </summary>
        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Schema version for migration compatibility.
        /// Current version: 1.
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; set; } = 1;
    }
}
