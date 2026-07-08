#nullable enable

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Fluent builder for constructing IBridgeConfiguration test objects.
    /// 
    /// Usage:
    ///   var configBuilder = new BridgeConfigurationBuilder()
    ///       .WithVersion("2.0.0")
    ///       .WithDebugMode(true)
    ///       .WithTelemetry(false);
    /// 
    /// Note: Use MockFactory.CreateMockBridgeConfiguration() to get actual mock instances.
    /// </summary>
    public class BridgeConfigurationBuilder
    {
        private string _version = TestConstants.DefaultTestVersion;
        private bool _debugMode = false;
        private bool _enableTelemetry = true;
        private string _logLevel = "info";

        /// <summary>
        /// Sets the bridge version.
        /// </summary>
        public BridgeConfigurationBuilder WithVersion(string version)
        {
            _version = version;
            return this;
        }

        /// <summary>
        /// Sets debug mode flag.
        /// </summary>
        public BridgeConfigurationBuilder WithDebugMode(bool enabled)
        {
            _debugMode = enabled;
            return this;
        }

        /// <summary>
        /// Sets telemetry collection flag.
        /// </summary>
        public BridgeConfigurationBuilder WithTelemetry(bool enabled)
        {
            _enableTelemetry = enabled;
            return this;
        }

        /// <summary>
        /// Sets the log level.
        /// </summary>
        public BridgeConfigurationBuilder WithLogLevel(string level)
        {
            _logLevel = level;
            return this;
        }

        /// <summary>
        /// Gets the configured version.
        /// </summary>
        public string GetVersion() => _version;

        /// <summary>
        /// Gets the configured debug mode flag.
        /// </summary>
        public bool GetDebugMode() => _debugMode;

        /// <summary>
        /// Gets the configured telemetry flag.
        /// </summary>
        public bool GetEnableTelemetry() => _enableTelemetry;

        /// <summary>
        /// Gets the configured log level.
        /// </summary>
        public string GetLogLevel() => _logLevel;
    }

    /// <summary>
    /// Fluent builder for constructing bridge transport test objects.
    /// 
    /// Usage:
    ///   var transportBuilder = new BridgeTransportBuilder()
    ///       .WithTimeout(5000);
    /// 
    /// Note: Use MockFactory.CreateMockBridgeTransport() to get actual mock instances.
    /// </summary>
    public class BridgeTransportBuilder
    {
        private int _timeoutMs = TestConstants.StandardRpcTimeoutMs;
        private string? _processPath = null;
        private string? _arguments = null;

        /// <summary>
        /// Sets the transport timeout.
        /// </summary>
        public BridgeTransportBuilder WithTimeout(int milliseconds)
        {
            _timeoutMs = milliseconds;
            return this;
        }

        /// <summary>
        /// Sets the process path.
        /// </summary>
        public BridgeTransportBuilder WithProcessPath(string path)
        {
            _processPath = path;
            return this;
        }

        /// <summary>
        /// Sets the process arguments.
        /// </summary>
        public BridgeTransportBuilder WithArguments(string args)
        {
            _arguments = args;
            return this;
        }

        /// <summary>
        /// Gets the configured timeout.
        /// </summary>
        public int GetTimeout() => _timeoutMs;

        /// <summary>
        /// Gets the configured process path.
        /// </summary>
        public string? GetProcessPath() => _processPath;

        /// <summary>
        /// Gets the configured process arguments.
        /// </summary>
        public string? GetArguments() => _arguments;
    }
}
