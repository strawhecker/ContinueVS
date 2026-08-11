using System;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Enumeration of session change types.
    /// </summary>
    public enum SessionChangeType
    {
        /// <summary>
        /// Session was created.
        /// </summary>
        Created,

        /// <summary>
        /// Session was updated.
        /// </summary>
        Updated,

        /// <summary>
        /// Session was deleted.
        /// </summary>
        Deleted
    }

    /// <summary>
    /// Event arguments for session changes.
    /// </summary>
    public class SessionChangedEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the session that changed.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Type of change that occurred.
        /// </summary>
        public SessionChangeType ChangeType { get; set; }

        /// <summary>
        /// The session object (if available).
        /// </summary>
        public Session? Session { get; set; }

        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
