using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Manages the complete lifecycle of the bridge: initialization, runtime message routing,
    /// health monitoring, and graceful shutdown.
    /// 
    /// This is the primary entry point for bridge integration. It orchestrates:
    /// - IBridgeFactory to create transports
    /// - IBridgeTransport for stdio communication
    /// - IHealthCheckService for bridge readiness monitoring
    /// - IBridgeLogger for diagnostic logging
    /// - IBridgeTelemetryCollector for metrics collection
    /// 
    /// Design:
    /// - Single composite service wiring Steps 24, 25, 26, 41 into coherent initialization
    /// - State machine: NotInitialized → Initializing → Ready → Degraded → Shutdown
    /// - Error recovery: 3-attempt exponential backoff (100ms, 500ms, 2000ms)
    /// - Graceful shutdown: 5-second drain timeout for in-flight messages
    /// - Event-driven: OnBridgeReady, OnBridgeDegraded, OnBridgeShutdown for lifecycle visibility
    /// 
    /// Usage:
    ///   var manager = new BridgeLifecycleManager(factory, logger, telemetry);
    ///   await manager.InitializeAsync(cancellationToken);
    ///   
    ///   var response = await manager.SendMessageAsync(
    ///     new Message { MessageType = "bridge:getEditorState", ... },
    ///     cancellationToken);
    ///   
    ///   var health = manager.GetHealthStatus();
    ///   await manager.ShutdownAsync();
    /// 
    /// Threading:
    /// - All async methods are cancellation-aware
    /// - Safe to call from any thread
    /// - State transitions are thread-safe via internal locking
    /// 
    /// Exceptions:
    /// - Throws BridgeLifecycleException for lifecycle operation failures
    /// - Includes OperationType enum for error context (Initialization, Transport, etc.)
    /// </summary>
    public interface IBridgeLifecycleManager : IDisposable
    {
        /// <summary>
        /// Asynchronously initializes the bridge: creates transport, starts health checks,
        /// warms up the Continue child process, and marks the bridge ready.
        /// 
        /// Steps:
        /// 1. Validate state (must be NotInitialized)
        /// 2. Create IBridgeTransport via factory
        /// 3. Start transport (spawns Continue child process)
        /// 4. Subscribe to transport events (errors, disconnection, etc.)
        /// 5. Perform initial health check
        /// 6. Emit OnBridgeReady event
        /// 
        /// Errors during initialization are wrapped in BridgeLifecycleException with
        /// OperationType.Initialization. Transport is automatically cleaned up on failure.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.
        /// If cancelled, cleanup proceeds and state reverts to NotInitialized.</param>
        /// <returns>Task representing the initialization operation.</returns>
        /// <exception cref="BridgeLifecycleException">Thrown if initialization fails
        /// (invalid state, factory error, transport startup error, health check timeout, etc.)</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled.</exception>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously sends a message through the bridge with automatic retry and health checks.
        /// 
        /// Steps:
        /// 1. Validate state (must be Ready or Degraded; not Shutdown)
        /// 2. Check transport health; if Degraded, attempt reconnection (up to 3 retries with backoff)
        /// 3. Queue message if transport temporarily unavailable
        /// 4. Send message via transport
        /// 5. Await response with correlation ID matching
        /// 6. Return typed response or throw if error response received
        /// 
        /// If transport is degraded and reconnection exhausts retries, throws
        /// BridgeLifecycleException with OperationType.Transport.
        /// 
        /// Message buffering: If transport disconnects mid-operation, message is queued
        /// and automatically resent when transport reconnects.
        /// </summary>
        /// <param name="message">Message to send (must have MessageType and MessageId set).
        /// MessageId is used for response correlation.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.
        /// If cancelled, message is dequeued (if queued) and OperationCanceledException is thrown.</param>
        /// <returns>Task&lt;Message&gt; representing the message send operation.
        /// On completion, contains the response message from the bridge.</returns>
        /// <exception cref="BridgeLifecycleException">Thrown if bridge state is Shutdown,
        /// if transport error occurs after retry exhaustion, or if health check fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled.</exception>
        Task<Message> SendMessageAsync(Message message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronously retrieves the current health status of the bridge.
        /// 
        /// Returns a snapshot of:
        /// - Current lifecycle state (NotInitialized, Initializing, Ready, Degraded, Shutdown)
        /// - Transport health (Healthy, Degraded, Failed)
        /// - Message latency metrics (p99, average)
        /// - Last health check timestamp
        /// - Failure count (for circuit breaker tracking)
        /// - Pending message count (in queue during degradation)
        /// 
        /// This is a fast, non-blocking query suitable for UI updates and health dashboards.
        /// </summary>
        /// <returns>BridgeHealthStatus containing current state and metrics.</returns>
        BridgeHealthStatus GetHealthStatus();

        /// <summary>
        /// Asynchronously shuts down the bridge: drains pending messages, terminates
        /// the child process, and releases all resources.
        /// 
        /// Steps:
        /// 1. Set state to Shutdown
        /// 2. Stop accepting new messages
        /// 3. Drain in-flight messages (with 5-second timeout)
        /// 4. Unsubscribe from transport events
        /// 5. Dispose transport (SIGTERM → wait 3s → SIGKILL)
        /// 6. Emit OnBridgeShutdown event
        /// 
        /// Errors during shutdown are logged but do not throw (graceful degradation).
        /// Messages not drained within 5 seconds are dropped.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.
        /// If cancelled, immediate cleanup proceeds without waiting for message drain.</param>
        /// <returns>Task representing the shutdown operation.</returns>
        Task ShutdownAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Raised when the bridge successfully transitions to Ready state.
        /// 
        /// Fired at the end of InitializeAsync() after transport is active and
        /// health checks pass. Intended for handlers to start publishing updates
        /// and handlers to begin processing requests.
        /// </summary>
        event EventHandler<BridgeLifecycleEventArgs>? OnBridgeReady;

        /// <summary>
        /// Raised when the bridge transitions to Degraded state.
        /// 
        /// Fired when transport becomes unhealthy (health check failures, crashes, etc.)
        /// but recovery is in progress. Intended for UI to show warning status and
        /// for handlers to implement fallback logic.
        /// 
        /// If reconnection succeeds, OnBridgeReady is fired again.
        /// If reconnection fails after max retries, OnBridgeShutdown is fired.
        /// </summary>
        event EventHandler<BridgeLifecycleEventArgs>? OnBridgeDegraded;

        /// <summary>
        /// Raised when the bridge transitions to Shutdown state.
        /// 
        /// Fired at the end of ShutdownAsync() after all cleanup completes,
        /// or when recovery attempts are exhausted and bridge is permanently down.
        /// Intended for handlers to stop processing, emit final telemetry,
        /// and notify the IDE that bridge is unavailable.
        /// </summary>
        event EventHandler<BridgeLifecycleEventArgs>? OnBridgeShutdown;
    }
}
