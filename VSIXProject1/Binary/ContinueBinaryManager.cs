using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Binary
{
    /// <summary>
    /// Manages the lifecycle of the continue-binary child process.
    ///
    /// Call <see cref="StartAsync"/> once from package initialization.
    /// The binary writes its listening port to stdout; this class parses it and
    /// exposes it via <see cref="Port"/>.  Crash-restart is handled automatically.
    /// </summary>
    internal sealed class ContinueBinaryManager : IDisposable
    {
        // -----------------------------------------------------------------
        // Constants
        // -----------------------------------------------------------------
        private static readonly string CacheDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "ContinueVS");

        private static readonly string BinaryPath =
            Path.Combine(CacheDir, "continue-binary.exe");

        // The binary prints a line like: "Listening on port 65432"
        private static readonly Regex PortPattern =
            new Regex(@"Listening on port (\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // -----------------------------------------------------------------
        // State
        // -----------------------------------------------------------------
        private Process?              _process;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _startLock = new SemaphoreSlim(1, 1);
        private bool                  _disposed;

        /// <summary>TCP port the binary is listening on; 0 until the binary is ready.</summary>
        public int Port { get; private set; }

        /// <summary>Fires once the port is known and the binary is accepting connections.</summary>
        public event EventHandler<int>? Ready;

        /// <summary>Fires when the binary exits unexpectedly.</summary>
        public event EventHandler? Crashed;

        // -----------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Downloads the binary if needed, then starts it and waits until the port is
        /// reported.  Safe to call multiple times (subsequent calls are no-ops while
        /// the process is running).
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

        /// <summary>Stops the binary process if it is running.</summary>
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
            Port = 0;

            var psi = new ProcessStartInfo(BinaryPath)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                WorkingDirectory       = CacheDir,
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;

            _process.Start();

            // Read stdout asynchronously until we see the port line.
            var portFound = new TaskCompletionSource<int>();
            _cts.Token.Register(() => portFound.TrySetCanceled());

            _ = Task.Run(async () =>
            {
                try
                {
                    string? line;
                    while ((line = await _process.StandardOutput.ReadLineAsync()) != null)
                    {
                        var m = PortPattern.Match(line);
                        if (m.Success)
                        {
                            portFound.TrySetResult(int.Parse(m.Groups[1].Value));
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    portFound.TrySetException(ex);
                }
            }, _cts.Token);

            // Give the binary up to 30 s to report its port.
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var combined = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeout.Token))
            {
                combined.Token.Register(() => portFound.TrySetCanceled());
                Port = await portFound.Task;
            }

            Ready?.Invoke(this, Port);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            if (_disposed || _cts?.IsCancellationRequested == true)
                return;

            Crashed?.Invoke(this, EventArgs.Empty);

            // Auto-restart after 3 s.
            _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(
                _ => StartAsync(CancellationToken.None),
                TaskScheduler.Default);
        }

        private void TryKillProcess()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                    _process.Kill();
            }
            catch { /* best-effort */ }
        }

        private static async Task<IVsStatusbar?> TryGetStatusBarAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            }
            catch
            {
                return null;
            }
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
