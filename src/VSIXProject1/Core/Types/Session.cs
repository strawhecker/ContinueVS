using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a conversation session containing messages and metadata.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Unique identifier for this session.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display title for the session.
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>
        /// List of messages in this session.
        /// </summary>
        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

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
        /// Whether this is the currently active session.
        /// </summary>
        [JsonProperty("isActive")]
        public bool IsActive { get; set; }
    }
}
