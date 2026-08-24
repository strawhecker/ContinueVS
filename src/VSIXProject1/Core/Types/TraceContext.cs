using System;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Immutable record representing parsed distributed trace context.
    /// 
    /// Used to hold trace identity components extracted from W3C Trace Context
    /// or OpenTelemetry headers. Flows across async/await boundaries via AsyncLocal.
    /// </summary>
    public sealed class TraceContext
    {
        /// <summary>Gets the trace identifier (128-bit hex string).</summary>
        public string TraceId { get; }

        /// <summary>Gets the span identifier (64-bit hex string).</summary>
        public string SpanId { get; }

        /// <summary>Gets the parent span identifier, if present.</summary>
        public string? ParentSpanId { get; }

        /// <summary>Gets a value indicating whether this context represents a valid parsed trace.</summary>
        public bool IsValid { get; }

        /// <summary>Gets the format this trace was parsed from (e.g., "W3C", "OpenTelemetry").</summary>
        public string Format { get; }

        /// <summary>
        /// Creates a new trace context with the specified components.
        /// </summary>
        /// <param name="traceId">The trace identifier (required, non-empty).</param>
        /// <param name="spanId">The span identifier (required, non-empty).</param>
        /// <param name="parentSpanId">The parent span identifier, if any.</param>
        /// <param name="isValid">Whether this trace context is valid.</param>
        /// <param name="format">The format this trace was parsed from.</param>
        public TraceContext(string traceId, string spanId, string? parentSpanId, bool isValid, string format)
        {
            TraceId = traceId ?? throw new ArgumentNullException(nameof(traceId));
            SpanId = spanId ?? throw new ArgumentNullException(nameof(spanId));
            ParentSpanId = parentSpanId;
            IsValid = isValid;
            Format = format ?? throw new ArgumentNullException(nameof(format));
        }

        /// <summary>
        /// Creates an invalid trace context placeholder.
        /// </summary>
        public static TraceContext CreateInvalid() =>
            new TraceContext(string.Empty, string.Empty, null, isValid: false, format: "Invalid");

        /// <summary>
        /// Returns a string representation of this trace context for debugging.
        /// Format: "TraceId={id} | SpanId={id} | ParentSpanId={id} | Format={format} | Valid={valid}"
        /// </summary>
        public override string ToString() =>
            $"TraceId={TraceId} | SpanId={SpanId} | ParentSpanId={ParentSpanId ?? "(none)"} | Format={Format} | Valid={IsValid}";
    }
}
