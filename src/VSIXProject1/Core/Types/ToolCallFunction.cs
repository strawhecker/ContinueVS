#nullable enable

using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents function call details within a ToolCallSchema.
    /// Contains the function name and arguments as a JSON string.
    /// </summary>
    public class ToolCallFunction
    {
        /// <summary>
        /// Name of the function being called.
        /// Must match a tool name from the available tools list.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Function arguments as a JSON string.
        /// Must be parsed (deserialized) to obtain the actual argument dictionary.
        /// Preserved as string to maintain exact formatting and allow lazy parsing.
        /// </summary>
        [JsonProperty("arguments")]
        public string? Arguments { get; set; }
    }
}
