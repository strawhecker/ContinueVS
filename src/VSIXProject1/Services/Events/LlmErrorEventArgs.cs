using System;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for LLM errors.
    /// </summary>
    public class LlmErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Error message.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Error code or type.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Exception that caused the error.
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// ID of the model involved in the error.
        /// </summary>
        public string? ModelId { get; set; }

        /// <summary>
        /// Timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
