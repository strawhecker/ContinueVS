using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration representing the severity level of a breadcrumb event.
    /// </summary>
    public enum BreadcrumbLevel
    {
        /// <summary>Informational message.</summary>
        Info = 0,

        /// <summary>Warning message.</summary>
        Warning = 1,

        /// <summary>Error message.</summary>
        Error = 2
    }

    /// <summary>
    /// Immutable record of a breadcrumb event in the application timeline.
    /// </summary>
    public class BreadcrumbRecord
    {
        /// <summary>
        /// Gets the timestamp when the breadcrumb was recorded.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the severity level of the breadcrumb.
        /// </summary>
        public BreadcrumbLevel Level { get; }

        /// <summary>
        /// Gets the message content of the breadcrumb (with sensitive data masked).
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the session identifier this breadcrumb belongs to.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Initializes a new instance of the BreadcrumbRecord class.
        /// </summary>
        /// <param name="timestamp">The timestamp when the breadcrumb was recorded.</param>
        /// <param name="level">The severity level of the breadcrumb.</param>
        /// <param name="message">The message content (already masked).</param>
        /// <param name="sessionId">The session identifier.</param>
        public BreadcrumbRecord(DateTime timestamp, BreadcrumbLevel level, string message, string sessionId)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
        }

        /// <summary>
        /// Returns a string representation of the breadcrumb.
        /// </summary>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Message}";
        }
    }
}
