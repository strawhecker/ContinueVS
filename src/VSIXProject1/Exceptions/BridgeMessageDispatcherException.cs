using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when the Continue bridge message dispatcher encounters errors
    /// during message routing, handler dispatch, or protocol validation.
    /// 
    /// Covers failures including:
    /// - Handler not found for message type
    /// - Message envelope validation failed
    /// - Handler execution threw an exception
    /// - Message dispatch timeout exceeded
    /// - Transport unavailable or disconnected
    /// 
    /// Used by MessageDispatcher (Step 42) to surface dispatch errors to the bridge
    /// lifecycle manager (Step 45) and error recovery middleware (Step 74).
    /// </summary>
    internal sealed class BridgeMessageDispatcherException : BridgeException
    {
        /// <summary>
        /// Categorizes the operation that failed, enabling error recovery routing.
        /// </summary>
        public enum OperationType
        {
            /// <summary>Handler not found or not registered for message type.</summary>
            HandlerNotFound,

            /// <summary>Message envelope validation failed (null, empty type, missing id).</summary>
            ValidationFailed,

            /// <summary>Handler execution threw an exception.</summary>
            DispatchError,

            /// <summary>Transport unavailable, disconnected, or closed.</summary>
            TransportError,

            /// <summary>Message dispatch exceeded timeout threshold.</summary>
            TimeoutExceeded,
        }

        /// <summary>
        /// Well-known error codes for dispatcher failures (JSON-RPC error code mappings).
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>Handler not found or not registered (-32003).</summary>
            public const string HandlerNotFound = "DISPATCHER_HANDLER_NOT_FOUND";

            /// <summary>Message validation failed (-32004).</summary>
            public const string ValidationFailed = "DISPATCHER_VALIDATION_FAILED";

            /// <summary>Handler execution threw exception (-32603).</summary>
            public const string DispatchError = "DISPATCHER_DISPATCH_ERROR";

            /// <summary>Transport unavailable (-32002).</summary>
            public const string TransportError = "DISPATCHER_TRANSPORT_ERROR";

            /// <summary>Dispatch timeout exceeded (-32000).</summary>
            public const string TimeoutExceeded = "DISPATCHER_TIMEOUT_EXCEEDED";
        }

        /// <summary>
        /// Gets the operation that failed.
        /// </summary>
        public OperationType Operation { get; }

        /// <summary>
        /// Gets the message type that triggered the error (e.g., "bridge:getEditorState").
        /// Null if validation failed before type extraction.
        /// </summary>
        public string? MessageType { get; }

        /// <summary>
        /// Gets the handler name that failed (e.g., "GetEditorStateHandler").
        /// Null if handler not found or dispatch error occurred before handler invocation.
        /// </summary>
        public string? HandlerName { get; }

        /// <summary>
        /// Initializes a new instance of BridgeMessageDispatcherException with message and operation type.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="operation">The dispatcher operation that failed.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., HandlerNotFound).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public BridgeMessageDispatcherException(
            string message,
            OperationType operation,
            string errorCode)
            : base(message, errorCode)
        {
            Operation = operation;
            MessageType = null;
            HandlerName = null;
        }

        /// <summary>
        /// Initializes a new instance of BridgeMessageDispatcherException with message, operation, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="operation">The dispatcher operation that failed.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public BridgeMessageDispatcherException(
            string message,
            OperationType operation,
            string errorCode,
            Exception? innerException)
            : base(message, errorCode, innerException)
        {
            Operation = operation;
            MessageType = null;
            HandlerName = null;
        }

        /// <summary>
        /// Initializes a new instance of BridgeMessageDispatcherException with message, operation, message type, and context.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="operation">The dispatcher operation that failed.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="messageType">The message type that triggered the error.</param>
        /// <param name="context">Debugging context (e.g., messageId, handlerName, timeout_ms).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public BridgeMessageDispatcherException(
            string message,
            OperationType operation,
            string errorCode,
            string? messageType,
            Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
            Operation = operation;
            MessageType = messageType;
            HandlerName = context?.TryGetValue("handlerName", out var handlerName) ?? false
                ? handlerName
                : null;
        }

        /// <summary>
        /// Initializes a new instance of BridgeMessageDispatcherException with message, operation, message type, handler name, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="operation">The dispatcher operation that failed.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="messageType">The message type that triggered the error.</param>
        /// <param name="handlerName">The handler name involved in the error.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public BridgeMessageDispatcherException(
            string message,
            OperationType operation,
            string errorCode,
            string? messageType,
            string? handlerName,
            Exception? innerException)
            : base(message, errorCode, innerException)
        {
            Operation = operation;
            MessageType = messageType;
            HandlerName = handlerName;
        }

        /// <summary>
        /// Initializes a new instance of BridgeMessageDispatcherException with full context.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="operation">The dispatcher operation that failed.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="messageType">The message type that triggered the error.</param>
        /// <param name="handlerName">The handler name involved in the error.</param>
        /// <param name="context">Debugging context (e.g., messageId, timeout_ms).</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public BridgeMessageDispatcherException(
            string message,
            OperationType operation,
            string errorCode,
            string? messageType,
            string? handlerName,
            Dictionary<string, string>? context,
            Exception? innerException)
            : base(message, errorCode, innerException, context)
        {
            Operation = operation;
            MessageType = messageType;
            HandlerName = handlerName;
        }
    }
}
