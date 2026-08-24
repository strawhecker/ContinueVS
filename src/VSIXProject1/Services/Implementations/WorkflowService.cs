#nullable enable

using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IWorkflowService that enforces continuation policies during tool execution (gap27_14).
    /// Handles Auto (execute immediately), Interactive (show confirmation), and Bypass (suppress dialogs).
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly IToolService _toolService;
        private readonly INotificationService _notificationService;
        private readonly IBridgeLogger? _logger;
        private ContinuationPolicy _currentPolicy = ContinuationPolicy.Interactive;

        /// <summary>
        /// Initializes a new instance of WorkflowService.
        /// </summary>
        /// <param name="toolService">The tool service for executing tools.</param>
        /// <param name="notificationService">The notification service for showing confirmations.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public WorkflowService(
            IToolService toolService,
            INotificationService notificationService,
            IBridgeLogger? logger = null)
        {
            _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger;
        }

        /// <summary>
        /// Sets the continuation policy for workflow execution.
        /// </summary>
        /// <param name="policy">The continuation policy to apply.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SetContinuationPolicyAsync(ContinuationPolicy policy)
        {
            _currentPolicy = policy;
            if (_logger != null)
            {
                await _logger.WriteDebugAsync($"Workflow policy set to {policy}");
            }
        }

        /// <summary>
        /// Executes a tool call according to the current continuation policy (gap27_14).
        /// Handles Auto (execute immediately), Interactive (show confirmation), and Bypass (skip dialogs).
        /// </summary>
        /// <param name="toolCall">The tool call to execute.</param>
        /// <param name="policy">Optional policy override; if null, uses current policy.</param>
        /// <returns>The result of tool execution, or null if execution was skipped.</returns>
        public async Task<ToolResult?> ExecuteToolAsync(ToolCall toolCall, ContinuationPolicy? policy = null)
        {
            if (toolCall == null)
            {
                throw new ArgumentNullException(nameof(toolCall));
            }

            var effectivePolicy = policy ?? _currentPolicy;
            var toolName = toolCall.Name;
            var args = toolCall.Arguments ?? new System.Collections.Generic.Dictionary<string, object>();

            if (_logger != null)
            {
                await _logger.WriteDebugAsync($"ExecuteToolAsync: {toolName}, Policy: {effectivePolicy}");
            }

            switch (effectivePolicy)
            {
                case ContinuationPolicy.Auto:
                    // Auto mode: Execute immediately, continue to next tool
                    if (_logger != null)
                    {
                        await _logger.WriteInfoAsync($"Policy: Auto | Tool: {toolName}");
                    }
                    return await _toolService.InvokeAsync(toolName, args);

                case ContinuationPolicy.Interactive:
                    // Interactive mode: Show confirmation dialog, wait for user approval
                    var confirmed = await _notificationService.ShowConfirmationAsync(
                        "Execute Tool?",
                        $"Execute {toolName}?");

                    if (!confirmed)
                    {
                        if (_logger != null)
                        {
                            await _logger.WriteInfoAsync($"Policy: Interactive | Tool: {toolName} | User declined execution");
                        }
                        return null;
                    }

                    if (_logger != null)
                    {
                        await _logger.WriteInfoAsync($"Policy: Interactive | Tool: {toolName} | User approved execution");
                    }
                    return await _toolService.InvokeAsync(toolName, args);

                case ContinuationPolicy.Bypass:
                    // Bypass mode: Execute without dialogs, suppress warnings
                    if (_logger != null)
                    {
                        await _logger.WriteInfoAsync($"Policy: Bypass | Tool: {toolName} | Suppressed warning");
                    }
                    return await _toolService.InvokeAsync(toolName, args);

                default:
                    throw new InvalidOperationException($"Unknown continuation policy: {effectivePolicy}");
            }
        }
    }
}
