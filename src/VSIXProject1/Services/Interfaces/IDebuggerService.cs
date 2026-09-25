using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for interacting with the Visual Studio debugger.
    /// Provides methods to inspect runtime state, set breakpoints, and control execution.
    /// </summary>
    public interface IDebuggerService
    {
        /// <summary>
        /// Gets the current runtime state (locals, callstack, watches).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for timeout support.</param>
        /// <returns>Current RuntimeState or null if debugger not active.</returns>
        Task<RuntimeState?> GetCurrentStateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a breakpoint at the specified location.
        /// </summary>
        /// <param name="filePath">File path where breakpoint should be set.</param>
        /// <param name="lineNumber">Line number (1-based) for breakpoint.</param>
        /// <param name="condition">Optional condition expression.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>BreakpointInfo if successful; null otherwise.</returns>
        Task<BreakpointInfo?> SetBreakpointAsync(string filePath, int lineNumber, string? condition = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears a breakpoint at the specified location.
        /// </summary>
        /// <param name="filePath">File path where breakpoint should be cleared.</param>
        /// <param name="lineNumber">Line number (1-based) for breakpoint.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if breakpoint was cleared; false if not found.</returns>
        Task<bool> ClearBreakpointAsync(string filePath, int lineNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a debug step action (step over, into, out, etc.).
        /// </summary>
        /// <param name="action">The step action to execute.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>RuntimeState after stepping; null if debugger not active.</returns>
        Task<RuntimeState?> ExecuteStepAsync(DebugStepAction action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resumes execution after a breakpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token (enforces 30-second timeout if used).</param>
        /// <returns>Completed task; throws TimeoutException if execution takes too long.</returns>
        Task ResumeExecutionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets whether debugger is currently active and process is in break state.
        /// </summary>
        /// <returns>True if debugger is active and paused at breakpoint.</returns>
        Task<bool> IsDebuggerActiveAsync();

        // -----------------------------------------------------------------------
        // gap92_2 — Inspection surface (Tier-0, read-only, guard-gated)
        // Each method returns a uniform DebugInspectionResult<T> state-echo and never throws.
        // -----------------------------------------------------------------------

        /// <summary>
        /// Reads the call stack (frames) for <paramref name="threadId"/>, capped at
        /// <paramref name="maxFrames"/>. Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<List<CallStackFrame>>> GetCallStackAsync(DebugSessionState state, int threadId, int maxFrames, CancellationToken cancellationToken = default);

        /// <summary>
        /// Establishes the frame cursor (<paramref name="threadId"/>, <paramref name="frameIndex"/>)
        /// that later step/evaluate binds bind to. Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<bool>> SelectFrameAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the source statement (file, line, text, surrounding lines) at the selected frame.
        /// Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<StatementInfo>> GetStatementAsync(DebugSessionState state, int threadId, int frameIndex, int contextLines, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the local variables of the selected frame. Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<List<VariableInfo>>> GetLocalsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the argument list of the selected frame. Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<List<VariableInfo>>> GetArgumentsAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the "this" object of the selected frame. Requires break mode.
        /// </summary>
        Task<DebugInspectionResult<VariableInfo>> GetThisAsync(DebugSessionState state, int threadId, int frameIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates all threads across being-debugged processes. Live (any) mode.
        /// </summary>
        Task<DebugInspectionResult<List<ThreadInfo>>> GetThreadsAsync(DebugSessionState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enumerates loaded modules. Live (any) mode. Not exposed by EnvDTE; benign rejection.
        /// </summary>
        Task<DebugInspectionResult<List<ModuleInfo>>> GetModulesAsync(DebugSessionState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the current being-debugged process. Live (any) mode.
        /// </summary>
        Task<DebugInspectionResult<ProcessInfo>> GetProcessInfoAsync(DebugSessionState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists exception-handling settings. Live (any) mode. Not exposed by EnvDTE; benign rejection.
        /// </summary>
        Task<DebugInspectionResult<List<ExceptionSettingInfo>>> ListExceptionSettingsAsync(DebugSessionState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the current (most recent) exception, when paused.
        /// </summary>
        Task<DebugInspectionResult<ExceptionInfo>> GetCurrentExceptionAsync(DebugSessionState state, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads captured debugger/console output. Live (any) mode. Not exposed by EnvDTE; benign rejection.
        /// </summary>
        Task<DebugInspectionResult<string>> GetOutputAsync(DebugSessionState state, int maxChars, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enables a breakpoint by id.
        /// </summary>
        Task<DebugInspectionResult<bool>> EnableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Disables a breakpoint by id.
        /// </summary>
        Task<DebugInspectionResult<bool>> DisableBreakpointAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets a condition on a breakpoint by id.
        /// </summary>
        Task<DebugInspectionResult<bool>> ConditionBreakpointAsync(DebugSessionState state, string breakpointId, string condition, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears a breakpoint by id.
        /// </summary>
        Task<DebugInspectionResult<bool>> ClearBreakpointByIdAsync(DebugSessionState state, string breakpointId, CancellationToken cancellationToken = default);
    }
}
