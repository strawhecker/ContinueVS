using System;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for indexing progress updates.
    /// </summary>
    public class IndexingProgressEventArgs : EventArgs
    {
        /// <summary>
        /// The indexing progress update information.
        /// </summary>
        public IndexingProgressUpdate? Progress { get; set; }

        /// <summary>
        /// Timestamp when this event was generated.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
