using Newtonsoft.Json.Linq;
using System;

namespace ContinueVS.IPC
{
    /// <summary>
    /// JSON-RPC 2.0 protocol utilities for Continue bridge communication.
    /// 
    /// Provides methods for creating well-formed RPC requests, responses, and error objects,
    /// as well as validation and inspection utilities.
    /// 
    /// Spec: https://www.jsonrpc.org/specification
    /// Continue Protocol: Extends JSON-RPC with messageType, messageId, data envelope
    /// 
    /// Integration:
    /// - StdioTransport.SendMessageAsync() sends these Message envelopes to stdout
    /// - MessageBufferer deserializes incoming Message objects
    /// - Handlers (Step 50+) use CreateRequest/CreateResponse for dispatch
    /// - No modifications to core-server.js required; Node relay is pass-through
    /// - Message validation via JsonRpcProtocol.ValidateMessage() recommended before sending
    /// </summary>
    internal static class JsonRpcProtocol
    {
        // =====================================================================
        // JSON-RPC 2.0 Version & Standard Error Codes (per JSON-RPC spec)
        // =====================================================================

        private const string RPC_VERSION = "2.0";

        /// <summary>
        /// Invalid JSON was received by the server (parse error).
        /// </summary>
        public const int PARSE_ERROR = -32700;

        /// <summary>
        /// The JSON sent is not a valid Request object.
        /// </summary>
        public const int INVALID_REQUEST = -32600;

        /// <summary>
        /// The method does not exist / is not available.
        /// </summary>
        public const int METHOD_NOT_FOUND = -32601;

        /// <summary>
        /// Invalid method parameter(s).
        /// </summary>
        public const int INVALID_PARAMS = -32602;

        /// <summary>
        /// Internal JSON-RPC error.
        /// </summary>
        public const int INTERNAL_ERROR = -32603;

        /// <summary>
        /// Server error code range: -32099 to -32000 (reserved for implementation-defined errors)
        /// </summary>
        public const int SERVER_ERROR_MIN = -32099;
        public const int SERVER_ERROR_MAX = -32000;

        // =====================================================================
        // Bridge-Specific Error Codes (within server error range)
        // =====================================================================

        /// <summary>
        /// RPC call timed out (timeout exceeded without response).
        /// </summary>
        public const int BRIDGE_TIMEOUT = -32000;

        /// <summary>
        /// Continue process is not running or was terminated.
        /// </summary>
        public const int BRIDGE_PROCESS_DEAD = -32001;

        /// <summary>
        /// Bridge is in invalid state for the requested operation.
        /// </summary>
        public const int BRIDGE_INVALID_STATE = -32002;

        /// <summary>
        /// Handler not found or not registered.
        /// </summary>
        public const int BRIDGE_HANDLER_NOT_FOUND = -32003;

        /// <summary>
        /// Message validation failed (malformed envelope).
        /// </summary>
        public const int BRIDGE_VALIDATION_ERROR = -32004;

        // =====================================================================
        // Request Creation
        // =====================================================================

        /// <summary>
        /// Creates a JSON-RPC request message with auto-generated messageId.
        /// </summary>
        /// <param name="messageType">The RPC method name (e.g., "bridge:getEditorState")</param>
        /// <param name="data">Optional request payload</param>
        /// <returns>A new Message envelope ready to send</returns>
        /// <exception cref="ArgumentException">If messageType is null or whitespace</exception>
        public static Message CreateRequest(string messageType, JToken? data = null)
        {
            if (string.IsNullOrWhiteSpace(messageType))
                throw new ArgumentException("messageType is required", nameof(messageType));

            return new Message
            {
                MessageType = messageType,
                MessageId = GenerateMessageId(),
                Data = data
            };
        }

        // =====================================================================
        // Response Creation
        // =====================================================================

        /// <summary>
        /// Creates a JSON-RPC success response message.
        /// The messageId must match the request being responded to.
        /// </summary>
        /// <param name="messageId">The request's messageId (for correlation)</param>
        /// <param name="data">The response payload/result</param>
        /// <returns>A new response Message envelope</returns>
        /// <exception cref="ArgumentException">If messageId is null or whitespace</exception>
        public static Message CreateResponse(string messageId, JToken? data = null)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("messageId is required", nameof(messageId));

            return new Message
            {
                MessageType = "rpc:response",
                MessageId = messageId,
                Data = data
            };
        }

        // =====================================================================
        // Error Response Creation
        // =====================================================================

        /// <summary>
        /// Creates a JSON-RPC error response message with code and description.
        /// </summary>
        /// <param name="messageId">The request's messageId (for correlation)</param>
        /// <param name="code">JSON-RPC error code (negative integer)</param>
        /// <param name="message">Human-readable error description</param>
        /// <returns>A new error Message envelope</returns>
        /// <exception cref="ArgumentException">If messageId or message is null/whitespace</exception>
        public static Message CreateError(string messageId, int code, string message)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("messageId is required", nameof(messageId));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("message is required", nameof(message));

            var errorObj = new JObject
            {
                ["code"] = code,
                ["message"] = message
            };

            return new Message
            {
                MessageType = "rpc:error",
                MessageId = messageId,
                Data = errorObj
            };
        }

        /// <summary>
        /// Creates a JSON-RPC error response with code, message, and additional data.
        /// </summary>
        /// <param name="messageId">The request's messageId (for correlation)</param>
        /// <param name="code">JSON-RPC error code (negative integer)</param>
        /// <param name="message">Human-readable error description</param>
        /// <param name="errorData">Additional error context/details</param>
        /// <returns>A new error Message envelope with data field populated</returns>
        /// <exception cref="ArgumentException">If messageId or message is null/whitespace</exception>
        public static Message CreateError(string messageId, int code, string message, JToken? errorData)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("messageId is required", nameof(messageId));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("message is required", nameof(message));

            var errorObj = new JObject
            {
                ["code"] = code,
                ["message"] = message
            };

            if (errorData != null)
            {
                errorObj["data"] = errorData;
            }

            return new Message
            {
                MessageType = "rpc:error",
                MessageId = messageId,
                Data = errorObj
            };
        }

        // =====================================================================
        // Message Validation
        // =====================================================================

        /// <summary>
        /// Validates that a message conforms to the JSON-RPC envelope requirements.
        /// 
        /// Checks:
        /// - Message is not null
        /// - messageType is present and non-empty
        /// - messageId is present and non-empty
        /// </summary>
        /// <param name="message">The message to validate</param>
        /// <returns>Tuple (isValid, errorMessage). errorMessage is null if valid.</returns>
        public static (bool isValid, string? error) ValidateMessage(Message? message)
        {
            if (message == null)
                return (false, "Message is null");

            if (string.IsNullOrWhiteSpace(message.MessageType))
                return (false, "messageType is required and must be non-empty");

            if (string.IsNullOrWhiteSpace(message.MessageId))
                return (false, "messageId is required and must be non-empty");

            return (true, null);
        }

        // =====================================================================
        // Message Inspection & Utilities
        // =====================================================================

        /// <summary>
        /// Safely extracts the messageId from a message.
        /// Returns null if the message is null or messageId is empty.
        /// </summary>
        public static string? ExtractMessageId(Message? message)
        {
            if (message?.MessageId == null)
                return null;

            return string.IsNullOrWhiteSpace(message.MessageId) ? null : message.MessageId;
        }

        /// <summary>
        /// Checks if a message is a response (success or error).
        /// Response types are "rpc:response" and "rpc:error".
        /// </summary>
        public static bool IsResponseMessage(string? messageType)
        {
            return messageType is "rpc:response" or "rpc:error";
        }

        /// <summary>
        /// Checks if a message is an error response (messageType == "rpc:error").
        /// </summary>
        public static bool IsErrorMessage(string? messageType)
        {
            return messageType == "rpc:error";
        }

        /// <summary>
        /// Checks if a message is a success response (messageType == "rpc:response").
        /// </summary>
        public static bool IsSuccessMessage(string? messageType)
        {
            return messageType == "rpc:response";
        }

        /// <summary>
        /// Extracts the error code from an error message's data field.
        /// Returns null if the message is not an error or code is missing.
        /// </summary>
        public static int? ExtractErrorCode(Message? message)
        {
            if (!IsErrorMessage(message?.MessageType) || message?.Data is not JObject errorObj)
                return null;

            return errorObj.Value<int?>("code");
        }

        /// <summary>
        /// Extracts the error message from an error message's data field.
        /// Returns null if the message is not an error or message is missing.
        /// </summary>
        public static string? ExtractErrorMessage(Message? message)
        {
            if (!IsErrorMessage(message?.MessageType) || message?.Data is not JObject errorObj)
                return null;

            return errorObj.Value<string?>("message");
        }

        // =====================================================================
        // ID Generation
        // =====================================================================

        /// <summary>
        /// Generates a unique message ID using UUID v4.
        /// Each call produces a new, globally unique identifier.
        /// </summary>
        private static string GenerateMessageId()
        {
            return Guid.NewGuid().ToString();
        }

        // =====================================================================
        // Error Code Helpers
        // =====================================================================

        /// <summary>
        /// Checks if a code is a standard JSON-RPC reserved error code.
        /// Returns true for codes in the range [-32768, -32000].
        /// </summary>
        public static bool IsReservedErrorCode(int code)
        {
            return code >= -32768 && code <= -32000;
        }

        /// <summary>
        /// Checks if a code is a bridge-specific error code.
        /// </summary>
        public static bool IsBridgeErrorCode(int code)
        {
            return code >= BRIDGE_HANDLER_NOT_FOUND && code <= BRIDGE_TIMEOUT;
        }

        /// <summary>
        /// Gets a human-readable description for standard JSON-RPC error codes.
        /// Returns null for unknown codes.
        /// </summary>
        public static string? GetStandardErrorDescription(int code)
        {
            return code switch
            {
                PARSE_ERROR => "Parse error",
                INVALID_REQUEST => "Invalid Request",
                METHOD_NOT_FOUND => "Method not found",
                INVALID_PARAMS => "Invalid params",
                INTERNAL_ERROR => "Internal error",
                _ => null
            };
        }

        /// <summary>
        /// Gets a human-readable description for bridge-specific error codes.
        /// Returns null for unknown codes.
        /// </summary>
        public static string? GetBridgeErrorDescription(int code)
        {
            return code switch
            {
                BRIDGE_TIMEOUT => "RPC call timed out",
                BRIDGE_PROCESS_DEAD => "Continue process is not running",
                BRIDGE_INVALID_STATE => "Bridge is in invalid state",
                BRIDGE_HANDLER_NOT_FOUND => "Handler not found",
                BRIDGE_VALIDATION_ERROR => "Message validation failed",
                _ => null
            };
        }
    }
}
