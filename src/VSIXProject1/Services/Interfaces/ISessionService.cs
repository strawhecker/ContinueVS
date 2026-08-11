using System;
using System.Collections.Generic;
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
        /// Event raised when the current session changes.
        /// </summary>
        event EventHandler<SessionChangedEventArgs>? SessionChanged;

        /// <summary>
        /// Event raised when a message is added to the current session.
        /// </summary>
        event EventHandler<MessageAddedEventArgs>? MessageAdded;
    }
}
