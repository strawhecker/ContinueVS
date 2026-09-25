using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Core, unit-testable logic for driving the real <c>EnvDTE.Debugger</c> automation object
    /// (gap92_1). Separated from <see cref="DebuggerService"/> so every DTE interaction can be
    /// tested against a fake <c>Debugger</c> without a live Visual Studio process.
    /// </summary>
    /// <remarks>
    /// All methods in this helper access <c>EnvDTE.Debugger</c> and must therefore be invoked
    /// on the UI thread. The only caller is <see cref="DebuggerService"/>, which marshals to the
    /// UI thread via <c>ThreadHelper</c> before delegating here. The VSTHRD010 suppressions below
    /// are justified by that guaranteed UI-thread contract.
    /// </remarks>
    internal static class DebuggerInterop
    {
        public const int MaxLocals = 100;
        public const int MaxCallStackDepth = 50;

        /// <summary>
        /// Whether a live debug session (run or break) is currently active.
        /// Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static bool IsActive(EnvDTE.Debugger? debugger)
        {
            return DebugStateGuard.IsDebuggerLive(debugger);
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Whether the debugger is currently paused at a breakpoint/statement. Used by the resume
        /// wait loop to detect that a <c>Go()</c> has actually left break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static bool IsPaused(EnvDTE.Debugger? debugger)
        {
            return DebugStateGuard.IsDebuggerPaused(debugger);
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Builds the current <see cref="RuntimeState"/> (callstack + locals + position) from the
        /// live debugger. Returns the benign empty <see cref="RuntimeState"/> when not debugging.
        /// Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static RuntimeState BuildState(EnvDTE.Debugger? debugger)
        {
            if (!DebugStateGuard.IsDebuggerLive(debugger))
                return EmptyState();

            var paused = debugger!.CurrentMode == DebugStateGuard.BreakMode;
            var state = new RuntimeState
            {
                IsRunning = !paused,
                CapturedAt = DateTime.Now
            };

            // Callstack: EnvDTE exposes the stack frames per-thread (Debugger.CurrentThread.StackFrames).
            // We cap depth so CollectDebugState stays responsive on large stacks.
            try
            {
                var thread = debugger.CurrentThread;
                var frames = thread?.StackFrames;
                if (frames != null)
                {
                    var count = frames.Count;
                    var depth = count > MaxCallStackDepth ? MaxCallStackDepth : count;
                    for (int i = 0; i < depth; i++)
                    {
                        try
                        {
                            var frame = frames.Item(i + 1);
                            state.CallStack.Add(new CallStackFrame
                            {
                                MethodName = frame.FunctionName ?? "method",
                                FrameIndex = i
                            });
                        }
                        catch (COMException) { break; }
                        catch { break; }
                    }
                }
            }
            catch { }

            if (paused)
            {
                state.CurrentLine = ReadCurrentLine(debugger);
            }

            return state;
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Reads the pause location (file + line) from the last-hit breakpoint, which is the
        /// source-position truth when paused. Falls back to the current stack frame function name.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        private static int ReadCurrentLine(EnvDTE.Debugger debugger)
        {
            try
            {
                var hit = debugger.BreakpointLastHit;
                if (hit != null)
                    return hit.FileLine;
            }
            catch { }
            return 0;
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Collects the current frame's local variables (name → stringified value), capped at
        /// <see cref="MaxLocals"/>. Reads each <c>Expression</c> defensively so a single failing
        /// local does not abort the whole scan. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static Dictionary<string, string> CollectLocals(EnvDTE.Debugger? debugger)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (debugger == null || !DebugStateGuard.IsDebuggerPaused(debugger))
                return result;

            try
            {
                var frame = debugger.CurrentStackFrame;
                var locals = frame?.Locals;
                if (locals == null)
                    return result;

                foreach (EnvDTE.Expression local in locals)
                {
                    if (result.Count >= MaxLocals)
                        break;
                    try
                    {
                        var name = local.Name ?? "?local";
                        var value = (local.Type ?? string.Empty) == string.Empty
                            ? (local.Value ?? "?")
                            : $"{local.Type} = {local.Value ?? "?"}";
                        result[name] = value;
                    }
                    catch (COMException) { }
                    catch { }
                }
            }
            catch { }
            return result;
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Adds a breakpoint on <paramref name="filePath"/> at <paramref name="lineNumber"/> with an
        /// optional <paramref name="condition"/>. Returns the created <see cref="BreakpointInfo"/>, or
        /// <c>null</c> if the breakpoint could not be added. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static BreakpointInfo? SetBreakpoint(EnvDTE.Debugger? debugger, string filePath, int lineNumber, string? condition)
        {
            if (debugger?.Breakpoints == null)
                return null;

            try
            {
                var bpType = string.IsNullOrEmpty(condition)
                    ? EnvDTE.dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue
                    : EnvDTE.dbgBreakpointConditionType.dbgBreakpointConditionTypeWhenTrue;

                debugger.Breakpoints.Add(
                    Function: string.Empty,
                    File: filePath,
                    Line: lineNumber,
                    Column: 1,
                    Condition: condition ?? string.Empty,
                    ConditionType: bpType,
                    Language: "CSharp");

                // Add() returns the whole Breakpoints collection; re-find our breakpoint by file+line.
                foreach (EnvDTE.Breakpoint bp in debugger.Breakpoints)
                {
                    try
                    {
                        if (string.Equals(bp.File, filePath, StringComparison.OrdinalIgnoreCase) &&
                            bp.FileLine == lineNumber)
                        {
                            return new BreakpointInfo
                            {
                                FilePath = bp.File,
                                LineNumber = bp.FileLine,
                                IsEnabled = bp.Enabled,
                                HitCount = ReadHitCount(bp),
                                Condition = string.IsNullOrEmpty(bp.Condition) ? condition : bp.Condition,
                                BreakpointId = bp.Name
                            };
                        }
                    }
                    catch (COMException) { }
                }

                // Breakpoint not re-found (may have no source column yet); still report created.
                return new BreakpointInfo
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    IsEnabled = true,
                    Condition = condition,
                    BreakpointId = $"{filePath}:{lineNumber}"
                };
            }
            catch
            {
                return null;
            }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Deletes all breakpoints at <paramref name="filePath"/>:<paramref name="lineNumber"/>.
        /// Returns true if at least one was removed. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static bool ClearBreakpoint(EnvDTE.Debugger? debugger, string filePath, int lineNumber)
        {
            if (debugger?.Breakpoints == null)
                return false;

            bool cleared = false;
            try
            {
                foreach (EnvDTE.Breakpoint bp in debugger.Breakpoints)
                {
                    try
                    {
                        if (string.Equals(bp.File, filePath, StringComparison.OrdinalIgnoreCase) &&
                            bp.FileLine == lineNumber)
                        {
                            bp.Delete();
                            cleared = true;
                        }
                    }
                    catch (COMException) { }
                }
            }
            catch { }
            return cleared;
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Maps a <see cref="DebugStepAction"/> to the corresponding EnvDTE debugger command and
        /// issues it. Requires the debugger to be paused. Returns the re-read state afterward.
        /// Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static RuntimeState ExecuteStep(EnvDTE.Debugger? debugger, DebugStepAction action)
        {
            if (debugger == null || !DebugStateGuard.IsDebuggerPaused(debugger))
                return EmptyState();

            try
            {
                switch (action)
                {
                    case DebugStepAction.StepOver:
                        debugger.StepOver(true);
                        break;
                    case DebugStepAction.StepInto:
                        debugger.StepInto(true);
                        break;
                    case DebugStepAction.StepOut:
                        debugger.StepOut(true);
                        break;
                    case DebugStepAction.Continue:
                        debugger.Go(false);
                        break;
                    case DebugStepAction.Pause:
                        debugger.Break(true);
                        break;
                }
            }
            catch { }

            return BuildState(debugger);
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Resumes execution (<c>Debugger.Go(false)</c>) when paused. Returns true if the resume
        /// command was issued. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static bool ResumeExecution(EnvDTE.Debugger? debugger)
        {
            if (debugger == null || !DebugStateGuard.IsDebuggerPaused(debugger))
                return false;
            try
            {
                debugger.Go(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
#pragma warning restore VSTHRD010

#pragma warning disable VSTHRD010 // DTE access; only called within UI-thread-guaranteed DebuggerInterop methods
        private static int ReadHitCount(EnvDTE.Breakpoint bp)
        {
            try { return bp.CurrentHits; }
            catch { return 0; }
        }
#pragma warning restore VSTHRD010

        public static RuntimeState EmptyState()
        {
            return new RuntimeState
            {
                IsRunning = false,
                CurrentFile = null,
                CurrentLine = 0,
                CapturedAt = DateTime.Now
            };
        }
    }
}
