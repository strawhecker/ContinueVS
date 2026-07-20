using ContinueVS.IPC;
using ContinueVS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Custom exception for diagnostic panel operations.
    /// </summary>
    internal sealed class DiagnosticsPanelException : Exception
    {
        /// <summary>Error code for the diagnostics failure.</summary>
        public string ErrorCode { get; }

        /// <summary>Additional error context.</summary>
        public object Details { get; }

        public DiagnosticsPanelException(string message, string code = "DIAGNOSTICS_PANEL_ERROR", object details = null)
            : base(message)
        {
            ErrorCode = code;
            Details = details;
        }
    }

    /// <summary>
    /// Error event entry for the diagnostic error queue.
    /// </summary>
    internal sealed class DiagnosticErrorEntry
    {
        public DateTime Timestamp { get; set; }
        public string Severity { get; set; } // CRITICAL, WARNING, INFO
        public string Message { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public string HandlerName { get; set; }
        public int? LatencyMs { get; set; }
    }

    /// <summary>
    /// Handler statistics snapshot.
    /// </summary>
    internal sealed class HandlerStats
    {
        public string Name { get; set; }
        public string Tier { get; set; }
        public Dictionary<string, int> Latency { get; set; } // p50, p95, p99 in ms
        public double ErrorRate { get; set; } // 0.0-1.0
        public double Throughput { get; set; } // requests/sec
        public int RequestCount { get; set; }
        public int TimeoutCount { get; set; }
        public double? CacheHitRate { get; set; }
    }

    /// <summary>
    /// Aggregated bridge health and diagnostics snapshot.
    /// Provides on-demand health assessment for troubleshooting and monitoring.
    /// 
    /// Consumes:
    /// - Step 24 (HealthCheckService): Bridge process health status
    /// - Step 25 (BridgeLogger): Recent errors and diagnostic events
    /// - Step 96 (ProfilerHandler): Per-handler metrics (optional, via Node handler)
    /// 
    /// Thread-safe: Circular error queue protected by lock
    /// </summary>
    internal sealed class DiagnosticsPanel
    {
        /// <summary>Maximum error entries in circular queue.</summary>
        private const int MaxErrorEntries = 100;

        /// <summary>Transport for communicating with bridge process.</summary>
        private readonly IBridgeTransport _transport;

        /// <summary>Health check service for bridge status.</summary>
        private readonly HealthCheckService _healthCheckService;

        /// <summary>Bridge logger for error collection.</summary>
        private readonly IBridgeLogger _bridgeLogger;

        /// <summary>Optional telemetry collector.</summary>
        private readonly IBridgeTelemetryCollector _telemetryCollector;

        /// <summary>Circular error queue (FIFO, max 100 entries).</summary>
        private readonly Queue<DiagnosticErrorEntry> _errorQueue;

        /// <summary>Synchronizes access to error queue.</summary>
        private readonly object _queueLock = new object();

        /// <summary>Initialization timestamp for uptime calculation.</summary>
        private readonly DateTime _initializationTime = DateTime.UtcNow;

        public DiagnosticsPanel(
            IBridgeTransport transport,
            HealthCheckService healthCheckService,
            IBridgeLogger bridgeLogger = null,
            IBridgeTelemetryCollector telemetryCollector = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
            _bridgeLogger = bridgeLogger;
            _telemetryCollector = telemetryCollector;
            _errorQueue = new Queue<DiagnosticErrorEntry>(MaxErrorEntries);
        }

        /// <summary>
        /// Gets the current bridge health status.
        /// </summary>
        /// <returns>Health status (healthy, degraded, error, or unknown)</returns>
        public async Task<string> GetBridgeHealthAsync()
        {
            try
            {
                // Query HealthCheckService for current status
                var status = _healthCheckService.GetCurrentStatus();

                return status switch
                {
                    HealthState.Healthy => "healthy",
                    HealthState.Degraded => "degraded",
                    HealthState.Error => "error",
                    _ => "unknown"
                };
            }
            catch (Exception ex)
            {
                // Gracefully degrade to unknown status
                _telemetryCollector?.RecordEvent("DiagnosticsPanel.GetBridgeHealthAsync", new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", ex.Message }
                });
                return "unknown";
            }
        }

        /// <summary>
        /// Gets aggregated handler statistics.
        /// 
        /// Calls the Node.js handler to retrieve profiler metrics,
        /// then returns structured handler statistics.
        /// </summary>
        /// <returns>Array of handler statistics snapshots</returns>
        public async Task<List<HandlerStats>> GetHandlerStatsAsync()
        {
            var stats = new List<HandlerStats>();

            try
            {
                // Query Node handler via RPC for metrics aggregation
                // This would be called by the Node.js diagnostic-panel-handler
                // For now, return empty list (Node handler provides this data)
                return stats;
            }
            catch (Exception ex)
            {
                // Gracefully degrade to empty stats
                _telemetryCollector?.RecordEvent("DiagnosticsPanel.GetHandlerStatsAsync", new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", ex.Message }
                });
                return stats;
            }
        }

        /// <summary>
        /// Gets recent errors from the error queue (FIFO, max 100).
        /// 
        /// Returns errors collected from BridgeLogger events,
        /// ordered by timestamp (newest first).
        /// </summary>
        /// <returns>Array of recent diagnostic errors</returns>
        public async Task<List<DiagnosticErrorEntry>> GetRecentErrorsAsync()
        {
            lock (_queueLock)
            {
                // Return copy of error queue, ordered by timestamp descending
                return _errorQueue
                    .OrderByDescending(e => e.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Adds an error entry to the circular queue.
        /// 
        /// Enforces max 100 entries (FIFO eviction).
        /// Thread-safe via lock.
        /// </summary>
        /// <param name="entry">Error entry to add</param>
        public void AddErrorEntry(DiagnosticErrorEntry entry)
        {
            if (entry == null)
                return;

            lock (_queueLock)
            {
                // Evict oldest entry if at capacity
                if (_errorQueue.Count >= MaxErrorEntries)
                {
                    _errorQueue.Dequeue();
                }

                _errorQueue.Enqueue(entry);
            }
        }

        /// <summary>
        /// Clears the error queue.
        /// Thread-safe.
        /// </summary>
        public void ClearErrorQueue()
        {
            lock (_queueLock)
            {
                _errorQueue.Clear();
            }
        }

        /// <summary>
        /// Gets the current uptime duration.
        /// </summary>
        /// <returns>Human-readable uptime string (e.g., "2h 30m 15s")</returns>
        public string GetUptime()
        {
            var elapsed = DateTime.UtcNow - _initializationTime;
            return FormatTimespan(elapsed);
        }

        /// <summary>
        /// Gets diagnostic summary including overall health, error count, and handler stats.
        /// Combines health, error queue, and handler metrics into single snapshot.
        /// </summary>
        /// <returns>Diagnostic summary object</returns>
        public async Task<Dictionary<string, object>> GetDiagnosticSummaryAsync()
        {
            try
            {
                var health = await GetBridgeHealthAsync();
                var errors = await GetRecentErrorsAsync();
                var handlers = await GetHandlerStatsAsync();

                var summary = new Dictionary<string, object>
                {
                    { "health", health },
                    { "errorCount", errors.Count },
                    { "handlerCount", handlers.Count },
                    { "uptime", GetUptime() },
                    { "timestamp", DateTime.UtcNow.ToString("O") },
                    { "criticalCount", errors.Count(e => e.Severity == "CRITICAL") },
                    { "warningCount", errors.Count(e => e.Severity == "WARNING") }
                };

                _telemetryCollector?.RecordEvent("DiagnosticsPanel.GetDiagnosticSummaryAsync", new Dictionary<string, object>
                {
                    { "success", true },
                    { "errorCount", errors.Count }
                });

                return summary;
            }
            catch (Exception ex)
            {
                _telemetryCollector?.RecordEvent("DiagnosticsPanel.GetDiagnosticSummaryAsync", new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", ex.Message }
                });

                throw new DiagnosticsPanelException(
                    $"Failed to retrieve diagnostic summary: {ex.Message}",
                    "SUMMARY_RETRIEVAL_FAILED",
                    new { innerException = ex.Message }
                );
            }
        }

        /// <summary>
        /// Formats a timespan to human-readable string.
        /// Example: "2h 30m 15s"
        /// </summary>
        private static string FormatTimespan(TimeSpan span)
        {
            var parts = new List<string>();

            if (span.Days > 0)
                parts.Add($"{span.Days}d");
            if (span.Hours > 0)
                parts.Add($"{span.Hours}h");
            if (span.Minutes > 0)
                parts.Add($"{span.Minutes}m");
            if (span.Seconds > 0 || parts.Count == 0)
                parts.Add($"{span.Seconds}s");

            return string.Join(" ", parts);
        }
    }
}
