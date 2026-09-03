using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the type of content in a streaming completion chunk.
    /// </summary>
    public enum ChunkType
    {
        /// <summary>
        /// Text content delta (incremental text of the response).
        /// </summary>
        Text,

        /// <summary>
        /// Tool call invocation within the stream.
        /// </summary>
        ToolCall,

        /// <summary>
        /// Stream completion marker (no more chunks expected).
        /// </summary>
        Done
    }

    /// <summary>
    /// Represents an incremental chunk of an LLM streaming response.
    /// Used by ILlmService.StreamAsync() to deliver streaming completions.
    /// </summary>
    public class CompletionChunk
    {
        /// <summary>
        /// Type of chunk content (Text, ToolCall, or Done).
        /// </summary>
        [JsonProperty("type")]
        public ChunkType Type { get; set; } = ChunkType.Text;

        /// <summary>
        /// Text delta content (for Type=Text chunks).
        /// Contains the incremental text from providers like OpenAI delta.content.
        /// Null for non-text chunk types.
        /// </summary>
        [JsonProperty("content")]
        public string? Content { get; set; }

        /// <summary>
        /// Role of the message sender (typically 'assistant' for streaming responses).
        /// Optional; included for context in multi-turn conversations.
        /// </summary>
        [JsonProperty("role")]
        public ChatMessageRole? Role { get; set; }

        /// <summary>
        /// Tool call data (for Type=ToolCall chunks).
        /// Contains the tool name, id, and arguments.
        /// Null for non-tool chunk types.
        /// </summary>
        [JsonProperty("toolCall")]
        public ToolCall? ToolCall { get; set; }

        /// <summary>
        /// List of tool calls accumulated from the response.
        /// Present when the LLM invokes multiple tools or when streaming completes with tool calls.
        /// Null if no tool calls are present.
        /// </summary>
        [JsonProperty("toolCalls")]
        public List<ToolCallSchema>? ToolCalls { get; set; }

        /// <summary>
        /// Indicates whether this chunk marks the end of the stream.
        /// True when Type=Done or when stream has completed.
        /// </summary>
        [JsonProperty("isDone")]
        public bool IsDone { get; set; }

        /// <summary>
        /// Reason for stream completion (e.g., "stop", "length", "tool_calls").
        /// Provides context about why streaming ended.
        /// </summary>
        [JsonProperty("doneReason")]
        public string? DoneReason { get; set; }

        /// <summary>
        /// Timestamp when this chunk was received from the provider.
        /// Used for latency tracking and debugging.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime? Timestamp { get; set; }
    }
}
