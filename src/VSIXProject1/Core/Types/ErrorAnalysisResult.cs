using System;
using ContinueVS.Core.Enums;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Structured representation of an error encountered during build, test, or execution.
    /// Used as input to FailureAnalyzerService for intelligent refinement.
    /// </summary>
    public class ErrorAnalysisResult
    {
        /// <summary>
        /// The type of error (Compilation, TestFailure, Exception, Unknown).
        /// </summary>
        [JsonProperty("errorType")]
        public ErrorType ErrorType { get; set; }

        /// <summary>
        /// Primary error message or assertion failure text.
        /// </summary>
        [JsonProperty("message")]
        public string? Message { get; set; }

        /// <summary>
        /// Full stack trace if available; null for compilation errors.
        /// </summary>
        [JsonProperty("stackTrace")]
        public string? StackTrace { get; set; }

        /// <summary>
        /// Source file where error occurred (from stack trace or compiler diagnostics).
        /// </summary>
        [JsonProperty("filePath")]
        public string? FilePath { get; set; }

        /// <summary>
        /// Line number in source file where error occurred.
        /// </summary>
        [JsonProperty("lineNumber")]
        public int? LineNumber { get; set; }

        /// <summary>
        /// Error category for grouping (e.g., "NullReferenceException", "CS0103", "AssertionFailure").
        /// </summary>
        [JsonProperty("category")]
        public string? Category { get; set; }

        /// <summary>
        /// Complete raw error output (compiler or test runner output).
        /// </summary>
        [JsonProperty("rawOutput")]
        public string? RawOutput { get; set; }

        /// <summary>
        /// Timestamp when error was captured.
        /// </summary>
        [JsonProperty("capturedAt")]
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        public ErrorAnalysisResult()
        {
        }

        public ErrorAnalysisResult(ErrorType errorType, string message, string? category = null)
        {
            ErrorType = errorType;
            Message = message ?? string.Empty;
            Category = category ?? errorType.ToString();
        }
    }
}
