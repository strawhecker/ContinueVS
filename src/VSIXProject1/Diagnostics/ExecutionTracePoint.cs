using System;
using System.Collections.Generic;

namespace ContinueVS.Diagnostics
{
    /// <summary>
    /// Immutable data structure representing a single execution trace point.
    /// Captures moment-in-time information about a step in the bridge initialization pipeline.
    /// 
    /// Used by ExecutionTracer to log t1, t1.1, t1.2, ..., t45 tokens with timing and context.
    /// </summary>
    public sealed class ExecutionTracePoint
    {
        /// <summary>Step token identifier (e.g., "t1", "t1.1", "t2", ..., "t45").</summary>
        public string Token { get; }

        /// <summary>UTC timestamp when this trace point was recorded (ISO 8601 format).</summary>
        public DateTime Timestamp { get; }

        /// <summary>Component or service name (e.g., "ContinueVSPackage", "BridgeLogger", "WebviewPusher").</summary>
        public string Component { get; }

        /// <summary>
        /// Elapsed milliseconds for this scope.
        /// Null for non-scoped trace points (e.g., single events).
        /// </summary>
        public double? DurationMs { get; }

        /// <summary>Optional metadata dictionary for contextual information (service status, error details, etc.).</summary>
        public IReadOnlyDictionary<string, object>? Metadata { get; }

        /// <summary>
        /// Creates an immutable execution trace point.
        /// </summary>
        /// <param name="token">Step token (e.g., "t1.3.4").</param>
        /// <param name="component">Component name (e.g., "BridgeLogger").</param>
        /// <param name="durationMs">Optional elapsed milliseconds for scoped operations.</param>
        /// <param name="metadata">Optional contextual metadata dictionary.</param>
        public ExecutionTracePoint(
            string token,
            string component,
            double? durationMs = null,
            IReadOnlyDictionary<string, object>? metadata = null)
        {
            // Validate token format (allow t1, t1.1, t1.2, etc., t2, ..., t45)
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or whitespace.", nameof(token));

            if (string.IsNullOrWhiteSpace(component))
                throw new ArgumentException("Component cannot be null or whitespace.", nameof(component));

            Token = token;
            Timestamp = DateTime.UtcNow;
            Component = component;
            DurationMs = durationMs;
            Metadata = metadata;
        }

        /// <summary>Serializes this trace point to JSON format (one-liner, suitable for jsonl output).</summary>
        public override string ToString()
        {
            // Manual JSON construction to avoid dependency on System.Text.Json
            var parts = new List<string>
            {
                $"\"token\":\"{Token}\"",
                $"\"timestamp\":\"{Timestamp:O}\"",
                $"\"component\":\"{Component}\"",
                $"\"duration_ms\":{(DurationMs.HasValue ? DurationMs.Value.ToString("F2") : "null")}"
            };

            // Add metadata if present
            if (Metadata != null && Metadata.Count > 0)
            {
                var metadataParts = new List<string>();
                foreach (var kvp in Metadata)
                {
                    var value = kvp.Value switch
                    {
                        string s => $"\"{s}\"",
                        bool b => b ? "true" : "false",
                        null => "null",
                        _ => kvp.Value.ToString() ?? "null"
                    };
                    metadataParts.Add($"\"{kvp.Key}\":{value}");
                }
                parts.Add($"\"metadata\":{{{string.Join(",", metadataParts)}}}");
            }

            return $"{{{string.Join(",", parts)}}}";
        }
    }
}
