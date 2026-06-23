using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Manages the lifecycle of the continue-binary child process.
    ///
    /// The binary communicates via stdio IPC (stdin/stdout, \r\n-delimited JSON).
    /// No TCP port is involved in production mode.
    ///
    /// Call <see cref="StartAsync"/> once.  Subscribe to <see cref="Ready"/> to
    /// receive the <see cref="Process"/> whose stdin/stdout are the IPC channel.
    /// </summary>
    internal sealed class ContinueBinaryManager : IDisposable
    {
        // -----------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------
        private static readonly string CacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ContinueVS");

        internal static readonly string BinaryPath =
            Path.Combine(CacheDir, "continue-binary.exe");

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------
        private Process?              _process;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _startLock = new SemaphoreSlim(1, 1);
        private bool                  _disposed;

        /// <summary>
        /// Fires once the process is running and stdin/stdout are available.
        /// The event argument is the <see cref="Process"/> object.
        /// </summary>
        public event EventHandler<Process>? Ready;

        /// <summary>Fires when the binary exits unexpectedly.</summary>
        public event EventHandler? Crashed;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Downloads the binary if needed, then starts it.
        /// Safe to call multiple times while the process is already running.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _startLock.WaitAsync(cancellationToken);
            try
            {
                if (_process != null && !_process.HasExited)
                    return;

                var statusBar = await TryGetStatusBarAsync();
                await BinaryDownloader.EnsureAsync(BinaryPath, statusBar, cancellationToken);
                await LaunchAsync(cancellationToken);
            }
            finally
            {
                _startLock.Release();
            }
        }

        /// <summary>Stops the binary process if running.</summary>
        public void Stop()
        {
            _cts?.Cancel();
            TryKillProcess();
        }

        // -----------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------

        private async Task LaunchAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var psi = new ProcessStartInfo(BinaryPath)
            {
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                WorkingDirectory       = CacheDir,
                // In production mode (no CONTINUE_DEVELOPMENT) the binary uses
                // stdio IPC — stdout is pure JSON, logs go to ~/.continue/core.log
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;
            _process.Start();

            // The binary sets up its Core synchronously after startup.
            // Give it a moment to initialise before we start sending messages.
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            Ready?.Invoke(this, _process);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            if (_disposed || _cts?.IsCancellationRequested == true) return;

            Crashed?.Invoke(this, EventArgs.Empty);

            // Auto-restart after 3 s.
            _ = Task.Delay(TimeSpan.FromSeconds(3))
                    .ContinueWith(_ => StartAsync(CancellationToken.None),
                                  TaskScheduler.Default);
        }

        private void TryKillProcess()
        {
            try { if (_process != null && !_process.HasExited) _process.Kill(); }
            catch { /* best-effort */ }
        }

        private static async Task<IVsStatusbar?> TryGetStatusBarAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            }
            catch { return null; }
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _process?.Dispose();
            _startLock.Dispose();
            _cts?.Dispose();
        }
    }
}
