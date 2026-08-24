using System;
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
    }
}
