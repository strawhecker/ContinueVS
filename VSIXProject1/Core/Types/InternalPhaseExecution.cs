using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents runtime execution metadata for an internal phase.
    /// Tracks strategy type, execution result, changes applied, and timing.
    /// This is a runtime annotation; it does not persist with the TestPlan definition.
    /// </summary>
    public class InternalPhaseExecution
    {
        /// <summary>
        /// The strategy type attempted during this phase execution.
        /// </summary>
        [JsonProperty("strategy")]
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// The result/outcome of the phase execution.
        /// </summary>
        [JsonProperty("result")]
        public string Result { get; set; } = string.Empty;

        /// <summary>
        /// Number of changes successfully applied during this phase.
        /// Zero for analysis or observation phases; 1+ for instrumentation/breakpoint phases.
        /// </summary>
        [JsonProperty("changesAppliedCount")]
        public int ChangesAppliedCount { get; set; } = 0;

        /// <summary>
        /// Timestamp when the phase execution started.
        /// </summary>
        [JsonProperty("executedAt")]
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional error message if the phase failed.
        /// </summary>
        [JsonProperty("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
