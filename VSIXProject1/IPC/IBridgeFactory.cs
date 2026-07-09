using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Factory for creating IBridgeTransport instances.
    /// 
    /// Encapsulates transport instantiation logic, decoupling creation from consumers
    /// (Step 42: message dispatcher, Step 45: lifecycle manager).
    /// 
    /// Design:
    /// - Accepts either version string (lazy configuration resolution) OR pre-built IBridgeConfiguration (eager)
    /// - Validates configuration before instantiation
    /// - Returns StdioTransport instances (or other IBridgeTransport implementations)
    /// - Raises OnTransportCreated event on successful creation for lifecycle tracing
    /// 
    /// Usage examples:
    ///   // Lazy: version string resolved to BridgeConfiguration internally
    ///   var transport = await factory.CreateTransportAsync("2.0.0", cancellationToken);
    ///   
    ///   // Eager: pre-built configuration passed through
    ///   var config = new BridgeConfiguration("2.0.0");
    ///   config.Validate();
    ///   var transport = await factory.CreateTransportAsync(config, cancellationToken);
    /// 
    /// Exception handling:
    /// - Throws BridgeFactoryException on validation failure, null input, process initialization errors
    /// - Logs errors via IBridgeLogger (if available)
    /// - Records metrics via IBridgeTelemetryCollector (if available)
    /// 
    /// Threading:
    /// - Both CreateTransportAsync methods are async and cancellation-aware
    /// - Safe to call from any thread
    /// - Returns immediately with created transport; does NOT start the transport
    /// </summary>
    public interface IBridgeFactory
    {
        /// <summary>
        /// Asynchronously creates a bridge transport instance from a version string.
        /// 
        /// Internally creates a BridgeConfiguration from the version string, validates it,
        /// and returns a new StdioTransport instance.
        /// 
        /// The returned transport is NOT started; caller must call StartAsync() before use.
        /// </summary>
        /// <param name="version">Semantic version string (e.g., "2.0.0") to locate bridge files.
        /// Must be non-null and non-empty; validation deferred until internal BridgeConfiguration creation.</param>
        /// <param name="cancellationToken">Cancellation token for the creation operation.
        /// Respects cancellation during configuration validation and file I/O.</param>
        /// <returns>Task&lt;IBridgeTransport&gt; representing the asynchronous creation operation.
        /// On completion, contains a new StdioTransport instance ready for StartAsync().</returns>
        /// <exception cref="BridgeFactoryException">Thrown if version string is null/empty,
        /// if version resolution fails (missing version directory),
        /// if configuration validation fails (missing npm executable, etc.),
        /// or if process initialization fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled
        /// before creation completes.</exception>
        Task<IBridgeTransport> CreateTransportAsync(string version, CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously creates a bridge transport instance from a pre-built configuration.
        /// 
        /// Validates the provided IBridgeConfiguration and returns a new StdioTransport instance.
        /// Preferred for scenarios where configuration is already resolved or mocked.
        /// 
        /// The returned transport is NOT started; caller must call StartAsync() before use.
        /// </summary>
        /// <param name="configuration">A fully-initialized IBridgeConfiguration instance.
        /// Must have IsCoreValid == true after Validate() is called; if not, throws BridgeFactoryException.</param>
        /// <param name="cancellationToken">Cancellation token for the creation operation.
        /// Respects cancellation during configuration validation.</param>
        /// <returns>Task&lt;IBridgeTransport&gt; representing the asynchronous creation operation.
        /// On completion, contains a new StdioTransport instance ready for StartAsync().</returns>
        /// <exception cref="BridgeFactoryException">Thrown if configuration is null,
        /// if configuration validation fails (IsCoreValid == false),
        /// or if StdioTransport instantiation fails.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellationToken is cancelled
        /// before creation completes.</exception>
        Task<IBridgeTransport> CreateTransportAsync(IBridgeConfiguration configuration, CancellationToken cancellationToken);

        /// <summary>
        /// Raised when a transport is successfully created by this factory.
        /// 
        /// Event args include:
        /// - Timestamp of creation
        /// - Handler name ("CreateTransport")
        /// - Success flag (always true; failures raise exceptions instead)
        /// 
        /// Intended for lifecycle tracing, telemetry, and diagnostic logging.
        /// Subscribers must not block; if long-running work needed, offload to background task.
        /// </summary>
        event EventHandler<HandlerInvokedEventArgs>? OnTransportCreated;
    }
}
