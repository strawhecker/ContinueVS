using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Read-only DTO used to export a session to a JSON file (gap91).
    /// Carries the session's metadata plus its full ordered message list.
    /// This is an export/snapshot shape only — it is never replayed as a
    /// delta log and never written into the session storage directory.
    /// </summary>
    public class SessionExport
    {
        /// <summary>
        /// Unique identifier of the exported session.
        /// </summary>
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Display title of the exported session.
        /// </summary>
        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Timestamp when the session was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Timestamp when the session was last updated.
        /// </summary>
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Chat mode used in the session (0=Ask, 1=Agent, 2=Plan).
        /// </summary>
        [JsonProperty("mode")]
        public int Mode { get; set; }

        /// <summary>
        /// Number of messages in the exported session.
        /// </summary>
        [JsonProperty("messageCount")]
        public int MessageCount { get; set; }

        /// <summary>
        /// The full ordered list of messages in the session.
        /// </summary>
        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
