#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a request to the Ollama API chat endpoint.
    /// </summary>
    public class OllamaRequest
    {
        /// <summary>
        /// Name of the model to use for completion.
        /// </summary>
        [JsonProperty("model")]
        public string? Model { get; set; }

        /// <summary>
        /// Messages to process (chat history + current user message).
        /// Each message has role (system, user, assistant) and content.
        /// </summary>
        [JsonProperty("messages")]
        public List<OllamaMessage> Messages { get; set; } = new List<OllamaMessage>();

        /// <summary>
        /// Whether to stream the response (true for chunked completions).
        /// </summary>
        [JsonProperty("stream")]
        public bool Stream { get; set; } = true;

        /// <summary>
        /// Optional sampling temperature (0.0 to 2.0).
        /// </summary>
        [JsonProperty("options")]
        public OllamaOptions? Options { get; set; }
    }

    /// <summary>
    /// Represents a single message in an Ollama chat request.
    /// </summary>
    public class OllamaMessage
    {
        /// <summary>
        /// Role of the message sender: "system", "user", or "assistant".
        /// </summary>
        [JsonProperty("role")]
        public string? Role { get; set; }

        /// <summary>
        /// Text content of the message.
        /// </summary>
        [JsonProperty("content")]
        public string? Content { get; set; }
    }

    /// <summary>
    /// Represents sampling options for Ollama inference.
    /// </summary>
    public class OllamaOptions
    {
        /// <summary>
        /// Sampling temperature; controls randomness (0.0=deterministic, 2.0=maximum randomness).
        /// </summary>
        [JsonProperty("temperature")]
        public double? Temperature { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate in the response.
        /// </summary>
        [JsonProperty("num_predict")]
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Top-p (nucleus) sampling parameter.
        /// </summary>
        [JsonProperty("top_p")]
        public double? TopP { get; set; }
    }
}
