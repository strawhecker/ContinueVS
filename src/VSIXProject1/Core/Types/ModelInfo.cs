using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents information about an LLM model.
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// Unique identifier for this model.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display name of the model.
        /// </summary>
        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Provider of the model (e.g., OpenAI, Anthropic, Ollama).
        /// </summary>
        [JsonProperty("provider")]
        public string? Provider { get; set; }

        /// <summary>
        /// API key for accessing the model provider.
        /// </summary>
        [JsonProperty("apiKey")]
        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL for API requests (used for self-hosted or alternative providers).
        /// </summary>
        [JsonProperty("baseUrl")]
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Context window size (number of tokens).
        /// </summary>
        [JsonProperty("contextWindow")]
        public int ContextWindow { get; set; }

        /// <summary>
        /// Whether the model supports function calling / tool use.
        /// </summary>
        [JsonProperty("supportsFunctionCalling")]
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// List of supported tool formats (e.g., "openai", "anthropic").
        /// </summary>
        [JsonProperty("supportedToolFormats")]
        public List<string> SupportedToolFormats { get; set; } = new List<string>();
    }
}
