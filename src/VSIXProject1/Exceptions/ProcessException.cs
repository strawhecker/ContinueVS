using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when the Continue npm bridge subprocess fails during lifecycle operations.
    /// 
    /// Covers failures including:
    /// - Process start (executable not found, insufficient privileges, spawn timeout)
    /// - Process unexpected exit (crash, termination by external signal)
    /// - Process stop (graceful shutdown timeout, kill timeout)
    /// - Stream initialization (stdin/stdout setup failure)
    /// 
    /// Used by ProcessManager (Step 19) and StdioTransport (Step 20) to surface process-level errors
    /// to bridge lifecycle manager (Step 45) and error recovery middleware (Step 74).
    /// </summary>
    internal sealed class ProcessException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for process-related failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>Process failed to start (executable not found, spawn error).</summary>
            public const string ProcessStartFailed = "PROC_START_FAILED";

            /// <summary>Process exited unexpectedly (crash, external termination).</summary>
            public const string ProcessExitedUnexpectedly = "PROC_EXIT_UNEXPECTED";

            /// <summary>Process stop timed out (graceful shutdown did not complete in time).</summary>
            public const string ProcessStopTimeout = "PROC_STOP_TIMEOUT";

            /// <summary>Process kill timed out (forced termination did not succeed).</summary>
            public const string ProcessKillTimeout = "PROC_KILL_TIMEOUT";

            /// <summary>Failed to initialize streams (stdin/stdout setup failure).</summary>
            public const string StreamInitializationFailed = "PROC_STREAM_INIT_FAILED";

            /// <summary>Process not running when operation requires it.</summary>
            public const string ProcessNotRunning = "PROC_NOT_RUNNING";

            /// <summary>Process already running when operation requires it to be stopped.</summary>
            public const string ProcessAlreadyRunning = "PROC_ALREADY_RUNNING";
        }

        /// <summary>
        /// Initializes a new instance of ProcessException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., ProcessStartFailed).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProcessException(string message, string errorCode)
            : base(message, errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProcessException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., IOException from stream access).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProcessException(string message, string errorCode, Exception? innerException)
            : base(message, errorCode, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProcessException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (e.g., processId, exitCode, workingDirectory).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProcessException(string message, string errorCode, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProcessException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., processId, exitCode, workingDirectory).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProcessException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
        }
    }
}
