#nullable enable

using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a single frame in a stack trace.
    /// </summary>
    public class StackTraceFrame
    {
        /// <summary>
        /// Index of this frame in the stack (0 = topmost frame).
        /// </summary>
        [JsonProperty("frameIndex")]
        public int FrameIndex { get; set; }

        /// <summary>
        /// Full file path of the frame source file.
        /// </summary>
        [JsonProperty("filePath")]
        public string? FilePath { get; set; }

        /// <summary>
        /// Method or function name.
        /// </summary>
        [JsonProperty("methodName")]
        public string? MethodName { get; set; }

        /// <summary>
        /// Line number in the source file (1-based).
        /// </summary>
        [JsonProperty("lineNumber")]
        public int LineNumber { get; set; }

        /// <summary>
        /// Column number in the source file (1-based, optional).
        /// </summary>
        [JsonProperty("columnNumber")]
        public int? ColumnNumber { get; set; }

        /// <summary>
        /// Exception type (e.g., "System.NullReferenceException").
        /// </summary>
        [JsonProperty("exceptionType")]
        public string? ExceptionType { get; set; }

        /// <summary>
        /// Exception message.
        /// </summary>
        [JsonProperty("exceptionMessage")]
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// The original source line from the stack trace.
        /// </summary>
        [JsonProperty("sourceLineContent")]
        public string? SourceLineContent { get; set; }

        /// <summary>
        /// Timestamp when this frame was extracted.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
