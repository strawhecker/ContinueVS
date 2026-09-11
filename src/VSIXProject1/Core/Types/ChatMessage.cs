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
        private bool _isThinking = false;
        private bool _isExpanded = false;

        /// <summary>
        /// Dynamic buffer for accumulating streaming content.
        /// Starts at 1KB and doubles as needed. Used during active streaming to avoid PropertyChanged thrashing.
        /// </summary>
        private char[] _contentBuffer = new char[1024];

        /// <summary>
        /// Current write position in the content buffer.
        /// </summary>
        private int _writePosition = 0;

        /// <summary>
        /// Tracks the last position where content triggered a PropertyChanged update.
        /// Used to determine segment boundaries for UI refresh frequency.
        /// </summary>
        private int _lastSegmentIndex = 0;

        /// <summary>
        /// Flag indicating whether streaming is complete and content has been finalized to _content string.
        /// When true, Content getter returns cached _content; when false, returns buffer snapshot.
        /// </summary>
        private bool _isFinalized = false;

        /// <summary>
        /// Word boundary characters used for update throttling when lines exceed 50 chars
        /// without encountering a newline.
        /// </summary>
        private static readonly char[] BoundaryTargets = { ' ', '\t', '\n', ',', '.', '!', '?', ';' };

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
        /// During streaming, returns a snapshot of the accumulated buffer. After finalization, returns cached string.
        /// Suitable for JSON serialization and external API exchanges.
        /// </summary>
        [JsonProperty("content")]
        public string Content
        {
            get => _isFinalized ? _content : new string(_contentBuffer, 0, _writePosition);
            set
            {
                if (SetProperty(ref _content, value))
                {
                    _isFinalized = true;
                    _writePosition = 0;
                    _lastSegmentIndex = 0;
                }
            }
        }

        /// <summary>
        /// Collection of tool calls requested by the assistant.
        /// Null or empty if the message does not request tool execution.
        /// Persisted in session history for LLM context, but NOT exposed in user-visible responses.
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

        /// <summary>
        /// Flag indicating that this message content should be excluded from context window calculations.
        /// Used to mark thinking/reasoning blocks that should not be included in future chat message contexts.
        /// Persisted to disk via JSON serialization.
        /// </summary>
        [JsonProperty("isThinking")]
        public bool IsThinking
        {
            get => _isThinking;
            set => SetProperty(ref _isThinking, value);
        }

        /// <summary>
        /// UI state flag indicating whether the thinking block is expanded or collapsed in the transcript.
        /// Not persisted to disk; allows user's expanded/collapsed preference to be restored when history is reopened.
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
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

        /// <summary>
        /// Appends a chunk of streamed content to the message buffer.
        /// Buffers content without firing PropertyChanged until complete lines or word boundaries arrive.
        /// Fires PropertyChanged only at meaningful boundaries to optimize UI responsiveness.
        /// </summary>
        /// <param name="chunk">The text chunk to append from the stream. Null or empty chunks are safely ignored.</param>
        public void AppendChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return;

            // Resize buffer if needed (double capacity)
            if (_writePosition + chunk.Length > _contentBuffer.Length)
            {
                Array.Resize(ref _contentBuffer, _contentBuffer.Length * 2);
            }

            // Write chunk to buffer (no PropertyChanged fired yet)
            chunk.CopyTo(0, _contentBuffer, _writePosition, chunk.Length);
            _writePosition += chunk.Length;

            // Fire update on complete lines
            if (_writePosition - _lastSegmentIndex > 50)
            {
                int lastBoundary = chunk.LastIndexOfAny(BoundaryTargets);
                if (lastBoundary >= 0)
                {
                    _lastSegmentIndex = _writePosition - (chunk.Length - lastBoundary - 1);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
                }
            }
        }

        /// <summary>
        /// Finalizes streaming by converting the buffer to a cached string.
        /// After finalization, the Content getter returns the string without allocating new strings on every access.
        /// Call this when streaming ends, even if the response doesn't end with a newline.
        /// </summary>
        public void FinalizeStreaming()
        {
            if (!_isFinalized)
            {
                _content = new string(_contentBuffer, 0, _writePosition);
                _isFinalized = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
            }
        }
    }
}
