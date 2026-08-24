using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for parsing and managing distributed trace context across async/await boundaries.
    /// 
    /// Supports W3C Trace Context (RFC 9411) and OpenTelemetry header formats.
    /// Maintains context flow across async boundaries using AsyncLocal semantics.
    /// 
    /// Formats:
    /// - W3C: "00-{trace-id}-{span-id}-{flags}" (32 hex trace-id, 16 hex span-id, 2 hex flags)
    /// - OpenTelemetry: "{trace-id}-{span-id}" or "{trace-id}-{span-id}-{flags}" (dash-delimited)
    /// </summary>
    public interface IDistributedTracingService
    {
        /// <summary>
        /// Parses a distributed trace ID from a header string.
        /// 
        /// Supports both W3C Trace Context and OpenTelemetry formats.
        /// Returns structured result with parsed components or error details.
        /// 
        /// Null or empty input returns failure with appropriate message.
        /// </summary>
        /// <param name="headerValue">The trace header value to parse (e.g., from traceparent header).</param>
        /// <returns>Parse result with success flag, trace context, and any error messages.</returns>
        Task<TraceParseResult> ParseTraceIdAsync(string? headerValue);

        /// <summary>
        /// Records a distributed tracing event.
        /// 
        /// Stub implementation that logs event to Debug output.
        /// Future work will integrate with System.Diagnostics.DiagnosticSource for full tracing.
        /// </summary>
        /// <param name="traceId">The trace identifier.</param>
        /// <param name="spanId">The span identifier.</param>
        /// <param name="parentSpanId">The parent span identifier, if any.</param>
        /// <param name="eventName">The event name for diagnostic purposes.</param>
        /// <returns>Task that completes when operation finishes.</returns>
        Task RecordDistributedEventAsync(string traceId, string spanId, string? parentSpanId, string eventName);

        /// <summary>
        /// Sets the current trace context for this async execution context.
        /// 
        /// Uses AsyncLocal<T> semantics; child tasks inherit parent context automatically.
        /// </summary>
        /// <param name="context">The trace context to set, or null to clear.</param>
        void SetCurrentTraceContext(TraceContext? context);

        /// <summary>
        /// Gets the current trace context for this async execution context.
        /// 
        /// Returns the value set in the current or parent async context, or null if not set.
        /// </summary>
        /// <returns>The current trace context, or null if not set.</returns>
        TraceContext? GetCurrentTraceContext();
    }
}
