using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.IPC
{
    /// <summary>
    /// Manages the lifecycle of the Continue npm bridge subprocess.
    /// 
    /// Responsibilities:
    /// - Spawn the npm process with configured parameters (working directory, environment, etc.)
    /// - Initialize stdin/stdout streams for JSON-RPC communication
    /// - Handle graceful shutdown (SIGTERM with timeout) followed by forced kill if necessary
    /// - Provide clean separation between OS-level process management and transport-level concerns
    /// 
    /// Invariants:
    /// - All public methods are reentrant; concurrent calls to Start/Stop are serialized by the caller
    /// - The Process object is disposed when the manager is disposed or when the process terminates
    /// - Streams (stdin, stdout) are obtained once during Start and remain valid until StopAsync or process death
    /// </summary>
    internal sealed class ProcessManager : IDisposable
    {
        private readonly IBridgeConfiguration _configuration;
        private readonly ProcessStartInfo _startInfo;
        private Process _process;
        private StreamWriter _stdinWriter;
        private StreamReader _stdoutReader;
        private bool _isDisposed;

        /// <summary>
        /// Gets a value indicating whether a process is currently running.
        /// </summary>
        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Gets the underlying Process object, or null if not started.
        /// </summary>
        public Process Process => _process;

        /// <summary>
        /// Gets the stdin writer stream, or null if not started.
        /// </summary>
        public StreamWriter StdinWriter => _stdinWriter;

        /// <summary>
        /// Gets the stdout reader stream, or null if not started.
        /// </summary>
        public StreamReader StdoutReader => _stdoutReader;

        public ProcessManager(IBridgeConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Build ProcessStartInfo for the npm server
            _startInfo = new ProcessStartInfo
            {
                FileName = configuration.NpmExecutablePath,
                Arguments = "start",
                WorkingDirectory = configuration.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Optional: set environment variables for debug mode
            if (configuration.IsDebugMode)
            {
                _startInfo.EnvironmentVariables["CONTINUE_DEBUG"] = "1";
                _startInfo.EnvironmentVariables["CONTINUE_LOG_LEVEL"] = configuration.LogLevel;
            }
        }

        /// <summary>
        /// Starts the Continue npm process with the configured parameters.
        /// </summary>
        /// <returns>The started Process object.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the process fails to start or is already running.</exception>
        public Process Start()
        {
            if (IsRunning)
                throw new InvalidOperationException("Process is already running.");

            try
            {
                _process = Process.Start(_startInfo);
                if (_process == null)
                    throw new InvalidOperationException("Process.Start() returned null.");

                // Obtain and cache the streams
                _stdinWriter = _process.StandardInput;
                _stdoutReader = _process.StandardOutput;

                return _process;
            }
            catch (Exception ex)
            {
                _process?.Dispose();
                _process = null;
                _stdinWriter = null;
                _stdoutReader = null;
                throw new InvalidOperationException($"Failed to start Continue npm process: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gracefully stops the process by sending SIGTERM, waiting for shutdown, then forcibly killing if needed.
        /// </summary>
        /// <param name="shutdownTimeoutMs">Maximum time to wait for graceful shutdown before force-killing.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StopAsync(long shutdownTimeoutMs)
        {
            if (!IsRunning)
                return;

            try
            {
                // Close streams first to signal EOF to the process
                _stdinWriter?.Close();
                _stdoutReader?.Close();

                // Attempt graceful shutdown
                if (!_process.HasExited)
                {
                    try
                    {
                        // Kill sends SIGTERM on Unix and TerminateProcess on Windows
                        _process.Kill();
                    }
                    catch (Exception ex)
                    {
                        // Fallback: process may have already exited
                        System.Diagnostics.Debug.WriteLine($"Error killing process: {ex.Message}");
                    }
                }

                // Wait for process to exit with timeout
                bool exited = await Task.Run(() => _process.WaitForExit((int)shutdownTimeoutMs));

                if (!exited && !_process.HasExited)
                {
                    // Force kill if still alive
                    try
                    {
                        _process.Kill();
                        _process.WaitForExit(1000); // Give it 1 second to clean up
                    }
                    catch { /* Already dead or permission issue */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during graceful shutdown: {ex.Message}");
            }
            finally
            {
                _stdinWriter?.Dispose();
                _stdoutReader?.Dispose();
                _process?.Dispose();
                _stdinWriter = null;
                _stdoutReader = null;
                _process = null;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                _stdinWriter?.Dispose();
                _stdoutReader?.Dispose();
                _process?.Dispose();
            }
            catch { /* Best effort */ }
        }
    }
}
