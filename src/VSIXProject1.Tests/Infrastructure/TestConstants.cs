#nullable enable

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Shared test constants used across all unit test suites.
    /// Centralizes magic numbers and well-known test values.
    /// 
    /// No instance should be created; use static members directly.
    /// </summary>
    public static class TestConstants
    {
        // === Timeouts ===

        /// <summary>
        /// Default timeout for general operations (5 seconds).
        /// Used for health checks, lifecycle operations.
        /// </summary>
        public const int DefaultTimeoutMs = 5000;

        /// <summary>
        /// Standard timeout for RPC/stdio operations (100 milliseconds).
        /// Used for message send/receive in transport tests.
        /// </summary>
        public const int StandardRpcTimeoutMs = 100;

        /// <summary>
        /// Short timeout for rapid tests (10 milliseconds).
        /// Used for quick assertions or polling loops.
        /// </summary>
        public const int ShortTimeoutMs = 10;

        /// <summary>
        /// Long timeout for slow operations (30 seconds).
        /// Used for process startup, npm downloads, integration tests.
        /// </summary>
        public const int LongTimeoutMs = 30000;

        // === Versions ===

        /// <summary>
        /// Default test bridge version.
        /// </summary>
        public const string DefaultTestVersion = "2.0.0";

        /// <summary>
        /// Array of valid test versions for parameterized tests.
        /// </summary>
        public static readonly string[] ValidTestVersions =
        {
            "2.0.0",
            "2.1.0",
            "2.2.0"
        };

        /// <summary>
        /// Array of invalid version strings for negative tests.
        /// </summary>
        public static readonly string[] InvalidVersions =
        {
            "invalid",
            "2.0",
            "not.a.version",
            "",
            "2.0.0.0.0"
        };

        /// <summary>
        /// Array of version strings that trigger downgrade warnings.
        /// </summary>
        public static readonly string[] DowngradeTestVersions =
        {
            "1.9.9",
            "1.0.0"
        };

        // === Network & Process ===

        /// <summary>
        /// Standard test port for stdio bridge communication.
        /// </summary>
        public const int StdioTestPort = 9999;

        /// <summary>
        /// Standard test process exit code (0 = success).
        /// </summary>
        public const int SuccessExitCode = 0;

        /// <summary>
        /// Standard test process exit code for general failure.
        /// </summary>
        public const int FailureExitCode = -1;

        /// <summary>
        /// Standard test process exit code for initialization failure.
        /// </summary>
        public const int InitFailureExitCode = 127;

        // === Paths & Files ===

        /// <summary>
        /// Relative path to the Continue npm package directory.
        /// </summary>
        public const string ContinueNpmPath = "src/versions/v2.0.0";

        /// <summary>
        /// Relative path to the version manifest directory.
        /// </summary>
        public const string VersionManifestPath = "src/versions";

        /// <summary>
        /// Expected core-server.js entry point name.
        /// </summary>
        public const string CoreServerEntryPoint = "core-server.js";

        /// <summary>
        /// Expected package.json filename.
        /// </summary>
        public const string PackageJsonFilename = "package.json";

        /// <summary>
        /// Expected manifest filename.
        /// </summary>
        public const string ManifestFilename = "manifest.json";

        // === RPC / Protocol ===

        /// <summary>
        /// JSON-RPC specification version.
        /// </summary>
        public const string JsonRpcVersion = "2.0";

        /// <summary>
        /// Standard JSON-RPC error code for invalid request.
        /// </summary>
        public const int JsonRpcErrorInvalidRequest = -32600;

        /// <summary>
        /// Standard JSON-RPC error code for method not found.
        /// </summary>
        public const int JsonRpcErrorMethodNotFound = -32601;

        /// <summary>
        /// Standard JSON-RPC error code for internal error.
        /// </summary>
        public const int JsonRpcErrorInternalError = -32603;

        /// <summary>
        /// Custom JSON-RPC error code for bridge protocol errors.
        /// </summary>
        public const int JsonRpcErrorBridgeProtocol = -32000;

        // === Log Levels ===

        /// <summary>
        /// Standard log level: trace (most verbose).
        /// </summary>
        public const string LogLevelTrace = "trace";

        /// <summary>
        /// Standard log level: debug.
        /// </summary>
        public const string LogLevelDebug = "debug";

        /// <summary>
        /// Standard log level: info (default).
        /// </summary>
        public const string LogLevelInfo = "info";

        /// <summary>
        /// Standard log level: warn.
        /// </summary>
        public const string LogLevelWarn = "warn";

        /// <summary>
        /// Standard log level: error.
        /// </summary>
        public const string LogLevelError = "error";

        /// <summary>
        /// Array of valid log levels for parameterized tests.
        /// </summary>
        public static readonly string[] ValidLogLevels =
        {
            LogLevelTrace,
            LogLevelDebug,
            LogLevelInfo,
            LogLevelWarn,
            LogLevelError
        };

        // === Error Codes ===

        /// <summary>
        /// Error code: process failed to start.
        /// </summary>
        public const string ErrorCodeProcessStartFailed = "PROCESS_START_FAILED";

        /// <summary>
        /// Error code: process exited unexpectedly.
        /// </summary>
        public const string ErrorCodeProcessExitedUnexpectedly = "PROCESS_EXITED_UNEXPECTEDLY";

        /// <summary>
        /// Error code: transport send failed.
        /// </summary>
        public const string ErrorCodeSendFailed = "SEND_FAILED";

        /// <summary>
        /// Error code: transport receive failed.
        /// </summary>
        public const string ErrorCodeReceiveFailed = "RECEIVE_FAILED";

        /// <summary>
        /// Error code: JSON-RPC protocol error.
        /// </summary>
        public const string ErrorCodeJsonRpcProtocol = "JSONRPC_PROTOCOL_ERROR";

        /// <summary>
        /// Error code: health check timeout.
        /// </summary>
        public const string ErrorCodeHealthCheckTimeout = "HEALTH_CHECK_TIMEOUT";

        /// <summary>
        /// Error code: configuration invalid.
        /// </summary>
        public const string ErrorCodeConfigurationInvalid = "CONFIGURATION_INVALID";
    }
}
