using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Debug configuration settings for troubleshooting.
    /// </summary>
    public class DebugSettings
    {
        /// <summary>
        /// If true, dumps the full context (system message, context items, user message) 
        /// to Debug Output before sending to the LLM. Shows raw text before tokenization.
        /// </summary>
        [JsonProperty("dumpContextBeforeSend")]
        public bool DumpContextBeforeSend { get; set; } = false;

        /// <summary>
        /// If true, dumps the LLM response to Debug Output after receiving.
        /// </summary>
        [JsonProperty("dumpResponseAfterReceive")]
        public bool DumpResponseAfterReceive { get; set; } = false;
    }

    /// <summary>
    /// Represents the Continue configuration schema.
    /// </summary>
    public partial class ContinueConfig
    {
        /// <summary>
        /// List of configured LLM models.
        /// </summary>
        [JsonProperty("models")]
        public List<ModelInfo> Models { get; set; } = new List<ModelInfo>();

        /// <summary>
        /// ID of the currently selected model.
        /// </summary>
        [JsonProperty("selectedModelId")]
        public string? SelectedModelId { get; set; }

        /// <summary>
        /// List of available tools.
        /// Note: This property is used for in-memory representation (full ToolDefinition instances).
        /// For JSON persistence, see ToolOverrides below.
        /// </summary>
        [JsonIgnore]
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();

        /// <summary>
        /// Lightweight tool state overrides persisted to continueVS.json.
        /// Only stores name and isEnabled for tools that differ from defaults.
        /// Full tool definitions are loaded from registry and merged with these overrides.
        /// </summary>
        [JsonProperty("toolOverrides")]
        public List<ToolOverride> ToolOverrides { get; set; } = new List<ToolOverride>();

        /// <summary>
        /// List of user profiles.
        /// </summary>
        [JsonProperty("profiles")]
        public List<ProfileInfo> Profiles { get; set; } = new List<ProfileInfo>();

        /// <summary>
        /// Custom settings and extensions.
        /// </summary>
        [JsonProperty("customSettings")]
        public Dictionary<string, object> CustomSettings { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Path to the configuration file.
        /// </summary>
        [JsonIgnore]
        public string? ConfigFilePath { get; set; }

        /// <summary>
        /// Debug configuration for troubleshooting and analysis.
        /// </summary>
        [JsonProperty("debug")]
        public DebugSettings Debug { get; set; } = new DebugSettings();

        /// <summary>
        /// Timestamp when the configuration was last modified.
        /// </summary>
        [JsonIgnore]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}
