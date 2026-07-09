using System;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception raised by BridgeLifecycleManager during lifecycle operations.
    /// 
    /// Provides operation context via OperationType enum to distinguish between:
    /// - Initialization failures (factory error, transport startup, health check timeout)
    /// - Transport failures (connection lost, message send timeout, relay error)
    /// - Health check failures (ping timeout, circuit breaker activated)
    /// - Shutdown failures (resource cleanup timeout, child process termination failure)
    /// - Message dispatch failures (retry exhaustion, degraded state)
    /// 
    /// Usage:
    ///   try
    ///   {
    ///     await manager.InitializeAsync();
    ///   }
    ///   catch (BridgeLifecycleException ex) when (ex.OperationType == OperationType.Initialization)
    ///   {
    ///     logger.Error($"Bridge init failed: {ex.Message}");
    ///   }
    /// </summary>
    public sealed class BridgeLifecycleException : Exception
    {
        /// <summary>
        /// Enumeration of bridge lifecycle operations that can fail.
        /// Used to categorize and handle different types of lifecycle errors.
        /// </summary>
        public enum OperationType
        {
            /// <summary>Initialization operation (InitializeAsync, startup sequence)</summary>
            Initialization,

            /// <summary>Transport operation (SendMessageAsync, connection management)</summary>
            Transport,

            /// <summary>Health check operation (ping, circuit breaker)</summary>
            HealthCheck,

            /// <summary>Shutdown operation (ShutdownAsync, resource cleanup)</summary>
            Shutdown,

            /// <summary>Message dispatch operation (routing, retry, buffering)</summary>
            MessageDispatch
        }

        /// <summary>
        /// The type of operation that failed.
        /// </summary>
        public OperationType Operation { get; }

        /// <summary>
        /// Optional inner exception providing additional context.
        /// </summary>
        public new Exception? InnerException => base.InnerException;

        /// <summary>
        /// Initializes a new instance of BridgeLifecycleException.
        /// </summary>
        /// <param name="operation">The lifecycle operation that failed.</param>
        /// <param name="message">Descriptive error message.</param>
        public BridgeLifecycleException(OperationType operation, string message)
            : base(message)
        {
            Operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of BridgeLifecycleException with an inner exception.
        /// </summary>
        /// <param name="operation">The lifecycle operation that failed.</param>
        /// <param name="message">Descriptive error message.</param>
        /// <param name="innerException">The underlying exception that caused this error.</param>
        public BridgeLifecycleException(OperationType operation, string message, Exception innerException)
            : base(message, innerException)
        {
            Operation = operation;
        }
    }
}
