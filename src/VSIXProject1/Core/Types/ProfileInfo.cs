using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a user profile with model and tool preferences.
    /// </summary>
    public class ProfileInfo
    {
        /// <summary>
        /// Unique identifier for this profile.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display name for the profile.
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Description of what this profile is used for.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /// <summary>
        /// ID of the default model for this profile.
        /// </summary>
        [JsonProperty("defaultModelId")]
        public string? DefaultModelId { get; set; }

        /// <summary>
        /// List of enabled tool names in this profile.
        /// </summary>
        [JsonProperty("enabledTools")]
        public List<string> EnabledTools { get; set; } = new List<string>();

        /// <summary>
        /// Custom prompts and settings specific to this profile.
        /// </summary>
        [JsonProperty("customPrompts")]
        public Dictionary<string, object> CustomPrompts { get; set; } = new Dictionary<string, object>();
    }
}
