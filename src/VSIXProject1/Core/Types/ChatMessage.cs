using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the role of a chat message in the conversation.
    /// </summary>
    public enum ChatMessageRole
    {
        /// <summary>
        /// User-initiated message.
        /// </summary>
        User,

        /// <summary>
        /// Assistant (LLM) response message.
        /// </summary>
        Assistant,

        /// <summary>
        /// System-level instruction or context.
        /// </summary>
        System,

        /// <summary>
        /// Result of a tool execution.
        /// </summary>
        Tool,

        /// <summary>
        /// Internal reasoning or thinking (for models that support it).
        /// </summary>
        Thinking
    }

    /// <summary>
    /// Represents the execution status of a tool invocation.
    /// </summary>
    public enum ToolInvocationStatus
    {
        /// <summary>
        /// Tool call has been detected but not yet executed.
        /// </summary>
        Pending,

        /// <summary>
        /// Tool is currently being executed.
        /// </summary>
        Running,

        /// <summary>
        /// Tool execution completed successfully.
        /// </summary>
        Complete,

        /// <summary>
        /// Tool execution failed with an error.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Represents a tool invocation request within a message.
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// Unique identifier for this tool call.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Name of the tool to invoke.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Arguments to pass to the tool, keyed by parameter name.
        /// </summary>
        [JsonProperty("arguments")]
        public IDictionary<string, object>? Arguments { get; set; }
    }

    /// <summary>
    /// Represents a message in a chat conversation.
    /// Used by ILlmService and ISessionService for message exchanges.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Unique identifier for this message.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        /// <summary>
        /// Role of the message sender (user, assistant, system, tool, or thinking).
        /// </summary>
        [JsonProperty("role")]
        public ChatMessageRole Role { get; set; }

        /// <summary>
        /// Text content of the message.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Collection of tool calls requested by the assistant.
        /// Null or empty if the message does not request tool execution.
        /// </summary>
        [JsonProperty("toolCalls")]
        public List<ToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// Timestamp when this message was created.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Execution status of a tool invocation (for Role.Tool messages).
        /// Null for non-tool messages.
        /// </summary>
        [JsonIgnore]
        public ToolInvocationStatus? InvocationStatus { get; set; }

        /// <summary>
        /// Timestamp when tool execution started.
        /// </summary>
        [JsonIgnore]
        public DateTime? ExecutionStartTime { get; set; }

        /// <summary>
        /// Timestamp when tool execution ended.
        /// </summary>
        [JsonIgnore]
        public DateTime? ExecutionEndTime { get; set; }
    }
}
