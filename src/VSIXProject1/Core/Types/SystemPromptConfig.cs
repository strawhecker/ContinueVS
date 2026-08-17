using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Deserializable model for system prompt configuration loaded from JSON.
    /// Maps mode names to their respective prompts and metadata.
    /// </summary>
    public class SystemPromptConfig
    {
        /// <summary>
        /// Dictionary of prompts keyed by mode name (e.g., "ask", "agent", "plan").
        /// </summary>
        [JsonProperty("systemPrompts")]
        public Dictionary<string, SystemPromptItem> SystemPrompts { get; set; } = new Dictionary<string, SystemPromptItem>();
    }

    /// <summary>
    /// Individual system prompt entry with content and optional metadata.
    /// </summary>
    public class SystemPromptItem
    {
        /// <summary>
        /// The actual prompt text sent to the LLM.
        /// </summary>
        [JsonProperty("prompt")]
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the mode's purpose.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
    }
}
