using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Telemetry collector for bridge operations.
    /// 
    /// Aggregates metrics from bridge operations including:
    /// - Handler execution timing (latency histogram)
    /// - RPC message counts and timing
    /// - Error counts and types
    /// - Message processing throughput
    /// 
    /// All operations respect the user's telemetry opt-out preference via ContinueOptionsPage.DisableTelemetry.
    /// When telemetry is disabled, all Record* methods gracefully no-op without throwing.
    /// 
    /// Thread-safe for concurrent handler operations.
    /// Uses async/await to avoid blocking the VS main thread during metric recording.
    /// Metric data is aggregated in-memory and can be retrieved via GetSummaryAsync() for diagnostics.
    /// 
    /// Integration:
    /// - Initialized: ContinueVSPackage.InitializeAsync() (after BridgeLogger)
    /// - Consumed by: BridgeLifecycleManager (Step 45), metrics dashboard (Steps 101-109)
    /// - Publishes: No events; purely a data collector
    /// - References: ContinueOptionsPage for DisableTelemetry setting
    /// </summary>
    public interface IBridgeTelemetryCollector
    {
        /// <summary>
        /// Records execution timing for a handler invocation.
        /// </summary>
        /// <param name="handlerName">The name of the handler (e.g., "GetEditorState", "FindReferences").</param>
        /// <param name="latencyMs">The execution time in milliseconds.</param>
        /// <param name="metadata">Optional metadata (e.g., requestId, userId, success/failure).</param>
        /// <returns>A task representing the async recording operation.</returns>
        Task RecordHandlerExecutionAsync(string handlerName, long latencyMs, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Records an RPC message transmission and its response latency.
        /// </summary>
        /// <param name="messageType">The type of RPC message (e.g., "onMessage", "onRequest").</param>
        /// <param name="latencyMs">Round-trip latency in milliseconds.</param>
        /// <param name="metadata">Optional metadata (e.g., messageSize, isError, errorCode).</param>
        /// <returns>A task representing the async recording operation.</returns>
        Task RecordRpcMessageAsync(string messageType, long latencyMs, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Records an error or exception during bridge operations.
        /// </summary>
        /// <param name="context">Human-readable context (e.g., "StdioTransport.SendMessage", "HealthCheck.Ping").</param>
        /// <param name="errorType">The exception type name (e.g., "TimeoutException", "InvalidOperationException").</param>
        /// <param name="metadata">Optional metadata (e.g., stackTrace, userId, retryCount).</param>
        /// <returns>A task representing the async recording operation.</returns>
        Task RecordErrorAsync(string context, string errorType, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Records a custom event with optional numeric value and metadata.
        /// Used for feature tracking, diagnostic markers, and business metrics.
        /// </summary>
        /// <param name="eventName">The name of the event (e.g., "BridgeStarted", "VersionDowngraded", "TelemetryDisabled").</param>
        /// <param name="value">Optional numeric value (e.g., duration, count, version number).</param>
        /// <param name="metadata">Optional metadata.</param>
        /// <returns>A task representing the async recording operation.</returns>
        Task RecordEventAsync(string eventName, long value = 0, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Retrieves a snapshot summary of all collected metrics.
        /// Used for diagnostics, testing, and dashboards.
        /// </summary>
        /// <returns>A read-only dictionary with metric keys and aggregated values. Empty if telemetry is disabled.</returns>
        Task<IReadOnlyDictionary<string, object>> GetSummaryAsync();

        /// <summary>
        /// Resets all collected metrics to zero.
        /// Called during tests or to flush old data before a new session.
        /// </summary>
        /// <returns>A task representing the async reset operation.</returns>
        Task ResetAsync();

        /// <summary>
        /// Returns whether telemetry collection is currently enabled.
        /// </summary>
        /// <returns>True if telemetry is enabled; false if disabled via settings or unavailable.</returns>
        Task<bool> IsTelemetryEnabledAsync();
    }
}
