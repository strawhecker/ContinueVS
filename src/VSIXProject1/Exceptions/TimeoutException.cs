using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when a bridge operation exceeds its timeout limit.
    /// 
    /// Covers timeouts including:
    /// - RPC call timeout (handler did not respond within deadline)
    /// - Health check timeout (process did not respond to health probe)
    /// - Process startup timeout (npm server did not initialize in time)
    /// - Process shutdown timeout (graceful or forced termination took too long)
    /// - Message send/receive timeout (stream I/O exceeded deadline)
    /// 
    /// Used by timeout manager (Step 64), health check service (Step 24),
    /// and lifecycle manager (Step 45) to enforce timing guarantees.
    /// </summary>
    internal sealed class TimeoutException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for timeout-related failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>RPC call did not complete within the configured timeout.</summary>
            public const string RpcCallTimeout = "TIMEOUT_RPC_CALL";

            /// <summary>Process health check did not complete within the configured timeout.</summary>
            public const string HealthCheckTimeout = "TIMEOUT_HEALTH_CHECK";

            /// <summary>Process startup did not complete within the configured timeout.</summary>
            public const string ProcessStartTimeout = "TIMEOUT_PROCESS_START";

            /// <summary>Graceful process shutdown did not complete within the configured timeout.</summary>
            public const string ProcessShutdownTimeout = "TIMEOUT_PROCESS_SHUTDOWN";

            /// <summary>Forced process kill did not complete within the configured timeout.</summary>
            public const string ProcessKillTimeout = "TIMEOUT_PROCESS_KILL";

            /// <summary>Message send operation did not complete within the configured timeout.</summary>
            public const string SendTimeout = "TIMEOUT_SEND";

            /// <summary>Message receive operation did not complete within the configured timeout.</summary>
            public const string ReceiveTimeout = "TIMEOUT_RECEIVE";
        }

        /// <summary>
        /// Gets the elapsed time in milliseconds when the timeout was exceeded.
        /// </summary>
        public long ElapsedMs { get; }

        /// <summary>
        /// Gets the timeout limit in milliseconds that was exceeded.
        /// </summary>
        public long TimeoutMs { get; }

        /// <summary>
        /// Initializes a new instance of TimeoutException with a message, error code, and timing information.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., RpcCallTimeout).</param>
        /// <param name="elapsedMs">The actual elapsed time in milliseconds.</param>
        /// <param name="timeoutMs">The timeout limit in milliseconds.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TimeoutException(string message, string errorCode, long elapsedMs, long timeoutMs)
            : base(message, errorCode)
        {
            ElapsedMs = elapsedMs;
            TimeoutMs = timeoutMs;
        }

        /// <summary>
        /// Initializes a new instance of TimeoutException with a message, error code, timing information, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="elapsedMs">The actual elapsed time in milliseconds.</param>
        /// <param name="timeoutMs">The timeout limit in milliseconds.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., OperationCanceledException).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TimeoutException(string message, string errorCode, long elapsedMs, long timeoutMs, Exception? innerException)
            : base(message, errorCode, innerException)
        {
            ElapsedMs = elapsedMs;
            TimeoutMs = timeoutMs;
        }

        /// <summary>
        /// Initializes a new instance of TimeoutException with a message, error code, timing information, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="elapsedMs">The actual elapsed time in milliseconds.</param>
        /// <param name="timeoutMs">The timeout limit in milliseconds.</param>
        /// <param name="context">Debugging context (e.g., requestId, operationName, component).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TimeoutException(string message, string errorCode, long elapsedMs, long timeoutMs, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
            ElapsedMs = elapsedMs;
            TimeoutMs = timeoutMs;
        }

        /// <summary>
        /// Initializes a new instance of TimeoutException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="elapsedMs">The actual elapsed time in milliseconds.</param>
        /// <param name="timeoutMs">The timeout limit in milliseconds.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., requestId, operationName, component).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TimeoutException(string message, string errorCode, long elapsedMs, long timeoutMs, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
            ElapsedMs = elapsedMs;
            TimeoutMs = timeoutMs;
        }
    }
}
