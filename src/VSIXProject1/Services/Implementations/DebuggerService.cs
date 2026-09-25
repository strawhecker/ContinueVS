using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Visual Studio implementation of IDebuggerService (gap92_1).
    /// Resolves the real <c>EnvDTE.Debugger</c> via <see cref="IDteProvider.GetDebugger"/> and marshals
    /// every DTE interaction to the UI thread, delegating all DTE-touching logic to the unit-testable
    /// <see cref="DebuggerInterop"/> helper. Fail-soft throughout: when the debugger is idle or a COM
    /// call fails, methods return the benign empty state rather than throwing.
    /// </summary>
    internal class DebuggerService : IDebuggerService
    {
        private readonly IDteProvider _dteProvider;
        private readonly ITimeoutHelper _timeoutHelper;
        private const int ResumeTimeoutSeconds = 30;

        public DebuggerService(IDteProvider dteProvider, ITimeoutHelper timeoutHelper)
        {
            _dteProvider = dteProvider ?? throw new ArgumentNullException(nameof(dteProvider));
            _timeoutHelper = timeoutHelper ?? throw new ArgumentNullException(nameof(timeoutHelper));
        }

        public async Task<RuntimeState?> GetCurrentStateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                return DebuggerInterop.BuildState(debugger);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return DebuggerInterop.EmptyState();
            }
        }

        public async Task<bool> IsDebuggerActiveAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                return DebuggerInterop.IsActive(debugger);
            }
            catch
            {
                return false;
            }
        }

        public async Task<BreakpointInfo?> SetBreakpointAsync(string filePath, int lineNumber, string? condition = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must not be empty.", nameof(filePath));
            if (lineNumber <= 0)
                throw new ArgumentException("lineNumber must be positive.", nameof(lineNumber));

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                return DebuggerInterop.SetBreakpoint(debugger, filePath, lineNumber, condition);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ClearBreakpointAsync(string filePath, int lineNumber, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath must not be empty.", nameof(filePath));
            if (lineNumber <= 0)
                throw new ArgumentException("lineNumber must be positive.", nameof(lineNumber));

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                return DebuggerInterop.ClearBreakpoint(debugger, filePath, lineNumber);
            }
            catch
            {
                return false;
            }
        }

        public async Task<RuntimeState?> ExecuteStepAsync(DebugStepAction action, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                return DebuggerInterop.ExecuteStep(debugger, action);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return DebuggerInterop.EmptyState();
            }
        }

        private static bool IsPausedSafe(EnvDTE.Debugger? debugger)
        {
            bool paused = false;
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                paused = DebuggerInterop.IsPaused(debugger);
            });
            return paused;
        }

        public async Task ResumeExecutionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(ResumeTimeoutSeconds));

                // Issue the resume command on the UI thread.
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var debugger = _dteProvider.GetDebugger();
                DebuggerInterop.ResumeExecution(debugger);

                // Wait for the debugger to leave break mode after Go(), honoring the timeout.
                // Accessing CurrentMode here is only safe on the UI thread; resume-wait re-checks
                // via the interop helper which is called infrequently during the wait loop.
                while (IsPausedSafe(debugger))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _timeoutHelper.DelayAsync(TimeSpan.FromMilliseconds(100), cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Execution did not resume within 30 seconds.", new OperationCanceledException());
            }
        }
    }

    /// <summary>
    /// Helper interface for timeout management (can be mocked in tests).
    /// </summary>
    public interface ITimeoutHelper
    {
        Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Default implementation of ITimeoutHelper.
    /// </summary>
    internal class TimeoutHelper : ITimeoutHelper
    {
        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            return Task.Delay(duration, cancellationToken);
        }
    }
}
