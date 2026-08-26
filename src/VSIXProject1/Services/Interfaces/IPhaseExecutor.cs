using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for executing debug phases generated from user instructions.
    /// Defines the contract for phase executors and result tracking.
    /// </summary>
    public interface IPhaseExecutor
    {
        /// <summary>
        /// Gets the phase type that this executor handles.
        /// </summary>
        InternalPhaseType PhaseType { get; }

        /// <summary>
        /// Executes a single phase and returns the execution result with annotation.
        /// </summary>
        /// <param name="phase">The phase to execute.</param>
        /// <param name="changeStack">The change stack to apply changes to (if any).</param>
        /// <param name="targetDir">The target directory for applying changes.</param>
        /// <param name="isInteractiveMode">If true, prompts user on phase failure. If false, auto-retries. (gap29_8_8)</param>
        /// <param name="cancellationToken">Cancellation token for async operation.</param>
        /// <returns>
        /// An InternalPhaseExecution object containing the result, status, and number of changes applied.
        /// Never throws; errors are captured in the execution result.
        /// </returns>
        Task<InternalPhaseExecution> ExecuteAsync(
            InternalPhase phase,
            ChangeStack changeStack,
            string targetDir,
            bool isInteractiveMode = false,
            CancellationToken cancellationToken = default);
    }
}
