#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents JSON Schema for tool parameters in OpenAI-compatible format.
    /// Defines the structure and requirements for function arguments.
    /// </summary>
    public class ParametersSchema
    {
        /// <summary>
        /// JSON Schema type. Always "object" for function parameters.
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        /// <summary>
        /// Dictionary mapping parameter names to their definitions.
        /// Each key is a parameter name; each value describes its type, description, etc.
        /// </summary>
        [JsonProperty("properties")]
        public Dictionary<string, ParameterDefinition>? Properties { get; set; }

        /// <summary>
        /// List of required parameter names.
        /// Parameters listed here must be provided when invoking the function.
        /// </summary>
        [JsonProperty("required")]
        public List<string>? Required { get; set; }
    }
}
