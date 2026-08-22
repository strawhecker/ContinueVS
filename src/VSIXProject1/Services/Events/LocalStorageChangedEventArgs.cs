using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for localStorage changes.
    /// </summary>
    public class LocalStorageChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The key that was changed.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The old value (if any).
        /// </summary>
        public object? OldValue { get; set; }

        /// <summary>
        /// The new value.
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
