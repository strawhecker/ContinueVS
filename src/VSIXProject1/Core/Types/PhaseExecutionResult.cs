using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the execution result for a single phase within a test plan.
    /// Captures status, evidence, timing, and error details separate from the phase definition.
    /// </summary>
    public class PhaseExecutionResult
    {
        /// <summary>
        /// Reference to the InternalPhase ID that this result tracks.
        /// </summary>
        [JsonProperty("phaseId")]
        public string PhaseId { get; set; } = string.Empty;

        /// <summary>
        /// Current execution status of this phase.
        /// </summary>
        [JsonProperty("status")]
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

        /// <summary>
        /// Human-readable summary of what happened during execution (e.g., "Analysis complete with 3 hypotheses").
        /// </summary>
        [JsonProperty("evidence")]
        public string Evidence { get; set; } = string.Empty;

        /// <summary>
        /// Number of times this phase has been attempted (1 for first try, 2+ for retries).
        /// </summary>
        [JsonProperty("attemptCount")]
        public int AttemptCount { get; set; } = 1;

        /// <summary>
        /// Timestamp when execution of this phase started (UTC).
        /// </summary>
        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when execution of this phase ended (UTC). Null if still running.
        /// </summary>
        [JsonProperty("endTime")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration of execution in milliseconds. Null if not completed.
        /// </summary>
        [JsonProperty("durationMs")]
        public double? DurationMs
        {
            get
            {
                if (EndTime.HasValue)
                {
                    return (EndTime.Value - StartTime).TotalMilliseconds;
                }
                return null;
            }
        }

        /// <summary>
        /// Optional error message if execution failed.
        /// </summary>
        [JsonProperty("errorDetails")]
        public string? ErrorDetails { get; set; }
    }
}
