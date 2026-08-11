using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for indexing errors.
    /// </summary>
    public class IndexingErrorEventArgs : EventArgs
    {
        /// <summary>
        /// File path where the indexing error occurred.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Exception that caused the error.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
