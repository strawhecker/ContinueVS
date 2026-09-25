using System;
using System.Runtime.InteropServices;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Guards debugger operations against invalid debugger state. Every
    /// <see cref="Services.Interfaces.IDebuggerService"/> method validates state through this
    /// guard BEFORE touching <c>EnvDTE.Debugger</c>, returning a benign rejection
    /// (<c>{ ok: false, reason, state }</c>) instead of throwing an <see cref="InvalidOperationException"/>.
    /// This keeps consumers like <see cref="WorkspaceStatsService"/>'s state collection non-fatal
    /// when the debugger is idle.
    /// </summary>
    public static class DebugStateGuard
    {
        /// <summary>
        /// The mode when the debugger is running (not paused).
        /// </summary>
        public const EnvDTE.dbgDebugMode RunMode = EnvDTE.dbgDebugMode.dbgRunMode;

        /// <summary>
        /// The mode when the debugger is paused at a breakpoint/statement.
        /// </summary>
        public const EnvDTE.dbgDebugMode BreakMode = EnvDTE.dbgDebugMode.dbgBreakMode;

        /// <summary>
        /// True when the debugger is in a live (run or break) mode.
        /// Never throws; a COM access failure yields false.
        /// </summary>
#pragma warning disable VSTHRD010 // DTE access; only ever invoked on the UI thread via DebuggerService
        public static bool IsDebuggerLive(EnvDTE.Debugger? debugger)
        {
            if (debugger == null)
                return false;
            try
            {
                var mode = debugger.CurrentMode;
                return mode == RunMode || mode == BreakMode;
            }
            catch (COMException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True when the debugger is paused at a breakpoint (break mode), the only state in which
        /// frame/locals/step/evaluate operations are meaningful. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // DTE access; only ever invoked on the UI thread via DebuggerService
        public static bool IsDebuggerPaused(EnvDTE.Debugger? debugger)
        {
            if (debugger == null)
                return false;
            try
            {
                return debugger.CurrentMode == BreakMode;
            }
            catch (COMException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Builds a benign "not debugging / can't act" <see cref="DebugSessionState"/> echo for a
        /// guarded call that cannot proceed.
        /// </summary>
        public static DebugSessionState NotActiveState(string reason)
        {
            return new DebugSessionState
            {
                Mode = "none",
                IsDebuggerActive = false,
                Frame = null,
                ThreadId = 0,
                BreakReason = reason
            };
        }

        /// <summary>
        /// Gates an inspection call (gap92_2) against the required <see cref="InspectionGate"/>, returning
        /// a benign <c>(Ok, State, Reason)</c> triple instead of throwing. When the gate is satisfied the
        /// returned state is the live captured <see cref="DebugSessionState"/>; otherwise it is a benign
        /// rejection echo with a reason such as <c>"requires-break-mode"</c> or <c>"debugger-not-active"</c>.
        /// Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // DTE access; only ever invoked on the UI thread via DebuggerService
        public static (bool Ok, DebugSessionState State, string? Reason) RequireInspection(EnvDTE.Debugger? debugger, InspectionGate gate)
        {
            if (!IsDebuggerLive(debugger))
                return (false, NotActiveState("debugger-not-active"), "debugger-not-active");

            var state = CaptureState(debugger);

            if (gate == InspectionGate.Paused && !IsDebuggerPaused(debugger))
                return (false, state, "requires-break-mode");

            return (true, state, null);
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Captures the current live debugger state into a <see cref="DebugSessionState"/> echo.
        /// Never throws; any COM failure yields a <see cref="NotActiveState"/>.
        /// </summary>
#pragma warning disable VSTHRD010 // DTE access; only ever invoked on the UI thread via DebuggerService
        public static DebugSessionState CaptureState(EnvDTE.Debugger? debugger)
        {
            if (!IsDebuggerLive(debugger))
                return NotActiveState("debugger-not-active");

            try
            {
                var mode = debugger!.CurrentMode;
                var paused = mode == BreakMode;
                var currentProgram = debugger.CurrentProgram;

                var state = new DebugSessionState
                {
                    Mode = paused ? "break" : "run",
                    IsDebuggerActive = true,
                    IsActiveProgram = currentProgram != null,
                    BreakReason = paused ? "breakpoint" : null
                };

                // Selected/current thread id (best effort)
                try
                {
                    if (debugger.CurrentThread != null)
                        state.ThreadId = debugger.CurrentThread.ID;
                }
                catch (COMException) { }

                // Selected/current frame name (best effort)
                try
                {
                    var frame = debugger.CurrentStackFrame;
                    if (frame != null)
                        state.Frame = frame.FunctionName ?? "frame";
                }
                catch (COMException) { }

                return state;
            }
            catch (COMException)
            {
                return NotActiveState("debugger-com-failure");
            }
            catch
            {
                return NotActiveState("debugger-unavailable");
            }
#pragma warning restore VSTHRD010
        }
    }
}
