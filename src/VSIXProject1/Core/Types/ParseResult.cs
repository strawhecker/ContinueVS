#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the outcome of a stack trace parsing operation.
    /// </summary>
    public class ParseResult
    {
        /// <summary>
        /// Array of successfully parsed stack frames.
        /// </summary>
        [JsonProperty("frames")]
        public StackTraceFrame[] Frames { get; set; } = Array.Empty<StackTraceFrame>();

        /// <summary>
        /// Array of errors encountered during parsing.
        /// </summary>
        [JsonProperty("errors")]
        public ParseError[] Errors { get; set; } = Array.Empty<ParseError>();

        /// <summary>
        /// Name of the parser that successfully parsed this trace (if any).
        /// </summary>
        [JsonProperty("successfulParserName")]
        public string? SuccessfulParserName { get; set; }

        /// <summary>
        /// Diagnostic message with details about the parse operation.
        /// </summary>
        [JsonProperty("diagnosticsMessage")]
        public string? DiagnosticsMessage { get; set; }

        /// <summary>
        /// Indicates if parsing was fully successful (at least one frame parsed).
        /// </summary>
        [JsonIgnore]
        public bool IsSuccessful => Frames.Length > 0;

        /// <summary>
        /// Indicates if parsing had partial success (some frames parsed, but also errors).
        /// </summary>
        [JsonIgnore]
        public bool IsPartialSuccess => IsSuccessful && Errors.Length > 0;

        /// <summary>
        /// Gets the frame at the specified index, or null if out of bounds.
        /// </summary>
        public StackTraceFrame? GetFrameAt(int index)
        {
            if (index >= 0 && index < Frames.Length)
                return Frames[index];
            return null;
        }

        /// <summary>
        /// Gets all errors with a specific severity.
        /// </summary>
        public IEnumerable<ParseError> GetErrorsBySeverity(ParseErrorSeverity severity)
        {
            return Errors.Where(e => e.Severity == severity);
        }
    }
}
