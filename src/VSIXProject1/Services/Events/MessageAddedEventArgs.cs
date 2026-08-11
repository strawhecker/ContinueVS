using System;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Events
{
    /// <summary>
    /// Event arguments for message additions.
    /// </summary>
    public class MessageAddedEventArgs : EventArgs
    {
        /// <summary>
        /// ID of the session the message was added to.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The message that was added.
        /// </summary>
        public ChatMessage? Message { get; set; }

        /// <summary>
        /// Whether the message is being streamed.
        /// </summary>
        public bool IsStreaming { get; set; }

        /// <summary>
        /// Timestamp when the message was added.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
