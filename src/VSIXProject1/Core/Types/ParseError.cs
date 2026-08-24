#nullable enable

using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Severity level for a parse error.
    /// </summary>
    public enum ParseErrorSeverity
    {
        /// <summary>
        /// Warning: parsing continued but may have skipped content.
        /// </summary>
        Warning,

        /// <summary>
        /// Error: parser failed completely on this input.
        /// </summary>
        Error
    }

    /// <summary>
    /// Represents an error encountered during stack trace parsing.
    /// </summary>
    public class ParseError
    {
        /// <summary>
        /// Name of the parser that encountered this error.
        /// </summary>
        [JsonProperty("parserName")]
        public string? ParserName { get; set; }

        /// <summary>
        /// Descriptive error message.
        /// </summary>
        [JsonProperty("errorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// The content of the line that caused the error (if applicable).
        /// </summary>
        [JsonProperty("lineContent")]
        public string? LineContent { get; set; }

        /// <summary>
        /// Severity of the error.
        /// </summary>
        [JsonProperty("severity")]
        public ParseErrorSeverity Severity { get; set; } = ParseErrorSeverity.Error;

        /// <summary>
        /// Optional line number in the input where the error occurred.
        /// </summary>
        [JsonProperty("inputLineNumber")]
        public int? InputLineNumber { get; set; }

        /// <summary>
        /// Timestamp when this error was recorded.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
