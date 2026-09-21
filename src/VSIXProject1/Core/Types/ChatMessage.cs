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
        private bool _isDeleted = false;

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
        /// Tracks how far into the buffer the incremental TokenAppended path has already emitted.
        /// Always &lt;= _writePosition. This lets consumers receive only the NEW text each time,
        /// without the producer assembling a full cumulative string on every addition.
        /// </summary>
        private int _tokenEmittedIndex = 0;

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
        /// Raised during streaming when a new segment of content is available.
        /// The handler receives ONLY the newly arrived portion (passed through directly
        /// from the buffer, never a cumulative reassembly of the whole message).
        /// This is the incremental path; it complements the full-replacement Path
        /// (RendererContent / Content) used for new-user messages and session reopen.
        /// </summary>
        public event Action<string>? TokenAppended;

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
                    _tokenEmittedIndex = 0;
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

        /// <summary>
        /// Soft-delete tombstone (gap80). A message marked Deleted is retained in the session
        /// (bytes stay accessible for version retention / undelete), but is EXCLUDED from the
        /// payload serialized to the LLM at request time. Never hard-remove a soft-deleted entry.
        /// Serves both auto-dedup (this gap) and manual user-prune (gap81).
        /// </summary>
        [JsonProperty("isDeleted")]
        public bool IsDeleted
        {
            get => _isDeleted;
            set => SetProperty(ref _isDeleted, value);
        }

        private bool _isMinimized = false;

        /// <summary>
        /// gap85: UI-only state flag indicating whether the message bubble is minimized
        /// (collapsed). Purely visual; NOT a tombstone and NOT persisted. The minimize/maximize
        /// toggle flips this flag to show "▢/⤢" (maximize) when minimized or "_" (minimize) when
        /// expanded. Independent of the delete/undelete tombstone path. Applies to user, reason,
        /// response, and tool-call entries alike.
        /// </summary>
        [JsonIgnore]
        public bool IsMinimized
        {
            get => _isMinimized;
            set => SetProperty(ref _isMinimized, value);
        }

        private string? _toolName;

        /// <summary>
        /// gap85: Name of the built-in tool associated with a Tool-role message (e.g. "read_file").
        /// Stored on the message so compact tool-call bubbles can render the tool name + optional
        /// file used without dumping full JSON/arguments. Set when tool result messages are created.
        /// </summary>
        [JsonIgnore]
        public string? ToolName
        {
            get => _toolName;
            set => SetProperty(ref _toolName, value);
        }

        private string? _toolCallDescription;

        /// <summary>
        /// gap87: Short, factual, human-readable description of what this tool call did, fabricated
        /// in code from the tool-call arguments — never from LLM prose. Display-only: it is
        /// [JsonIgnore] and never written into <see cref="Content"/>, so it is never serialized back
        /// into the LLM payload regardless of length. Rendered as the primary line of the gap85
        /// tool-call bubble so the user can tell what happened enough to evaluate/prune the entry.
        /// </summary>
        [JsonIgnore]
        public string? ToolCallDescription
        {
            get => _toolCallDescription;
            set => SetProperty(ref _toolCallDescription, value);
        }

        /// <summary>
        /// gap85: Compact display label for a tool-call bubble. Returns the tool name
        /// (an explicit ToolName, the first request's tool name, or "tool"). Never dumps
        /// full tool-call JSON/arguments — keeps the bubble compact.
        /// </summary>
        [JsonIgnore]
        public string ToolCallLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_toolName))
                    return _toolName!;
                if (_toolCalls != null && _toolCalls.Count > 0 && _toolCalls[0] is ToolCall first && !string.IsNullOrWhiteSpace(first.Name))
                    return first.Name;
                return "tool";
            }
        }

        /// <summary>
        /// gap85: Truncated file name (basename or short path) referenced by a tool call,
        /// when one is present in the arguments (filepath/path/file/filename). Falls back to
        /// looking at the message content. Returns null when nothing is found. Kept short so
        /// the tool bubble stays compact.
        /// </summary>
        [JsonIgnore]
        public string? ToolFileName
        {
            get
            {
                var raw = ExtractToolFilePath();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                // After the null/whitespace guard, raw is guaranteed non-null.
                string path = raw!;

                // Show a short basename when the path is long/absolute.
                try
                {
                    var name = System.IO.Path.GetFileName(path);
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
                catch
                {
                    // fall through to raw
                }

                if (path.Length > 40)
                    return path.Substring(path.Length - 40);
                return path;
            }
        }

        private string? ExtractToolFilePath()
        {
            if (_toolCalls != null && _toolCalls.Count > 0 && _toolCalls[0] is ToolCall first && first.Arguments != null)
            {
                foreach (var key in new[] { "filepath", "path", "file", "filename" })
                {
                    if (first.Arguments.TryGetValue(key, out var val) && val is string s && !string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            // Fall back to scanning content for a path-like token.
            if (!string.IsNullOrWhiteSpace(_content))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    _content,
                    @"([A-Za-z]:\\[^\s]+|/[a-zA-Z0-9._\-/]+(?:\.\w+)?)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                    return match.Groups[1].Value;
            }
            return null;
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
        /// Raises TokenAppended with ONLY the buffer content not yet emitted, advancing
        /// _tokenEmittedIndex to the current write position. Safe to call multiple times.
        /// </summary>
        private void EmitPendingTokens()
        {
            if (_tokenEmittedIndex >= _writePosition)
                return;

            string segment = new string(_contentBuffer, _tokenEmittedIndex, _writePosition - _tokenEmittedIndex);
            _tokenEmittedIndex = _writePosition;
            TokenAppended?.Invoke(segment);
        }

        /// <summary>
        /// Appends a chunk of streamed content to the message buffer.
        /// Buffers content without firing PropertyChanged until complete lines or word boundaries arrive.
        /// Fires PropertyChanged (full replacement) at boundaries AND raises TokenAppended
        /// with only the new segment (incremental, passed through — no cumulative build).
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

            // Emit the new text through the incremental path (pass-through only).
            EmitPendingTokens();

            // Fire full-replacement update on complete lines / word boundaries
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
        /// Flushes any remaining un-emitted tokens through the incremental path.
        /// Call this when streaming ends, even if the response doesn't end with a newline.
        /// </summary>
        public void FinalizeStreaming()
        {
            if (!_isFinalized)
            {
                _content = new string(_contentBuffer, 0, _writePosition);
                _isFinalized = true;

                // Flush any trailing content that was never emitted through the incremental path.
                EmitPendingTokens();

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
            }
        }
    }
}
