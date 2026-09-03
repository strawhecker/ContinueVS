#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a tool in OpenAI-compatible JSON Schema format.
    /// Used to describe available tools to the Ollama API for function calling.
    /// </summary>
    public class ToolSchema
    {
        /// <summary>
        /// Type of tool definition. Always "function" for function calling.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        /// <summary>
        /// Function schema details (name, description, parameters).
        /// </summary>
        [JsonProperty("function")]
        public ToolFunctionSchema? Function { get; set; }
    }
}
