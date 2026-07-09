using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Exceptions;
using ContinueVS.Services;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Snapshot of the bridge's current health status and metrics.
    /// Returned by IBridgeLifecycleManager.GetHealthStatus() for health dashboards and status checks.
    /// </summary>
    public sealed class BridgeHealthStatus
    {
        /// <summary>Current bridge lifecycle state.</summary>
        public BridgeLifecycleState LifecycleState { get; }

        /// <summary>Current transport health state.</summary>
        public HealthState TransportHealthState { get; }

        /// <summary>Number of messages currently pending in the queue (due to degradation).</summary>
        public int PendingMessageCount { get; }

        /// <summary>Timestamp of the last successful health check.</summary>
        public DateTime LastHealthCheckTime { get; }

        /// <summary>Count of consecutive health check failures.</summary>
        public int HealthCheckFailureCount { get; }

        /// <summary>99th percentile message latency in milliseconds.</summary>
        public long MessageLatencyP99Ms { get; }

        /// <summary>Average message latency in milliseconds.</summary>
        public long MessageLatencyAvgMs { get; }

        /// <summary>Whether the bridge is ready to accept messages.</summary>
        public bool IsReady => LifecycleState == BridgeLifecycleState.Ready;

        /// <summary>Initializes a new BridgeHealthStatus snapshot.</summary>
        public BridgeHealthStatus(
            BridgeLifecycleState lifecycleState,
            HealthState transportHealthState,
            int pendingMessageCount,
            DateTime lastHealthCheckTime,
            int healthCheckFailureCount,
            long messageLatencyP99Ms,
            long messageLatencyAvgMs)
        {
            LifecycleState = lifecycleState;
            TransportHealthState = transportHealthState;
            PendingMessageCount = pendingMessageCount;
            LastHealthCheckTime = lastHealthCheckTime;
            HealthCheckFailureCount = healthCheckFailureCount;
            MessageLatencyP99Ms = messageLatencyP99Ms;
            MessageLatencyAvgMs = messageLatencyAvgMs;
        }
    }

    /// <summary>
    /// Orchestrates the complete bridge lifecycle: initialization, runtime routing,
    /// health monitoring, error recovery, and graceful shutdown.
    /// 
    /// This class wires together Steps 24 (HealthCheckService), 25 (IBridgeLogger),
    /// 26 (IBridgeTelemetryCollector), and 41 (IBridgeFactory) into a single
    /// composite service for initialization and message routing.
    /// </summary>
    public sealed class BridgeLifecycleManager : IBridgeLifecycleManager
    {
        private const int MaxReconnectAttempts = 3;
        private static readonly int[] ReconnectBackoffMs = { 100, 500, 2000 };
        private const int DrainTimeoutMs = 5000;

        private readonly IBridgeFactory _factory;
        private readonly IBridgeLogger? _logger;
        private readonly IBridgeTelemetryCollector? _telemetry;
        private readonly IHealthCheckService? _healthCheckService;

        private IBridgeTransport? _transport;
        private BridgeLifecycleState _state = BridgeLifecycleState.NotInitialized;
        private int _reconnectAttempts;
        private readonly object _stateLock = new();
        private bool _disposed;

        // Message latency tracking
        private readonly ConcurrentBag<long> _messageLatencies = new();
        private DateTime _lastHealthCheckTime = DateTime.UtcNow;
        private int _healthCheckFailureCount;

        // Pending message queue for degradation scenarios
        private readonly ConcurrentQueue<(Message, TaskCompletionSource<Message>)> _messageQueue = new();

        public event EventHandler<BridgeLifecycleEventArgs>? OnBridgeReady;
        public event EventHandler<BridgeLifecycleEventArgs>? OnBridgeDegraded;
        public event EventHandler<BridgeLifecycleEventArgs>? OnBridgeShutdown;

        /// <summary>Initializes a new BridgeLifecycleManager.</summary>
        public BridgeLifecycleManager(
            IBridgeFactory factory,
            IBridgeLogger? logger = null,
            IBridgeTelemetryCollector? telemetry = null,
            IHealthCheckService? healthCheckService = null)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = factory;
            _logger = logger;
            _telemetry = telemetry;
            _healthCheckService = healthCheckService;
        }

        /// <summary>Initializes the bridge: creates transport, starts health checks, emits OnBridgeReady.</summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                if (_state != BridgeLifecycleState.NotInitialized)
                    throw new BridgeLifecycleException(
                        BridgeLifecycleException.OperationType.Initialization,
                        $"Bridge cannot be initialized from state {_state}");

                _state = BridgeLifecycleState.Initializing;
            }

            try
            {
                // Create transport via factory
                _transport = await _factory.CreateTransportAsync("2.0.0", cancellationToken);

                // Start transport (spawns Continue child process)
                await _transport.StartAsync(cancellationToken);

                // Subscribe to transport events
                _transport.OnTransportError += HandleTransportError;
                _transport.OnTransportDisconnected += HandleTransportDisconnected;

                // Perform initial health check
                if (_healthCheckService != null)
                {
                    await _healthCheckService.PerformHealthCheckAsync(cancellationToken);
                    _lastHealthCheckTime = DateTime.UtcNow;
                    _healthCheckFailureCount = 0;
                }

                lock (_stateLock)
                {
                    _state = BridgeLifecycleState.Ready;
                    _reconnectAttempts = 0;
                }

                await _logger?.WriteInfoAsync("Bridge initialized and ready") ?? Task.CompletedTask;
                _telemetry?.RecordHandlerExecution("bridge:initialize", success: true, latencyMs: 0);

                OnBridgeReady?.Invoke(this, new BridgeLifecycleEventArgs
                {
                    Timestamp = DateTime.UtcNow,
                    State = BridgeLifecycleState.Ready,
                    Reason = "Bridge initialization complete"
                });
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _state = BridgeLifecycleState.NotInitialized;
                }

                _transport?.Dispose();
                _transport = null;

                await _logger?.WriteErrorAsync("Bridge initialization failed", ex) ?? Task.CompletedTask;
                _telemetry?.RecordHandlerExecution("bridge:initialize", success: false, latencyMs: 0);

                throw new BridgeLifecycleException(
                    BridgeLifecycleException.OperationType.Initialization,
                    "Bridge initialization failed",
                    ex);
            }
        }

        /// <summary>Sends a message with automatic retry and health checks.</summary>
        public async Task<Message> SendMessageAsync(Message message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            lock (_stateLock)
            {
                if (_state == BridgeLifecycleState.Shutdown)
                    throw new BridgeLifecycleException(
                        BridgeLifecycleException.OperationType.MessageDispatch,
                        "Cannot send messages after shutdown");

                if (_state == BridgeLifecycleState.NotInitialized || _state == BridgeLifecycleState.Initializing)
                    throw new BridgeLifecycleException(
                        BridgeLifecycleException.OperationType.MessageDispatch,
                        $"Bridge not ready (state: {_state})");
            }

            // Create completion source for response
            var responseCompletion = new TaskCompletionSource<Message>();

            // If degraded, queue the message and attempt reconnection
            if (_state == BridgeLifecycleState.Degraded)
            {
                _messageQueue.Enqueue((message, responseCompletion));

                try
                {
                    await AttemptReconnectAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Dequeue if reconnection failed
                    responseCompletion.TrySetException(ex);
                    throw;
                }

                // If successfully reconnected, send the message
                lock (_stateLock)
                {
                    if (_state == BridgeLifecycleState.Ready)
                    {
                        _messageQueue.TryDequeue(out var pending);
                        // Continue to send it via normal path below
                    }
                    else if (_state == BridgeLifecycleState.Shutdown)
                    {
                        responseCompletion.TrySetException(new BridgeLifecycleException(
                            BridgeLifecycleException.OperationType.MessageDispatch,
                            "Bridge reconnection failed; shutdown initiated"));
                        throw new BridgeLifecycleException(
                            BridgeLifecycleException.OperationType.MessageDispatch,
                            "Bridge reconnection failed; shutdown initiated");
                    }
                }
            }

            // Send message via transport
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await _transport!.SendMessageAsync(message, cancellationToken);
                sw.Stop();

                _messageLatencies.Add(sw.ElapsedMilliseconds);
                responseCompletion.TrySetResult(response);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                _messageLatencies.Add(sw.ElapsedMilliseconds);

                await _logger?.WriteErrorAsync("Message send failed", ex) ?? Task.CompletedTask;
                responseCompletion.TrySetException(ex);

                throw new BridgeLifecycleException(
                    BridgeLifecycleException.OperationType.Transport,
                    "Message send failed",
                    ex);
            }
        }

        /// <summary>Gets current health status snapshot.</summary>
        public BridgeHealthStatus GetHealthStatus()
        {
            lock (_stateLock)
            {
                var latencies = _messageLatencies.ToList();
                var p99 = latencies.Any()
                    ? latencies.OrderBy(x => x).Skip((int)(latencies.Count * 0.99)).FirstOrDefault()
                    : 0;
                var avg = latencies.Any() ? latencies.Average() : 0;

                return new BridgeHealthStatus(
                    lifecycleState: _state,
                    transportHealthState: HealthState.Healthy, // TODO: query from transport
                    pendingMessageCount: _messageQueue.Count,
                    lastHealthCheckTime: _lastHealthCheckTime,
                    healthCheckFailureCount: _healthCheckFailureCount,
                    messageLatencyP99Ms: (long)p99,
                    messageLatencyAvgMs: (long)avg);
            }
        }

        /// <summary>Shuts down the bridge: drains messages, terminates process, releases resources.</summary>
        public async Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            BridgeLifecycleState stateAtShutdown;
            lock (_stateLock)
            {
                stateAtShutdown = _state;
                _state = BridgeLifecycleState.Shutdown;
            }

            try
            {
                // Drain in-flight messages with timeout
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(DrainTimeoutMs);

                    while (_messageQueue.TryDequeue(out var pending))
                    {
                        try
                        {
                            await cts.Token.CanCellationRequested;
                            pending.Item2.TrySetCanceled(cts.Token);
                        }
                        catch { }
                    }
                }

                // Unsubscribe from transport events
                if (_transport != null)
                {
                    _transport.OnTransportError -= HandleTransportError;
                    _transport.OnTransportDisconnected -= HandleTransportDisconnected;
                }

                // Dispose transport (triggers child process termination)
                _transport?.Dispose();

                await _logger?.WriteInfoAsync("Bridge shutdown complete") ?? Task.CompletedTask;
                _telemetry?.RecordHandlerExecution("bridge:shutdown", success: true, latencyMs: 0);

                OnBridgeShutdown?.Invoke(this, new BridgeLifecycleEventArgs
                {
                    Timestamp = DateTime.UtcNow,
                    State = BridgeLifecycleState.Shutdown,
                    Reason = "Graceful shutdown complete"
                });
            }
            catch (Exception ex)
            {
                await _logger?.WriteErrorAsync("Error during shutdown", ex) ?? Task.CompletedTask;
            }
        }

        /// <summary>Attempts to reconnect with exponential backoff and drains pending message queue on success.</summary>
        private async Task AttemptReconnectAsync(CancellationToken cancellationToken)
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                lock (_stateLock)
                {
                    _state = BridgeLifecycleState.Shutdown;
                }

                OnBridgeShutdown?.Invoke(this, new BridgeLifecycleEventArgs
                {
                    Timestamp = DateTime.UtcNow,
                    State = BridgeLifecycleState.Shutdown,
                    Reason = "Reconnection failed after max retries"
                });

                throw new BridgeLifecycleException(
                    BridgeLifecycleException.OperationType.Transport,
                    "Bridge reconnection failed after 3 attempts");
            }

            var backoffMs = ReconnectBackoffMs[_reconnectAttempts];
            _reconnectAttempts++;

            await Task.Delay(backoffMs, cancellationToken);

            try
            {
                // Recreate transport and restart
                _transport?.Dispose();
                _transport = await _factory.CreateTransportAsync("2.0.0", cancellationToken);
                await _transport.StartAsync(cancellationToken);

                // Subscribe to events
                _transport.OnTransportError += HandleTransportError;
                _transport.OnTransportDisconnected += HandleTransportDisconnected;

                lock (_stateLock)
                {
                    _state = BridgeLifecycleState.Ready;
                    _reconnectAttempts = 0;
                }

                OnBridgeReady?.Invoke(this, new BridgeLifecycleEventArgs
                {
                    Timestamp = DateTime.UtcNow,
                    State = BridgeLifecycleState.Ready,
                    Reason = "Bridge reconnected successfully"
                });

                // Drain pending message queue
                await DrainMessageQueueAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await _logger?.WriteWarningAsync($"Reconnection attempt {_reconnectAttempts} failed", null) ?? Task.CompletedTask;

                if (_reconnectAttempts >= MaxReconnectAttempts)
                {
                    lock (_stateLock)
                    {
                        _state = BridgeLifecycleState.Shutdown;
                    }

                    throw new BridgeLifecycleException(
                        BridgeLifecycleException.OperationType.Transport,
                        "Bridge reconnection failed",
                        ex);
                }
            }
        }

        /// <summary>Drains pending messages from the queue and resends them.</summary>
        private async Task DrainMessageQueueAsync(CancellationToken cancellationToken)
        {
            while (_messageQueue.TryDequeue(out var pending))
            {
                try
                {
                    var (message, completion) = pending;
                    var response = await _transport!.SendMessageAsync(message, cancellationToken);
                    completion.TrySetResult(response);
                }
                catch (Exception ex)
                {
                    pending.Item2.TrySetException(ex);
                }
            }
        }

        private void HandleTransportError(object? sender, EventArgs e)
        {
            lock (_stateLock)
            {
                if (_state == BridgeLifecycleState.Ready)
                {
                    _state = BridgeLifecycleState.Degraded;
                }
            }

            OnBridgeDegraded?.Invoke(this, new BridgeLifecycleEventArgs
            {
                Timestamp = DateTime.UtcNow,
                State = BridgeLifecycleState.Degraded,
                Reason = "Transport error detected"
            });
        }

        private void HandleTransportDisconnected(object? sender, EventArgs e)
        {
            lock (_stateLock)
            {
                if (_state != BridgeLifecycleState.Shutdown)
                {
                    _state = BridgeLifecycleState.Degraded;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _transport?.Dispose();
        }
    }
}
