using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an internal phase in a debug session.
    /// A phase is a discrete strategy attempt (analysis, breakpoint, instrumentation, test, or observation).
    /// </summary>
    public class InternalPhase
    {
        /// <summary>
        /// Unique identifier for this phase.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of phase (strategy).
        /// </summary>
        [JsonProperty("type")]
        public InternalPhaseType Type { get; set; }

        /// <summary>
        /// Description of what this phase should accomplish.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Current execution status of this phase.
        /// </summary>
        [JsonProperty("status")]
        public InternalPhaseStatus Status { get; set; } = InternalPhaseStatus.Pending;

        /// <summary>
        /// Timestamp when this phase was created.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Runtime execution annotation (not persisted with plan definition).
        /// Tracks strategy, result, changes applied, and execution timing.
        /// </summary>
        [JsonIgnore]
        public InternalPhaseExecution? Execution { get; set; }
    }
}
