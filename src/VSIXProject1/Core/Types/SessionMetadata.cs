using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Lightweight metadata for session list display.
    /// </summary>
    public class SessionMetadata
    {
        /// <summary>
        /// Unique identifier for the session.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display title for the session.
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Timestamp when the session was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when the session was last updated.
        /// </summary>
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of messages in the session.
        /// </summary>
        [JsonProperty("messageCount")]
        public int MessageCount { get; set; }
    }
}
