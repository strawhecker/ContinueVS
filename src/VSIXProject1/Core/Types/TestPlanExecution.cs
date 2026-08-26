using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an execution history record for a TestPlan.
    /// Wraps the plan ID with a collection of PhaseExecutionResult, separating
    /// execution annotations from the immutable TestPlan definition.
    /// Enables plan re-execution with fresh annotations while preserving history.
    /// </summary>
    public class TestPlanExecution
    {
        /// <summary>
        /// Unique identifier for this execution record.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Reference to the TestPlan ID being executed.
        /// </summary>
        [JsonProperty("planId")]
        public string PlanId { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of phase execution results for this plan execution.
        /// </summary>
        [JsonProperty("phases")]
        public List<PhaseExecutionResult> Phases { get; set; } = new List<PhaseExecutionResult>();

        /// <summary>
        /// Timestamp when this execution started (UTC).
        /// </summary>
        [JsonProperty("startedAt")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this execution completed (UTC). Null if still running.
        /// </summary>
        [JsonProperty("completedAt")]
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Overall status of this plan execution (Pending, Running, Succeeded, Failed, Skipped, Cancelled).
        /// Derived from individual phase statuses if not explicitly set.
        /// </summary>
        [JsonProperty("overallStatus")]
        public ExecutionStatus OverallStatus { get; set; } = ExecutionStatus.Pending;

        /// <summary>
        /// Total number of times this plan has been executed (affects retry/resume tracking).
        /// </summary>
        [JsonProperty("attemptCount")]
        public int AttemptCount { get; set; } = 1;

        /// <summary>
        /// Total duration of this plan execution in milliseconds. Null if not completed.
        /// </summary>
        [JsonIgnore]
        public double? DurationMs
        {
            get
            {
                if (CompletedAt.HasValue)
                {
                    return (CompletedAt.Value - StartedAt).TotalMilliseconds;
                }
                return null;
            }
        }

        /// <summary>
        /// Initializes a new TestPlanExecution with null-safety checks.
        /// </summary>
        public TestPlanExecution()
        {
            if (Phases == null)
            {
                Phases = new List<PhaseExecutionResult>();
            }
        }
    }
}
