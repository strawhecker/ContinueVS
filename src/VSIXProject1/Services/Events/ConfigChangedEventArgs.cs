using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for configuration changes.
    /// </summary>
    public class ConfigChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Configuration key that changed.
        /// </summary>
        public string? ConfigKey { get; set; }

        /// <summary>
        /// Previous value of the configuration.
        /// </summary>
        public object? OldValue { get; set; }

        /// <summary>
        /// New value of the configuration.
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// Timestamp when the change occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
