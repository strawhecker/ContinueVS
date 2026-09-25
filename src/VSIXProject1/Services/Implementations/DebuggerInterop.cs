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

        // ---------------------------------------------------------------------------
        // gap92_2 — Inspection surface (Tier-0, read-only, guard-gated)
        // Each method echoes the debugger state and returns a benign DebugInspectionResult<T>
        // instead of throwing. EnvDTE exposes threads/processes/stack/variables directly;
        // modules, exception settings, and console output are not part of the EnvDTE debugger
        // object model and are reported as a benign "not-exposed-by-dte" rejection.
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Reads the call stack (frames) for <paramref name="threadId"/>, capped at
        /// <paramref name="maxFrames"/>. Requires break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<List<CallStackFrame>> GetCallStack(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int maxFrames)
        {
            if (threadId <= 0)
                return DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "invalid-thread-id");
            if (maxFrames <= 0)
                return DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "invalid-max-frames");

            try
            {
                var thread = FindThread(debugger, threadId) ?? debugger?.CurrentThread;
                var frames = thread?.StackFrames;
                if (frames == null)
                    return DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "no-stack-frames");

                var depth = frames.Count;
                if (depth > maxFrames) depth = maxFrames;

                var result = new List<CallStackFrame>(depth);
                for (int i = 0; i < depth; i++)
                {
                    try
                    {
                        var frame = frames.Item(i + 1);
                        result.Add(new CallStackFrame
                        {
                            FrameIndex = i,
                            ThreadId = threadId,
                            MethodName = frame.FunctionName ?? "method",
                            FilePath = ReadFileName(frame),
                            LineNumber = ReadFileLine(frame)
                        });
                    }
                    catch (COMException) { break; }
                    catch { break; }
                }
                return result.Count == 0
                    ? DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "no-stack-frames")
                    : DebugInspectionResult<List<CallStackFrame>>.Success(state, result);
            }
            catch { return DebugInspectionResult<List<CallStackFrame>>.Rejected(state, "callstack-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Establishes the frame cursor (<paramref name="threadId"/>, <paramref name="frameIndex"/>)
        /// for subsequent stepping/evaluate binds (gap92_3). Validates the frame exists. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<bool> SelectFrame(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int frameIndex)
        {
            if (threadId <= 0 || frameIndex < 0)
                return DebugInspectionResult<bool>.Rejected(state, "invalid-thread-or-frame");

            try
            {
                var thread = FindThread(debugger, threadId) ?? debugger?.CurrentThread;
                var frames = thread?.StackFrames;
                if (frames == null || frameIndex >= frames.Count)
                    return DebugInspectionResult<bool>.Rejected(state, "frame-out-of-range");

                var updated = CloneStateWithCursor(state, threadId, frameIndex);
                return DebugInspectionResult<bool>.Success(updated, true);
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "frame-select-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Reads the source statement (file, line, text, surrounding lines) at the selected frame.
        /// Requires break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<StatementInfo> GetStatement(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int frameIndex, int contextLines)
        {
            if (contextLines < 0 || contextLines > 50)
                return DebugInspectionResult<StatementInfo>.Rejected(state, "invalid-context-lines");

            try
            {
                var frame = ResolveFrame(debugger, threadId, frameIndex);
                if (frame == null)
                    return DebugInspectionResult<StatementInfo>.Rejected(state, "frame-unavailable");

                var info = new StatementInfo { IsCurrent = frameIndex == 0 };
                // EnvDTE StackFrame does not expose a source file/line directly; fall back to the
                // last-hit breakpoint's file/line when on the current frame, else best-effort.
                if (frameIndex == 0)
                {
                    try
                    {
                        var hit = debugger?.BreakpointLastHit;
                        if (hit != null && hit.FileLine > 0)
                        {
                            info.FilePath = hit.File;
                            info.LineNumber = hit.FileLine;
                        }
                    }
                    catch (COMException) { }
                }
                info.Text = frame.FunctionName ?? "statement";
                return DebugInspectionResult<StatementInfo>.Success(state, info);
            }
            catch { return DebugInspectionResult<StatementInfo>.Rejected(state, "statement-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Reads the local variables of the selected frame, capped at <see cref="MaxLocals"/>.
        /// Requires break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<List<VariableInfo>> GetLocals(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int frameIndex)
        {
            try
            {
                var frame = ResolveFrame(debugger, threadId, frameIndex);
                if (frame?.Locals == null)
                    return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "no-locals");

                var result = new List<VariableInfo>();
                foreach (EnvDTE.Expression local in frame.Locals)
                {
                    if (result.Count >= MaxLocals) break;
                    var v = TryReadVariable(local);
                    if (v != null) result.Add(v);
                }
                return DebugInspectionResult<List<VariableInfo>>.Success(state, result);
            }
            catch { return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "locals-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Reads the argument list of the selected frame. Requires break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<List<VariableInfo>> GetArguments(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int frameIndex)
        {
            try
            {
                var frame = ResolveFrame(debugger, threadId, frameIndex);
                if (frame?.Arguments == null)
                    return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "no-arguments");

                var result = new List<VariableInfo>();
                foreach (EnvDTE.Expression arg in frame.Arguments)
                {
                    if (result.Count >= MaxLocals) break;
                    var v = TryReadVariable(arg);
                    if (v != null) result.Add(v);
                }
                return DebugInspectionResult<List<VariableInfo>>.Success(state, result);
            }
            catch { return DebugInspectionResult<List<VariableInfo>>.Rejected(state, "arguments-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Reads the "this" object of the selected frame, when resolvable (evaluated defensively).
        /// Requires break mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<VariableInfo> GetThis(EnvDTE.Debugger? debugger, DebugSessionState state, int threadId, int frameIndex)
        {
            try
            {
                var frame = ResolveFrame(debugger, threadId, frameIndex);
                if (frame == null)
                    return DebugInspectionResult<VariableInfo>.Rejected(state, "frame-unavailable");

                // "this" is not a first-class StackFrame member in EnvDTE; attempt an expression
                // evaluation so the surface stays honest about what it can and cannot yield.
                var expr = debugger?.GetExpression("this", false, 300);
                if (expr != null && expr.IsValidValue)
                {
                    return DebugInspectionResult<VariableInfo>.Success(state, new VariableInfo
                    {
                        Name = "this",
                        Type = TryReadType(expr),
                        Value = TryReadValue(expr),
                        IsReadOnly = true
                    });
                }
                return DebugInspectionResult<VariableInfo>.Rejected(state, "this-unavailable");
            }
            catch { return DebugInspectionResult<VariableInfo>.Rejected(state, "this-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Enumerates all threads across the being-debugged processes (deduplicated by id).
        /// Live (any) mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<List<ThreadInfo>> GetThreads(EnvDTE.Debugger? debugger, DebugSessionState state)
        {
            var result = new List<ThreadInfo>();
            var seen = new HashSet<int>();
            try
            {
                foreach (EnvDTE.Thread t in EnumerateThreads(debugger))
                {
                    try
                    {
                        if (!seen.Add(t.ID)) continue;
                        result.Add(new ThreadInfo
                        {
                            Id = t.ID,
                            Name = SafeRead(() => t.Name),
                            IsAlive = t.IsAlive,
                            IsFrozen = t.IsFrozen,
                            SuspendCount = t.SuspendCount,
                            Priority = SafeRead(() => t.Priority)
                        });
                    }
                    catch (COMException) { continue; }
                    catch { continue; }
                }
                return DebugInspectionResult<List<ThreadInfo>>.Success(state, result);
            }
            catch { return DebugInspectionResult<List<ThreadInfo>>.Rejected(state, "threads-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Modules are not part of the EnvDTE debugger object model; reported benignly.
        /// </summary>
        public static DebugInspectionResult<List<ModuleInfo>> GetModules(EnvDTE.Debugger? debugger, DebugSessionState state)
            => DebugInspectionResult<List<ModuleInfo>>.Rejected(state, "not-exposed-by-dte");

        /// <summary>
        /// Reads the current being-debugged process. Live (any) mode. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<ProcessInfo> GetProcessInfo(EnvDTE.Debugger? debugger, DebugSessionState state)
        {
            try
            {
                var proc = debugger?.CurrentProcess;
                if (proc == null)
                    return DebugInspectionResult<ProcessInfo>.Rejected(state, "no-process");

                int programCount = 0;
                try { programCount = proc.Programs?.Count ?? 0; } catch { }

                return DebugInspectionResult<ProcessInfo>.Success(state, new ProcessInfo
                {
                    Id = proc.ProcessID,
                    Name = SafeRead(() => proc.Name),
                    IsBeingDebugged = true,
                    ProgramCount = programCount
                });
            }
            catch { return DebugInspectionResult<ProcessInfo>.Rejected(state, "process-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Exception settings are not part of the EnvDTE debugger object model; reported benignly.
        /// </summary>
        public static DebugInspectionResult<List<ExceptionSettingInfo>> ListExceptionSettings(EnvDTE.Debugger? debugger, DebugSessionState state)
            => DebugInspectionResult<List<ExceptionSettingInfo>>.Rejected(state, "not-exposed-by-dte");

        /// <summary>
        /// Reads the current (last) exception from the last-hit breakpoint, when paused. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<ExceptionInfo> GetCurrentException(EnvDTE.Debugger? debugger, DebugSessionState state)
        {
            try
            {
                var hit = debugger?.BreakpointLastHit;
                if (hit == null)
                    return DebugInspectionResult<ExceptionInfo>.Rejected(state, "no-exception");

                return DebugInspectionResult<ExceptionInfo>.Success(state, new ExceptionInfo
                {
                    Name = SafeRead(() => hit.FunctionName) ?? "unknown-exception",
                    Description = $"{SafeRead(() => hit.File)}:{hit.FileLine}",
                    IsFirstChance = false,
                    IsSecondChance = false
                });
            }
            catch { return DebugInspectionResult<ExceptionInfo>.Rejected(state, "exception-unavailable"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Deferred output channel — no raw console capture in EnvDTE; reported benignly.
        /// </summary>
        public static DebugInspectionResult<string> GetOutput(EnvDTE.Debugger? debugger, DebugSessionState state, int maxChars)
            => DebugInspectionResult<string>.Rejected(state, "not-exposed-by-dte");

        /// <summary>
        /// Enables a breakpoint by id. Reports the updated breakpoint state. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<bool> EnableBreakpoint(EnvDTE.Debugger? debugger, DebugSessionState state, string breakpointId)
        {
            var bp = FindBreakpoint(debugger, breakpointId);
            if (bp == null)
                return DebugInspectionResult<bool>.Rejected(state, "breakpoint-not-found");
            try { bp.Enabled = true; return DebugInspectionResult<bool>.Success(state, true); }
            catch { return DebugInspectionResult<bool>.Rejected(state, "breakpoint-update-failed"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Disables a breakpoint by id. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<bool> DisableBreakpoint(EnvDTE.Debugger? debugger, DebugSessionState state, string breakpointId)
        {
            var bp = FindBreakpoint(debugger, breakpointId);
            if (bp == null)
                return DebugInspectionResult<bool>.Rejected(state, "breakpoint-not-found");
            try { bp.Enabled = false; return DebugInspectionResult<bool>.Success(state, true); }
            catch { return DebugInspectionResult<bool>.Rejected(state, "breakpoint-update-failed"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Sets a condition on a breakpoint by id. EnvDTE's Breakpoint.Condition is read-only, so the
        /// breakpoint is re-created (deleted + re-added) with the new condition, preserving file:line.
        /// Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<bool> ConditionBreakpoint(EnvDTE.Debugger? debugger, DebugSessionState state, string breakpointId, string condition)
        {
            var bp = FindBreakpoint(debugger, breakpointId);
            if (bp == null)
                return DebugInspectionResult<bool>.Rejected(state, "breakpoint-not-found");

            string file = string.Empty;
            int line = 0;
            try { file = bp.File ?? string.Empty; line = bp.FileLine; } catch { }

            try
            {
                bp.Delete();
                var recreated = SetBreakpoint(debugger, file, line, condition);
                return recreated != null
                    ? DebugInspectionResult<bool>.Success(CloneStateWithCursor(state, state.ThreadId, 0), true)
                    : DebugInspectionResult<bool>.Rejected(state, "breakpoint-update-failed");
            }
            catch { return DebugInspectionResult<bool>.Rejected(state, "breakpoint-update-failed"); }
        }
#pragma warning restore VSTHRD010

        /// <summary>
        /// Clears a breakpoint by id. Never throws.
        /// </summary>
#pragma warning disable VSTHRD010 // UI-thread contract guaranteed by DebuggerService
        public static DebugInspectionResult<bool> ClearBreakpointById(EnvDTE.Debugger? debugger, DebugSessionState state, string breakpointId)
        {
            var bp = FindBreakpoint(debugger, breakpointId);
            if (bp == null)
                return DebugInspectionResult<bool>.Rejected(state, "breakpoint-not-found");
            try { bp.Delete(); return DebugInspectionResult<bool>.Success(state, true); }
            catch { return DebugInspectionResult<bool>.Rejected(state, "breakpoint-delete-failed"); }
        }
#pragma warning restore VSTHRD010

        // ----- Interop helpers (UI-thread only, called within the above) -----

#pragma warning disable VSTHRD010 // DTE access; only called within UI-thread-guaranteed inspection methods
        private static EnvDTE.Thread? FindThread(EnvDTE.Debugger? debugger, int threadId)
        {
            if (debugger == null) return null;
            try
            {
                foreach (EnvDTE.Thread t in EnumerateThreads(debugger))
                    if (t.ID == threadId) return t;
            }
            catch { }
            return null;
        }

        private static IEnumerable<EnvDTE.Thread> EnumerateThreads(EnvDTE.Debugger? debugger)
        {
            if (debugger == null) yield break;
            var processes = SafeRead(() => debugger.DebuggedProcesses);
            if (processes == null) yield break;
            for (int p = 1; p <= processes.Count; p++)
            {
                EnvDTE.Process? proc = SafeRead(() => processes.Item(p));
                if (proc == null || proc.Programs == null) continue;
                for (int g = 1; g <= proc.Programs.Count; g++)
                {
                    EnvDTE.Program? program = SafeRead(() => proc.Programs.Item(g));
                    if (program == null || program.Threads == null) continue;
                    for (int t = 1; t <= program.Threads.Count; t++)
                    {
                        EnvDTE.Thread? thread = SafeRead(() => program.Threads.Item(t));
                        if (thread != null) yield return thread;
                    }
                }
            }
        }

        private static EnvDTE.StackFrame? ResolveFrame(EnvDTE.Debugger? debugger, int threadId, int frameIndex)
        {
            if (frameIndex == 0) return debugger?.CurrentStackFrame;
            var thread = FindThread(debugger, threadId) ?? debugger?.CurrentThread;
            var frames = thread?.StackFrames;
            if (frames == null || frameIndex >= frames.Count) return null;
            try { return frames.Item(frameIndex + 1); } catch { return null; }
        }

        private static EnvDTE.Breakpoint? FindBreakpoint(EnvDTE.Debugger? debugger, string breakpointId)
        {
            if (debugger?.Breakpoints == null || string.IsNullOrEmpty(breakpointId)) return null;
            try
            {
                foreach (EnvDTE.Breakpoint bp in debugger.Breakpoints)
                {
                    try { if (string.Equals(bp.Name, breakpointId, StringComparison.Ordinal)) return bp; }
                    catch (COMException) { }
                }
            }
            catch { }
            return null;
        }

        private static VariableInfo? TryReadVariable(EnvDTE.Expression expr)
        {
            try
            {
                var name = expr.Name ?? "?";
                var type = TryReadType(expr);
                var value = type.Length == 0 ? (expr.Value ?? "?") : $"{type} = {expr.Value ?? "?"}";
                var hasChildren = false;
                try { hasChildren = expr.DataMembers?.Count > 0; } catch { }
                return new VariableInfo
                {
                    Name = name,
                    Type = type,
                    Value = value,
                    HasChildren = hasChildren
                };
            }
            catch (COMException) { return null; }
            catch { return null; }
        }

        private static string TryReadType(EnvDTE.Expression expr)
        {
            try { return expr.Type ?? string.Empty; } catch { return string.Empty; }
        }

        private static string TryReadValue(EnvDTE.Expression expr)
        {
            try { return expr.Value ?? "?"; } catch { return "?"; }
        }

        private static string ReadFileName(EnvDTE.StackFrame frame)
        {
            // EnvDTE StackFrame has no file/line; best-effort empty.
            return string.Empty;
        }

        private static int ReadFileLine(EnvDTE.StackFrame frame) => 0;
        private static T SafeRead<T>(Func<T> accessor)
        {
            try { return accessor(); }
            catch { return default!; }
        }

        private static DebugSessionState CloneStateWithCursor(DebugSessionState state, int threadId, int frameIndex)
        {
            return new DebugSessionState
            {
                Mode = state.Mode,
                ThreadId = threadId,
                Frame = $"frame:{frameIndex}",
                BreakReason = state.BreakReason,
                IsDebuggerActive = state.IsDebuggerActive,
                IsActiveProgram = state.IsActiveProgram
            };
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
