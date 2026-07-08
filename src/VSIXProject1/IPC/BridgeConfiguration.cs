using System;
using System.IO;
using System.Linq;
using ContinueVS.Services;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Provides configuration for the bridge transport layer, implementing <see cref="IBridgeConfiguration"/>.
    /// 
    /// Manages:
    /// - Version resolution (accepts version string, resolves to filesystem paths)
    /// - Path discovery (VersionPath, NpmExecutablePath, NpmServerScriptPath, WorkingDirectory, ManifestPath)
    /// - Lazy validation (properties validate on first access; IsCoreValid signals completion)
    /// - Mutable runtime flags (IsDebugMode, EnableTelemetry, LogLevel)
    /// 
    /// Design: Constructor caches version string; separate Validate() method performs filesystem checks.
    /// This allows tests to mock missing paths and Step 19 (StdioTransport) to check IsCoreValid before proceeding.
    /// 
    /// Example usage:
    ///     var versionManager = new VersionManager();
    ///     var activeVersion = versionManager.GetActiveVersion(); // e.g., "2.0.0"
    ///     
    ///     var config = new BridgeConfiguration(activeVersion);
    ///     config.Validate(); // Validates all paths; throws InvalidOperationException on failure
    ///     
    ///     if (config.IsCoreValid)
    ///     {
    ///         // Safe to use config properties with StdioTransport
    ///     }
    /// </summary>
    public sealed class BridgeConfiguration : IBridgeConfiguration
    {
        // Timeout constants (immutable, class-level)
        private const long DefaultProcessStartupTimeoutMs = 5000;
        private const long DefaultRpcTimeoutMs = 30000;
        private const long DefaultShutdownTimeoutMs = 3000;

        // Private fields
        private readonly VersionSelectorService _versionSelector;
        private readonly string _version;
        private bool _isValidated;

        // Immutable cached properties (set during Validate())
        private string? _versionPath;
        private string? _manifestPath;
        private string? _npmExecutablePath;
        private string? _npmServerScriptPath;
        private string? _workingDirectory;

        // Mutable runtime flags
        private bool _isDebugMode;
        private bool _enableTelemetry;
        private string _logLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="BridgeConfiguration"/> class.
        /// 
        /// Constructor caches the version string but defers filesystem validation to <see cref="Validate()"/>.
        /// This allows tests to mock missing paths without throwing during construction.
        /// </summary>
        /// <param name="version">The semantic version string (e.g., "2.0.0") from <see cref="VersionManager.GetActiveVersion()"/>.
        /// Must be non-empty; validation is deferred.</param>
        /// <param name="versionSelector">Optional <see cref="VersionSelectorService"/> for version discovery.
        /// If null, a new instance is created. Provided for testing/mocking.</param>
        /// <param name="debugMode">Initial debug mode flag. Mutable via <see cref="IsDebugMode"/> property.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="version"/> is null.</exception>
        public BridgeConfiguration(string version, VersionSelectorService? versionSelector = null, bool debugMode = false)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            _versionSelector = versionSelector ?? new VersionSelectorService();
            _isValidated = false;
            _isDebugMode = debugMode;
            _enableTelemetry = true; // Default: telemetry enabled
            _logLevel = "info"; // Default: info level
        }

        /// <summary>
        /// Validates all core configuration properties and resolves filesystem paths.
        /// 
        /// This method:
        /// - Verifies version string is non-empty
        /// - Resolves VersionPath using version string (src/versions/v{version})
        /// - Verifies VersionPath directory exists
        /// - Verifies ManifestPath file exists ({VersionPath}/manifest.json)
        /// - Resolves NpmExecutablePath (hardcoded defaults + PATH fallback)
        /// - Verifies NpmExecutablePath is executable
        /// - Resolves NpmServerScriptPath ({VersionPath}/core-server.js)
        /// - Verifies NpmServerScriptPath file exists
        /// - Sets WorkingDirectory = VersionPath
        /// - Sets IsCoreValid = true on success
        /// 
        /// After successful validation, all immutable properties are safe to access.
        /// Step 19 (StdioTransport) checks IsCoreValid before proceeding.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if version string is empty or any path validation fails.</exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(_version))
            {
                throw new InvalidOperationException("Version string is empty. Cannot validate configuration.");
            }

            // Resolve VersionPath: src/versions/v{version}
            var versionPath = ResolveVersionPath(_version);
            if (!Directory.Exists(versionPath))
            {
                throw new InvalidOperationException($"Version directory does not exist: {versionPath}");
            }

            // Verify ManifestPath exists
            var manifestPath = Path.Combine(versionPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException($"Manifest file not found: {manifestPath}");
            }

            // Resolve NpmExecutablePath
            var npmExecutablePath = ResolveNpmExecutablePath();
            if (string.IsNullOrEmpty(npmExecutablePath) || !File.Exists(npmExecutablePath))
            {
                throw new InvalidOperationException($"npm executable not found. Searched: npm.cmd, npm, and PATH environment variable.");
            }

            // Resolve NpmServerScriptPath
            var npmServerScriptPath = Path.Combine(versionPath, "core-server.js");
            if (!File.Exists(npmServerScriptPath))
            {
                throw new InvalidOperationException($"core-server.js not found: {npmServerScriptPath}");
            }

            // Cache all resolved properties
            _versionPath = versionPath;
            _manifestPath = manifestPath;
            _npmExecutablePath = npmExecutablePath;
            _npmServerScriptPath = npmServerScriptPath;
            _workingDirectory = versionPath; // Working directory = version path
            _isValidated = true;
        }

        /// <summary>
        /// Gets the semantic version of the Continue bridge (e.g., "2.0.0").
        /// 
        /// Returns the version string passed to the constructor.
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string Version
        {
            get
            {
                ThrowIfNotValidated();
                return _version;
            }
        }

        /// <summary>
        /// Gets the absolute filesystem path to the version directory (e.g., "src/versions/v2.0.0").
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string VersionPath
        {
            get
            {
                ThrowIfNotValidated();
                return _versionPath!;
            }
        }

        /// <summary>
        /// Gets the absolute filesystem path to the manifest.json file for this version.
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string ManifestPath
        {
            get
            {
                ThrowIfNotValidated();
                return _manifestPath!;
            }
        }

        /// <summary>
        /// Gets the absolute filesystem path to the npm executable.
        /// 
        /// On Windows: "npm.cmd" (batch wrapper)
        /// On Unix: "npm" (shell script)
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string NpmExecutablePath
        {
            get
            {
                ThrowIfNotValidated();
                return _npmExecutablePath!;
            }
        }

        /// <summary>
        /// Gets the absolute filesystem path to the core-server.js entry point script.
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string NpmServerScriptPath
        {
            get
            {
                ThrowIfNotValidated();
                return _npmServerScriptPath!;
            }
        }

        /// <summary>
        /// Gets the working directory for the npm process (typically equals VersionPath).
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public string WorkingDirectory
        {
            get
            {
                ThrowIfNotValidated();
                return _workingDirectory!;
            }
        }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for the npm process to start.
        /// Default: 5000ms (5 seconds).
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public long ProcessStartupTimeoutMs
        {
            get
            {
                ThrowIfNotValidated();
                return DefaultProcessStartupTimeoutMs;
            }
        }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for a single RPC request/response cycle.
        /// Default: 30000ms (30 seconds).
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public long RpcTimeoutMs
        {
            get
            {
                ThrowIfNotValidated();
                return DefaultRpcTimeoutMs;
            }
        }

        /// <summary>
        /// Gets the maximum time (in milliseconds) to wait for graceful process shutdown.
        /// Default: 3000ms (3 seconds).
        /// 
        /// Throws InvalidOperationException if accessed before Validate() completes.
        /// </summary>
        public long ShutdownTimeoutMs
        {
            get
            {
                ThrowIfNotValidated();
                return DefaultShutdownTimeoutMs;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug mode is enabled for the bridge.
        /// 
        /// Mutable at runtime. Can be toggled after construction without restarting the transport.
        /// Default: false (or value passed to constructor).
        /// </summary>
        public bool IsDebugMode
        {
            get => _isDebugMode;
            set => _isDebugMode = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether telemetry collection is enabled.
        /// 
        /// Mutable at runtime. Can be toggled after construction.
        /// Default: true.
        /// </summary>
        public bool EnableTelemetry
        {
            get => _enableTelemetry;
            set => _enableTelemetry = value;
        }

        /// <summary>
        /// Gets or sets the logging level for the bridge (e.g., "error", "warn", "info", "debug", "trace").
        /// 
        /// Mutable at runtime. Changes apply to subsequent log calls.
        /// Default: "info".
        /// </summary>
        public string LogLevel
        {
            get => _logLevel;
            set => _logLevel = value ?? "info";
        }

        /// <summary>
        /// Gets a value indicating whether the core properties of this configuration are valid
        /// and safe to use for transport initialization.
        /// 
        /// Returns true if Validate() has completed successfully without throwing.
        /// Returns false if Validate() has not yet been called or failed.
        /// </summary>
        public bool IsCoreValid => _isValidated;

        // === Private Helper Methods ===

        /// <summary>
        /// Throws InvalidOperationException if validation has not been completed.
        /// Used by property getters to enforce lazy validation semantics.
        /// </summary>
        private void ThrowIfNotValidated()
        {
            if (!_isValidated)
            {
                throw new InvalidOperationException(
                    "Configuration has not been validated. Call Validate() before accessing core properties.");
            }
        }

        /// <summary>
        /// Resolves the version string to the absolute filesystem path of the version directory.
        /// 
        /// Pattern: src/versions/v{version}
        /// Example: "2.0.0" → "C:\...\src\versions\v2.0.0"
        /// </summary>
        private string ResolveVersionPath(string version)
        {
            // Get the base directory where this assembly is located
            var assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyPath);

            if (string.IsNullOrEmpty(assemblyDir))
            {
                throw new InvalidOperationException("Cannot determine assembly directory.");
            }

            // Navigate to src/versions/v{version}
            // Assembly is typically in: bin/Debug/net472 or similar
            // Need to go up to solution root, then into src/versions/v{version}
            var rootPath = Path.GetFullPath(Path.Combine(assemblyDir, "..\\..\\..\\.."));
            var versionPath = Path.Combine(rootPath, "src", "versions", $"v{version}");

            return versionPath;
        }

        /// <summary>
        /// Resolves the npm executable path using hardcoded defaults and PATH environment variable.
        /// 
        /// Resolution strategy (in order):
        /// 1. On Windows: try "npm.cmd" in PATH
        /// 2. Try "npm" in PATH
        /// 3. Return "npm" as fallback (may not exist, will be caught by Validate())
        /// 
        /// This approach provides predictability: npm is expected to be in PATH on all platforms.
        /// </summary>
        private string? ResolveNpmExecutablePath()
        {
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);

            // Get PATH environment variable
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathDirs = pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

            // On Windows, try npm.cmd first
            if (isWindows)
            {
                var npmCmd = FindExecutableInPath("npm.cmd", pathDirs);
                if (!string.IsNullOrEmpty(npmCmd))
                {
                    return npmCmd;
                }
            }

            // Try npm on all platforms
            var npm = FindExecutableInPath("npm", pathDirs);
            if (!string.IsNullOrEmpty(npm))
            {
                return npm;
            }

            // Fallback: return npm (will be validated in Validate())
            return isWindows ? "npm.cmd" : "npm";
        }

        /// <summary>
        /// Searches for an executable in the PATH directories.
        /// </summary>
        private string? FindExecutableInPath(string executableName, string[] pathDirs)
        {
            foreach (var dir in pathDirs)
            {
                var fullPath = Path.Combine(dir, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}
