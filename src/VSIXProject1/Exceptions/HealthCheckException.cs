using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when the Continue bridge process health check fails.
    /// 
    /// Covers failures including:
    /// - Health probe response invalid (malformed, unexpected data)
    /// - Process not responding (no response within timeout)
    /// - Health check disabled but required for operation
    /// - Multiple consecutive health check failures (circuit breaker)
    /// - Process state inconsistent (crashed but still marked as running)
    /// 
    /// Used by health check service (Step 24) and bridge lifecycle manager (Step 45)
    /// to detect and respond to process degradation or failure.
    /// </summary>
    internal sealed class HealthCheckException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for health monitoring failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>Health check probe returned invalid or unexpected response.</summary>
            public const string InvalidProbeResponse = "HEALTH_INVALID_RESPONSE";

            /// <summary>Process not responding to health check (no response).</summary>
            public const string ProcessNotResponding = "HEALTH_NOT_RESPONDING";

            /// <summary>Health check probe failed with transport error.</summary>
            public const string ProbeFailed = "HEALTH_PROBE_FAILED";

            /// <summary>Process claimed healthy but showing signs of degradation.</summary>
            public const string ProcessDegraded = "HEALTH_DEGRADED";

            /// <summary>Multiple consecutive health checks failed (circuit breaker triggered).</summary>
            public const string CircuitBreakerTriggered = "HEALTH_CIRCUIT_BREAKER";

            /// <summary>Process state inconsistent (crashed or exited but still marked as running).</summary>
            public const string StateInconsistent = "HEALTH_STATE_INCONSISTENT";

            /// <summary>Health check is disabled but required for this operation.</summary>
            public const string CheckDisabled = "HEALTH_CHECK_DISABLED";
        }

        /// <summary>
        /// Gets the number of consecutive health check failures.
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., ProcessNotResponding).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode)
            : base(message, errorCode)
        {
            FailureCount = 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message, error code, and failure count.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="failureCount">The number of consecutive health check failures.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, int failureCount)
            : base(message, errorCode)
        {
            FailureCount = failureCount > 0 ? failureCount : 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., TransportException, TimeoutException).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, Exception? innerException)
            : base(message, errorCode, innerException)
        {
            FailureCount = 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message, error code, failure count, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="failureCount">The number of consecutive health check failures.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, int failureCount, Exception? innerException)
            : base(message, errorCode, innerException)
        {
            FailureCount = failureCount > 0 ? failureCount : 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (e.g., lastProbeTime, responseTime, processMemory, processUptime).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
            FailureCount = 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with a message, error code, failure count, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="failureCount">The number of consecutive health check failures.</param>
        /// <param name="context">Debugging context (e.g., lastProbeTime, responseTime, processMemory, processUptime).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, int failureCount, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
            FailureCount = failureCount > 0 ? failureCount : 1;
        }

        /// <summary>
        /// Initializes a new instance of HealthCheckException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="failureCount">The number of consecutive health check failures.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., lastProbeTime, responseTime, processMemory, processUptime).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public HealthCheckException(string message, string errorCode, int failureCount, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
            FailureCount = failureCount > 0 ? failureCount : 1;
        }
    }
}
