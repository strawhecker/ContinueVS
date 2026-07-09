using System;
using System.Collections.Generic;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Health status enumeration for bridge health checks.
    /// </summary>
    internal enum HealthCheckStatus
    {
        /// <summary>Bridge process is responsive and operational.</summary>
        Healthy = 0,

        /// <summary>Bridge process is responding but showing signs of degradation (slow responses, high memory, etc.).</summary>
        Degraded = 1,

        /// <summary>Bridge process is non-responsive or has failed.</summary>
        Failed = 2,
    }

    /// <summary>
    /// Bridge lifecycle state enumeration.
    /// </summary>
    internal enum BridgeState
    {
        /// <summary>Bridge state is unknown (not yet initialized or after unrecoverable failure).</summary>
        Unknown = 0,

        /// <summary>Bridge is initializing (process startup in progress).</summary>
        Initializing = 1,

        /// <summary>Bridge is running and operational.</summary>
        Running = 2,

        /// <summary>Bridge is shutting down (graceful termination in progress).</summary>
        Stopping = 3,

        /// <summary>Bridge is stopped (process terminated).</summary>
        Stopped = 4,

        /// <summary>Bridge is in a failed state (unrecoverable error occurred).</summary>
        Failed = 5,
    }

    /// <summary>
    /// Event arguments raised when a handler is about to be invoked.
    /// 
    /// Used for pre-invocation tracing, validation, and logging.
    /// Fired by bridge factory (Step 41) and bridge lifecycle manager (Step 45)
    /// before dispatching a handler call to the Continue npm bridge.
    /// </summary>
    internal sealed class HandlerInvokedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the handler being invoked (e.g., "getEditorState", "search", "refactor").
        /// </summary>
        public string HandlerName { get; }

        /// <summary>
        /// Gets the name of the method being invoked (e.g., "HandleGetEditorStateAsync", "HandleSearchAsync").
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Gets the UTC timestamp when the handler was invoked.
        /// </summary>
        public DateTime InvokedAt { get; }

        /// <summary>
        /// Gets the optional identifier of the component that initiated this handler invocation.
        /// May be null. Example values: "editorService", "userCommand", "diagnostic".
        /// </summary>
        public string? CallerId { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HandlerInvokedEventArgs"/>.
        /// </summary>
        /// <param name="handlerName">The name of the handler. Must not be null or empty.</param>
        /// <param name="methodName">The name of the method. Must not be null or empty.</param>
        /// <param name="invokedAt">The UTC timestamp of invocation.</param>
        /// <param name="callerId">Optional identifier of the invoking component. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if handlerName or methodName is null.</exception>
        /// <exception cref="ArgumentException">Thrown if handlerName or methodName is empty.</exception>
        public HandlerInvokedEventArgs(string handlerName, string methodName, DateTime invokedAt, string? callerId = null)
        {
            if (handlerName == null)
                throw new ArgumentNullException(nameof(handlerName));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));
            if (handlerName.Length == 0)
                throw new ArgumentException("Handler name must not be empty.", nameof(handlerName));
            if (methodName.Length == 0)
                throw new ArgumentException("Method name must not be empty.", nameof(methodName));

            HandlerName = handlerName;
            MethodName = methodName;
            InvokedAt = invokedAt;
            CallerId = callerId;
        }
    }

    /// <summary>
    /// Event arguments raised when a handler completes execution (success or failure).
    /// 
    /// Provides execution metrics and result context for monitoring, logging, and telemetry.
    /// Fired by bridge factory (Step 41) and handler dispatcher (Step 42)
    /// after handler completion.
    /// </summary>
    internal sealed class HandlerResultEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the handler that completed.
        /// </summary>
        public string HandlerName { get; }

        /// <summary>
        /// Gets a value indicating whether the handler completed successfully (without throwing).
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the exception thrown by the handler, or null if the handler succeeded.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the time in milliseconds from handler invocation to completion.
        /// </summary>
        public long ExecutionTimeMs { get; }

        /// <summary>
        /// Gets the UTC timestamp when the handler completed.
        /// </summary>
        public DateTime CompletedAt { get; }

        /// <summary>
        /// Gets optional structured metadata about the result (e.g., "itemsFound": 42, "duration": 1234).
        /// May be null. Common keys: "itemsCount", "pageNumber", "resultSize", "cacheHit".
        /// </summary>
        public IReadOnlyDictionary<string, object>? ResultMetadata { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HandlerResultEventArgs"/> for a successful result.
        /// </summary>
        /// <param name="handlerName">The name of the handler. Must not be null or empty.</param>
        /// <param name="executionTimeMs">Time in milliseconds from invocation to completion.</param>
        /// <param name="completedAt">The UTC timestamp of completion.</param>
        /// <param name="resultMetadata">Optional structured result metadata. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if handlerName is null.</exception>
        /// <exception cref="ArgumentException">Thrown if handlerName is empty.</exception>
        public HandlerResultEventArgs(string handlerName, long executionTimeMs, DateTime completedAt, IReadOnlyDictionary<string, object>? resultMetadata = null)
            : this(handlerName, success: true, exception: null, executionTimeMs, completedAt, resultMetadata)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="HandlerResultEventArgs"/> for a failed result.
        /// </summary>
        /// <param name="handlerName">The name of the handler. Must not be null or empty.</param>
        /// <param name="exception">The exception thrown by the handler. Must not be null.</param>
        /// <param name="executionTimeMs">Time in milliseconds from invocation to completion.</param>
        /// <param name="completedAt">The UTC timestamp of completion.</param>
        /// <exception cref="ArgumentNullException">Thrown if handlerName or exception is null.</exception>
        /// <exception cref="ArgumentException">Thrown if handlerName is empty.</exception>
        public HandlerResultEventArgs(string handlerName, Exception exception, long executionTimeMs, DateTime completedAt)
            : this(handlerName, success: false, exception: exception, executionTimeMs, completedAt, resultMetadata: null)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>
        /// Internal constructor supporting both success and failure cases.
        /// </summary>
        private HandlerResultEventArgs(string handlerName, bool success, Exception? exception, long executionTimeMs, DateTime completedAt, IReadOnlyDictionary<string, object>? resultMetadata)
        {
            if (handlerName == null)
                throw new ArgumentNullException(nameof(handlerName));
            if (handlerName.Length == 0)
                throw new ArgumentException("Handler name must not be empty.", nameof(handlerName));

            HandlerName = handlerName;
            Success = success;
            Exception = exception;
            ExecutionTimeMs = executionTimeMs;
            CompletedAt = completedAt;
            ResultMetadata = resultMetadata;
        }
    }

    /// <summary>
    /// Event arguments raised when the bridge health check status changes.
    /// 
    /// Signals health check transitions for monitoring and recovery coordination.
    /// Fired by health check service (Step 24) and bridge lifecycle manager (Step 45)
    /// during periodic health checks or status transitions.
    /// </summary>
    internal sealed class HealthCheckEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the current health status.
        /// </summary>
        public HealthCheckStatus Status { get; }

        /// <summary>
        /// Gets the number of consecutive health check failures (0 if status is Healthy).
        /// </summary>
        public int FailureCount { get; }

        /// <summary>
        /// Gets the UTC timestamp when the health check was executed.
        /// </summary>
        public DateTime CheckedAt { get; }

        /// <summary>
        /// Gets a human-readable message describing the health status.
        /// Example: "Bridge is responding normally", "Bridge response time degraded", "Bridge not responding".
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the time in milliseconds for the probe round-trip, or null if the probe was not executed.
        /// </summary>
        public int? ProbeTimeMs { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="HealthCheckEventArgs"/>.
        /// </summary>
        /// <param name="status">The current health status.</param>
        /// <param name="failureCount">The number of consecutive failures (0 if Healthy).</param>
        /// <param name="checkedAt">The UTC timestamp of the health check.</param>
        /// <param name="message">Human-readable status message. Must not be null or empty.</param>
        /// <param name="probeTimeMs">Optional probe round-trip time in milliseconds. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if message is null.</exception>
        /// <exception cref="ArgumentException">Thrown if message is empty.</exception>
        public HealthCheckEventArgs(HealthCheckStatus status, int failureCount, DateTime checkedAt, string message, int? probeTimeMs = null)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (message.Length == 0)
                throw new ArgumentException("Message must not be empty.", nameof(message));

            Status = status;
            FailureCount = failureCount > 0 ? failureCount : 0;
            CheckedAt = checkedAt;
            Message = message;
            ProbeTimeMs = probeTimeMs;
        }
    }

    /// <summary>
    /// Event arguments raised when the bridge lifecycle state transitions.
    /// 
    /// Signals state changes (Initializing → Running → Stopping → Stopped, etc.) for coordination
    /// and telemetry. Fired by bridge lifecycle manager (Step 45) during state transitions.
    /// </summary>
    internal sealed class BridgeStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the previous bridge state.
        /// </summary>
        public BridgeState OldState { get; }

        /// <summary>
        /// Gets the new (current) bridge state.
        /// </summary>
        public BridgeState NewState { get; }

        /// <summary>
        /// Gets a human-readable description of the state transition reason.
        /// Example values: "UserInitiated", "HealthCheckFailed", "ProcessCrashed", "Idle", "Configuration".
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets the UTC timestamp when the state transition occurred.
        /// </summary>
        public DateTime TransitionedAt { get; }

        /// <summary>
        /// Gets optional debugging context about the transition (e.g., "crashReason": "OutOfMemory", "uptime": 3600000).
        /// May be null. Common keys: "crashReason", "uptime", "memoryUsage", "lastHealthStatus".
        /// </summary>
        public IReadOnlyDictionary<string, object>? Context { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="BridgeStateChangedEventArgs"/>.
        /// </summary>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        /// <param name="reason">Human-readable transition reason. Must not be null or empty.</param>
        /// <param name="transitionedAt">The UTC timestamp of the transition.</param>
        /// <param name="context">Optional debugging context. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if reason is null.</exception>
        /// <exception cref="ArgumentException">Thrown if reason is empty.</exception>
        public BridgeStateChangedEventArgs(BridgeState oldState, BridgeState newState, string reason, DateTime transitionedAt, IReadOnlyDictionary<string, object>? context = null)
        {
            if (reason == null)
                throw new ArgumentNullException(nameof(reason));
            if (reason.Length == 0)
                throw new ArgumentException("Reason must not be empty.", nameof(reason));

            OldState = oldState;
            NewState = newState;
            Reason = reason;
            TransitionedAt = transitionedAt;
            Context = context;
        }
    }

    /// <summary>
    /// Event arguments raised during bridge lifecycle transitions.
    /// Used by IBridgeLifecycleManager for OnBridgeReady, OnBridgeDegraded, OnBridgeShutdown events.
    /// </summary>
    public sealed class BridgeLifecycleEventArgs : EventArgs
    {
        /// <summary>The current bridge lifecycle state.</summary>
        public required BridgeLifecycleState State { get; set; }

        /// <summary>Timestamp of the state transition.</summary>
        public required DateTime Timestamp { get; set; }

        /// <summary>Human-readable reason for the state transition.</summary>
        public required string Reason { get; set; }

        /// <summary>Optional error details if the transition was due to an error.</summary>
        public Exception? Error { get; set; }
    }

    /// <summary>
    /// Bridge lifecycle state enumeration (used by Step 45 BridgeLifecycleManager).
    /// </summary>
    public enum BridgeLifecycleState
    {
        /// <summary>Bridge not yet initialized.</summary>
        NotInitialized,

        /// <summary>Bridge initialization in progress.</summary>
        Initializing,

        /// <summary>Bridge ready and operational.</summary>
        Ready,

        /// <summary>Bridge experiencing issues; recovery in progress.</summary>
        Degraded,

        /// <summary>Bridge has been shut down.</summary>
        Shutdown
    }
}
