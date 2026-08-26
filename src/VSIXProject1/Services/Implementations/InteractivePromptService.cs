using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IInteractivePromptService.
    /// Orchestrates user prompts for interactive debug mode decisions.
    /// Delegates UI rendering to INotificationService.
    /// </summary>
    public class InteractivePromptService : IInteractivePromptService
    {
        private readonly INotificationService _notificationService;
        private readonly IBridgeLogger? _logger;

        public InteractivePromptService(INotificationService notificationService, IBridgeLogger? logger = null)
        {
            if (notificationService == null)
                throw new ArgumentNullException(nameof(notificationService));

            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<UserPromptChoice> PromptOnPhaseFailureAsync(
            string phaseName,
            string errorMessage,
            bool isInteractiveMode = true)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                throw new ArgumentException("Phase name cannot be empty.", nameof(phaseName));
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

            if (!isInteractiveMode)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"[gap29_8_8] Phase '{phaseName}' failed; Autonomous mode auto-retries without prompt");
                return UserPromptChoice.Retry;
            }

            var title = $"Phase Failed: {phaseName}";
            var message = $"Error:\n{errorMessage}\n\nWould you like to retry this phase?";

            if (_logger != null)
                await _logger.WriteDebugAsync($"[gap29_8_8] Interactive prompt: {title}");

            var confirmed = await _notificationService.ShowConfirmationAsync(title, message);
            return confirmed ? UserPromptChoice.Retry : UserPromptChoice.Skip;
        }

        public async Task<UserPromptChoice> PromptOnRetryThresholdAsync(
            string changeDescription,
            int attemptCount,
            int maxRetries,
            bool isInteractiveMode = true)
        {
            if (string.IsNullOrWhiteSpace(changeDescription))
                throw new ArgumentException("Change description cannot be empty.", nameof(changeDescription));
            if (attemptCount < 1)
                throw new ArgumentException("Attempt count must be >= 1.", nameof(attemptCount));
            if (maxRetries < 1)
                throw new ArgumentException("Max retries must be >= 1.", nameof(maxRetries));

            if (!isInteractiveMode)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"[gap29_8_8] Retry threshold reached for '{changeDescription}'; Autonomous mode halts without prompt");
                return UserPromptChoice.Cancel;
            }

            var title = "Retry Threshold Reached";
            var message = $"Change: {changeDescription}\n" +
                         $"Attempts: {attemptCount}/{maxRetries}\n\n" +
                         "Maximum retry attempts reached. Halt here or try once more?";

            if (_logger != null)
                await _logger.WriteDebugAsync($"[gap29_8_8] Interactive prompt: {title}");

            var confirmed = await _notificationService.ShowConfirmationAsync(title, message);
            return confirmed ? UserPromptChoice.Retry : UserPromptChoice.Cancel;
        }

        public async Task<UserPromptChoice> PromptOnRiskyChangeAsync(
            string filePath,
            string riskReason,
            string? changePreview = null,
            bool isInteractiveMode = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty.", nameof(filePath));
            if (string.IsNullOrWhiteSpace(riskReason))
                throw new ArgumentException("Risk reason cannot be empty.", nameof(riskReason));

            if (!isInteractiveMode)
            {
                if (_logger != null)
                    await _logger.WriteDebugAsync($"[gap29_8_8] Risky change to '{filePath}'; Autonomous mode auto-approves without prompt");
                return UserPromptChoice.Retry; // Retry means "approve and apply" for risky changes
            }

            var previewText = string.IsNullOrWhiteSpace(changePreview) ? "" : $"\n\nPreview:\n{changePreview}";
            var title = "Risky Code Change";
            var message = $"File: {filePath}\n" +
                         $"Risk: {riskReason}" +
                         previewText +
                         "\n\nApprove and apply this change?";

            if (_logger != null)
                await _logger.WriteDebugAsync($"[gap29_8_8] Interactive prompt: {title}");

            var confirmed = await _notificationService.ShowConfirmationAsync(title, message);
            return confirmed ? UserPromptChoice.Retry : UserPromptChoice.Cancel;
        }
    }
}
