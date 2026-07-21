using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services;

namespace VSIXProject1.Services
{
    /// <summary>
    /// Represents a snapshot of bridge runtime state at a specific point in time.
    /// Used for persistence, diagnostics, and dashboard display.
    /// 
    /// Related: Step 105 (bridge state persistence), Step 101 (metrics dashboard),
    ///          Step 112 (regression suite baseline)
    /// </summary>
    public class BridgeStateSnapshot
    {
        /// <summary>
        /// Timestamp when snapshot was captured (UTC).
        /// </summary>
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current bridge initialization phase.
        /// Values: bootstrap, connected, subscribed, ready, degraded
        /// </summary>
        public string CurrentPhase { get; set; } = "bootstrap";

        /// <summary>
        /// Total number of handlers registered in bridge.
        /// </summary>
        public int HandlerCount { get; set; }

        /// <summary>
        /// Names of currently active handlers.
        /// </summary>
        public List<string> ActiveHandlers { get; set; } = new();

        /// <summary>
        /// Total number of active subscriptions across all handlers.
        /// </summary>
        public int SubscriptionCount { get; set; }

        /// <summary>
        /// Number of pending RPC requests awaiting response.
        /// </summary>
        public int PendingRequestCount { get; set; }

        /// <summary>
        /// How long the bridge has been running (seconds).
        /// </summary>
        public int UptimeSeconds { get; set; }

        /// <summary>
        /// Bridge semantic version (e.g., "2.0.0").
        /// </summary>
        public string BridgeVersion { get; set; } = "2.0.0";

        /// <summary>
        /// Validate snapshot structure.
        /// </summary>
        public bool Validate()
        {
            // All counts must be non-negative
            if (HandlerCount < 0 || SubscriptionCount < 0 || PendingRequestCount < 0 || UptimeSeconds < 0)
                return false;

            // Phase must be valid
            var validPhases = new[] { "bootstrap", "connected", "subscribed", "ready", "degraded" };
            if (string.IsNullOrEmpty(CurrentPhase) || !validPhases.Contains(CurrentPhase))
                return false;

            // Captured timestamp should not be in the future
            if (CapturedAt > DateTime.UtcNow.AddSeconds(1)) // allow 1s clock skew
                return false;

            return true;
        }

        /// <summary>
        /// Convert snapshot to JSON-serializable dictionary.
        /// </summary>
        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>
            {
                { "capturedAt", CapturedAt.ToString("O") },
                { "currentPhase", CurrentPhase },
                { "handlerCount", HandlerCount },
                { "activeHandlers", ActiveHandlers },
                { "subscriptionCount", SubscriptionCount },
                { "pendingRequestCount", PendingRequestCount },
                { "uptimeSeconds", UptimeSeconds },
                { "bridgeVersion", BridgeVersion }
            };
        }
    }

    /// <summary>
    /// Collects current bridge runtime state for persistence and diagnostics.
    /// Captures handler statuses, subscription counts, pending requests, etc.
    /// 
    /// Integration: Called by bridge lifecycle manager (Step 45) on graceful shutdown.
    /// Dependency: Optional IBridgeLogger for error logging.
    /// 
    /// Performance: Snapshot creation should complete in <100ms.
    /// </summary>
    public class BridgeStateCollector
    {
        private readonly IBridgeLogger? _logger;
        private readonly Stopwatch _bridgeStartTime;

        /// <summary>
        /// Initialize state collector with optional logger.
        /// </summary>
        public BridgeStateCollector(IBridgeLogger? logger = null)
        {
            _logger = logger;
            _bridgeStartTime = Stopwatch.StartNew();
        }

        /// <summary>
        /// Create a snapshot of current bridge state.
        /// Gracefully handles null dependencies and concurrent state changes.
        /// </summary>
        public async Task<BridgeStateSnapshot?> CreateSnapshotAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var snapshot = new BridgeStateSnapshot
                {
                    CapturedAt = DateTime.UtcNow,
                    CurrentPhase = GetCurrentPhase(),
                    HandlerCount = GetHandlerCount(),
                    ActiveHandlers = GetActiveHandlers(),
                    SubscriptionCount = GetSubscriptionCount(),
                    PendingRequestCount = GetPendingRequestCount(),
                    UptimeSeconds = (int)_bridgeStartTime.Elapsed.TotalSeconds,
                    BridgeVersion = GetBridgeVersion()
                };

                // Validate before returning
                if (!snapshot.Validate())
                {
                    if (_logger != null)
                    {
                        await _logger.WriteWarningAsync("[BridgeStateCollector] Snapshot validation failed");
                    }
                    return null;
                }

                return snapshot;
            }
            catch (OperationCanceledException)
            {
                if (_logger != null)
                {
                    await _logger.WriteWarningAsync("[BridgeStateCollector] Snapshot creation cancelled");
                }
                throw;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    await _logger.WriteErrorAsync($"[BridgeStateCollector] Failed to create snapshot: {ex.Message}", ex);
                }
                return null;
            }
        }

        /// <summary>
        /// Get current bridge initialization phase.
        /// Defaults to "bootstrap" if phase info unavailable.
        /// </summary>
        private string GetCurrentPhase()
        {
            // TODO: Integrate with BridgeLifecycleManager (Step 45) to get actual phase
            // For now, return bootstrap as default
            return "bootstrap";
        }

        /// <summary>
        /// Get total number of handlers in the registry.
        /// </summary>
        private int GetHandlerCount()
        {
            // TODO: Integrate with Step 66 HandlerRegistry when available
            return 0;
        }

        /// <summary>
        /// Get list of currently active handler names.
        /// </summary>
        private List<string> GetActiveHandlers()
        {
            // TODO: Integrate with Step 66 HandlerRegistry when available
            return new List<string>();
        }

        /// <summary>
        /// Get total count of active subscriptions.
        /// </summary>
        private int GetSubscriptionCount()
        {
            // TODO: Integrate with Step 66 HandlerRegistry when available
            return 0;
        }

        /// <summary>
        /// Get count of pending RPC requests.
        /// </summary>
        private int GetPendingRequestCount()
        {
            // TODO: Integrate with bridge context when available
            return 0;
        }

        /// <summary>
        /// Get bridge version string.
        /// </summary>
        private string GetBridgeVersion()
        {
            return "2.0.0";
        }
    }
}
