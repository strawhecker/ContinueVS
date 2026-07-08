#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Infrastructure
{
    /// <summary>
    /// Helper utilities for testing async operations with retries, polling, and assertions.
    /// 
    /// Provides methods for:
    /// - Retrying async operations with exponential backoff
    /// - Polling for conditions with timeout
    /// - Asserting task completion within a time window
    /// 
    /// Usage:
    ///   await AsyncTestHelper.RetryAsync(
    ///       async () => await _transport.StartAsync(cts.Token),
    ///       maxAttempts: 3,
    ///       delayMs: 100
    ///   );
    /// </summary>
    public static class AsyncTestHelper
    {
        /// <summary>
        /// Retries an async operation with exponential backoff until it succeeds or max attempts exceeded.
        /// </summary>
        /// <param name="operation">The async operation to retry</param>
        /// <param name="maxAttempts">Maximum number of attempts (default: 3)</param>
        /// <param name="delayMs">Initial delay in milliseconds before first retry (default: 100)</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff (default: 2.0)</param>
        /// <returns>Task that completes when operation succeeds or max attempts exceeded</returns>
        /// <exception cref="AggregateException">Thrown if all attempts fail; contains all exceptions encountered</exception>
        public static async Task RetryAsync(
            Func<Task> operation,
            int maxAttempts = 3,
            int delayMs = 100,
            double backoffMultiplier = 2.0)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (maxAttempts < 1) throw new ArgumentException("maxAttempts must be at least 1", nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentException("delayMs must be non-negative", nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();
            var currentDelay = delayMs;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    await operation();
                    return; // Success
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);

                    // Only delay if we're not on the last attempt
                    if (attempt < maxAttempts - 1)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = (int)(currentDelay * backoffMultiplier);
                    }
                }
            }

            // All attempts failed
            throw new AggregateException(
                $"Retry exhausted after {maxAttempts} attempts",
                exceptions);
        }

        /// <summary>
        /// Retries an async operation with a return value, applying exponential backoff.
        /// </summary>
        /// <typeparam name="T">The return type of the operation</typeparam>
        /// <param name="operation">The async operation to retry</param>
        /// <param name="maxAttempts">Maximum number of attempts (default: 3)</param>
        /// <param name="delayMs">Initial delay in milliseconds before first retry (default: 100)</param>
        /// <param name="backoffMultiplier">Multiplier for exponential backoff (default: 2.0)</param>
        /// <returns>Task that returns the operation result when successful</returns>
        /// <exception cref="AggregateException">Thrown if all attempts fail; contains all exceptions encountered</exception>
        public static async Task<T> RetryAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts = 3,
            int delayMs = 100,
            double backoffMultiplier = 2.0)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (maxAttempts < 1) throw new ArgumentException("maxAttempts must be at least 1", nameof(maxAttempts));
            if (delayMs < 0) throw new ArgumentException("delayMs must be non-negative", nameof(delayMs));

            var exceptions = new System.Collections.Generic.List<Exception>();
            var currentDelay = delayMs;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);

                    // Only delay if we're not on the last attempt
                    if (attempt < maxAttempts - 1)
                    {
                        await Task.Delay(currentDelay);
                        currentDelay = (int)(currentDelay * backoffMultiplier);
                    }
                }
            }

            // All attempts failed
            throw new AggregateException(
                $"Retry exhausted after {maxAttempts} attempts",
                exceptions);
        }

        /// <summary>
        /// Polls for a condition to become true, timing out if the condition is not met.
        /// </summary>
        /// <param name="condition">Predicate to poll (return true when condition is met)</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds (default: 5000)</param>
        /// <param name="pollIntervalMs">Interval between polls in milliseconds (default: 50)</param>
        /// <returns>Task that completes when condition is true or timeout exceeded</returns>
        /// <exception cref="TimeoutException">Thrown if condition not met within timeout</exception>
        public static async Task WaitForAsync(
            Func<bool> condition,
            int timeoutMs = 5000,
            int pollIntervalMs = 50)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (timeoutMs < 0) throw new ArgumentException("timeoutMs must be non-negative", nameof(timeoutMs));
            if (pollIntervalMs < 1) throw new ArgumentException("pollIntervalMs must be at least 1", nameof(pollIntervalMs));

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return; // Condition met
                }

                await Task.Delay(pollIntervalMs);
            }

            throw new TimeoutException(
                $"Condition not met within {timeoutMs}ms timeout");
        }

        /// <summary>
        /// Polls for an async condition to become true, timing out if the condition is not met.
        /// </summary>
        /// <param name="condition">Async predicate to poll (return true when condition is met)</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds (default: 5000)</param>
        /// <param name="pollIntervalMs">Interval between polls in milliseconds (default: 50)</param>
        /// <returns>Task that completes when condition is true or timeout exceeded</returns>
        /// <exception cref="TimeoutException">Thrown if condition not met within timeout</exception>
        public static async Task WaitForAsync(
            Func<Task<bool>> condition,
            int timeoutMs = 5000,
            int pollIntervalMs = 50)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (timeoutMs < 0) throw new ArgumentException("timeoutMs must be non-negative", nameof(timeoutMs));
            if (pollIntervalMs < 1) throw new ArgumentException("pollIntervalMs must be at least 1", nameof(pollIntervalMs));

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (await condition())
                {
                    return; // Condition met
                }

                await Task.Delay(pollIntervalMs);
            }

            throw new TimeoutException(
                $"Condition not met within {timeoutMs}ms timeout");
        }

        /// <summary>
        /// Asserts that a task completes within the specified timeout.
        /// </summary>
        /// <param name="task">The task to wait for</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <exception cref="TimeoutException">Thrown if task does not complete within timeout</exception>
#pragma warning disable VSTHRD003 // Avoid awaiting work not started in context (safe in test code)
        public static async Task AssertCompletesAsync(
            Task task,
            int timeoutMs = TestConstants.DefaultTimeoutMs)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        $"Task did not complete within {timeoutMs}ms timeout");
                }
            }
        }
#pragma warning restore VSTHRD003

        /// <summary>
        /// Asserts that a task completes within the specified timeout and returns the result.
        /// </summary>
        /// <typeparam name="T">The return type of the task</typeparam>
        /// <param name="task">The task to wait for</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <returns>The result of the task</returns>
        /// <exception cref="TimeoutException">Thrown if task does not complete within timeout</exception>
#pragma warning disable VSTHRD003 // Avoid awaiting work not started in context (safe in test code)
        public static async Task<T> AssertCompletesAsync<T>(
            Task<T> task,
            int timeoutMs = TestConstants.DefaultTimeoutMs)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        $"Task did not complete within {timeoutMs}ms timeout");
                }
            }
        }
#pragma warning restore VSTHRD003

        /// <summary>
        /// Asserts that a task faults with the specified exception type within the timeout.
        /// </summary>
        /// <param name="task">The task to wait for</param>
        /// <param name="expectedExceptionType">The expected exception type</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <exception cref="TimeoutException">Thrown if task does not complete within timeout</exception>
        /// <exception cref="XunitException">Thrown if task does not fault with expected exception type</exception>
#pragma warning disable VSTHRD003 // Avoid awaiting work not started in context (safe in test code)
        public static async Task AssertFaultsAsync(
            Task task,
            Type expectedExceptionType,
            int timeoutMs = TestConstants.DefaultTimeoutMs)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (expectedExceptionType == null) throw new ArgumentNullException(nameof(expectedExceptionType));

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    await task.ConfigureAwait(false);
                    Assert.Fail($"Task completed successfully; expected {expectedExceptionType.Name}");
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        $"Task did not complete within {timeoutMs}ms timeout");
                }
                catch (Exception ex)
                {
                    Assert.IsType(expectedExceptionType, ex);
                }
            }
        }
#pragma warning restore VSTHRD003

        /// <summary>
        /// Waits for multiple tasks to all complete within the specified timeout.
        /// </summary>
        /// <param name="tasks">The tasks to wait for</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <exception cref="TimeoutException">Thrown if any task does not complete within timeout</exception>
        public static async Task AssertAllCompleteAsync(
            Task[] tasks,
            int timeoutMs = TestConstants.DefaultTimeoutMs)
        {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Not all tasks completed within {timeoutMs}ms timeout");
            }

            // Additional timeout check via WaitAll (for compatibility)
            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    await Task.Delay(0, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // All tasks completed within timeout
                }
            }
        }

        /// <summary>
        /// Runs an async action with a timeout and cancellation token.
        /// </summary>
        /// <param name="action">The async action to execute</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
        /// <param name="cancellationToken">Cancellation token (optional)</param>
        /// <returns>Task that completes when action is done or timeout/cancellation occurs</returns>
        public static async Task RunWithTimeoutAsync(
            Func<CancellationToken, Task> action,
            int timeoutMs = TestConstants.DefaultTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                cts.CancelAfter(timeoutMs);

                try
                {
                    await action(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Operation did not complete within {timeoutMs}ms timeout");
                }
            }
        }
    }
}
