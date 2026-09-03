#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents function metadata in OpenAI-compatible function calling schema.
    /// Contains tool name, description, and parameter definitions.
    /// </summary>
    public class ToolFunctionSchema
    {
        /// <summary>
        /// Name of the function (tool). Must be alphanumeric with underscores only.
        /// Pattern: [a-z_][a-z0-9_]*
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what the function does.
        /// Provided to the LLM for understanding tool purpose.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /// <summary>
        /// JSON Schema defining the function's input parameters.
        /// Describes expected arguments, types, and requirements.
        /// </summary>
        [JsonProperty("parameters")]
        public ParametersSchema? Parameters { get; set; }
    }
}
