using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the result of a change execution attempt, including retry history and final status.
    /// Enables caller to understand if change succeeded, was retried, or hit retry threshold.
    /// </summary>
    public class ChangeExecutionResult
    {
        /// <summary>
        /// Execution status enumeration.
        /// </summary>
        public enum StatusCode
        {
            /// <summary>
            /// Change succeeded on first attempt; no retry was needed.
            /// </summary>
            Success = 0,

            /// <summary>
            /// Change initially failed, but succeeded after one or more refined attempts.
            /// </summary>
            RetriedSuccess = 1,

            /// <summary>
            /// Change failed after exhausting max retry attempts; execution halted without automatic rollback.
            /// </summary>
            RetryThresholdExceeded = 2,

            /// <summary>
            /// Execution was cancelled (e.g., via CancellationToken).
            /// </summary>
            ExecutionCancelled = 3
        }

        /// <summary>
        /// The execution status (Success, RetriedSuccess, RetryThresholdExceeded, or ExecutionCancelled).
        /// </summary>
        [JsonProperty("status")]
        public StatusCode Status { get; set; }

        /// <summary>
        /// The total number of attempts made (1 for first attempt, 2+ for retries).
        /// </summary>
        [JsonProperty("executedAttemptCount")]
        public int ExecutedAttemptCount { get; set; }

        /// <summary>
        /// The final CodeChange that was applied (original on success, last refined on retry).
        /// Null if no change was applied.
        /// </summary>
        [JsonProperty("finalChange")]
        public CodeChange? FinalChange { get; set; }

        /// <summary>
        /// Complete history of refinement attempts during retry loop.
        /// Timeline of what was analyzed and proposed at each stage.
        /// </summary>
        [JsonProperty("refinementHistory")]
        public List<RefinementAttempt> RefinementHistory { get; set; } = new List<RefinementAttempt>();

        /// <summary>
        /// Summary evidence string describing the execution outcome and key logs.
        /// </summary>
        [JsonProperty("evidence")]
        public string Evidence { get; set; } = string.Empty;

        /// <summary>
        /// Total execution time in milliseconds.
        /// </summary>
        [JsonProperty("executionTimeMs")]
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Timestamp when execution began.
        /// </summary>
        [JsonProperty("executedAt")]
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }
}
