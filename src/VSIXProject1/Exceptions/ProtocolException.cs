using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when Continue bridge protocol message handling fails.
    /// 
    /// Covers failures including:
    /// - Malformed JSON-RPC message (missing required fields, invalid types)
    /// - Message ID mismatch (request/response correlation failure)
    /// - Invalid message type or handler routing
    /// - Protocol version incompatibility
    /// - Required field missing or null (messageType, messageId, data)
    /// 
    /// Used by JsonRpcProtocol (Step 21), MessageBufferer, and handlers (Steps 50+)
    /// to validate message structure before dispatching.
    /// </summary>
    internal sealed class ProtocolException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for protocol validation failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>JSON-RPC message is malformed (invalid structure, missing fields).</summary>
            public const string MalformedMessage = "PROTOCOL_MALFORMED_MESSAGE";

            /// <summary>Required message field is missing or null.</summary>
            public const string MissingRequiredField = "PROTOCOL_MISSING_FIELD";

            /// <summary>Message field has invalid type or value.</summary>
            public const string InvalidFieldValue = "PROTOCOL_INVALID_FIELD_VALUE";

            /// <summary>Message ID mismatch (response does not match outstanding request).</summary>
            public const string MessageIdMismatch = "PROTOCOL_ID_MISMATCH";

            /// <summary>Message type is unknown or unsupported.</summary>
            public const string UnknownMessageType = "PROTOCOL_UNKNOWN_TYPE";

            /// <summary>Handler for this message type does not exist.</summary>
            public const string HandlerNotFound = "PROTOCOL_HANDLER_NOT_FOUND";

            /// <summary>Protocol version is incompatible.</summary>
            public const string IncompatibleVersion = "PROTOCOL_INCOMPATIBLE_VERSION";

            /// <summary>Request payload validation failed.</summary>
            public const string InvalidRequest = "PROTOCOL_INVALID_REQUEST";

            /// <summary>Response payload validation failed.</summary>
            public const string InvalidResponse = "PROTOCOL_INVALID_RESPONSE";
        }

        /// <summary>
        /// Initializes a new instance of ProtocolException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., MalformedMessage).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProtocolException(string message, string errorCode)
            : base(message, errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProtocolException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., JsonReaderException, NullReferenceException).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProtocolException(string message, string errorCode, Exception? innerException)
            : base(message, errorCode, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProtocolException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (e.g., messageType, messageId, expectedMessageId, missingField, fieldName, fieldValue).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProtocolException(string message, string errorCode, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of ProtocolException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., messageType, messageId, expectedMessageId, missingField, fieldName, fieldValue).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ProtocolException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
        }
    }
}
