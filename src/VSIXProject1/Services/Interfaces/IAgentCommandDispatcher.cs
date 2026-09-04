using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for dispatching incoming agent commands to tool handlers.
    /// Validates commands against mode policy and routes to IToolService for execution.
    /// </summary>
    public interface IAgentCommandDispatcher
    {
        /// <summary>
        /// Dispatches an agent command with mode-based policy validation.
        /// </summary>
        /// <param name="commandName">The name of the command to dispatch (e.g., "read_file", "write_file").</param>
        /// <param name="commandArguments">Arguments to pass to the command handler.</param>
        /// <param name="currentMode">The current chat mode, used for policy validation.</param>
        /// <param name="ct">Cancellation token to stop the dispatch.</param>
        /// <returns>A ToolResult representing the outcome of the command execution.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the command is not authorized for the current mode,
        /// or when the mode does not support tool looping.
        /// </exception>
        Task<ToolResult> DispatchAgentCommandAsync(
            string commandName,
            IDictionary<string, object> commandArguments,
            ChatMode currentMode,
            CancellationToken ct = default);
    }
}
