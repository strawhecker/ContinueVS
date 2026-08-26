using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service contract for executing changes with automatic retry-on-failure and LLM-driven refinement.
    /// Implements the change-level retry loop with bailout behavior (stop on max retries, no auto-rollback).
    /// </summary>
    public interface IChangeExecutor
    {
        /// <summary>
        /// Attempts to execute a change with automatic retry and refinement.
        /// On first attempt failure, LLM analyzes error and generates refined change.
        /// Retries up to maxRetriesPerChange (from config) with refined changes.
        /// On threshold hit, returns RetryThresholdExceeded status (no automatic rollback).
        /// </summary>
        /// <param name="change">The initial change to attempt.</param>
        /// <param name="changeStack">The change stack to apply to.</param>
        /// <param name="filePath">The target file path for the change.</param>
        /// <param name="isAutonomousMode">True if autonomous mode (retries silently); false if interactive (requires prompts for retry decisions).</param>
        /// <param name="cancellationToken">Cancellation token to abort execution.</param>
        /// <returns>
        /// ChangeExecutionResult with status and history. Never throws; all errors captured in result.
        /// Status values: Success (attempt 1 passed), RetriedSuccess (retry passed), RetryThresholdExceeded (all attempts failed), ExecutionCancelled.
        /// </returns>
        Task<ChangeExecutionResult> AttemptChangeAsync(
            CodeChange change,
            ChangeStack changeStack,
            string filePath,
            bool isAutonomousMode,
            CancellationToken cancellationToken = default);
    }
}
