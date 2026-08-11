using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for active file changes.
    /// </summary>
    public class ActiveFileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Path of the newly active file.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Whether a file became active or inactive.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
