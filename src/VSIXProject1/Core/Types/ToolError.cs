using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an error that occurred during tool execution.
    /// Returned when a tool invocation fails or encounters exceptional conditions.
    /// Part of the error handling contract alongside ToolResult.
    /// </summary>
    public class ToolError
    {
        /// <summary>
        /// Name of the tool where the error occurred (must match a ToolDefinition.Name).
        /// </summary>
        [JsonProperty("toolName")]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for the tool call that resulted in this error.
        /// Correlates with the ToolCall.Id from the original request.
        /// Allows matching errors back to their originating invocations in chat/session context.
        /// </summary>
        [JsonProperty("toolCallId")]
        public string? ToolCallId { get; set; }

        /// <summary>
        /// Human-readable error message describing what went wrong.
        /// Should be brief and actionable; used for logging, UI display, and LLM error understanding.
        /// Example: "File not found: /path/to/file.cs"
        /// </summary>
        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Machine-readable error code or exception type name.
        /// Examples: "FileNotFound", "PermissionDenied", "McpServerError", "HttpTimeout"
        /// Enables programmatic error handling and routing.
        /// </summary>
        [JsonProperty("errorCode")]
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Full stack trace from the exception (if available).
        /// Included only in verbose/debug scenarios; omitted from user-facing messages.
        /// Useful for troubleshooting and incident investigation.
        /// </summary>
        [JsonProperty("stackTrace")]
        public string? StackTrace { get; set; }

        /// <summary>
        /// Indicates whether the tool invocation can be safely retried.
        /// True: Transient error (network glitch, service unavailable, temporary lock)
        /// False: Permanent error (authentication failure, invalid arguments, resource not found)
        /// Used by callers to decide whether to retry or skip retry logic.
        /// </summary>
        [JsonProperty("isRetryable")]
        public bool IsRetryable { get; set; } = false;

        /// <summary>
        /// Suggested maximum number of retry attempts if error is retryable.
        /// Null or 0 means use default retry policy; typically 1-3 for transient errors.
        /// </summary>
        [JsonProperty("maxRetries")]
        public int? MaxRetries { get; set; }

        /// <summary>
        /// Timestamp when the error occurred.
        /// Used for auditing, error tracking, and correlation with logs.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Duration in milliseconds from tool invocation start to error occurrence.
        /// Useful for identifying where failures occur (early vs. late in execution).
        /// </summary>
        [JsonProperty("durationMs")]
        public long? DurationMs { get; set; }

        /// <summary>
        /// Optional context or metadata about the error (e.g., server response, system state).
        /// Provides diagnostic information for debugging and error resolution.
        /// </summary>
        [JsonProperty("context")]
        public Dictionary<string, string>? Context { get; set; }
    }
}
