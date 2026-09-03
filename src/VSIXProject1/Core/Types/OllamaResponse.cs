#nullable enable

using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a streaming chunk response from the Ollama API chat endpoint.
    /// Sent as NDJSON (newline-delimited JSON); each line during streaming is a separate response.
    /// </summary>
    public class OllamaResponse
    {
        /// <summary>
        /// The model that generated this response.
        /// </summary>
        [JsonProperty("model")]
        public string? Model { get; set; }

        /// <summary>
        /// The response message chunk (contains role and content delta).
        /// </summary>
        [JsonProperty("message")]
        public OllamaMessage? Message { get; set; }

        /// <summary>
        /// Indicates whether this is the final chunk / stream completion.
        /// </summary>
        [JsonProperty("done")]
        public bool Done { get; set; }

        /// <summary>
        /// Optional reason for completion.
        /// Common values: "stop" (normal completion), "length" (max tokens reached), 
        /// "tool_calls" (model called a tool), or other Ollama-specific reasons.
        /// </summary>
        [JsonProperty("done_reason")]
        public string? DoneReason { get; set; }

        /// <summary>
        /// Total tokens in the prompt (for final response only).
        /// </summary>
        [JsonProperty("prompt_eval_count")]
        public int? PromptTokenCount { get; set; }

        /// <summary>
        /// Total tokens in the response (for final response only).
        /// </summary>
        [JsonProperty("eval_count")]
        public int? ResponseTokenCount { get; set; }

        /// <summary>
        /// Time taken to evaluate the prompt (milliseconds).
        /// </summary>
        [JsonProperty("eval_duration")]
        public long? EvalDurationMs { get; set; }

        /// <summary>
        /// Timestamp when this response was generated.
        /// </summary>
        [JsonProperty("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
