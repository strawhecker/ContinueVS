using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a persistent error record stored in the error repository.
    /// Contains all metadata needed for querying, grouping, and exporting errors.
    /// </summary>
    public class ErrorRecord
    {
        /// <summary>
        /// Timestamp when the error was recorded (UTC).
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; }

        /// <summary>
        /// The SHA256 fingerprint of the error (from IErrorFingerprintService).
        /// Used for deduplication and grouping.
        /// </summary>
        [JsonProperty("fingerprint")]
        public string Fingerprint { get; }

        /// <summary>
        /// The exception type (e.g., "System.NullReferenceException").
        /// </summary>
        [JsonProperty("exceptionType")]
        public string ExceptionType { get; }

        /// <summary>
        /// The exception message.
        /// </summary>
        [JsonProperty("exceptionMessage")]
        public string ExceptionMessage { get; }

        /// <summary>
        /// The complete stack trace JSON (serialized ParseResult or raw frames).
        /// </summary>
        [JsonProperty("stackTraceJson")]
        public string StackTraceJson { get; }

        /// <summary>
        /// Optional user notes (e.g., context info, reproduction steps).
        /// Sanitized to prevent path traversal attacks.
        /// </summary>
        [JsonProperty("userNotes")]
        public string UserNotes { get; }

        /// <summary>
        /// The session ID in which the error occurred.
        /// Allows grouping errors by session.
        /// </summary>
        [JsonProperty("sessionId")]
        public string SessionId { get; }

        /// <summary>
        /// Initializes a new instance of the ErrorRecord class.
        /// </summary>
        public ErrorRecord(
            string fingerprint,
            string exceptionType,
            string exceptionMessage,
            string stackTraceJson,
            string userNotes = "",
            string sessionId = ""
        )
        {
            Timestamp = DateTime.UtcNow;
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            ExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
            ExceptionMessage = exceptionMessage ?? string.Empty;
            StackTraceJson = stackTraceJson ?? string.Empty;
            UserNotes = userNotes ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the ErrorRecord class with explicit timestamp.
        /// Used for deserialization and testing.
        /// </summary>
        [JsonConstructor]
        public ErrorRecord(
            DateTime timestamp,
            string fingerprint,
            string exceptionType,
            string exceptionMessage,
            string stackTraceJson,
            string userNotes,
            string sessionId
        )
        {
            Timestamp = timestamp;
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            ExceptionType = exceptionType ?? throw new ArgumentNullException(nameof(exceptionType));
            ExceptionMessage = exceptionMessage ?? string.Empty;
            StackTraceJson = stackTraceJson ?? string.Empty;
            UserNotes = userNotes ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
        }
    }
}
