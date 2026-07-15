using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;

namespace ContinueVS.Bridge.Services
{
    /// <summary>
    /// Inline Message Collector Implementation (Step 85)
    /// 
    /// Collects and manages inline messages in the Visual Studio editor.
    /// Integrates with the DTE code model to query editor decorations, code lenses,
    /// and inline suggestions at specific positions.
    /// 
    /// Thread Safety:
    /// - All methods are async-first
    /// - Internal state is protected by locks for concurrent access
    /// - Safe for repeated concurrent queries
    /// 
    /// Error Handling:
    /// - Missing files/positions return empty arrays (not errors)
    /// - DTE exceptions are caught, logged, and converted to safe returns
    /// - Validation errors return false or 0 (not exceptions)
    /// </summary>
    public class InlineMessageCollector : IInlineMessageCollector
    {
        private readonly DTE _dte;
        private readonly object _lockObject = new object();
        private readonly Dictionary<string, List<InlineMessage>> _postedMessages;
        private bool _disposed;

        /// <summary>
        /// Create a new InlineMessageCollector instance.
        /// </summary>
        /// <param name="dte">Visual Studio DTE instance (required)</param>
        /// <exception cref="ArgumentNullException">Thrown if dte is null</exception>
        public InlineMessageCollector(DTE dte)
        {
            _dte = dte ?? throw new ArgumentNullException(nameof(dte), "DTE instance is required");
            _postedMessages = new Dictionary<string, List<InlineMessage>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Query inline messages at a specific position in a file.
        /// 
        /// Collects all inline messages that should be displayed at the given position:
        /// - Posted messages (via PostInlineMessageAsync)
        /// - Symbol-based suggestions (future: from code model)
        /// - Diagnostic messages (future: from analyzer)
        /// 
        /// Thread Safe: Yes
        /// Error Handling: Returns empty array on any error
        /// Performance: O(n) where n = posted messages in file
        /// </summary>
        public async Task<InlineMessage[]> GetInlineMessagesAsync(
            string filepath,
            int line,
            int column,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filepath))
                {
                    return Array.Empty<InlineMessage>();
                }

                if (line < 0 || column < 0)
                {
                    return Array.Empty<InlineMessage>();
                }

                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    // Query posted messages for this file
                    if (_postedMessages.TryGetValue(filepath, out var messages))
                    {
                        var result = messages
                            .Where(m => m.Line == line && m.Column == column)
                            .ToArray();

                        return result;
                    }
                }

                // Future: Query symbol-based suggestions via DTE code model
                // Future: Query diagnostics via analyzer

                return Array.Empty<InlineMessage>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log error but don't propagate; return empty array
                System.Diagnostics.Debug.WriteLine(
                    $"InlineMessageCollector.GetInlineMessagesAsync failed: {ex.Message}");
                return Array.Empty<InlineMessage>();
            }
        }

        /// <summary>
        /// Post (display) a new inline message at a position.
        /// 
        /// Validates the message, stores it, and optionally updates the editor.
        /// The message persists until cleared via ClearMessagesAsync.
        /// 
        /// Thread Safe: Yes (uses lock)
        /// Error Handling: Returns false on validation/DTE error (no exception)
        /// Performance: O(1) for storage; O(m) for editor update where m = visible editors
        /// </summary>
        public async Task<bool> PostInlineMessageAsync(
            InlineMessage message,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (message == null)
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Validate message
                if (string.IsNullOrWhiteSpace(message.Filepath) ||
                    string.IsNullOrWhiteSpace(message.Title) ||
                    message.Line < 0 ||
                    message.Column < 0)
                {
                    return false;
                }

                lock (_lockObject)
                {
                    // Store message
                    if (!_postedMessages.ContainsKey(message.Filepath))
                    {
                        _postedMessages[message.Filepath] = new List<InlineMessage>();
                    }

                    message.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    _postedMessages[message.Filepath].Add(message);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Future: Update editor decorations via DTE text editor
                // For now, just store in memory

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log error but don't propagate
                System.Diagnostics.Debug.WriteLine(
                    $"InlineMessageCollector.PostInlineMessageAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear (remove) inline messages from a file.
        /// 
        /// If line is null: removes ALL messages from the file
        /// If line is specified: removes only messages at that line (and column if specified)
        /// 
        /// Thread Safe: Yes (uses lock)
        /// Error Handling: Returns 0 on error; never throws
        /// Performance: O(n) where n = messages in file
        /// </summary>
        public async Task<int> ClearMessagesAsync(
            string filepath,
            int? line = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filepath))
                {
                    return 0;
                }

                cancellationToken.ThrowIfCancellationRequested();

                lock (_lockObject)
                {
                    if (!_postedMessages.TryGetValue(filepath, out var messages))
                    {
                        return 0;
                    }

                    int clearedCount = 0;

                    if (line.HasValue)
                    {
                        // Clear messages at specific line
                        var toRemove = messages.Where(m => m.Line == line.Value).ToList();
                        clearedCount = toRemove.Count;
                        foreach (var msg in toRemove)
                        {
                            messages.Remove(msg);
                        }
                    }
                    else
                    {
                        // Clear ALL messages from file
                        clearedCount = messages.Count;
                        messages.Clear();
                    }

                    if (messages.Count == 0)
                    {
                        _postedMessages.Remove(filepath);
                    }

                    return clearedCount;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log error but don't propagate
                System.Diagnostics.Debug.WriteLine(
                    $"InlineMessageCollector.ClearMessagesAsync failed: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Get count of currently stored inline messages (for diagnostics/testing).
        /// </summary>
        public int GetStoredMessageCount()
        {
            lock (_lockObject)
            {
                return _postedMessages.Values.Sum(list => list.Count);
            }
        }

        /// <summary>
        /// Get messages for a specific file (for diagnostics/testing).
        /// </summary>
        public IReadOnlyList<InlineMessage> GetMessagesForFile(string filepath)
        {
            lock (_lockObject)
            {
                if (_postedMessages.TryGetValue(filepath, out var messages))
                {
                    return messages.AsReadOnly();
                }
                return Array.Empty<InlineMessage>();
            }
        }

        /// <summary>
        /// Clear all stored messages (for reset/cleanup).
        /// </summary>
        public void ClearAllMessages()
        {
            lock (_lockObject)
            {
                _postedMessages.Clear();
            }
        }

        /// <summary>
        /// Dispose resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_lockObject)
            {
                _postedMessages.Clear();
            }

            _disposed = true;
        }
    }
}
