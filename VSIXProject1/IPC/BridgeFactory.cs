using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Represents an error that occurred during bridge transport creation or configuration validation.
    /// 
    /// Includes operation type context (VersionResolution, ConfigurationValidation, TransportCreation, ProcessInitialization)
    /// to aid in diagnostics and error recovery.
    /// 
    /// Typical error scenarios:
    /// - VersionResolution: version directory not found, invalid version format
    /// - ConfigurationValidation: npm executable not found, manifest missing, core configuration invalid
    /// - TransportCreation: null configuration, IsCoreValid == false
    /// - ProcessInitialization: process failed to spawn, stdio setup failed
    /// </summary>
    public sealed class BridgeFactoryException : InvalidOperationException
    {
        /// <summary>
        /// Enumeration of factory operations that can fail.
        /// </summary>
        internal enum OperationType
        {
            /// <summary>Resolving version string to filesystem paths (Step 9 VersionManager).</summary>
            VersionResolution = 0,

            /// <summary>Validating IBridgeConfiguration properties (IsCoreValid check).</summary>
            ConfigurationValidation = 1,

            /// <summary>Creating/instantiating StdioTransport from configuration.</summary>
            TransportCreation = 2,

            /// <summary>Initializing the Continue process (ProcessManager.Start).</summary>
            ProcessInitialization = 3,
        }

        /// <summary>
        /// Gets the type of operation that failed.
        /// </summary>
        internal OperationType Operation { get; }

        /// <summary>
        /// Gets the version string involved in the failure (if applicable).
        /// Null if failure is not version-related.
        /// </summary>
        internal string? FailedVersion { get; }

        /// <summary>
        /// Initializes a new instance of BridgeFactoryException.
        /// </summary>
        /// <param name="operation">The type of operation that failed.</param>
        /// <param name="message">Description of the failure.</param>
        /// <param name="innerException">Optional inner exception that caused this failure.</param>
        /// <param name="version">Optional version string involved in the failure.</param>
        internal BridgeFactoryException(
            OperationType operation,
            string message,
            Exception? innerException = null,
            string? version = null)
            : base($"Bridge factory {operation} failed: {message}", innerException)
        {
            Operation = operation;
            FailedVersion = version;
        }
    }

    /// <summary>
    /// Concrete factory for creating IBridgeTransport instances.
    /// 
    /// Implements IBridgeFactory to encapsulate transport creation logic.
    /// Handles configuration resolution, validation, error logging, and event firing.
    /// 
    /// Dependencies (injected):
    /// - IBridgeLogger (Step 25): logs errors and diagnostic info
    /// - IBridgeTelemetryCollector (Step 26): records factory metrics and error events
    /// 
    /// Design:
    /// - Constructor caches logger/telemetry references (both optional; factory degrades gracefully if null)
    /// - CreateTransportAsync(string) lazily resolves version to BridgeConfiguration, then delegates to overload
    /// - CreateTransportAsync(IBridgeConfiguration) validates config, creates StdioTransport, fires OnTransportCreated event
    /// - ValidateConfiguration() private helper throws BridgeFactoryException on validation failure
    /// - All exceptions logged and telemetry recorded before re-throwing
    /// 
    /// Error Handling:
    /// - Null inputs: ArgumentNullException for configuration, BridgeFactoryException for null version
    /// - Empty version: BridgeFactoryException with VersionResolution operation type
    /// - Configuration invalid: BridgeFactoryException with ConfigurationValidation operation type
    /// - Transport creation fails: wrapped in BridgeFactoryException with TransportCreation operation type
    /// 
    /// Event Firing:
    /// - OnTransportCreated fires ONLY on successful creation (if configuration is valid and StdioTransport instantiated)
    /// - Event args include timestamp, handler name "CreateTransport", success=true
    /// - If creation fails, exception is thrown instead of firing event
    /// 
    /// Usage:
    ///   var factory = new BridgeFactory(logger, telemetry);
    ///   try
    ///   {
    ///       var transport = await factory.CreateTransportAsync("2.0.0", ct);
    ///       await transport.StartAsync(ct);
    ///   }
    ///   catch (BridgeFactoryException ex)
    ///   {
    ///       // Handle factory-specific errors
    ///   }
    /// </summary>
    public sealed class BridgeFactory : IBridgeFactory
    {
        private readonly IBridgeLogger? _logger;
        private readonly IBridgeTelemetryCollector? _telemetry;

        /// <summary>
        /// Initializes a new instance of BridgeFactory.
        /// </summary>
        /// <param name="logger">Optional logger for error and diagnostic logging.
        /// If null, errors are still thrown but not logged.</param>
        /// <param name="telemetry">Optional telemetry collector for metrics.
        /// If null, factory operations are not tracked in telemetry.</param>
        internal BridgeFactory(IBridgeLogger? logger = null, IBridgeTelemetryCollector? telemetry = null)
        {
            _logger = logger;
            _telemetry = telemetry;
        }

        /// <summary>
        /// Raised when a transport is successfully created.
        /// </summary>
        public event EventHandler<HandlerInvokedEventArgs>? OnTransportCreated;

        /// <summary>
        /// Creates a bridge transport from a version string.
        /// 
        /// Lazily resolves the version string to a BridgeConfiguration,
        /// then delegates to CreateTransportAsync(IBridgeConfiguration, CancellationToken).
        /// </summary>
        public async Task<IBridgeTransport> CreateTransportAsync(
            string version,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(version))
            {
                var ex = new BridgeFactoryException(
                    BridgeFactoryException.OperationType.VersionResolution,
                    "Version string cannot be null or empty.",
                    version: version);
                LogError(ex);
                RecordFactoryError("CreateTransport_InvalidVersion");
                throw ex;
            }

            try
            {
                // Lazily create and validate BridgeConfiguration from version string
                var config = new BridgeConfiguration(version);
                config.Validate();

                // Delegate to the configuration overload
                return await CreateTransportAsync(config, cancellationToken);
            }
            catch (BridgeFactoryException)
            {
                throw; // Re-throw factory exceptions as-is
            }
            catch (Exception ex)
            {
                var factoryEx = new BridgeFactoryException(
                    BridgeFactoryException.OperationType.VersionResolution,
                    $"Failed to resolve version '{version}' to configuration.",
                    innerException: ex,
                    version: version);
                LogError(factoryEx);
                RecordFactoryError("CreateTransport_ResolutionFailed");
                throw factoryEx;
            }
        }

        /// <summary>
        /// Creates a bridge transport from a pre-built configuration.
        /// 
        /// Validates the configuration, instantiates StdioTransport,
        /// and fires OnTransportCreated event on success.
        /// </summary>
        public async Task<IBridgeTransport> CreateTransportAsync(
            IBridgeConfiguration configuration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ArgumentNullException.ThrowIfNull(configuration);

            try
            {
                // Validate configuration before creating transport
                ValidateConfiguration(configuration);

                // Instantiate StdioTransport
                var transport = new StdioTransport(configuration);

                // Fire event on successful creation
                OnTransportCreated?.Invoke(
                    this,
                    new HandlerInvokedEventArgs(
                        handlerName: "CreateTransport",
                        methodName: "CreateTransportAsync",
                        invokedAt: DateTime.UtcNow));

                RecordFactorySuccess("CreateTransport");
                return transport;
            }
            catch (BridgeFactoryException)
            {
                throw; // Re-throw factory exceptions as-is
            }
            catch (Exception ex)
            {
                var factoryEx = new BridgeFactoryException(
                    BridgeFactoryException.OperationType.TransportCreation,
                    $"Failed to create StdioTransport for version '{configuration.Version}'.",
                    innerException: ex);
                LogError(factoryEx);
                RecordFactoryError("CreateTransport_InstantiationFailed");
                throw factoryEx;
            }
        }

        /// <summary>
        /// Validates that the provided configuration is ready for transport instantiation.
        /// </summary>
        /// <exception cref="BridgeFactoryException">Thrown if configuration is invalid.</exception>
        private void ValidateConfiguration(IBridgeConfiguration configuration)
        {
            if (!configuration.IsCoreValid)
            {
                var ex = new BridgeFactoryException(
                    BridgeFactoryException.OperationType.ConfigurationValidation,
                    $"Configuration core validation failed. Version: {configuration.Version}. " +
                    "Check that version directory exists, npm is installed, and manifest is valid.",
                    version: configuration.Version);
                LogError(ex);
                RecordFactoryError("CreateTransport_ConfigurationInvalid");
                throw ex;
            }
        }

        /// <summary>
        /// Logs an error via the telemetry system if available.
        /// </summary>
        private void LogError(Exception exception)
        {
            if (_logger != null)
            {
                try
                {
                    _logger.LogError($"[BridgeFactory] {exception.Message}", exception);
                }
                catch
                {
                    // Suppress logger exceptions to avoid masking the original error
                }
            }
        }

        /// <summary>
        /// Records a successful factory operation in telemetry.
        /// </summary>
        private void RecordFactorySuccess(string operationName)
        {
            if (_telemetry != null)
            {
                try
                {
                    _telemetry.RecordEvent("bridge_factory_success", new() { { "operation", operationName } });
                }
                catch
                {
                    // Suppress telemetry exceptions to avoid side effects
                }
            }
        }

        /// <summary>
        /// Records a failed factory operation in telemetry.
        /// </summary>
        private void RecordFactoryError(string operationName)
        {
            if (_telemetry != null)
            {
                try
                {
                    _telemetry.RecordEvent("bridge_factory_error", new() { { "operation", operationName } });
                }
                catch
                {
                    // Suppress telemetry exceptions to avoid side effects
                }
            }
        }
    }
}
