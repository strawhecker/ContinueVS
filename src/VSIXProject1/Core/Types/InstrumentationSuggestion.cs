using System;
using ContinueVS.Core.Types;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a suggestion for instrumentation around a failure point.
    /// Contains the exception context, target location, reasoning, and the suggested strategy to apply.
    /// </summary>
    public class InstrumentationSuggestion
    {
        /// <summary>
        /// The exception type that triggered this suggestion (e.g., "System.NullReferenceException").
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// The file path where instrumentation is suggested.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// The line number where the failure occurred or where instrumentation should be inserted.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Human-readable reasoning for this suggestion (e.g., "Similar NullReferenceException found 3 times; null-guard recommended").
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>
        /// The instrumentation strategy to apply (contains code snippets and metadata).
        /// </summary>
        public InstrumentationStrategy? SuggestedStrategy { get; set; }

        /// <summary>
        /// Optional confidence score (0.0 to 1.0) indicating how confident we are in this suggestion.
        /// Based on number of historical matches and LLM confidence.
        /// </summary>
        public double? ConfidenceScore { get; set; }

        /// <summary>
        /// Optional fingerprint of the matching historical error that led to this suggestion.
        /// Useful for tracing back to the original error record.
        /// </summary>
        public string? MatchFingerprint { get; set; }

        /// <summary>
        /// Timestamp (UTC) when this suggestion was generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
