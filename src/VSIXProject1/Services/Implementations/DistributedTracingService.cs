using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Thread-safe implementation of IDistributedTracingService.
    /// 
    /// Parses W3C Trace Context (RFC 9411) and OpenTelemetry trace headers.
    /// Maintains trace context flow across async/await boundaries using AsyncLocal.
    /// 
    /// Supports formats:
    /// - W3C: "00-{32 hex trace-id}-{16 hex span-id}-{2 hex flags}"
    /// - OpenTelemetry: "{trace-id}-{span-id}" or "{trace-id}-{span-id}-{flags}"
    /// </summary>
    public sealed class DistributedTracingService : IDistributedTracingService
    {
        // W3C Trace Context format: 00-{32 hex}-{16 hex}-{2 hex}
        private static readonly Regex W3CTraceContextPattern = new Regex(
            @"^00-([a-f0-9]{32})-([a-f0-9]{16})-([0-9a-f]{2})$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // AsyncLocal flow-safe storage for current trace context
        private readonly AsyncLocal<TraceContext?> _currentContext = new AsyncLocal<TraceContext?>();

        /// <summary>
        /// Parses a distributed trace ID from a header string.
        /// Attempts W3C format first, then OpenTelemetry format.
        /// </summary>
        public Task<TraceParseResult> ParseTraceIdAsync(string? headerValue)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                return Task.FromResult(
                    TraceParseResult.CreateFailure("Trace header is null or empty")
                );
            }

            // Try W3C format first
            var w3cMatch = W3CTraceContextPattern.Match(headerValue);
            if (w3cMatch.Success)
            {
                var traceId = w3cMatch.Groups[1].Value;
                var spanId = w3cMatch.Groups[2].Value;
                var flags = w3cMatch.Groups[3].Value;

                var context = new TraceContext(
                    traceId: traceId,
                    spanId: spanId,
                    parentSpanId: null,
                    isValid: true,
                    format: "W3C"
                );

                _ = LoggerService.Current.WriteDebugAsync($"[TRACING] Parsed W3C trace context: {context}");
                return Task.FromResult(TraceParseResult.CreateSuccess(context));
            }

            // Try OpenTelemetry format: trace-id-span-id or trace-id-span-id-flags
            var parts = headerValue?.Split('-');
            if (parts?.Length >= 2)
            {
                try
                {
                    var traceId = parts[0];
                    var spanId = parts[1];
                    var flags = parts.Length > 2 ? parts[2] : null;

                    // Basic validation: trace-id and span-id should not be empty
                    if (!string.IsNullOrWhiteSpace(traceId) && !string.IsNullOrWhiteSpace(spanId))
                    {
                        var context = new TraceContext(
                            traceId: traceId,
                            spanId: spanId,
                            parentSpanId: null,
                            isValid: true,
                            format: "OpenTelemetry"
                        );

                        _ = LoggerService.Current.WriteDebugAsync($"[TRACING] Parsed OpenTelemetry trace context: {context}");
                        return Task.FromResult(TraceParseResult.CreateSuccess(context));
                    }
                }
                catch (Exception ex)
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[TRACING] OpenTelemetry format parse failed: {ex.Message}");
                }
            }

            // Both parse attempts failed
            var errors = new List<string>
            {
                "Failed to parse trace header",
                "Expected W3C format: 00-{32 hex trace-id}-{16 hex span-id}-{2 hex flags}",
                "Or OpenTelemetry format: {trace-id}-{span-id}[-{flags}]"
            };

            return Task.FromResult(TraceParseResult.CreateFailure(errors.ToArray()));
        }

        /// <summary>
        /// Records a distributed tracing event (stub implementation).
        /// Logs to Debug output; future work will integrate DiagnosticSource.
        /// </summary>
        public Task RecordDistributedEventAsync(string traceId, string spanId, string? parentSpanId, string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentNullException(nameof(eventName));

            var parentInfo = string.IsNullOrWhiteSpace(parentSpanId) ? "(root)" : parentSpanId;
            _ = LoggerService.Current.WriteDebugAsync($"[DISTRIBUTED_TRACE] {traceId} | {spanId} | {parentInfo} | {eventName}");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets the current trace context for this async execution context.
        /// Uses AsyncLocal flow-safe storage.
        /// </summary>
        public void SetCurrentTraceContext(TraceContext? context)
        {
            _currentContext.Value = context;
            if (context != null)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[TRACING] Set current context: {context}");
            }
        }

        /// <summary>
        /// Gets the current trace context for this async execution context.
        /// Returns null if not set in current or parent async context.
        /// </summary>
        public TraceContext? GetCurrentTraceContext()
        {
            return _currentContext.Value;
        }
    }
}
