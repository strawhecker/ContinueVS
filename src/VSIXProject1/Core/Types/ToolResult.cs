using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the successful result/response from a tool execution.
    /// Used by IToolService.InvokeAsync() to return structured output.
    /// </summary>
    public class ToolResult
    {
        /// <summary>
        /// Name of the tool that was executed (must match a ToolDefinition.Name).
        /// </summary>
        [JsonProperty("toolName")]
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier for the tool call that produced this result.
        /// Correlates with the ToolCall.Id from the original request.
        /// Used to match results back to their originating invocations in chat/session context.
        /// </summary>
        [JsonProperty("toolCallId")]
        public string? ToolCallId { get; set; }

        /// <summary>
        /// String representation of the tool output/result.
        /// For simple outputs, this is the primary response.
        /// For complex results, this may be a formatted/serialized summary.
        /// </summary>
        [JsonProperty("output")]
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Raw, unformatted output object from the tool execution.
        /// Preserves complex data types (lists, objects, nested structures) that cannot fit in Output string.
        /// Useful for UI rendering or downstream processing requiring structured data.
        /// Null if tool only returns string output.
        /// </summary>
        [JsonProperty("rawOutput")]
        public object? RawOutput { get; set; }

        /// <summary>
        /// Indicates whether the tool execution was successful.
        /// True if tool completed without errors; False indicates a partial/degraded result.
        /// When False, check associated ToolError for details if available.
        /// </summary>
        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// Timestamp when the tool execution completed.
        /// Used for latency tracking, result validation, and debugging.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Duration in milliseconds from tool invocation start to completion.
        /// Useful for performance monitoring and optimization.
        /// </summary>
        [JsonProperty("durationMs")]
        public long? DurationMs { get; set; }

        /// <summary>
        /// Optional metadata key-value pairs providing context about the result.
        /// May include source information, caching status, pagination hints, etc.
        /// </summary>
        [JsonProperty("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }
    }
}
