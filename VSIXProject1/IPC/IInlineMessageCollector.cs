using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Bridge.IPC
{
    /// <summary>
    /// Interface for collecting and managing inline messages in the IDE editor.
    /// 
    /// Inline messages are decorators, code lenses, and inline suggestions displayed
    /// at specific positions in the code editor. They can represent:
    /// - Code fixes (light bulb suggestions)
    /// - Code improvements (suggestions)
    /// - Diagnostic information (warnings, errors)
    /// - AI-generated hints (inlay hints, inline comments)
    /// 
    /// Implementation Contract:
    /// - Must be async-first (all methods return Task)
    /// - Must handle null/missing files gracefully (return empty array, not throw)
    /// - Must validate position bounds (negative/out-of-bounds returns empty)
    /// - Must normalize file paths (relative to workspace root)
    /// - Must be thread-safe for concurrent queries
    /// 
    /// Integration Points:
    /// - Consumed by: inline-message-handler.mjs (Node.js handler)
    /// - Produces: InlineMessage objects with metadata and UI hints
    /// - Related: ISymbolExtractor (symbol info), IDiagnosticsCollector (errors/warnings)
    /// </summary>
    public interface IInlineMessageCollector
    {
        /// <summary>
        /// Query inline messages at a specific position in a file.
        /// 
        /// This method collects all inline messages (decorators, code lenses, suggestions)
        /// that should be displayed at the given position. It may query:
        /// - Symbol metadata (via ISymbolExtractor)
        /// - Diagnostics (via IDiagnosticsCollector)
        /// - Code model (via DTE)
        /// - AI suggestions (cached or external)
        /// 
        /// Error Handling:
        /// - File not found: returns empty array
        /// - Position out of bounds: returns empty array
        /// - DTE/collection error: logs warning, returns empty array
        /// - Thread aborted (CancellationToken): throws OperationCanceledException
        /// 
        /// Performance:
        /// - Expected latency: 10-50ms (typically cached)
        /// - Should use fast-path for repeated queries
        /// - May implement per-file caching
        /// 
        /// Thread Safety:
        /// - Must be safe for concurrent calls
        /// - May use internal locking if necessary
        /// 
        /// </summary>
        /// <param name="filepath">
        /// Absolute or workspace-relative file path (e.g., "src/MyClass.cs" or "C:\proj\src\MyClass.cs")
        /// </param>
        /// <param name="line">0-based line number in file</param>
        /// <param name="column">0-based column position in line</param>
        /// <param name="cancellationToken">
        /// Cancellation token for async operations. If cancelled, must throw OperationCanceledException.
        /// </param>
        /// <returns>
        /// Array of InlineMessage objects (may be empty if no messages at position).
        /// Never returns null; returns empty array instead.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled</exception>
        Task<InlineMessage[]> GetInlineMessagesAsync(
            string filepath,
            int line,
            int column,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Post (display) a new inline message at a position.
        /// 
        /// This method displays a new inline message (decorator, code lens, suggestion)
        /// at the specified position. The message persists until explicitly cleared.
        /// 
        /// Implementation should:
        /// - Validate message fields (non-empty title, valid position)
        /// - Store/register message in IDE (DTE editor decorators or code lens provider)
        /// - Update any visible editors showing the file
        /// - Record metric/telemetry for analytics
        /// 
        /// Error Handling:
        /// - Invalid filepath: return false (log warning)
        /// - Invalid position: return false (log warning)
        /// - Null/empty title: return false (log warning)
        /// - DTE/registration error: return false (log error), do not propagate
        /// - Thread aborted: throw OperationCanceledException
        /// 
        /// Performance:
        /// - Expected latency: 5-20ms
        /// - Should be fast (UI-blocking operation)
        /// 
        /// Thread Safety:
        /// - Must be safe for concurrent calls
        /// - May use internal locking
        /// 
        /// </summary>
        /// <param name="message">
        /// InlineMessage object with all required fields populated (filepath, line, column, title)
        /// </param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>
        /// true if message was successfully posted and is now visible;
        /// false if posting failed (validation, DTE error, etc.)
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled</exception>
        Task<bool> PostInlineMessageAsync(
            InlineMessage message,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Clear (remove) inline messages from a file.
        /// 
        /// This method removes inline messages previously posted via PostInlineMessageAsync.
        /// 
        /// Modes:
        /// - If line is null: remove ALL messages from the file
        /// - If line is not null: remove only messages at that specific line (and column if specified)
        /// 
        /// Implementation should:
        /// - Remove messages from internal storage
        /// - Update any visible editors showing the file
        /// - Return count of messages removed
        /// 
        /// Error Handling:
        /// - File not found: return 0 (no error)
        /// - No messages at position: return 0 (no error)
        /// - DTE/removal error: log warning, return best-effort count
        /// - Thread aborted: throw OperationCanceledException
        /// 
        /// Performance:
        /// - Expected latency: 5-15ms
        /// - Should be fast (UI-blocking operation)
        /// 
        /// Thread Safety:
        /// - Must be safe for concurrent calls
        /// - May use internal locking
        /// 
        /// </summary>
        /// <param name="filepath">File path (absolute or workspace-relative)</param>
        /// <param name="line">
        /// 0-based line number. If null, clear ALL messages from file.
        /// If not null, clear only at this line (and column if provided).
        /// </param>
        /// <param name="cancellationToken">Cancellation token for async operations</param>
        /// <returns>
        /// Number of messages cleared. Returns 0 if no messages were found or removed.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled</exception>
        Task<int> ClearMessagesAsync(
            string filepath,
            int? line = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Inline message data structure for display in editor.
    /// 
    /// Represents a single inline message (decorator, code lens, suggestion) to be
    /// displayed at a specific position. Includes metadata about type, appearance, and interactivity.
    /// 
    /// Properties:
    /// - Position: filepath, line, column
    /// - Content: title (primary text), description (full text)
    /// - Appearance: iconName, color, actionType
    /// - Interactivity: clickable (whether user can interact)
    /// - Metadata: createdAt (timestamp for UI sorting)
    /// </summary>
    public class InlineMessage
    {
        /// <summary>
        /// Absolute or workspace-relative file path (e.g., "src/MyClass.cs")
        /// Required. Must not be null or empty.
        /// </summary>
        public string Filepath { get; set; }

        /// <summary>
        /// 0-based line number in the file.
        /// Required. Must be >= 0.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// 0-based column position in the line.
        /// Required. Must be >= 0.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Type of action/message: "fix" | "suggest" | "info" | "warning"
        /// 
        /// - "fix": Code fix suggestion (light bulb)
        /// - "suggest": Code improvement suggestion
        /// - "info": Informational message
        /// - "warning": Warning/caution message
        /// 
        /// Used for styling, icon selection, and UI presentation.
        /// Optional; defaults to "info".
        /// </summary>
        public string ActionType { get; set; } = "info";

        /// <summary>
        /// Primary message text (short, shown in inline decoration).
        /// Required. Must not be null or empty.
        /// Typically 1-50 characters (e.g., "Unused variable", "Extract to method").
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Full message description (shown in tooltip or expanded view).
        /// Optional. May be longer (100-500 characters).
        /// Markdown formatting is NOT supported; use plain text.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Icon identifier for the message.
        /// 
        /// Supported values:
        /// - "lightbulb": Code fix icon
        /// - "info": Info icon
        /// - "warning": Warning icon
        /// - "error": Error icon
        /// - "suggest": Suggestion icon
        /// - "custom": Custom icon (color will be used)
        /// 
        /// Optional; defaults to "info".
        /// </summary>
        public string IconName { get; set; } = "info";

        /// <summary>
        /// CSS color or theme color name for the message.
        /// 
        /// Examples:
        /// - "#FF6B6B" (red)
        /// - "#4ECDC4" (teal)
        /// - "rgb(255, 100, 100)" (red)
        /// - "gold" (named color)
        /// - "var(--vs-editor-informationForeground)" (VS Code theme var)
        /// 
        /// Optional; defaults to "#808080" (gray).
        /// </summary>
        public string Color { get; set; } = "#808080";

        /// <summary>
        /// Whether the message is interactive (clickable).
        /// If true, clicking the message may trigger an action (e.g., apply fix).
        /// If false, message is display-only.
        /// 
        /// Optional; defaults to true.
        /// </summary>
        public bool Clickable { get; set; } = true;

        /// <summary>
        /// Unix timestamp (milliseconds since epoch) when message was created.
        /// Used for UI sorting and display.
        /// 
        /// Set by the collector or handler; typically DateTime.UtcNow.Ticks * 100 converted to ms.
        /// </summary>
        public long CreatedAt { get; set; }
    }
}
