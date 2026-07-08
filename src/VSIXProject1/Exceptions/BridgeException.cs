using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Base exception class for all Continue bridge-related errors.
    /// 
    /// Provides a consistent exception hierarchy for bridge IPC, transport, and protocol failures.
    /// Subclasses must specify an ErrorCode (e.g., "PROC_START_FAILED", "TRANSPORT_IO_ERROR")
    /// and may include a Context dictionary for debugging metadata.
    /// 
    /// All bridge exceptions support inner exception chaining to preserve root-cause information.
    /// </summary>
    internal abstract class BridgeException : Exception
    {
        /// <summary>
        /// Gets the error code for this exception.
        /// Used for structured logging, telemetry, and error recovery routing.
        /// Examples: "PROC_START_FAILED", "TRANSPORT_IO_ERROR", "PROTOCOL_PARSE_ERROR".
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the debugging context for this exception.
        /// Contains key-value pairs (e.g., "processId", "filepath", "timeout_ms")
        /// useful for diagnostics and telemetry aggregation.
        /// </summary>
        public IReadOnlyDictionary<string, string> Context { get; }

        /// <summary>
        /// Initializes a new instance of BridgeException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., "PROC_START_FAILED").</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        protected BridgeException(string message, string errorCode)
            : this(message, errorCode, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of BridgeException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        protected BridgeException(string message, string errorCode, Exception? innerException)
            : this(message, errorCode, innerException, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of BridgeException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (key-value pairs). If null, an empty dictionary is used.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        protected BridgeException(string message, string errorCode, Dictionary<string, string>? context)
            : this(message, errorCode, null, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of BridgeException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception, if any.</param>
        /// <param name="context">Debugging context (key-value pairs). If null, an empty dictionary is used.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        protected BridgeException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
            : base(message ?? throw new ArgumentNullException(nameof(message)), innerException)
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            Context = context ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Returns a string representation of this exception, including error code and context.
        /// </summary>
        public override string ToString()
        {
            var result = $"{base.ToString()}{Environment.NewLine}ErrorCode: {ErrorCode}";

            if (Context.Count > 0)
            {
                result += Environment.NewLine + "Context:";
                foreach (var kvp in Context)
                {
                    result += Environment.NewLine + $"  {kvp.Key}: {kvp.Value}";
                }
            }

            return result;
        }
    }
}
