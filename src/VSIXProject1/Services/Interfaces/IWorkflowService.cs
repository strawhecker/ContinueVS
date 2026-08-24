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
    }
}
