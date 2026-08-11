using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for tool execution errors.
    /// </summary>
    public class ToolErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the tool that failed.
        /// </summary>
        public string? ToolName { get; set; }

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
