using System;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Represents the health status of the Continue bridge process.
    /// Encapsulates health metrics returned by HealthCheckService.PerformHealthCheckAsync().
    /// </summary>
    public readonly record struct HealthStatus
    {
        /// <summary>
        /// The current health state of the bridge process.
        /// </summary>
        public HealthState State { get; }

        /// <summary>
        /// The timestamp when the health check was performed.
        /// </summary>
        public DateTime CheckTimestamp { get; }

        /// <summary>
        /// The time (in milliseconds) taken to complete the health check ping and receive a response.
        /// Only set when State is Healthy or Degraded; zero if check failed to complete.
        /// </summary>
        public long ResponseTimeMs { get; }

        /// <summary>
        /// The number of consecutive health check failures.
        /// Zero when State is Healthy; incremented on failure and reset on success.
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Optional error details if the health check failed.
        /// Null if State is Healthy.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of HealthStatus with the given parameters.
        /// </summary>
        /// <param name="state">The health state.</param>
        /// <param name="checkTimestamp">When the check was performed.</param>
        /// <param name="responseTimeMs">Response time in milliseconds (0 if no response).</param>
        /// <param name="failureCount">Consecutive failure count.</param>
        /// <param name="errorMessage">Optional error details.</param>
        public HealthStatus(
            HealthState state,
            DateTime checkTimestamp,
            long responseTimeMs = 0,
            int failureCount = 0,
            string? errorMessage = null)
        {
            State = state;
            CheckTimestamp = checkTimestamp;
            ResponseTimeMs = responseTimeMs;
            FailureCount = failureCount;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a HealthStatus indicating the bridge is healthy.
        /// </summary>
        public static HealthStatus Healthy(DateTime checkTimestamp, long responseTimeMs)
            => new(HealthState.Healthy, checkTimestamp, responseTimeMs, failureCount: 0);

        /// <summary>
        /// Creates a HealthStatus indicating the bridge is degraded (multiple failures detected).
        /// </summary>
        public static HealthStatus Degraded(DateTime checkTimestamp, int failureCount, string? errorMessage = null)
            => new(HealthState.Degraded, checkTimestamp, responseTimeMs: 0, failureCount, errorMessage);

        /// <summary>
        /// Creates a HealthStatus indicating a health check failure.
        /// </summary>
        public static HealthStatus Failed(DateTime checkTimestamp, string errorMessage, int failureCount = 1)
            => new(HealthState.Failed, checkTimestamp, responseTimeMs: 0, failureCount, errorMessage);

        /// <summary>
        /// Gets a human-readable summary of the health status.
        /// </summary>
        public override string ToString()
        {
            return State switch
            {
                HealthState.Healthy => $"Healthy (response: {ResponseTimeMs}ms, checked: {CheckTimestamp:O})",
                HealthState.Degraded => $"Degraded (failures: {FailureCount}, error: {ErrorMessage ?? "unknown"}, checked: {CheckTimestamp:O})",
                HealthState.Failed => $"Failed (failures: {FailureCount}, error: {ErrorMessage ?? "unknown"}, checked: {CheckTimestamp:O})",
                _ => $"Unknown state: {State}"
            };
        }
    }

    /// <summary>
    /// Enumeration of possible bridge process health states.
    /// </summary>
    public enum HealthState
    {
        /// <summary>Bridge process is responding normally to health checks.</summary>
        Healthy = 0,

        /// <summary>Bridge process shows signs of degradation (multiple check failures, but not completely unresponsive).</summary>
        Degraded = 1,

        /// <summary>Bridge process health check failed; process may be unresponsive or crashed.</summary>
        Failed = 2
    }
}
