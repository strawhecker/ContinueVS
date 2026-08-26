using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Orchestrates change execution with retry loop and LLM-driven refinement.
    /// On failure, analyzes error, generates refined change, and retries (up to threshold).
    /// On threshold, halts without automatic rollback; user controls resume.
    /// </summary>
    internal class ChangeExecutionStack : IChangeExecutor
    {
        private readonly IChangeStackService _changeStack;
        private readonly IFailureAnalyzerService _failureAnalyzer;
        private readonly IConfigService _configService;
        private readonly IBridgeLogger _logger;

        public ChangeExecutionStack(
            IChangeStackService changeStack,
            IFailureAnalyzerService failureAnalyzer,
            IConfigService configService,
            IBridgeLogger logger)
        {
            _changeStack = changeStack ?? throw new ArgumentNullException(nameof(changeStack));
            _failureAnalyzer = failureAnalyzer ?? throw new ArgumentNullException(nameof(failureAnalyzer));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ChangeExecutionResult> AttemptChangeAsync(
            CodeChange change,
            ChangeStack changeStack,
            string filePath,
            bool isAutonomousMode,
            CancellationToken cancellationToken = default)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            if (changeStack == null) throw new ArgumentNullException(nameof(changeStack));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("filePath cannot be null or whitespace", nameof(filePath));

            var stopwatch = Stopwatch.StartNew();
            var result = new ChangeExecutionResult
            {
                ExecutedAt = DateTime.UtcNow
            };

            var config = _configService.GetCurrentConfig();
            int maxRetries = config?.MaxRetriesPerChange ?? 3;
            var currentChange = change;
            int attemptNumber = 1;

            try
            {
                while (attemptNumber <= maxRetries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await _logger.WriteInfoAsync($"[gap29_8_7] Change attempt {attemptNumber}/{maxRetries}: {change.Description}");

                    try
                    {
                        // Apply the change
                        await _changeStack.ApplyChangeAsync(changeStack.Id, currentChange, filePath);

                        await _logger.WriteInfoAsync($"[gap29_8_7] Change attempt {attemptNumber} SUCCEEDED");

                        result.Status = attemptNumber == 1 
                            ? ChangeExecutionResult.StatusCode.Success 
                            : ChangeExecutionResult.StatusCode.RetriedSuccess;
                        result.FinalChange = currentChange;
                        result.ExecutedAttemptCount = attemptNumber;
                        result.Evidence = $"Change '{change.Description}' applied successfully on attempt {attemptNumber}";

                        stopwatch.Stop();
                        result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                        return result;
                    }
                    catch (Exception ex)
                    {
                        await _logger.WriteErrorAsync($"[gap29_8_7] Change attempt {attemptNumber} FAILED: {ex.Message}", ex);

                        // If this was the last attempt, bail out
                        if (attemptNumber >= maxRetries)
                        {
                            await _logger.WriteInfoAsync($"[gap29_8_7] Max retries ({maxRetries}) exhausted. Halting without automatic rollback.");

                            result.Status = ChangeExecutionResult.StatusCode.RetryThresholdExceeded;
                            result.ExecutedAttemptCount = attemptNumber;
                            result.FinalChange = currentChange;
                            result.Evidence = $"Change '{change.Description}' failed after {maxRetries} attempts. No automatic rollback applied.";

                            stopwatch.Stop();
                            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
                            return result;
                        }

                        // Attempt refinement
                        attemptNumber++;
                        var refinedChange = await AttemptRefinementAsync(
                            ex,
                            change,
                            currentChange,
                            isAutonomousMode,
                            cancellationToken,
                            result);
                        currentChange = refinedChange;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await _logger.WriteInfoAsync("[gap29_8_7] Change execution cancelled.");
                result.Status = ChangeExecutionResult.StatusCode.ExecutionCancelled;
                result.ExecutedAttemptCount = attemptNumber;
                result.Evidence = $"Change attempt cancelled at attempt {attemptNumber}";
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                await _logger.WriteErrorAsync($"[gap29_8_7] Unexpected error in change execution: {ex.Message}", ex);
                result.Status = ChangeExecutionResult.StatusCode.RetryThresholdExceeded;
                result.ExecutedAttemptCount = attemptNumber;
                result.Evidence = $"Unexpected error: {ex.Message}";
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        private async Task<CodeChange> AttemptRefinementAsync(
            Exception previousError,
            CodeChange originalChange,
            CodeChange lastAppliedChange,
            bool isAutonomousMode,
            CancellationToken cancellationToken,
            ChangeExecutionResult result)
        {
            var refinedChange = lastAppliedChange;

            try
            {
                await _logger.WriteInfoAsync($"[gap29_8_7] Analyzing failure to generate refined change...");

                var refinementAttempt = await _failureAnalyzer.AnalyzeFailureAsync(
                    previousError.Message,
                    lastAppliedChange,
                    string.Empty,
                    isAutonomousMode,
                    cancellationToken);

                result.RefinementHistory.Add(refinementAttempt);

                if (refinementAttempt.IsViable())
                {
                    refinedChange = refinementAttempt.RefinedChange!;
                    await _logger.WriteInfoAsync(
                        $"[gap29_8_7] Refined change generated (confidence: {refinementAttempt.ConfidenceScore:F2}): {refinementAttempt.ApproachDescription}");
                }
                else
                {
                    await _logger.WriteInfoAsync("[gap29_8_7] Refinement analysis did not generate viable refined change.");
                }
            }
            catch (Exception ex)
            {
                await _logger.WriteErrorAsync($"[gap29_8_7] Error during failure analysis and refinement: {ex.Message}", ex);
            }

            return refinedChange;
        }
    }
}
