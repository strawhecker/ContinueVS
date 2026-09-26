using System;
using System.Collections.Generic;
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

        // -----------------------------------------------------------------------
        // gap92_2 — Inspection surface (Tier-0, read-only, guard-gated)
        // Every method marshals to the UI thread, gates via DebugStateGuard, delegates to
        // DebuggerInterop, and echoes state. Never throws (operations cancel-safe).
        // -----------------------------------------------------------------------

        public async Task<DebugInspectionResult<List<CallStackFrame>>> GetCallStackAsync(DebugSessionState state, int threadId, int maxFrames, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<List<CallStackFrame>>.Rejected(live, reason!);
                return DebuggerInterop.GetCallStack(_dteProvider.GetDebugger(), live, threadId, maxFrames);
            }
            catch { return DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> SelectFrameAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.SelectFrame(_dteProvider.GetDebugger(), live, threadId, frameIndex);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<StatementInfo>> GetStatementAsync(DebugSessionState state, int threadId, int frameIndex, int contextLines, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<StatementInfo>.Rejected(live, reason!);
                return DebuggerInterop.GetStatement(_dteProvider.GetDebugger(), live, threadId, frameIndex, contextLines);
            }
            catch { return DebugInspectionResult<StatementInfo>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<List<VariableInfo>>> GetLocalsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<List<VariableInfo>>.Rejected(live, reason!);
                return DebuggerInterop.GetLocals(_dteProvider.GetDebugger(), live, threadId, frameIndex);
            }
            catch { return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<List<VariableInfo>>> GetArgumentsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<List<VariableInfo>>.Rejected(live, reason!);
                return DebuggerInterop.GetArguments(_dteProvider.GetDebugger(), live, threadId, frameIndex);
            }
            catch { return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<VariableInfo>> GetThisAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<VariableInfo>.Rejected(live, reason!);
                return DebuggerInterop.GetThis(_dteProvider.GetDebugger(), live, threadId, frameIndex);
            }
            catch { return DebugInspectionResult<VariableInfo>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<List<ThreadInfo>>> GetThreadsAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Any);
                if (!ok) return DebugInspectionResult<List<ThreadInfo>>.Rejected(live, reason!);
                return DebuggerInterop.GetThreads(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<List<ThreadInfo>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<List<ModuleInfo>>> GetModulesAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Any);
                if (!ok) return DebugInspectionResult<List<ModuleInfo>>.Rejected(live, reason!);
                return DebuggerInterop.GetModules(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<List<ModuleInfo>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<ProcessInfo>> GetProcessInfoAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Any);
                if (!ok) return DebugInspectionResult<ProcessInfo>.Rejected(live, reason!);
                return DebuggerInterop.GetProcessInfo(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<ProcessInfo>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<List<ExceptionSettingInfo>>> ListExceptionSettingsAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Any);
                if (!ok) return DebugInspectionResult<List<ExceptionSettingInfo>>.Rejected(live, reason!);
                return DebuggerInterop.ListExceptionSettings(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<List<ExceptionSettingInfo>>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<ExceptionInfo>> GetCurrentExceptionAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<ExceptionInfo>.Rejected(live, reason!);
                return DebuggerInterop.GetCurrentException(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<ExceptionInfo>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<string>> GetOutputAsync(DebugSessionState state, int maxChars, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Any);
                if (!ok) return DebugInspectionResult<string>.Rejected(live, reason!);
                return DebuggerInterop.GetOutput(_dteProvider.GetDebugger(), live, maxChars);
            }
            catch { return DebugInspectionResult<string>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> EnableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.EnableBreakpoint(_dteProvider.GetDebugger(), live, breakpointId);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> DisableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.DisableBreakpoint(_dteProvider.GetDebugger(), live, breakpointId);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> ConditionBreakpointAsync(DebugSessionState state, string breakpointId, string condition, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.ConditionBreakpoint(_dteProvider.GetDebugger(), live, breakpointId, condition);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> ClearBreakpointByIdAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.ClearBreakpointById(_dteProvider.GetDebugger(), live, breakpointId);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        // -----------------------------------------------------------------------
        // gap92_3 — Evaluate / Mutate surface (Tier-2, gated)
        // Every method marshals to the UI thread, gates via DebugStateGuard (Paused),
        // delegates to DebuggerInterop, and echoes state. Never throws.
        // -----------------------------------------------------------------------

        public async Task<DebugInspectionResult<VariableInfo>> EvaluateAsync(DebugSessionState state, int threadId, int frameIndex, string expression, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<VariableInfo>.Rejected(live, reason!);
                return DebuggerInterop.EvaluateExpression(_dteProvider.GetDebugger(), live, threadId, frameIndex, expression);
            }
            catch { return DebugInspectionResult<VariableInfo>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> SetValueAsync(DebugSessionState state, int threadId, int frameIndex, string name, string value, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.SetValue(_dteProvider.GetDebugger(), live, threadId, frameIndex, name, value);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<string>> MemoryReadAsync(DebugSessionState state, string address, int length, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<string>.Rejected(live, reason!);
                return DebuggerInterop.MemoryRead(_dteProvider.GetDebugger(), live, address, length);
            }
            catch { return DebugInspectionResult<string>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> MemoryWriteAsync(DebugSessionState state, string address, string bytes, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.MemoryWrite(_dteProvider.GetDebugger(), live, address, bytes);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> FreezeThreadAsync(DebugSessionState state, int threadId, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.FreezeThread(_dteProvider.GetDebugger(), live, threadId);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> ThawThreadAsync(DebugSessionState state, int threadId, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.ThawThread(_dteProvider.GetDebugger(), live, threadId);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
        }

        public async Task<DebugInspectionResult<bool>> RunToCursorAsync(DebugSessionState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var (ok, live, reason) = DebugStateGuard.RequireInspection(_dteProvider.GetDebugger(), InspectionGate.Paused);
                if (!ok) return DebugInspectionResult<bool>.Rejected(live, reason!);
                return DebuggerInterop.RunToCursor(_dteProvider.GetDebugger(), live);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "inspection-unavailable"); }
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
