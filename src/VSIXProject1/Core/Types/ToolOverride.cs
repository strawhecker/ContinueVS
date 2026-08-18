using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Lightweight representation of tool enabled/disabled state override.
    /// Persisted to continueVS.json; full tool details are restored from registry.
    /// Only stores non-default overrides to minimize file size.
    /// </summary>
    public class ToolOverride
    {
        /// <summary>
        /// Unique name of the tool (e.g., "read_file", "edit_file").
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether this tool is enabled (true) or disabled (false).
        /// Only persisted if different from the built-in default (true).
        /// </summary>
        [JsonProperty("isEnabled")]
        public bool IsEnabled { get; set; } = true;
    }
}
