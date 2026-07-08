using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when the Continue bridge transport encounters message I/O failures.
    /// 
    /// Covers failures including:
    /// - Message send (serialization, stream write, buffer full)
    /// - Message receive (deserialization, stream read, EOF, malformed JSON)
    /// - Stream state errors (reader/writer disposed, stream closed)
    /// - Buffering errors (internal queue overflow, buffer corruption)
    /// 
    /// Used by StdioTransport (Step 20) and MessageBufferer to surface I/O errors
    /// to error recovery middleware (Step 74) and bridge lifecycle manager (Step 45).
    /// </summary>
    internal sealed class TransportException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for transport I/O failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>Failed to send message (serialization or stream write error).</summary>
            public const string SendFailed = "TRANSPORT_SEND_FAILED";

            /// <summary>Failed to receive message (deserialization or stream read error).</summary>
            public const string ReceiveFailed = "TRANSPORT_RECEIVE_FAILED";

            /// <summary>Message serialization failed (JSON encoding error).</summary>
            public const string SerializationFailed = "TRANSPORT_SERIALIZATION_FAILED";

            /// <summary>Message deserialization failed (JSON parsing or validation error).</summary>
            public const string DeserializationFailed = "TRANSPORT_DESERIALIZATION_FAILED";

            /// <summary>Stream closed unexpectedly (reader/writer disposed or EOF reached).</summary>
            public const string StreamClosed = "TRANSPORT_STREAM_CLOSED";

            /// <summary>Stream in invalid state (disposed, not initialized).</summary>
            public const string InvalidStreamState = "TRANSPORT_INVALID_STREAM_STATE";

            /// <summary>Message buffering failed (queue overflow, buffer corruption).</summary>
            public const string BufferingFailed = "TRANSPORT_BUFFERING_FAILED";

            /// <summary>Transport not connected (not running).</summary>
            public const string NotConnected = "TRANSPORT_NOT_CONNECTED";
        }

        /// <summary>
        /// Initializes a new instance of TransportException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., SendFailed).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TransportException(string message, string errorCode)
            : base(message, errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of TransportException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., IOException, JsonReaderException).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TransportException(string message, string errorCode, Exception? innerException)
            : base(message, errorCode, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of TransportException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (e.g., messageId, messageType, bytesRead, jsonSnippet).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TransportException(string message, string errorCode, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of TransportException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., messageId, messageType, bytesRead, jsonSnippet).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public TransportException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
        }
    }
}
