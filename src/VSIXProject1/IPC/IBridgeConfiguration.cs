using System;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Provides configuration for the bridge transport layer, including version selection,
    /// process initialization parameters, and runtime behavior settings.
    /// 
    /// The configuration follows a hybrid immutability model:
    /// - **Core properties** (version, paths, timeouts) are immutable after construction
    /// - **Optional settings** (debug mode, telemetry, logging) are mutable for runtime adjustment
    /// 
    /// Configuration flow:
    /// 1. VersionManager (Step 9) provides the active version string from Windows registry
    /// 2. BridgeConfiguration (Step 18) implements this interface, resolving version strings to full paths
    /// 3. StdioTransport (Step 19) receives this configuration and uses it to spawn the npm process
    /// 4. BridgeLifecycleManager (Step 45) orchestrates configuration creation and transport initialization
    /// 
    /// Example implementation strategy (for Step 18):
    /// - Constructor takes version string, optional debug flag
    /// - Version string is resolved to paths (VersionPath, NpmExecutablePath, etc.) via directory scanning
    /// - Timeouts are set to sensible defaults (5000ms startup, 30000ms RPC, 3000ms shutdown)
    /// - All properties throw InvalidOperationException if accessed before initialization is complete
    /// </summary>
    internal interface IBridgeConfiguration
    {
        /// <summary>
        /// Gets the semantic version of the Continue bridge (e.g., "2.0.0").
        /// 
        /// This version identifier is used to locate the correct version directory
        /// in src/versions/ and select appropriate handlers and npm packages.
        /// 
        /// Immutable. Set during construction. Matches the VersionManager.GetActiveVersion() result
        /// or an override from the version selection UI (Step 9).
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the absolute filesystem path to the version directory (e.g., "src/versions/v2.0.0").
        /// 
        /// This directory contains:
        /// - package.json (npm metadata)
        /// - manifest.json (version checksums, handler list, requirements)
        /// - core-server.js (bridge entry point)
        /// - handlers/ (handler modules)
        /// - lib/ (shared utilities)
        /// - tests/ (test suite)
        /// 
        /// Immutable. Resolved during construction from the Version property.
        /// Throws InvalidOperationException if the directory does not exist or lacks manifest.json.
        /// </summary>
        string VersionPath { get; }

        /// <summary>
        /// Gets the absolute filesystem path to the manifest.json file for this version
        /// (e.g., "src/versions/v2.0.0/manifest.json").
        /// 
        /// The manifest contains:
        /// - Version metadata (release date, status)
        /// - System requirements (Node.js, npm versions)
        /// - Checksums for integrity verification
        /// - Handler registry (available commands)
        /// - Transport modes (e.g., "stdio")
        /// 
        /// Immutable. Derived from VersionPath. Validation is deferred to Step 18 implementation.
        /// </summary>
        string ManifestPath { get; }

        /// <summary>
        /// Gets the absolute filesystem path to the npm executable
        /// (e.g., "C:\Program Files\nodejs\npm.cmd" on Windows, "/usr/bin/npm" on Unix).
        /// 
        /// This executable is used by StdioTransport to install dependencies and run core-server.js.
        /// On Windows, this may be "npm.cmd" (batch wrapper); on Unix, "npm" (shell script).
        /// 
        /// Immutable. Resolved during construction by searching PATH or returning a hardcoded
        /// system default. Validation (executable exists and is runnable) is deferred to Step 18.
        /// </summary>
        string NpmExecutablePath { get; }

        /// <summary>
        /// Gets the absolute filesystem path to the core-server.js entry point script
        /// (e.g., "src/versions/v2.0.0/core-server.js").
        /// 
        /// This is the main bridge server script that StdioTransport launches via npm or Node.js.
        /// The script expects stdin for receiving JSON-RPC messages and writes to stdout.
        /// 
        /// Immutable. Derived from VersionPath. Validation (file exists) is deferred to Step 18.
        /// </summary>
        string NpmServerScriptPath { get; }

        /// <summary>
        /// Gets the working directory for the npm process
        /// (typically the version directory, e.g., "src/versions/v2.0.0").
        /// 
        /// StdioTransport will cd into this directory before launching npm to ensure
        /// relative imports in core-server.js and dependencies resolve correctly.
        /// 
        /// Immutable. Typically equals VersionPath. Derived during construction.
        /// </summary>
        string WorkingDirectory { get; }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for the npm process to start
        /// and signal readiness (e.g., 5000ms = 5 seconds).
        /// 
        /// StdioTransport will throw InvalidOperationException if the process does not
        /// initialize within this window. This timeout should account for:
        /// - npm startup overhead
        /// - Node.js VM initialization
        /// - First handler registration in core-server.js
        /// 
        /// Immutable. Default: 5000ms. Value must be > 0. Validation deferred to Step 18.
        /// </summary>
        long ProcessStartupTimeoutMs { get; }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for a single RPC request/response cycle
        /// (e.g., 30000ms = 30 seconds).
        /// 
        /// This timeout applies to all handler calls (search, goToDefinition, codeCompletion, etc.)
        /// and represents the full round-trip time from request to response. Does not include
        /// network latency (stdio is local IPC) but may include heavy computation on the bridge side.
        /// 
        /// Immutable. Default: 30000ms. Value must be > 0. Validation deferred to Step 18.
        /// </summary>
        long RpcTimeoutMs { get; }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for graceful process shutdown
        /// (e.g., 3000ms = 3 seconds).
        /// 
        /// StdioTransport will attempt a graceful SIGTERM/SIGINT signal, wait this duration,
        /// then forcibly kill the process if it has not exited. This timeout should be short
        /// enough to prevent extension hangs but long enough for clean resource cleanup.
        /// 
        /// Immutable. Default: 3000ms. Value must be > 0. Validation deferred to Step 18.
        /// </summary>
        long ShutdownTimeoutMs { get; }

        /// <summary>
        /// Gets or sets a value indicating whether debug mode is enabled for the bridge.
        /// 
        /// When true:
        /// - core-server.js may emit verbose logging to stderr
        /// - StdioTransport may log all JSON-RPC messages to the VS output pane
        /// - Additional validation and assertion checks may be enabled
        /// 
        /// Mutable at runtime. Can be toggled after construction without restarting the transport.
        /// Default: false (unless overridden by extension settings or Step 9 UI).
        /// </summary>
        bool IsDebugMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether telemetry collection is enabled.
        /// 
        /// When true:
        /// - BridgeTelemetryCollector (Step 26) collects metrics (request count, latency, errors)
        /// - Metrics may be sent to a remote telemetry service for diagnostics
        /// - User privacy is preserved; no code or sensitive data is sent
        /// 
        /// Mutable at runtime. Can be toggled after construction.
        /// Default: true (unless disabled by user or extension settings).
        /// </summary>
        bool EnableTelemetry { get; set; }

        /// <summary>
        /// Gets or sets the logging level for the bridge
        /// (e.g., "error", "warn", "info", "debug", "trace").
        /// 
        /// This controls verbosity of both VS output pane logs and any bridge-side logging.
        /// Typical values: "error" (minimal), "warn" (warnings), "info" (normal), "debug" (verbose), "trace" (very verbose).
        /// 
        /// Mutable at runtime. Changes apply to subsequent log calls.
        /// Default: "info" (unless overridden by extension settings or Step 9 UI).
        /// </summary>
        string LogLevel { get; set; }

        /// <summary>
        /// Gets a value indicating whether the core properties of this configuration are valid
        /// and safe to use for transport initialization.
        /// 
        /// Core properties are: Version, VersionPath, NpmExecutablePath, NpmServerScriptPath, WorkingDirectory, and all timeout values.
        /// 
        /// This property is primarily for Step 18 implementation to signal completion of validation.
        /// Returns true if:
        /// - Version string is non-empty
        /// - VersionPath and ManifestPath directories exist
        /// - NpmExecutablePath points to an executable file
        /// - NpmServerScriptPath points to a readable .js file
        /// - All timeout values are positive
        /// 
        /// Immutable. Set once during construction. Step 19 (StdioTransport) checks this
        /// before attempting process launch.
        /// </summary>
        bool IsCoreValid { get; }
    }
}
