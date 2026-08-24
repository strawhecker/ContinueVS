using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a fingerprint of an error based on exception type and stack trace frames.
    /// Used for identifying and deduplicating recurring errors.
    /// </summary>
    public class ErrorFingerprint
    {
        /// <summary>
        /// The SHA256 hash fingerprint of the error.
        /// </summary>
        [JsonProperty("fingerprint")]
        public string Fingerprint { get; }

        /// <summary>
        /// The exception type (e.g., "System.NullReferenceException").
        /// </summary>
        [JsonProperty("exceptionType")]
        public string ExceptionType { get; }

        /// <summary>
        /// Summaries of the top 3 stack frames (method name + file path pairs).
        /// </summary>
        [JsonProperty("topFrameSummaries")]
        public string[] TopFrameSummaries { get; }

        /// <summary>
        /// Timestamp when this fingerprint was created.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; }

        /// <summary>
        /// Initializes a new instance of the ErrorFingerprint class.
        /// </summary>
        /// <param name="fingerprint">The SHA256 hash fingerprint.</param>
        /// <param name="exceptionType">The exception type.</param>
        /// <param name="topFrameSummaries">Array of top 3 frame summaries (method + file).</param>
        public ErrorFingerprint(string fingerprint, string exceptionType, string[] topFrameSummaries)
        {
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            ExceptionType = exceptionType ?? string.Empty;
            TopFrameSummaries = topFrameSummaries ?? Array.Empty<string>();
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Returns a string representation of the fingerprint.
        /// </summary>
        public override string ToString()
        {
            return $"Fingerprint: {Fingerprint.Substring(0, Math.Min(8, Fingerprint.Length))}... | Type: {ExceptionType}";
        }
    }
}
