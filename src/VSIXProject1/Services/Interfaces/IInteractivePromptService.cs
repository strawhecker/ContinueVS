using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service for handling interactive prompts in Debug mode.
    /// Prompts the user for decisions on phase failures, retry thresholds, and risky changes.
    /// Only active in Interactive mode; Autonomous mode skips all prompts.
    /// </summary>
    public interface IInteractivePromptService
    {
        /// <summary>
        /// Prompts the user when a phase fails.
        /// Offers options to retry the phase, skip it, or cancel the entire session.
        /// </summary>
        /// <param name="phaseName">Name or description of the failed phase.</param>
        /// <param name="errorMessage">Error message describing why the phase failed.</param>
        /// <param name="isInteractiveMode">If false, returns Retry (autonomously) without prompting.</param>
        /// <returns>User's choice: Retry, Skip, or Cancel.</returns>
        Task<UserPromptChoice> PromptOnPhaseFailureAsync(string phaseName, string errorMessage, bool isInteractiveMode = true);

        /// <summary>
        /// Prompts the user when a retry threshold is about to be exceeded.
        /// Warns the user that max retries have been reached and offers final options.
        /// </summary>
        /// <param name="changeDescription">Description of the change being retried.</param>
        /// <param name="attemptCount">Number of attempts made so far.</param>
        /// <param name="maxRetries">Maximum allowed retries.</param>
        /// <param name="isInteractiveMode">If false, returns Retry (autonomously) without prompting.</param>
        /// <returns>User's choice: Retry (one more time) or Cancel.</returns>
        Task<UserPromptChoice> PromptOnRetryThresholdAsync(string changeDescription, int attemptCount, int maxRetries, bool isInteractiveMode = true);

        /// <summary>
        /// Prompts the user when a risky code change is about to be applied.
        /// Allows the user to review the change and approve or cancel before application.
        /// </summary>
        /// <param name="filePath">File path where the change will be applied.</param>
        /// <param name="riskReason">Reason why the change is considered risky (e.g., "deletes code", "modifies critical function").</param>
        /// <param name="changePreview">Optional preview of the change (first 200 chars of diff).</param>
        /// <param name="isInteractiveMode">If false, returns Retry (auto-approves) without prompting.</param>
        /// <returns>User's choice: Retry (approve and apply) or Cancel.</returns>
        Task<UserPromptChoice> PromptOnRiskyChangeAsync(string filePath, string riskReason, string? changePreview = null, bool isInteractiveMode = true);
    }
}
