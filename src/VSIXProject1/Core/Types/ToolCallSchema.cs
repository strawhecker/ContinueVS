#nullable enable

using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a tool invocation sent by Ollama in the response message.
    /// Used when the LLM decides to call a tool as part of its response.
    /// </summary>
    public class ToolCallSchema
    {
        /// <summary>
        /// Unique identifier for this tool call instance.
        /// Used to correlate the call with its result in multi-turn conversations.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Type of invocation. Always "function" for tool calls.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        /// <summary>
        /// Function call details (name and arguments).
        /// </summary>
        [JsonProperty("function")]
        public ToolCallFunction? Function { get; set; }
    }
}
