using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services.Utilities
{
    /// <summary>
    /// Helper class that implements exponential backoff retry logic for transient failures.
    /// </summary>
    public static class RetryPolicyHelper
    {
        private const int DefaultMaxRetries = 3;
        private const int InitialDelayMs = 1000;
        private const double BackoffMultiplier = 2.0;

        /// <summary>
        /// Executes an async operation with exponential backoff retry logic.
        /// </summary>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token to abort retry loop.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task ExecuteWithRetryAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken,
            int maxRetries = DefaultMaxRetries)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int retryCount = 0;

            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await operation(cancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (IsTransient(ex) && retryCount < maxRetries)
                {
                    retryCount++;
                    int delayMs = (int)(InitialDelayMs * Math.Pow(BackoffMultiplier, retryCount - 1));
                    Debug.WriteLine($"[RetryPolicy] Transient error detected (attempt {retryCount}/{maxRetries}). Retrying after {delayMs}ms: {ex.GetType().Name}");

                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Executes an async operation with exponential backoff retry logic and returns a result.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="cancellationToken">Cancellation token to abort retry loop.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
        /// <returns>The result of the operation.</returns>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken,
            int maxRetries = DefaultMaxRetries)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            int retryCount = 0;

            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await operation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (IsTransient(ex) && retryCount < maxRetries)
                {
                    retryCount++;
                    int delayMs = (int)(InitialDelayMs * Math.Pow(BackoffMultiplier, retryCount - 1));
                    Debug.WriteLine($"[RetryPolicy] Transient error detected (attempt {retryCount}/{maxRetries}). Retrying after {delayMs}ms: {ex.GetType().Name}");

                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Determines if an exception is transient (retryable).
        /// </summary>
        private static bool IsTransient(Exception ex)
        {
            if (ex is HttpRequestException)
                return true;

            if (ex is TimeoutException)
                return true;

            if (ex is OperationCanceledException)
                return false;

            return false;
        }
    }
}
