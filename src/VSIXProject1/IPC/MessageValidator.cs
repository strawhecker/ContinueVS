using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Static utility for validating bridge messages.
    /// 
    /// Validates both custom Message envelope and JSON-RPC payload within data.
    /// Provides error response building per JSON-RPC 2.0 spec.
    /// 
    /// Related: Step 73 (validation hook), Step 47 (MiddlewareChain)
    /// 
    /// Note: This class is internal because Message is internal.
    /// If Message becomes public in future, this can be made public.
    /// </summary>
    internal static class MessageValidator
    {
        /// <summary>
        /// JSON-RPC error codes per spec.
        /// </summary>
        private static readonly Dictionary<string, int> ErrorCodeMap = new()
        {
            // Spec-reserved codes
            ["ParseError"] = -32700,
            ["InvalidRequest"] = -32600,
            ["MethodNotFound"] = -32601,
            ["InvalidParams"] = -32602,
            ["InternalError"] = -32603,
        };

        /// <summary>
        /// Validates Message envelope structure.
        ///
        /// Checks:
        /// - message is not null
        /// - messageType is non-null, non-empty string
        /// - messageId is non-null, non-empty string, ≤256 characters
        /// - data can be null or an object (null data is allowed for some message types)
        /// </summary>
        /// <param name="message">Message to validate (can be null)</param>
        /// <returns>Tuple (isValid, errorMessage). errorMessage is null if valid.</returns>
        public static (bool isValid, string? error) ValidateEnvelope(Message? message)
        {
            if (message == null)
                return (false, "Message is null");

            if (string.IsNullOrWhiteSpace(message.MessageType))
                return (false, "messageType is required and must be non-empty");

            if (string.IsNullOrWhiteSpace(message.MessageId))
                return (false, "messageId is required and must be non-empty");

            // MessageId max length check (256 chars per existing validation)
            if (message.MessageId.Length > 256)
                return (false, $"messageId exceeds maximum length of 256 characters (got {message.MessageId.Length})");

            // Data can be null for some message types (e.g., notifications without payload)
            // If present, it should be an object-like JObject; validation will check that later

            return (true, null);
        }

        /// <summary>
        /// Validates JSON-RPC payload within message.data.
        ///
        /// For requests (isRequest=true):
        /// - method: string, required
        /// - params: object or array, optional
        /// - id: string or number, optional (if missing, treated as notification)
        ///
        /// For responses (isRequest=false):
        /// - Exactly one of result OR error must be present
        /// - If error: must have code (int) and message (string)
        /// </summary>
        /// <param name="data">Message.data to validate (can be null)</param>
        /// <param name="isRequest">True for request validation, false for response</param>
        /// <returns>Tuple (isValid, errorMessage, errorCode?). errorMessage/errorCode null if valid.</returns>
        public static (bool isValid, string? error, int? code) ValidatePayload(
            JObject? data,
            bool isRequest)
        {
            if (data == null)
                return (false, "Payload must not be null", ErrorCodeMap["InvalidRequest"]);

            if (isRequest)
                return ValidateRequestPayload(data);
            else
                return ValidateResponsePayload(data);
        }

        /// <summary>
        /// Validates JSON-RPC request payload.
        /// </summary>
        private static (bool isValid, string? error, int? code) ValidateRequestPayload(JObject data)
        {
            // method is required
            var methodToken = data["method"];
            if (methodToken == null)
                return (false, "Request method is required", ErrorCodeMap["InvalidRequest"]);

            if (methodToken.Type != JTokenType.String || string.IsNullOrEmpty(methodToken.Value<string>()))
                return (false, "Request method must be a non-empty string", ErrorCodeMap["InvalidRequest"]);

            // params is optional, but if present must be object or array
            var paramsToken = data["params"];
            if (paramsToken != null && paramsToken.Type != JTokenType.Object && paramsToken.Type != JTokenType.Array)
                return (false, $"Request params must be an object or array, got {paramsToken.Type}", 
                    ErrorCodeMap["InvalidParams"]);

            // id is optional (notifications have no id)
            // If present, should be string or number
            var idToken = data["id"];
            if (idToken != null && idToken.Type != JTokenType.String && idToken.Type != JTokenType.Integer)
                return (false, $"Request id must be string or number, got {idToken.Type}",
                    ErrorCodeMap["InvalidRequest"]);

            return (true, null, null);
        }

        /// <summary>
        /// Validates JSON-RPC response payload.
        /// </summary>
        private static (bool isValid, string? error, int? code) ValidateResponsePayload(JObject data)
        {
            var hasResult = data.ContainsKey("result");
            var hasError = data.ContainsKey("error");

            // XOR: exactly one of result or error
            if (!hasResult && !hasError)
                return (false, "Response must have either result or error field", 
                    ErrorCodeMap["InternalError"]);

            if (hasResult && hasError)
                return (false, "Response must have either result or error, not both",
                    ErrorCodeMap["InternalError"]);

            // If error present, validate structure
            if (hasError)
            {
                var errorToken = data["error"];
                if (errorToken == null || errorToken.Type != JTokenType.Object)
                    return (false, "Response error must be an object",
                        ErrorCodeMap["InternalError"]);

                var errorObj = (JObject)errorToken;

                var codeToken = errorObj["code"];
                if (codeToken == null || codeToken.Type != JTokenType.Integer)
                    return (false, "Response error must have numeric code field",
                        ErrorCodeMap["InternalError"]);

                var messageToken = errorObj["message"];
                if (messageToken == null || messageToken.Type != JTokenType.String)
                    return (false, "Response error must have string message field",
                        ErrorCodeMap["InternalError"]);
            }

            return (true, null, null);
        }

        /// <summary>
        /// Builds structured error response following JSON-RPC 2.0 spec.
        ///
        /// Returns Message with messageType='rpc:error', success=false, and
        /// error object in data field.
        /// </summary>
        /// <param name="original">Original invalid message (used for messageId correlation)</param>
        /// <param name="errorCode">JSON-RPC error code</param>
        /// <param name="errorMessage">Error description</param>
        /// <returns>Error response Message</returns>
        public static Message BuildErrorResponse(
            Message original,
            int errorCode,
            string errorMessage)
        {
            var errorData = new JObject
            {
                ["error"] = new JObject
                {
                    ["code"] = errorCode,
                    ["message"] = errorMessage ?? "Unknown error",
                },
                ["originalMessage"] = JObject.FromObject(original),
            };

            return new Message
            {
                MessageType = "rpc:error",
                MessageId = original.MessageId,
                Data = errorData,
            };
        }

        /// <summary>
        /// Gets a JSON-RPC error code by name.
        /// </summary>
        /// <param name="errorName">Error code name (e.g., "InvalidRequest")</param>
        /// <param name="defaultCode">Default code if name not found</param>
        /// <returns>Error code (negative integer)</returns>
        public static int GetErrorCode(string errorName, int defaultCode = -32603)
        {
            return ErrorCodeMap.TryGetValue(errorName, out var code) ? code : defaultCode;
        }
    }
}
