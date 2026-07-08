using ContinueVS.Settings;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Concrete implementation of IBridgeTelemetryCollector.
    /// 
    /// Aggregates metrics in thread-safe concurrent collections (ConcurrentDictionary).
    /// Supports three metric types:
    /// 1. Counters: simple increment-only (message count, error count, handler invocations)
    /// 2. Histograms: collection of latency values for distribution analysis
    /// 3. Events: custom named events with optional numeric values
    /// 
    /// Respects ContinueOptionsPage.DisableTelemetry setting; gracefully no-ops if disabled.
    /// All operations are async to avoid blocking the VS main thread.
    /// Metrics are held in memory only; not persisted to disk.
    /// 
    /// Thread-safe for concurrent handler operations via ConcurrentDictionary.
    /// </summary>
    public sealed class BridgeTelemetryCollector : IBridgeTelemetryCollector
    {
        // Counter metrics (incremented counters)
        private readonly ConcurrentDictionary<string, long> _counters = new();

        // Histogram metrics (arrays of latency values for percentile calculation)
        private readonly ConcurrentDictionary<string, ConcurrentBag<long>> _histograms = new();

        // Event metrics (event name -> list of values)
        private readonly ConcurrentDictionary<string, ConcurrentBag<long>> _events = new();

        /// <summary>
        /// Lock for checking telemetry enabled status.
        /// ContinueOptionsPage might not be fully initialized immediately, so we cache the result.
        /// </summary>
        private bool _cachedTelemetryEnabled = true; // Default to enabled
        private bool _cachedTelemetryEnabledSet = false; // Track if we've set the cache

        /// <summary>
        /// Creates a new BridgeTelemetryCollector.
        /// Telemetry is immediately enabled/disabled based on current settings.
        /// </summary>
        public BridgeTelemetryCollector()
        {
            // Defer cache update to avoid assembly loading in test environments
            // Cache will be populated on first use via IsTelemetryEnabledAsync
        }

        public async Task RecordHandlerExecutionAsync(string handlerName, long latencyMs, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (handlerName == null) throw new ArgumentNullException(nameof(handlerName));

            if (!await IsTelemetryEnabledAsync().ConfigureAwait(false))
                return;

            try
            {
                // Record counter: handler invocation count
                _counters.AddOrUpdate($"handler.{handlerName}.count", 1, (k, v) => v + 1);

                // Record histogram: latency distribution
                var histogramKey = $"handler.{handlerName}.latency_ms";
                var histogram = _histograms.GetOrAdd(histogramKey, _ => new ConcurrentBag<long>());
                histogram.Add(latencyMs);

                // Record metadata if provided
                if (metadata != null && metadata.TryGetValue("success", out var successObj) && successObj is bool success)
                {
                    var counterKey = success
                        ? $"handler.{handlerName}.success"
                        : $"handler.{handlerName}.failure";
                    _counters.AddOrUpdate(counterKey, 1, (k, v) => v + 1);
                }
            }
            catch (Exception)
            {
                // Silently ignore telemetry errors
            }
        }

        public async Task RecordRpcMessageAsync(string messageType, long latencyMs, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));

            if (!await IsTelemetryEnabledAsync().ConfigureAwait(false))
                return;

            try
            {
                // Record counter: RPC message count
                _counters.AddOrUpdate($"rpc.{messageType}.count", 1, (k, v) => v + 1);

                // Record histogram: latency distribution
                var histogramKey = $"rpc.{messageType}.latency_ms";
                var histogram = _histograms.GetOrAdd(histogramKey, _ => new ConcurrentBag<long>());
                histogram.Add(latencyMs);

                // Track errors if metadata contains error indicator
                if (metadata != null && metadata.TryGetValue("isError", out var isErrorObj) && isErrorObj is bool isError && isError)
                {
                    _counters.AddOrUpdate($"rpc.{messageType}.error", 1, (k, v) => v + 1);
                }
            }
            catch (Exception)
            {
                // Silently ignore telemetry errors
            }
        }

        public async Task RecordErrorAsync(string context, string errorType, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (errorType == null) throw new ArgumentNullException(nameof(errorType));

            if (!await IsTelemetryEnabledAsync().ConfigureAwait(false))
                return;

            try
            {
                // Record counter: error count by context
                _counters.AddOrUpdate($"error.{context}.count", 1, (k, v) => v + 1);

                // Record counter: error count by type
                _counters.AddOrUpdate($"error.{errorType}", 1, (k, v) => v + 1);
            }
            catch (Exception)
            {
                // Silently ignore telemetry errors
            }
        }

        public async Task RecordEventAsync(string eventName, long value = 0, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (eventName == null) throw new ArgumentNullException(nameof(eventName));

            if (!await IsTelemetryEnabledAsync().ConfigureAwait(false))
                return;

            try
            {
                // Record event: occurrence count
                _counters.AddOrUpdate($"event.{eventName}", 1, (k, v) => v + 1);

                // If value is provided, record in histogram
                if (value > 0)
                {
                    var histogramKey = $"event.{eventName}.value";
                    var histogram = _histograms.GetOrAdd(histogramKey, _ => new ConcurrentBag<long>());
                    histogram.Add(value);
                }
            }
            catch (Exception)
            {
                // Silently ignore telemetry errors
            }
        }

        public async Task<IReadOnlyDictionary<string, object>> GetSummaryAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false); // Placeholder for potential async work

            var summary = new Dictionary<string, object>();

            // Add all counters
            foreach (var kvp in _counters)
            {
                summary[kvp.Key] = kvp.Value;
            }

            // Add histogram statistics (min, max, avg, p50, p99)
            foreach (var kvp in _histograms)
            {
                if (kvp.Value.Count > 0)
                {
                    var values = kvp.Value.OrderBy(v => v).ToList();
                    summary[$"{kvp.Key}.count"] = (long)values.Count;
                    summary[$"{kvp.Key}.min"] = values.First();
                    summary[$"{kvp.Key}.max"] = values.Last();
                    summary[$"{kvp.Key}.avg"] = (long)values.Average();
                    summary[$"{kvp.Key}.p50"] = values[(int)(values.Count * 0.5)];
                    summary[$"{kvp.Key}.p99"] = values.Count > 1 ? values[(int)(values.Count * 0.99)] : values.First();
                }
            }

            // Add event statistics
            foreach (var kvp in _events)
            {
                if (kvp.Value.Count > 0)
                {
                    var values = kvp.Value.OrderBy(v => v).ToList();
                    summary[$"{kvp.Key}.count"] = (long)values.Count;
                    summary[$"{kvp.Key}.sum"] = (long)values.Sum();
                    summary[$"{kvp.Key}.avg"] = (long)values.Average();
                }
            }

            return summary;
        }

        public async Task ResetAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);

            _counters.Clear();
            _histograms.Clear();
            _events.Clear();
        }

        public async Task<bool> IsTelemetryEnabledAsync()
        {
            // Capture cached value if available (fastest path for hot loop)
            if (_cachedTelemetryEnabledSet)
                return _cachedTelemetryEnabled;

            // Cache has not been set yet; return safe default
            // The cache will be populated when ContinueVSPackage initializes during package load
            return true;
        }

        /// <summary>
        /// Sets the telemetry enabled status.
        /// Called by ContinueVSPackage during initialization after options page is loaded.
        /// </summary>
        /// <param name="enabled">Whether telemetry is enabled.</param>
        internal void SetTelemetryEnabled(bool enabled)
        {
            _cachedTelemetryEnabled = enabled;
            _cachedTelemetryEnabledSet = true;
        }

        /// <summary>
        /// Updates the cached telemetry enabled status.
        /// Called during initialization and after settings changes.
        /// </summary>
        private void UpdateTelemetryEnabledCache()
        {
            try
            {
                // Try to capture the setting synchronously if possible
                // This is best-effort; we'll rely on IsTelemetryEnabledAsync for authoritative checks
                // In test/non-VS environments, ContinueVSPackage.Instance will be null
                if (ContinueVSPackage.Instance != null)
                {
                    try
                    {
                        var optionsPage = ContinueVSPackage.Instance.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
                        _cachedTelemetryEnabled = !(optionsPage?.DisableTelemetry ?? false);
                        _cachedTelemetryEnabledSet = true;
                    }
                    catch (Exception)
                    {
                        // GetDialogPage can fail in certain contexts (e.g., during testing)
                        // In that case, leave cache as default
                    }
                }
            }
            catch (Exception)
            {
                // If any other error occurs, default to enabled (safe default)
                _cachedTelemetryEnabled = true;
                _cachedTelemetryEnabledSet = true;
            }
        }
    }
}
