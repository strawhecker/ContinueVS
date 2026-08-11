using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
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
        /// </summary>
        [JsonProperty("tools")]
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();

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
        /// Timestamp when the configuration was last modified.
        /// </summary>
        [JsonIgnore]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
}
