using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Enumeration of file change types.
    /// </summary>
    public enum FileChangeType
    {
        /// <summary>
        /// File was created.
        /// </summary>
        Created,

        /// <summary>
        /// File was modified.
        /// </summary>
        Modified,

        /// <summary>
        /// File was deleted.
        /// </summary>
        Deleted
    }

    /// <summary>
    /// Event arguments for file changes.
    /// </summary>
    public class FileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Path of the file that changed.
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Type of change.
        /// </summary>
        public FileChangeType ChangeType { get; set; }

        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
