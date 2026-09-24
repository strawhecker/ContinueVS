using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of ISessionService that manages conversation sessions.
    ///
    /// gap83 — Persistence is a write-only, append-only JSONL delta log. There is no
    /// full-file rewrite: each mutation serializes ONE tagged delta line to a dedicated
    /// background writer thread, and the session is reconstructed by replaying the log
    /// from the beginning on reopen. An O(1) dictionary holds the SAME ChatMessage
    /// references as <see cref="Session.Messages"/>, so updates are O(1) and
    /// INotifyPropertyChanged is preserved. <see cref="Session.Messages"/> is the live
    /// LLM surface. Reads (reopen) run on a pooled worker thread via Task.Run so the
    /// UI is never blocked by a long session.
    /// </summary>
    public class SessionService : ISessionService, IDisposable
    {
        private Session? _currentSession;
        private readonly object _lockObj = new object();
        private readonly ITokenCountingService _tokenCountingService;
        private readonly SessionDeltaLog _deltaLog;
        private readonly Dictionary<string, ChatMessage> _messageIndex = new Dictionary<string, ChatMessage>();
        private readonly HashSet<string> _initializedSessions = new HashSet<string>();
        private ContextBudgetState _cachedBudgetState = ContextBudgetState.Safe;
        private bool _disposed;

        /// <summary>
        /// Computes the base directory for session storage: ~/.continueVS/sessions/
        /// </summary>
        private static string DefaultSessionStoragePath
        {
            get
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, ".continueVS", "sessions");
            }
        }

        /// <summary>
        /// Creates a SessionService using the default ~/.continueVS/sessions directory.
        /// </summary>
        public SessionService(ITokenCountingService tokenCountingService)
            : this(tokenCountingService, DefaultSessionStoragePath)
        {
        }

        /// <summary>
        /// Creates a SessionService writing to an explicit storage directory (used by
        /// tests for isolation; the default constructor preserves DI compatibility).
        /// </summary>
        public SessionService(ITokenCountingService tokenCountingService, string storageDirectory)
        {
            _tokenCountingService = tokenCountingService ?? throw new ArgumentNullException(nameof(tokenCountingService));
            _deltaLog = new SessionDeltaLog(storageDirectory);
        }

        public event EventHandler<SessionChangedEventArgs>? SessionChanged;
        public event EventHandler<MessageAddedEventArgs>? MessageAdded;

        /// <summary>
        /// Gets the currently active session.
        /// Creates a default empty session if none exists on first call.
        /// </summary>
        public Session GetCurrentSession()
        {
            lock (_lockObj)
            {
                if (_currentSession == null)
                {
                    _currentSession = new Session
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = "New Conversation",
                        Messages = new List<ChatMessage>(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        IsActive = true
                    };
                }
                return _currentSession;
            }
        }

        /// <summary>
        /// Creates a new session and sets it as the current session.
        /// </summary>
        public async Task CreateNewSessionAsync(string? title = null)
        {
            await EnsureSessionsDirectoryAsync();

            var newSession = new Session
            {
                Id = Guid.NewGuid().ToString(),
                Title = title ?? "New Conversation",
                Messages = new List<ChatMessage>(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsActive = true,
                ToolCallsExecuted = 0
            };

            lock (_lockObj)
            {
                _currentSession = newSession;
                RebuildIndexFor(newSession);
            }

            await SaveSessionToFileAsync(newSession);

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = newSession.Id,
                ChangeType = SessionChangeType.Created,
                Session = newSession,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Saves the current session to the JSONL delta log: guarantees the init header
        /// is present and flushes all pending deltas to disk.
        /// </summary>
        public async Task SaveCurrentSessionAsync()
        {
            var session = GetCurrentSession();
            session.UpdatedAt = DateTime.Now;
            await SaveSessionToFileAsync(session);

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Loads a session by ID and sets it as the current session.
        /// Replays the JSONL log from the beginning on a background worker thread so the
        /// UI is never blocked by a long session; the async continuation wires results.
        /// Restores mode from the replayed init delta (gap27_5).
        /// </summary>
        public async Task LoadSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            // Flush any in-flight writes so the reader sees an ordered, complete log.
            _deltaLog.FlushSync();

            var session = await LoadSessionFromFileAsync(sessionId);

            lock (_lockObj)
            {
                _currentSession = session;
                RebuildIndexFor(session);
            }

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                CurrentMode = session.Mode,  // Restore mode from persisted Session.Mode (gap27_5)
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Adds a message to the current session: mutates in-memory O(1), enqueues an
        /// "add" delta, and flushes so the message is durable before returning. The
        /// instance stored in Session.Messages is the SAME reference tracked by the
        /// O(1) dictionary (INPC preserved).
        /// </summary>
        public async Task AddMessageAsync(ChatMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var session = GetCurrentSession();

            // Assign ID if not present
            if (string.IsNullOrEmpty(message.Id))
            {
                message.Id = Guid.NewGuid().ToString();
            }

            if (message.Timestamp == null)
            {
                message.Timestamp = DateTime.Now;
            }

            lock (_lockObj)
            {
                session.Messages.Add(message);
                _messageIndex[message.Id!] = message;
                session.UpdatedAt = DateTime.Now;
            }

            await AppendDeltaAndFlushAsync(new SessionDeltaAdd
            {
                SessionId = session.Id,
                MessageId = message.Id,
                Message = message
            });

            MessageAdded?.Invoke(this, new MessageAddedEventArgs
            {
                SessionId = session.Id,
                Message = message,
                IsStreaming = false,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Updates a message in the current session by ID.
        /// Mutates the SAME instance held by the dictionary/list (O(1), INPC preserved)
        /// and appends an "update" delta.
        /// </summary>
        public async Task UpdateMessageAsync(string messageId, ChatMessage updatedMessage)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(messageId));
            }

            if (updatedMessage == null)
            {
                throw new ArgumentNullException(nameof(updatedMessage));
            }

            var session = GetCurrentSession();
            ChatMessage existing;

            lock (_lockObj)
            {
                if (!_messageIndex.TryGetValue(messageId, out existing!))
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                updatedMessage.Id = messageId; // Preserve ID
                SessionDeltaLog.MergeInto(existing, updatedMessage);
                session.UpdatedAt = DateTime.Now;
            }

            await AppendDeltaAndFlushAsync(new SessionDeltaUpdate
            {
                SessionId = session.Id,
                MessageId = messageId,
                Message = existing
            });
        }

        /// <summary>
        /// Deletes a message from the current session by ID: removes it from the
        /// in-memory index/list and appends a "delete" delta (bytes are never erased
        /// from the log — append-only).
        /// </summary>
        public async Task DeleteMessageAsync(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(messageId));
            }

            var session = GetCurrentSession();

            lock (_lockObj)
            {
                if (!_messageIndex.TryGetValue(messageId, out var message))
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                session.Messages.Remove(message);
                _messageIndex.Remove(messageId);
                session.UpdatedAt = DateTime.Now;
            }

            await AppendDeltaAndFlushAsync(new SessionDeltaDelete
            {
                SessionId = session.Id,
                MessageId = messageId
            });
        }

        /// <summary>
        /// Soft-deletes a message in the current session (gap81): marks IsDeleted = true
        /// (gap80 tombstone) on the SAME instance, appends a "softDelete" delta, and
        /// NEVER hard-removes the bytes. The entry stays in the session for undelete.
        /// </summary>
        public async Task SoftDeleteMessageAsync(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(messageId));
            }

            var session = GetCurrentSession();

            lock (_lockObj)
            {
                if (!_messageIndex.TryGetValue(messageId, out var message))
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                message.IsDeleted = true;
                session.UpdatedAt = DateTime.Now;
            }

            await AppendDeltaAndFlushAsync(new SessionDeltaSoftDelete
            {
                SessionId = session.Id,
                MessageId = messageId
            });
        }

        /// <summary>
        /// Undeletes a previously soft-deleted message in the current session (gap81).
        /// Clears the gap80 tombstone (IsDeleted = false) and appends an "undelete" delta.
        /// </summary>
        public async Task UndeleteMessageAsync(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                throw new ArgumentException("Message ID cannot be null or empty.", nameof(messageId));
            }

            var session = GetCurrentSession();

            lock (_lockObj)
            {
                if (!_messageIndex.TryGetValue(messageId, out var message))
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                message.IsDeleted = false;
                session.UpdatedAt = DateTime.Now;
            }

            await AppendDeltaAndFlushAsync(new SessionDeltaUndelete
            {
                SessionId = session.Id,
                MessageId = messageId
            });
        }

        /// <summary>
        /// Lists all available sessions by scanning the *.jsonl delta logs.
        /// </summary>
        public async IAsyncEnumerable<SessionMetadata> ListSessionsAsync(int limit = 50)
        {
            var directory = new DirectoryInfo(SessionStoragePath);
            if (!directory.Exists)
            {
                yield break;
            }

            var files = directory.GetFiles("*.jsonl")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(limit);

            int count = 0;
            foreach (var file in files)
            {
                Session? session = await TryLoadSessionAsync(Path.GetFileNameWithoutExtension(file.Name));

                if (session != null)
                {
                    yield return new SessionMetadata
                    {
                        Id = session.Id,
                        Title = session.Title,
                        CreatedAt = session.CreatedAt,
                        UpdatedAt = session.UpdatedAt,
                        MessageCount = session.Messages.Count
                    };

                    count++;
                    if (count >= limit)
                    {
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to safely load a session, returning null on error.
        /// </summary>
        private async Task<Session?> TryLoadSessionAsync(string sessionId)
        {
            try
            {
                return await LoadSessionFromFileAsync(sessionId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deletes a session's JSONL log by ID.
        /// </summary>
        public async Task DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            var filePath = SessionDeltaLog.GetPath(SessionStoragePath, sessionId);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // If deleted session was current, clear it
            lock (_lockObj)
            {
                if (_currentSession?.Id == sessionId)
                {
                    _currentSession = null;
                    _messageIndex.Clear();
                }
            }

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = sessionId,
                ChangeType = SessionChangeType.Deleted,
                Session = null,
                Timestamp = DateTime.Now
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Exports a session (messages + metadata) to a JSON file (gap91).
        /// Loads the full session by replaying its JSONL log, serializes it read-only to a
        /// <see cref="SessionExport"/> DTO, and writes an indented JSON file. The store is never
        /// mutated (pure export). Default target is the user's Downloads folder (falling back to
        /// the session storage directory when the Downloads folder cannot be resolved); the
        /// filename is <c>{sanitizedTitle}_{yyyyMMdd_HHmmss}.json</c> to avoid collisions.
        /// </summary>
        /// <param name="sessionId">The ID of the session to export.</param>
        /// <param name="exportDirectory">Optional target directory (used by tests for isolation).</param>
        /// <returns>The absolute path of the written export file.</returns>
        public async Task<string> ExportSessionAsync(string sessionId, string? exportDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            // Flush any in-flight writes so the reader sees an ordered, complete log.
            _deltaLog.FlushSync();

            var session = await LoadSessionFromFileAsync(sessionId);

            var export = new SessionExport
            {
                SessionId = session.Id,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                Mode = session.Mode,
                MessageCount = session.Messages.Count,
                Messages = session.Messages
            };

            var json = JsonConvert.SerializeObject(export, Formatting.Indented);

            var directory = ResolveExportDirectory(exportDirectory);
            Directory.CreateDirectory(directory);

            var safeTitle = SanitizeFileName(session.Title ?? "session");
            var fileName = $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(directory, fileName);

            await Task.Run(() => File.WriteAllText(filePath, json, System.Text.Encoding.UTF8));

            LoggerService.Current.WriteDebug($"[gap91-export] Session {sessionId} exported to {filePath}");
            return filePath;
        }

        /// <summary>
        /// Resolves the directory an exported session is written to (gap91).
        /// Uses the caller-supplied directory first (tests), then the user's Downloads folder,
        /// falling back to the session storage directory when Downloads cannot be resolved.
        /// </summary>
        private static string ResolveExportDirectory(string? exportDirectory)
        {
            if (!string.IsNullOrWhiteSpace(exportDirectory))
            {
                return exportDirectory!;
            }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile, "Downloads");

            if (Directory.Exists(downloads))
            {
                var savedChats = Path.Combine(downloads, "SavedChats");
                return savedChats;
            }

            return DefaultSessionStoragePath;
        }

        /// <summary>
        /// Removes characters that are invalid in Windows file names (gap91).
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name
                .Select(ch => invalid.Contains(ch) ? '_' : ch)
                .ToArray())
                .Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "session" : sanitized;
        }

        /// <summary>
        /// Gets the ID of the currently active session (gap76).
        /// </summary>
        public async Task<string?> GetCurrentSessionIdAsync()
        {
            var currentSession = GetCurrentSession();
            await Task.CompletedTask;
            return currentSession?.Id;
        }

        /// <summary>
        /// Sets the active session ID and persists it (gap76).
        /// </summary>
        public async Task SetCurrentSessionIdAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
            }

            await LoadSessionAsync(sessionId);
            await SaveCurrentSessionAsync();
        }

        /// <summary>
        /// Prunes old messages from the current session when token count exceeds maxTokens.
        /// Removes oldest non-system messages first. Each pruned message is removed from
        /// the in-memory index/list and logged as a "delete" delta (append-only; bytes in
        /// the log are never erased). Uses ITokenCountingService for token estimation.
        /// </summary>
        public async Task<(int RemovedCount, List<ChatMessage> Pruned)> PruneOldMessagesAsync(int maxTokens, bool keepSystemMessages = true)
        {
            var session = GetCurrentSession();
            var prunedMessages = new List<ChatMessage>();
            int removedCount = 0;

            lock (_lockObj)
            {
                if (session.Messages.Count == 0)
                    return (0, prunedMessages);

                // Collect messages to prune: oldest non-system messages first
                var messagesToConsider = session.Messages
                    .Where(m => !keepSystemMessages || m.Role != ChatMessageRole.System)
                    .ToList();

                // Calculate current token usage using token counting service
                int currentTokens = _tokenCountingService.CountMessagesTokens(messagesToConsider);

                // Remove oldest messages until we're under maxTokens
                if (currentTokens > maxTokens)
                {
                    var toRemove = messagesToConsider
                        .OrderBy(m => m.Timestamp ?? DateTime.Now)
                        .ToList();

                    // Remove messages from oldest to newest until under threshold
                    foreach (var msg in toRemove)
                    {
                        if (currentTokens <= maxTokens)
                            break;

                        if (session.Messages.Remove(msg))
                        {
                            prunedMessages.Add(msg);
                            removedCount++;
                            if (!string.IsNullOrEmpty(msg.Id))
                            {
                                _messageIndex.Remove(msg.Id!);
                            }
                            // Recalculate tokens after removal
                            int msgTokens = _tokenCountingService.CountMessageTokens(msg);
                            currentTokens -= msgTokens;
                        }
                    }

                    session.UpdatedAt = DateTime.Now;
                }
            }

            if (removedCount > 0)
            {
                foreach (var msg in prunedMessages)
                {
                    await AppendDeltaAndFlushAsync(new SessionDeltaDelete
                    {
                        SessionId = session.Id,
                        MessageId = msg.Id
                    });
                }
            }

            return (removedCount, prunedMessages);
        }

        /// <summary>
        /// Ensures the sessions storage directory exists.
        /// </summary>
        private async Task EnsureSessionsDirectoryAsync()
        {
            var directory = new DirectoryInfo(SessionStoragePath);
            if (!directory.Exists)
            {
                directory.Create();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Persists a session to the JSONL delta log. If the init header for this
        /// session has not yet been written, it is appended first; pending deltas are
        /// then flushed synchronously so the operation is durable before returning.
        /// </summary>
        private async Task SaveSessionToFileAsync(Session session)
        {
            await EnsureSessionsDirectoryAsync();

            // Always persist the header (first write creates the file; subsequent
            // writes re-assert title/mode so replay reconstructs the latest intent).
            await AppendDeltaAndFlushAsync(new SessionDeltaInit
            {
                SessionId = session.Id,
                Id = session.Id,
                Title = session.Title ?? "New Conversation",
                CreatedAt = session.CreatedAt,
                Mode = session.Mode
            });
        }

        /// <summary>
        /// Returns the storage directory for the current service (defaults to
        /// ~/.continueVS/sessions unless an explicit dir was supplied in the constructor).
        /// </summary>
        private string SessionStoragePath => _deltaLog.StorageDirectory;

        /// <summary>
        /// Appends a delta line and flushes it to disk, guaranteeing durability/order.
        /// </summary>
        private async Task AppendDeltaAndFlushAsync(SessionDelta delta)
        {
            await EnsureInitAsync(delta.SessionId);
            await _deltaLog.EnqueueAndFlushAsync(delta);
        }

        /// <summary>
        /// Writes (once per session) the init header delta. Subsequent real deltas are
        /// ordered strictly after it so replay sees a valid session header first.
        /// </summary>
        private async Task EnsureInitAsync(string? sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            bool needInit;
            lock (_initializedSessions)
            {
                needInit = _initializedSessions.Add(sessionId!);
            }

            if (!needInit) return;

            Session session = GetCurrentSession();
            await _deltaLog.EnqueueAndFlushAsync(new SessionDeltaInit
            {
                SessionId = session.Id,
                Id = session.Id,
                Title = session.Title ?? "New Conversation",
                CreatedAt = session.CreatedAt,
                Mode = session.Mode
            });
        }

        /// <summary>
        /// Loads a session by replaying its JSONL log from the first line. The replay
        /// (file read + apply) runs on a pooled background thread via Task.Run so the
        /// UI/awaiting caller is never blocked. Malformed tail lines are skipped.
        /// </summary>
        private async Task<Session> LoadSessionFromFileAsync(string sessionId)
        {
            var filePath = SessionDeltaLog.GetPath(SessionStoragePath, sessionId);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Session file not found: {filePath}");
            }

            var index = new Dictionary<string, ChatMessage>();
            var order = new List<ChatMessage>();

            // File read + replay on a worker thread; async continuation only wires results.
            var result = await Task.Run(() => _deltaLog.ReplayInto(sessionId, index, order));

            if (string.IsNullOrEmpty(result.Id))
            {
                throw new InvalidOperationException($"Failed to replay session log at {filePath}");
            }

            var session = new Session
            {
                Id = result.Id ?? sessionId,
                Title = result.Title ?? "New Conversation",
                Messages = order,
                CreatedAt = result.CreatedAt ?? DateTime.Now,
                UpdatedAt = File.GetLastWriteTimeUtc(filePath),
                IsActive = false,
                Mode = result.Mode
            };

            _messageIndex.Clear();
            foreach (var kvp in index)
            {
                _messageIndex[kvp.Key] = kvp.Value;
            }

            return session;
        }

        /// <summary>
        /// Rebuilds the O(1) dictionary index so it references the same instances as
        /// the session's Messages list (used when creating/loading a session).
        /// </summary>
        private void RebuildIndexFor(Session session)
        {
            _messageIndex.Clear();
            if (session.Messages == null) return;
            foreach (var msg in session.Messages)
            {
                if (!string.IsNullOrEmpty(msg.Id))
                {
                    _messageIndex[msg.Id!] = msg;
                }
            }
        }

        /// <summary>
        /// Packages messages for an LLM send with token-budget-aware history pruning (gap34).
        /// Assembles: [systemMessage] + [oldest-first history that fits budget] + [new user turn].
        /// Budget = 80% of model.ContextWindow (fallback 4096). System + new user turn always included.
        /// </summary>
        public List<ChatMessage> PackageMessages(ModelInfo? model, ChatMessage systemMessage, string newUserContent)
        {
            if (systemMessage == null) throw new ArgumentNullException(nameof(systemMessage));
            if (newUserContent == null) throw new ArgumentNullException(nameof(newUserContent));

            int contextWindow = (model != null && model.ContextWindow > 0) ? model.ContextWindow : 4096;
            int budget = (int)(contextWindow * 0.8);

            var newUserMessage = new ChatMessage { Role = ChatMessageRole.User, Content = newUserContent };

            int systemTokens = _tokenCountingService.CountMessageTokens(systemMessage);
            int newUserTokens = _tokenCountingService.CountMessageTokens(newUserMessage);
            int remainingBudget = budget - systemTokens - newUserTokens;

            // Retrieve User/Assistant history ordered oldest-first
            var session = GetCurrentSession();
            var history = session.Messages
                .Where(m => !m.IsDeleted && (m.Role == ChatMessageRole.User || m.Role == ChatMessageRole.Assistant))
                .OrderBy(m => m.Timestamp ?? DateTime.MinValue)
                .ToList();

            // Exclude the new user message just added to the session store (avoid duplicate)
            if (history.Count > 0
                && history[history.Count - 1].Role == ChatMessageRole.User
                && history[history.Count - 1].Content == newUserContent)
            {
                history = history.Take(history.Count - 1).ToList();
            }

            // Walk newest-to-oldest; accumulate history that fits within remainingBudget
            var fittingHistory = new List<ChatMessage>();
            int historyTokens = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                int msgTokens = _tokenCountingService.CountMessageTokens(history[i]);
                if (historyTokens + msgTokens <= remainingBudget)
                {
                    fittingHistory.Insert(0, history[i]);
                    historyTokens += msgTokens;
                }
                else
                {
                    break;
                }
            }

            var result = new List<ChatMessage>();
            result.Add(systemMessage);
            result.AddRange(fittingHistory);
            result.Add(newUserMessage);
            return result;
        }

        /// <summary>
        /// Sets the current chat mode and fires SessionChanged event for mode-change propagation (gap27_3).
        /// Persists a new init delta so the mode change survives replay (gap27_5).
        /// </summary>
        public async Task SetCurrentModeAsync(int newMode)
        {
            var session = GetCurrentSession();
            session.Mode = newMode;

            // Mode is carried by the init delta; append a fresh one so replay sees it.
            await SaveSessionToFileAsync(session);

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                CurrentMode = newMode,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Sets the display title of the current session (gap session-title).
        /// Mutates Session.Title, persists a fresh init delta so the title survives JSONL replay,
        /// and fires SessionChanged (Updated) for UI/history propagation.
        /// </summary>
        public async Task SetSessionTitleAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Session title cannot be null or whitespace.", nameof(title));
            }

            var session = GetCurrentSession();
            session.Title = title;

            // Title is carried by the init delta; append a fresh one so replay sees it.
            await SaveSessionToFileAsync(session);

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                Timestamp = DateTime.Now
            });
        }

        /// <summary>
        /// Gets the current context budget state based on estimated token usage.
        /// </summary>
        public ContextBudgetState GetContextBudgetState()
        {
            var session = GetCurrentSession();
            int used = EstimateTokensUsed(session.Messages);

            int maxTokens = 4096;
            int reserve = 512;

            int safeThreshold = (int)((maxTokens - reserve) * 0.85);
            int cautionThreshold = maxTokens - reserve;

            if (used <= safeThreshold)
            {
                _cachedBudgetState = ContextBudgetState.Safe;
            }
            else if (used < cautionThreshold)
            {
                _cachedBudgetState = ContextBudgetState.Caution;
            }
            else
            {
                _cachedBudgetState = ContextBudgetState.Locked;
            }

            return _cachedBudgetState;
        }

        /// <summary>
        /// Estimates total tokens used by a message history using conservative heuristics.
        /// </summary>
        public int EstimateTokensUsed(List<ChatMessage> history)
        {
            if (history == null || history.Count == 0)
                return 0;

            int totalTokens = 0;

            foreach (var message in history)
            {
                if (message.Role == ChatMessageRole.User)
                {
                    totalTokens += (message.Content?.Length ?? 0) / 4;
                }
                else if (message.Role == ChatMessageRole.Assistant)
                {
                    totalTokens += (message.Content?.Length ?? 0) / 4;
                    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
                    {
                        totalTokens += message.ToolCalls.Count * 150;
                    }
                }
                else if (message.Role == ChatMessageRole.Tool)
                {
                    totalTokens += (message.Content?.Length ?? 0) / 4;
                    totalTokens += 50;
                }
                else
                {
                    totalTokens += (message.Content?.Length ?? 0) / 4;
                }
            }

            return totalTokens;
        }

        /// <summary>
        /// Helper method to find the newest conversation unit (Assistant message + following ToolResults).
        /// </summary>
        private (int startIndex, int count) FindNewestConversationUnit(List<ChatMessage> history)
        {
            if (history == null || history.Count == 0)
                return (-1, 0);

            int lastAssistantIndex = -1;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Role == ChatMessageRole.Assistant)
                {
                    lastAssistantIndex = i;
                    break;
                }
            }

            if (lastAssistantIndex == -1)
            {
                return (history.Count - 1, 1);
            }

            int toolResultCount = 0;
            for (int i = lastAssistantIndex + 1; i < history.Count; i++)
            {
                if (history[i].Role == ChatMessageRole.Tool)
                {
                    toolResultCount++;
                }
                else
                {
                    break;
                }
            }

            return (lastAssistantIndex, 1 + toolResultCount);
        }

        /// <summary>
        /// Backtracks and optimizes conversation history by removing newest units until budget fits.
        /// </summary>
        public async Task<(List<ChatMessage> trimmed, string summary)> BacktrackAndOptimizeAsync(List<ChatMessage> history, int maxTokens, int reserve)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            var workingHistory = new List<ChatMessage>(history);
            int budgetLimit = maxTokens - reserve;
            int removedCount = 0;

            while (EstimateTokensUsed(workingHistory) > budgetLimit)
            {
                if (workingHistory.Count == 0)
                {
                    throw new InvalidOperationException("Context budget reserve is too small; cannot continue.");
                }

                var (startIndex, count) = FindNewestConversationUnit(workingHistory);
                if (startIndex < 0)
                {
                    throw new InvalidOperationException("Cannot find conversation unit to remove.");
                }

                workingHistory.RemoveRange(startIndex, count);
                removedCount += count;
            }

            string summary = $"Removed {removedCount} messages; preserved {workingHistory.Count} foundational context.";
            return await Task.FromResult((workingHistory, summary));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _deltaLog.Dispose();
        }
    }
}
