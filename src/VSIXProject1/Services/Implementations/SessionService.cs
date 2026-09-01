using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of ISessionService that manages conversation sessions.
    /// Handles creation, persistence, and navigation of sessions with file-based storage.
    /// </summary>
    public class SessionService : ISessionService
    {
        private Session? _currentSession;
        private readonly object _lockObj = new object();
        private readonly ITokenCountingService _tokenCountingService;

        /// <summary>
        /// Computes the base directory for session storage: ~/.continue/sessions/
        /// </summary>
        private string SessionStoragePath
        {
            get
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, ".continue", "sessions");
            }
        }

        public event EventHandler<SessionChangedEventArgs>? SessionChanged;
        public event EventHandler<MessageAddedEventArgs>? MessageAdded;

        /// <summary>
        /// Initializes a new instance of the SessionService with token counting dependency.
        /// </summary>
        /// <param name="tokenCountingService">Service for estimating message tokens</param>
        public SessionService(ITokenCountingService tokenCountingService)
        {
            _tokenCountingService = tokenCountingService ?? throw new ArgumentNullException(nameof(tokenCountingService));
        }

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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                ToolCallsExecuted = 0
            };

            await SaveSessionToFileAsync(newSession);

            lock (_lockObj)
            {
                _currentSession = newSession;
            }

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = newSession.Id,
                ChangeType = SessionChangeType.Created,
                Session = newSession,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Saves the current session to persistent storage.
        /// </summary>
        public async Task SaveCurrentSessionAsync()
        {
            var session = GetCurrentSession();
            session.UpdatedAt = DateTime.UtcNow;
            await SaveSessionToFileAsync(session);

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Loads a session by ID and sets it as the current session.
        /// Restores mode from Session.Mode if present (gap27_5).
        /// </summary>
        public async Task LoadSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            var session = await LoadSessionFromFileAsync(sessionId);

            lock (_lockObj)
            {
                _currentSession = session;
            }

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                CurrentMode = session.Mode,  // Restore mode from persisted Session.Mode (gap27_5)
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Adds a message to the current session and fires MessageAdded event.
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
                message.Timestamp = DateTime.UtcNow;
            }

            lock (_lockObj)
            {
                session.Messages.Add(message);
                session.UpdatedAt = DateTime.UtcNow;
            }

            await SaveSessionToFileAsync(session);

            MessageAdded?.Invoke(this, new MessageAddedEventArgs
            {
                SessionId = session.Id,
                Message = message,
                IsStreaming = false,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Updates a message in the current session by ID.
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

            lock (_lockObj)
            {
                var index = session.Messages.FindIndex(m => m.Id == messageId);
                if (index < 0)
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                updatedMessage.Id = messageId; // Preserve ID
                session.Messages[index] = updatedMessage;
                session.UpdatedAt = DateTime.UtcNow;
            }

            await SaveSessionToFileAsync(session);
        }

        /// <summary>
        /// Deletes a message from the current session by ID.
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
                var message = session.Messages.FirstOrDefault(m => m.Id == messageId);
                if (message == null)
                {
                    throw new InvalidOperationException($"Message with ID '{messageId}' not found in current session.");
                }

                session.Messages.Remove(message);
                session.UpdatedAt = DateTime.UtcNow;
            }

            await SaveSessionToFileAsync(session);
        }

        /// <summary>
        /// Lists all available sessions asynchronously.
        /// </summary>
        public async IAsyncEnumerable<SessionMetadata> ListSessionsAsync(int limit = 50)
        {
            var directory = new DirectoryInfo(SessionStoragePath);
            if (!directory.Exists)
            {
                yield break;
            }

            var files = directory.GetFiles("*.json")
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
        /// Deletes a session by ID.
        /// </summary>
        public async Task DeleteSessionAsync(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("Session ID cannot be null or empty.", nameof(sessionId));
            }

            var filePath = Path.Combine(SessionStoragePath, $"{sessionId}.json");

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
                }
            }

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = sessionId,
                ChangeType = SessionChangeType.Deleted,
                Session = null,
                Timestamp = DateTime.UtcNow
            });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Prunes old messages from the current session when token count exceeds maxTokens.
        /// Removes oldest messages first, preserving system messages if requested.
        /// Uses ITokenCountingService for accurate token estimation.
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
                        .OrderBy(m => m.Timestamp ?? DateTime.UtcNow)
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
                            // Recalculate tokens after removal
                            int msgTokens = _tokenCountingService.CountMessageTokens(msg);
                            currentTokens -= msgTokens;
                        }
                    }

                    session.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (removedCount > 0)
            {
                await SaveSessionToFileAsync(session);
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
        /// Saves a session to disk as JSON.
        /// </summary>
        private async Task SaveSessionToFileAsync(Session session)
        {
            await EnsureSessionsDirectoryAsync();

            var filePath = Path.Combine(SessionStoragePath, $"{session.Id}.json");
            var json = JsonConvert.SerializeObject(session, Formatting.Indented);

            using (var writer = new StreamWriter(filePath, false))
            {
                await writer.WriteAsync(json);
            }
        }

        /// <summary>
        /// Loads a session from disk by ID.
        /// </summary>
        private async Task<Session> LoadSessionFromFileAsync(string sessionId)
        {
            var filePath = Path.Combine(SessionStoragePath, $"{sessionId}.json");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Session file not found: {filePath}");
            }

            using (var reader = new StreamReader(filePath))
            {
                var json = await reader.ReadToEndAsync();
                var session = JsonConvert.DeserializeObject<Session>(json);

                if (session == null)
                {
                    throw new InvalidOperationException($"Failed to deserialize session from {filePath}");
                }

                return session;
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
                .Where(m => m.Role == ChatMessageRole.User || m.Role == ChatMessageRole.Assistant)
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

            int totalEstimated = systemTokens + historyTokens + newUserTokens;
            _ = LoggerService.Current.WriteDebugAsync(
                $"[gap34-package] sending {1 + fittingHistory.Count + 1} messages, est. tokens: {totalEstimated}, model context: {contextWindow}");

            var result = new List<ChatMessage>();
            result.Add(systemMessage);
            result.AddRange(fittingHistory);
            result.Add(newUserMessage);
            return result;
        }

        /// <summary>
        /// Sets the current chat mode and fires SessionChanged event for mode-change propagation (gap27_3).
        /// Also updates Session.Mode for persistence (gap27_5).
        /// </summary>
        public async Task SetCurrentModeAsync(int newMode)
        {
            var session = GetCurrentSession();
            session.Mode = newMode;  // Persist mode to current session (gap27_5)
            await SaveCurrentSessionAsync();

            SessionChanged?.Invoke(this, new SessionChangedEventArgs
            {
                SessionId = session.Id,
                ChangeType = SessionChangeType.Updated,
                Session = session,
                CurrentMode = newMode,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

