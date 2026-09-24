using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for session management.
    /// Handles creation, persistence, and navigation of conversation sessions.
    /// </summary>
    public interface ISessionService
    {
        /// <summary>
        /// Gets the currently active session.
        /// </summary>
        /// <returns>The current Session instance.</returns>
        Session GetCurrentSession();

        /// <summary>
        /// Creates a new session.
        /// </summary>
        /// <param name="title">Optional title for the session.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CreateNewSessionAsync(string? title = null);

        /// <summary>
        /// Saves the current session to persistent storage.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveCurrentSessionAsync();

        /// <summary>
        /// Loads a session by ID.
        /// </summary>
        /// <param name="sessionId">The ID of the session to load.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LoadSessionAsync(string sessionId);

        /// <summary>
        /// Adds a message to the current session.
        /// </summary>
        /// <param name="message">The message to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddMessageAsync(ChatMessage message);

        /// <summary>
        /// Updates a message in the current session.
        /// </summary>
        /// <param name="messageId">The ID of the message to update.</param>
        /// <param name="updatedMessage">The updated message content.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateMessageAsync(string messageId, ChatMessage updatedMessage);

        /// <summary>
        /// Deletes a message from the current session.
        /// </summary>
        /// <param name="messageId">The ID of the message to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteMessageAsync(string messageId);

        /// <summary>
        /// Soft-deletes a message in the current session (gap81).
        /// Marks the message IsDeleted = true (gap80 tombstone); retains the bytes in the
        /// session so the user can undelete it. The tombstone excludes the entry from the
        /// payload serialized to the LLM at request time (see PackageMessages). Never hard-removes.
        /// </summary>
        /// <param name="messageId">The ID of the message to soft-delete.</param>
        /// <exception cref="ArgumentException">Thrown when messageId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the message is not found in the current session.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SoftDeleteMessageAsync(string messageId);

        /// <summary>
        /// Undeletes a previously soft-deleted message in the current session (gap81).
        /// Clears the gap80 tombstone (IsDeleted = false), restoring the entry to the payload
        /// serialized to the LLM. Visibility-only; performs no file-state/restore operation.
        /// </summary>
        /// <param name="messageId">The ID of the message to undelete.</param>
        /// <exception cref="ArgumentException">Thrown when messageId is null or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the message is not found in the current session.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UndeleteMessageAsync(string messageId);

        /// <summary>
        /// Lists all available sessions.
        /// </summary>
        /// <param name="limit">Maximum number of sessions to return.</param>
        /// <returns>An async enumerable of SessionMetadata instances.</returns>
        IAsyncEnumerable<SessionMetadata> ListSessionsAsync(int limit = 50);

        /// <summary>
        /// Deletes a session.
        /// </summary>
        /// <param name="sessionId">The ID of the session to delete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteSessionAsync(string sessionId);

        /// <summary>
        /// Exports a session (messages + metadata) to a JSON file (gap91).
        /// The session is serialized read-only; the store is never mutated.
        /// </summary>
        /// <param name="sessionId">The ID of the session to export.</param>
        /// <param name="exportDirectory">Optional target directory. Defaults to the user's
        /// Downloads folder (falling back to the session storage directory when unavailable).</param>
        /// <returns>The absolute path of the written export file.</returns>
        /// <exception cref="ArgumentException">Thrown when sessionId is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the session's log does not exist.</exception>
        Task<string> ExportSessionAsync(string sessionId, string? exportDirectory = null);

        /// <summary>
        /// Event raised when the current session changes.
        /// </summary>
        event EventHandler<SessionChangedEventArgs>? SessionChanged;

        /// <summary>
        /// Event raised when a message is added to the current session.
        /// </summary>
        event EventHandler<MessageAddedEventArgs>? MessageAdded;

        /// <summary>
        /// Prunes old messages from the current session if token count exceeds maxTokens.
        /// Removes oldest messages first while preserving system messages if requested.
        /// </summary>
        /// <param name="maxTokens">Maximum tokens allowed in remaining messages.</param>
        /// <param name="keepSystemMessages">If true, system messages are always preserved.</param>
        /// <returns>Tuple of (count of removed messages, list of removed messages).</returns>
        Task<(int RemovedCount, List<ChatMessage> Pruned)> PruneOldMessagesAsync(int maxTokens, bool keepSystemMessages = true);

        /// <summary>
        /// Sets the current chat mode and fires SessionChanged event for mode-change propagation (gap27_3).
        /// </summary>
        /// <param name="newMode">The chat mode to set as an integer (0=Ask, 1=Agent, 2=Plan).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetCurrentModeAsync(int newMode);

        /// <summary>
        /// Sets the display title of the current session and persists a fresh init delta so the
        /// title survives replay. Fires SessionChanged (Updated) for UI/history propagation.
        /// </summary>
        /// <param name="title">The new session title.</param>
        /// <exception cref="ArgumentNullException">Thrown when title is null or whitespace.</exception>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetSessionTitleAsync(string title);

        /// <summary>
        /// Packages messages for an LLM send: [systemMessage] + [token-budget-pruned history turns] + [new user turn].
        /// History is pruned oldest-first to fit within 80% of the model's ContextWindow (fallback 4096 if unset).
        /// The system message and new user turn are always preserved regardless of budget pressure.
        /// </summary>
        /// <param name="model">Active model; ContextWindow determines the token budget.</param>
        /// <param name="systemMessage">The system-role message to prepend (mode prompt combined with context).</param>
        /// <param name="newUserContent">Content of the new user turn to append.</param>
        /// <returns>Ordered list: systemMessage → pruned history → new user message.</returns>
        List<ChatMessage> PackageMessages(ModelInfo? model, ChatMessage systemMessage, string newUserContent);

        /// <summary>
        /// Gets the current context budget state based on estimated token usage.
        /// Compares current message history against Safe/Caution/Locked thresholds.
        /// </summary>
        /// <returns>ContextBudgetState enum value: Safe, Caution, or Locked.</returns>
        ContextBudgetState GetContextBudgetState();

        /// <summary>
        /// Estimates total tokens used by a message history using conservative heuristics.
        /// No LLM tokenizer required; uses character-based approximation.
        /// User message: content.Length / 4
        /// Assistant response: content.Length / 4 + tool_calls.Count * 150
        /// Tool result: result.content.Length / 4 + 50
        /// </summary>
        /// <param name="history">List of messages to estimate tokens for.</param>
        /// <returns>Approximate token count as integer.</returns>
        int EstimateTokensUsed(List<ChatMessage> history);

        /// <summary>
        /// Backtracks and optimizes conversation history by removing newest Assistant + paired ToolResults units
        /// until the message history fits within the specified token budget.
        /// Preserves oldest foundational context; removes newest work units first.
        /// </summary>
        /// <param name="history">Full conversation history (oldest to newest).</param>
        /// <param name="maxTokens">Maximum tokens allowed by model context window.</param>
        /// <param name="reserve">Token buffer reserved for response; actual limit is (maxTokens - reserve).</param>
        /// <returns>Tuple of (trimmed history, truncation summary string).</returns>
        Task<(List<ChatMessage> trimmed, string summary)> BacktrackAndOptimizeAsync(List<ChatMessage> history, int maxTokens, int reserve);

        /// <summary>
        /// Gets the ID of the currently active session (gap76).
        /// Used to restore active session on extension restart.
        /// </summary>
        /// <returns>The active session ID, or null if none is set.</returns>
        Task<string?> GetCurrentSessionIdAsync();

        /// <summary>
        /// Sets the active session ID and persists it (gap76).
        /// Used to restore active session on extension restart.
        /// </summary>
        /// <param name="sessionId">The session ID to mark as active.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetCurrentSessionIdAsync(string sessionId);
    }
}
