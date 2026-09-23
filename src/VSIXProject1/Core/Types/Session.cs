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
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Timestamp when the session was last updated.
        /// </summary>
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether this is the currently active session.
        /// </summary>
        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        /// <summary>
        /// Count of tool calls executed in the current user action (per-action budget).
        /// Resets to 0 when the user clicks Send. Incremented during ask/agent/plan
        /// execution and by auto-continuations (which accumulate but never reset).
        /// If an action exhausts the per-action budget (Agent_MaxToolCallsPerAction),
        /// the action stops. The next real user Send grants a fresh budget.
        /// </summary>
        [JsonProperty("toolCallsExecuted")]
        public int ToolCallsExecuted { get; set; }

        /// <summary>
        /// Stores the chat mode used in this session (gap27_5).
        /// 0 = Ask, 1 = Agent, 2 = Plan.
        /// Persisted to session JSON for history restoration.
        /// </summary>
        [JsonProperty("mode")]
        public int Mode { get; set; } = 0;
    }
}
