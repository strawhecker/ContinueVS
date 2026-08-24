using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for workflow execution control and policy management (gap27_12).
    /// Handles continuation policies: Auto, Interactive, Bypass.
    /// </summary>
    public interface IWorkflowService
    {
        /// <summary>
        /// Sets the continuation policy for workflow execution.
        /// </summary>
        /// <param name="policy">The continuation policy to apply.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetContinuationPolicyAsync(ContinuationPolicy policy);

        /// <summary>
        /// Executes a tool call according to the current continuation policy (gap27_14).
        /// Handles Auto (execute immediately), Interactive (show confirmation), and Bypass (skip dialogs).
        /// </summary>
        /// <param name="toolCall">The tool call to execute.</param>
        /// <param name="policy">Optional policy override; if null, uses current policy.</param>
        /// <returns>The result of tool execution, or null if execution was skipped.</returns>
        Task<ToolResult?> ExecuteToolAsync(ToolCall toolCall, ContinuationPolicy? policy = null);
    }
}
