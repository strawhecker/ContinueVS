using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        Failed,

        /// <summary>
        /// Tool execution was skipped due to policy (Disabled, AskFirst not approved, etc.).
        /// </summary>
        Skipped
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
    /// Implements INotifyPropertyChanged to support real-time UI updates during streaming.
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string? _id;
        private ChatMessageRole _role;
        private string _content = string.Empty;
        private List<ToolCall>? _toolCalls;
        private string? _toolCallId;
        private DateTime? _timestamp;
        private ToolInvocationStatus? _invocationStatus;
        private DateTime? _executionStartTime;
        private DateTime? _executionEndTime;
        private MarkdownNode? _renderedMarkdown;

        /// <summary>
        /// Unique identifier for this message.
        /// </summary>
        [JsonProperty("id")]
        public string? Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// Role of the message sender (user, assistant, system, tool, or thinking).
        /// </summary>
        [JsonProperty("role")]
        public ChatMessageRole Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        /// <summary>
        /// Text content of the message.
        /// </summary>
        [JsonProperty("content")]
        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        /// <summary>
        /// Collection of tool calls requested by the assistant.
        /// Null or empty if the message does not request tool execution.
        /// </summary>
        [JsonProperty("toolCalls")]
        public List<ToolCall>? ToolCalls
        {
            get => _toolCalls;
            set => SetProperty(ref _toolCalls, value);
        }

        /// <summary>
        /// Unique identifier linking this tool result message back to the original ToolCall.
        /// Only present in Tool role messages (role=Tool).
        /// Used to correlate tool results with their originating invocations.
        /// </summary>
        [JsonProperty("toolCallId")]
        public string? ToolCallId
        {
            get => _toolCallId;
            set => SetProperty(ref _toolCallId, value);
        }

        /// <summary>
        /// Timestamp when this message was created.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime? Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        /// <summary>
        /// Execution status of a tool invocation (for Role.Tool messages).
        /// Null for non-tool messages.
        /// </summary>
        [JsonIgnore]
        public ToolInvocationStatus? InvocationStatus
        {
            get => _invocationStatus;
            set => SetProperty(ref _invocationStatus, value);
        }

        /// <summary>
        /// Parsed markdown content for rich rendering.
        /// Lazily computed from Content via IMarkdownService.
        /// </summary>
        [JsonIgnore]
        public MarkdownNode? RenderedMarkdown
        {
            get => _renderedMarkdown;
            set => SetProperty(ref _renderedMarkdown, value);
        }

        /// <summary>
        /// Timestamp when tool execution started.
        /// </summary>
        [JsonIgnore]
        public DateTime? ExecutionStartTime
        {
            get => _executionStartTime;
            set => SetProperty(ref _executionStartTime, value);
        }

        /// <summary>
        /// Timestamp when tool execution ended.
        /// </summary>
        [JsonIgnore]
        public DateTime? ExecutionEndTime
        {
            get => _executionEndTime;
            set => SetProperty(ref _executionEndTime, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Sets a property and raises PropertyChanged if the value changed.
        /// </summary>
        protected bool SetProperty<T>(ref T backingField, T newValue, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, newValue))
                return false;

            backingField = newValue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
