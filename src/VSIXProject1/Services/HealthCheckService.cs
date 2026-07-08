using ContinueVS.Exceptions;
using ContinueVS.IPC;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Monitors the health of the Continue bridge process via periodic ping RPC requests.
    /// 
    /// Features:
    /// - Async-first health check via StdioTransport.SendMessageAsync()
    /// - Circuit breaker pattern: marks process as "degraded" after N consecutive failures
    /// - Event publishing for status transitions (for use by BridgeLifecycleManager at Step 45)
    /// - Configurable timeout, interval, and failure threshold
    /// 
    /// Integration:
    /// - Consumed by: BridgeLifecycleManager (Step 45) for auto-recovery decisions
    /// - Uses: StdioTransport.SendMessageAsync() for ping requests
    /// - Publishes: OnHealthStatusChanged event
    /// - References: HealthStatus, HealthState, HealthCheckException
    /// </summary>
    internal sealed class HealthCheckService
    {
        /// <summary>Default timeout for health check ping requests (in milliseconds).</summary>
        private const int DefaultPingTimeoutMs = 5000;

        /// <summary>Default interval between health checks (in milliseconds).</summary>
        private const int DefaultCheckIntervalMs = 30000; // 30 seconds

        /// <summary>Default failure count threshold for marking process as degraded.</summary>
        private const int DefaultDegradedThreshold = 3;

        /// <summary>Transport layer for communicating with the bridge process.</summary>
        private readonly IBridgeTransport _transport;

        /// <summary>Timeout for individual health check ping requests.</summary>
        private readonly int _pingTimeoutMs;

        /// <summary>Failure count threshold for circuit breaker activation.</summary>
        private readonly int _degradedThreshold;

        /// <summary>Tracks the most recent health check result.</summary>
        private HealthStatus _currentStatus;

        /// <summary>Synchronizes access to _currentStatus and consecutive failure tracking.</summary>
        private readonly object _statusLock = new();

        /// <summary>Raised when health status transitions (e.g., Healthy → Degraded).</summary>
        public event EventHandler<HealthStatusChangedEventArgs>? OnHealthStatusChanged;

        /// <summary>
        /// Initializes a new instance of HealthCheckService.
        /// </summary>
        /// <param name="transport">StdioTransport or mock IBridgeTransport for sending ping requests.</param>
        /// <param name="pingTimeoutMs">Timeout for ping requests (milliseconds). Defaults to 5000.</param>
        /// <param name="degradedThreshold">Consecutive failure count threshold for degraded state. Defaults to 3.</param>
        /// <exception cref="ArgumentNullException">Thrown if transport is null.</exception>
        public HealthCheckService(
            IBridgeTransport transport,
            int pingTimeoutMs = DefaultPingTimeoutMs,
            int degradedThreshold = DefaultDegradedThreshold)
        {
            if (transport == null)
                throw new ArgumentNullException(nameof(transport));
            _transport = transport;
            _pingTimeoutMs = pingTimeoutMs > 0 ? pingTimeoutMs : DefaultPingTimeoutMs;
            _degradedThreshold = degradedThreshold > 0 ? degradedThreshold : DefaultDegradedThreshold;
            _currentStatus = new HealthStatus(HealthState.Healthy, DateTime.UtcNow, responseTimeMs: 0, failureCount: 0);
        }

        /// <summary>
        /// Performs a synchronous health check by sending a ping request to the bridge process.
        /// Implements circuit breaker logic: after N consecutive failures, marks process as degraded.
        /// Throws HealthCheckException on critical failures.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>HealthStatus containing the check result, response time, and failure count.</returns>
        /// <exception cref="HealthCheckException">Thrown if the health check ping fails or times out.</exception>
        public async Task<HealthStatus> PerformHealthCheckAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var checkTime = DateTime.UtcNow;

            try
            {
                // Send ping request with timeout
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(_pingTimeoutMs);

                    // Send ping message to bridge; expect string response
                    // Reference: protocol.md - ping: [string, string]
                    await _transport.SendMessageAsync(
                        new Message
                        {
                            MessageType = "ping",
                            MessageId = Guid.NewGuid().ToString(),
                            Data = JToken.FromObject(DateTime.UtcNow.Ticks.ToString())
                        },
                        cts.Token);
                }

                stopwatch.Stop();
                var responseTimeMs = stopwatch.ElapsedMilliseconds;

                // Update status to Healthy and reset failure count
                lock (_statusLock)
                {
                    var newStatus = HealthStatus.Healthy(checkTime, responseTimeMs);
                    if (_currentStatus.State != HealthState.Healthy)
                    {
                        PublishStatusChanged(_currentStatus, newStatus);
                    }
                    _currentStatus = newStatus;
                    return _currentStatus;
                }
            }
            catch (OperationCanceledException ex)
            {
                stopwatch.Stop();
                var errorMessage = $"Health check ping timed out after {_pingTimeoutMs}ms";
                UpdateFailureAndCheckDegradation(checkTime, errorMessage);
                throw new HealthCheckException(errorMessage, HealthCheckException.ErrorCodes.ProcessNotResponding, ex);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var errorMessage = $"Health check ping failed: {ex.Message}";
                UpdateFailureAndCheckDegradation(checkTime, errorMessage);
                throw new HealthCheckException(errorMessage, HealthCheckException.ErrorCodes.ProbeFailed, ex);
            }
        }

        /// <summary>
        /// Gets the current cached health status without performing a new check.
        /// Safe to call from any thread; does not block on I/O.
        /// </summary>
        /// <returns>The most recent HealthStatus; initially Healthy with zero failures.</returns>
        public HealthStatus GetCurrentStatus()
        {
            lock (_statusLock)
            {
                return _currentStatus;
            }
        }

        /// <summary>
        /// Convenience predicate to check if the process is currently healthy.
        /// </summary>
        /// <returns>True if current status is Healthy; false if Degraded or Failed.</returns>
        public bool IsHealthy()
        {
            lock (_statusLock)
            {
                return _currentStatus.State == HealthState.Healthy;
            }
        }

        /// <summary>
        /// Resets the health status to Healthy with zero failures.
        /// Called by recovery mechanisms (e.g., process restart in BridgeLifecycleManager).
        /// </summary>
        public void Reset()
        {
            lock (_statusLock)
            {
                var newStatus = new HealthStatus(HealthState.Healthy, DateTime.UtcNow, responseTimeMs: 0, failureCount: 0);
                if (_currentStatus.State != HealthState.Healthy)
                {
                    PublishStatusChanged(_currentStatus, newStatus);
                }
                _currentStatus = newStatus;
            }
        }

        /// <summary>
        /// Updates the failure count and checks if the circuit breaker threshold is reached.
        /// If so, transitions status to Degraded and publishes event.
        /// </summary>
        private void UpdateFailureAndCheckDegradation(DateTime checkTime, string errorMessage)
        {
            lock (_statusLock)
            {
                var newFailureCount = _currentStatus.FailureCount + 1;
                HealthStatus newStatus;

                if (newFailureCount >= _degradedThreshold)
                {
                    newStatus = HealthStatus.Degraded(checkTime, newFailureCount, errorMessage);
                }
                else
                {
                    newStatus = HealthStatus.Failed(checkTime, errorMessage, newFailureCount);
                }

                if (_currentStatus.State != newStatus.State)
                {
                    PublishStatusChanged(_currentStatus, newStatus);
                }

                _currentStatus = newStatus;
            }
        }

        /// <summary>
        /// Publishes the OnHealthStatusChanged event for status transitions.
        /// </summary>
        private void PublishStatusChanged(HealthStatus previousStatus, HealthStatus newStatus)
        {
            OnHealthStatusChanged?.Invoke(this, new HealthStatusChangedEventArgs(previousStatus, newStatus));
        }
    }

    /// <summary>
    /// Event arguments for health status change notifications.
    /// </summary>
    public sealed class HealthStatusChangedEventArgs : EventArgs
    {
        /// <summary>The previous health status.</summary>
        public HealthStatus PreviousStatus { get; }

        /// <summary>The new health status.</summary>
        public HealthStatus NewStatus { get; }

        /// <summary>
        /// Initializes event arguments with previous and new status.
        /// </summary>
        public HealthStatusChangedEventArgs(HealthStatus previousStatus, HealthStatus newStatus)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }
    }
}
