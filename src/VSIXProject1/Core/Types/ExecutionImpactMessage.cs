using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a structured summary of execution impact in agent mode.
    /// Displays which files were changed, tools ran, and overall status/timing.
    /// Inherits from ChatMessage with Role=System for template routing.
    /// </summary>
    public class ExecutionImpactMessage : ChatMessage
    {
        /// <summary>
        /// Collection of phase execution results from the completed execution cycle.
        /// </summary>
        [JsonProperty("phases")]
        public List<PhaseExecutionResult> Phases { get; set; } = new List<PhaseExecutionResult>();

        /// <summary>
        /// Count of files changed across all phases.
        /// </summary>
        [JsonProperty("filesChanged")]
        public int FilesChanged { get; set; }

        /// <summary>
        /// Count of tools executed across all phases.
        /// </summary>
        [JsonProperty("toolsRun")]
        public int ToolsRun { get; set; }

        /// <summary>
        /// Total duration of execution in milliseconds.
        /// </summary>
        [JsonProperty("durationMs")]
        public long DurationMs { get; set; }

        /// <summary>
        /// Overall status of execution (aggregated from phases).
        /// </summary>
        [JsonProperty("status")]
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Succeeded;

        /// <summary>
        /// Whether execution was marked as collapsed (for UI state persistence).
        /// </summary>
        [JsonProperty("isCollapsed")]
        public bool IsCollapsed { get; set; } = true;

        /// <summary>
        /// Constructor: Initializes ExecutionImpactMessage from a collection of phase results.
        /// Automatically computes aggregated metrics.
        /// </summary>
        public ExecutionImpactMessage()
        {
            Role = ChatMessageRole.System;
            Content = "Execution Impact Summary";
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Initialize with phases and compute aggregates.
        /// </summary>
        public void Initialize(IEnumerable<PhaseExecutionResult> phases)
        {
            if (phases == null)
                return;

            Phases = new List<PhaseExecutionResult>(phases);
            ComputeAggregates();
        }

        /// <summary>
        /// Computes aggregated metrics from phases list.
        /// </summary>
        private void ComputeAggregates()
        {
            if (Phases == null || Phases.Count == 0)
            {
                FilesChanged = 0;
                ToolsRun = 0;
                DurationMs = 0;
                Status = ExecutionStatus.Succeeded;
                return;
            }

            // Count tools (assume one tool per phase for now; can be extended)
            ToolsRun = Phases.Count;

            // Sum files changed (placeholder: 0 for now; would require phase to track files)
            FilesChanged = 0;

            // Compute total duration from first start to last end
            var startTimes = Phases.Where(p => p.StartTime != default).Select(p => p.StartTime).ToList();
            var endTimes = Phases.Where(p => p.EndTime.HasValue).Select(p => p.EndTime!.Value).ToList();

            if (startTimes.Any() && endTimes.Any())
            {
                var firstStart = startTimes.Min();
                var lastEnd = endTimes.Max();
                DurationMs = (long)(lastEnd - firstStart).TotalMilliseconds;
            }
            else
            {
                DurationMs = 0;
            }

            // Determine overall status: Failed if any phase failed, else Succeeded
            Status = Phases.Any(p => p.Status == ExecutionStatus.Failed)
                ? ExecutionStatus.Failed
                : ExecutionStatus.Succeeded;
        }

        /// <summary>
        /// Formatted status display string (e.g., "✓ 3 files modified | 5 tools executed | 12.3s").
        /// </summary>
        public string GetSummaryText()
        {
            var statusIcon = Status == ExecutionStatus.Succeeded ? "✓" : "✗";
            return $"{statusIcon} {FilesChanged} files modified | {ToolsRun} tools executed | {DurationMs / 1000.0:F1}s";
        }
    }
}
