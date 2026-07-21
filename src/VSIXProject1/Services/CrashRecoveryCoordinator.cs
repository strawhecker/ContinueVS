using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VSIXProject1.Services
{
    /// <summary>
    /// Exception thrown during crash recovery coordination
    /// </summary>
    public class CrashRecoveryCoordinationException : Exception
    {
        public CrashRecoveryCoordinationException(string message, Exception? innerException = null)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Represents recovery event from bridge
    /// </summary>
    public class RecoveryEvent
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("strategy")]
        public string? Strategy { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("duration")]
        public long? Duration { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Restart strategy with exponential backoff
    /// </summary>
    public class RestartStrategy
    {
        private int retryCount = 0;
        private int[] backoffDelays = { 2000, 4000, 8000, 16000 }; // ms
        private const int MaxRetries = 5;
        private DateTime lastRestartTime = DateTime.MinValue;

        public int RetryCount => retryCount;
        public int MaxRetryCount => MaxRetries;

        /// <summary>
        /// Get next backoff delay in milliseconds
        /// </summary>
        public int GetNextBackoffDelay()
        {
            if (retryCount >= backoffDelays.Length)
            {
                return backoffDelays[backoffDelays.Length - 1];
            }
            return backoffDelays[retryCount];
        }

        /// <summary>
        /// Record successful restart (resets retry count)
        /// </summary>
        public void RecordSuccess()
        {
            retryCount = 0;
            lastRestartTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Increment retry count
        /// </summary>
        public void IncrementRetry()
        {
            retryCount++;
        }

        /// <summary>
        /// Check if max retries exceeded
        /// </summary>
        public bool IsMaxRetriesExceeded()
        {
            return retryCount >= MaxRetries;
        }

        /// <summary>
        /// Reset strategy
        /// </summary>
        public void Reset()
        {
            retryCount = 0;
            lastRestartTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Host-side crash recovery coordinator
    /// Implements restart strategy, graceful shutdown, and fallback mode detection
    /// </summary>
    public class CrashRecoveryCoordinator
    {
        private readonly ILogger? _logger;
        private readonly ITelemetryCollector? _telemetry;
        private readonly RestartStrategy _restartStrategy;
        internal Process? _bridgeProcess;
        private int consecutiveCrashCount = 0;
        private const int FallbackThreshold = 2; // Enter degraded mode after 2+ consecutive crashes
        private bool isInDegradedMode = false;
        private List<RecoveryEvent> recoveryHistory = new List<RecoveryEvent>();
        private CancellationTokenSource cancellationTokenSource;

        public CrashRecoveryCoordinator(
            ILogger? logger = null,
            ITelemetryCollector? telemetry = null)
        {
            _logger = logger;
            _telemetry = telemetry;
            _restartStrategy = new RestartStrategy();
            _bridgeProcess = null;
            cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Initialize crash recovery coordination
        /// </summary>
        public async Task InitializeAsync(Process bridgeProcess)
        {
            try
            {
                _bridgeProcess = bridgeProcess;
                LogDebug("Crash recovery coordinator initialized");
                RecordMetric("crash_recovery_coordinator.initialized", 1);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new CrashRecoveryCoordinationException(
                    $"Failed to initialize crash recovery coordinator: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Handle bridge crash and execute recovery
        /// </summary>
        public async Task<bool> HandleCrashAsync(string reason, string? diagnosticsPath = null)
        {
            try
            {
                LogDebug($"Handling bridge crash: {reason}");
                RecordMetric("crash_recovery.crash_detected", 1);

                consecutiveCrashCount++;
                _restartStrategy.IncrementRetry();

                // Check if should enter degraded mode
                if (consecutiveCrashCount >= FallbackThreshold && !isInDegradedMode)
                {
                    LogDebug($"Consecutive crashes ({consecutiveCrashCount}) exceeds threshold - entering degraded mode");
                    await EnterDegradedModeAsync(diagnosticsPath);
                    RecordMetric("crash_recovery.entered_degraded_mode", 1);
                    return false;
                }

                // Check if max retries exceeded
                if (_restartStrategy.IsMaxRetriesExceeded())
                {
                    LogDebug("Max restart retries exceeded - initiating graceful shutdown");
                    await RequestGracefulShutdownAsync(diagnosticsPath);
                    RecordMetric("crash_recovery.max_retries_exceeded", 1);
                    return false;
                }

                // Execute restart with exponential backoff
                return await RestartBridgeWithBackoffAsync(diagnosticsPath);
            }
            catch (Exception ex)
            {
                LogError($"Crash handling failed: {ex.Message}");
                RecordMetric("crash_recovery.crash_handling_failed", 1);
                throw new CrashRecoveryCoordinationException(
                    $"Failed to handle bridge crash: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Restart bridge process with exponential backoff
        /// </summary>
        public async Task<bool> RestartBridgeWithBackoffAsync(string? diagnosticsPath = null)
        {
            try
            {
                int backoffDelayMs = _restartStrategy.GetNextBackoffDelay();
                LogDebug($"Restarting bridge with {backoffDelayMs}ms backoff (attempt {_restartStrategy.RetryCount + 1}/{_restartStrategy.MaxRetryCount})");

                RecordMetric("crash_recovery.restart_initiated", 1);
                RecordMetric("crash_recovery.restart_backoff_ms", backoffDelayMs);

                // Wait for backoff delay
                await Task.Delay(backoffDelayMs, cancellationTokenSource.Token);

                // Attempt restart
                bool restartSuccess = await PerformBridgeRestartAsync();

                if (restartSuccess)
                {
                    LogDebug("Bridge restart succeeded");
                    _restartStrategy.RecordSuccess();
                    consecutiveCrashCount = 0;
                    RecordMetric("crash_recovery.restart_successful", 1);

                    // Record recovery event
                    var recoveryEvent = new RecoveryEvent
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Strategy = "auto-restart",
                        Success = true,
                        Duration = backoffDelayMs,
                        Reason = "Restart successful after backoff"
                    };
                    recoveryHistory.Add(recoveryEvent);

                    return true;
                }
                else
                {
                    LogDebug("Bridge restart failed");
                    RecordMetric("crash_recovery.restart_failed", 1);
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                LogDebug("Bridge restart cancelled");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Restart failed: {ex.Message}");
                RecordMetric("crash_recovery.restart_exception", 1);
                throw;
            }
        }

        /// <summary>
        /// Perform actual bridge process restart
        /// </summary>
        private async Task<bool> PerformBridgeRestartAsync()
        {
            try
            {
                // Attempt to kill existing process if still running
                if (_bridgeProcess != null && !_bridgeProcess.HasExited)
                {
                    _bridgeProcess.Kill();
                    await Task.Delay(500); // Wait for process to exit
                }

                // Restart would be handled by calling code
                // Here we signal that restart should be attempted
                LogDebug("Bridge restart initiated");
                RecordMetric("crash_recovery.restart_process_initiated", 1);

                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to perform restart: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Request graceful shutdown of bridge
        /// </summary>
        public async Task RequestGracefulShutdownAsync(string? diagnosticsPath = null)
        {
            try
            {
                LogDebug("Requesting graceful bridge shutdown");
                RecordMetric("crash_recovery.graceful_shutdown_requested", 1);

                if (_bridgeProcess != null && !_bridgeProcess.HasExited)
                {
                    // Send shutdown signal (implementation depends on bridge IPC mechanism)
                    // For now, just record the request
                    LogDebug($"Shutdown diagnostics: {diagnosticsPath}");

                    // Wait for shutdown with timeout
                    bool shutdownComplete = _bridgeProcess.WaitForExit(10000); // 10 second timeout

                    if (shutdownComplete)
                    {
                        LogDebug("Bridge shutdown completed successfully");
                        RecordMetric("crash_recovery.graceful_shutdown_completed", 1);
                    }
                    else
                    {
                        LogDebug("Bridge shutdown timeout - forcing exit");
                        _bridgeProcess.Kill();
                        RecordMetric("crash_recovery.graceful_shutdown_timeout", 1);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Graceful shutdown failed: {ex.Message}");
                RecordMetric("crash_recovery.graceful_shutdown_failed", 1);
                throw new CrashRecoveryCoordinationException(
                    $"Failed to request graceful shutdown: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Enter degraded mode (reduce functionality, disable expensive handlers)
        /// </summary>
        public async Task EnterDegradedModeAsync(string? diagnosticsPath = null)
        {
            try
            {
                isInDegradedMode = true;
                LogDebug($"Entering degraded mode. Diagnostics: {diagnosticsPath}");
                RecordMetric("crash_recovery.degraded_mode_entered", 1);

                // Implementation: Signal to IDE to disable expensive handlers
                // This would be communicated through the bridge interface

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Failed to enter degraded mode: {ex.Message}");
                throw new CrashRecoveryCoordinationException(
                    $"Failed to enter degraded mode: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Exit degraded mode (restore full functionality)
        /// </summary>
        public async Task ExitDegradedModeAsync()
        {
            try
            {
                if (isInDegradedMode)
                {
                    isInDegradedMode = false;
                    _restartStrategy.Reset();
                    consecutiveCrashCount = 0;

                    LogDebug("Exiting degraded mode");
                    RecordMetric("crash_recovery.degraded_mode_exited", 1);
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Failed to exit degraded mode: {ex.Message}");
                throw new CrashRecoveryCoordinationException(
                    $"Failed to exit degraded mode: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Check if in degraded mode
        /// </summary>
        public bool IsInDegradedMode => isInDegradedMode;

        /// <summary>
        /// Get recovery history
        /// </summary>
        public IReadOnlyList<RecoveryEvent> GetRecoveryHistory()
        {
            return recoveryHistory.AsReadOnly();
        }

        /// <summary>
        /// Record recovery metrics
        /// </summary>
        public void RecordRecoveryMetrics()
        {
            RecordMetric("crash_recovery.crash_count", consecutiveCrashCount);
            RecordMetric("crash_recovery.retry_count", _restartStrategy.RetryCount);
            RecordMetric("crash_recovery.in_degraded_mode", isInDegradedMode ? 1 : 0);
        }

        /// <summary>
        /// Dispose and cleanup resources
        /// </summary>
        public async Task DisposeAsync()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                LogDebug("Crash recovery coordinator disposed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                LogError($"Disposal error: {ex.Message}");
            }
        }

        /// <summary>
        /// Log debug message
        /// </summary>
        private void LogDebug(string message)
        {
            _logger?.Debug(message);
        }

        /// <summary>
        /// Log error message
        /// </summary>
        private void LogError(string message)
        {
            _logger?.Error(message);
        }

        /// <summary>
        /// Record metric
        /// </summary>
        private void RecordMetric(string name, long value)
        {
            _telemetry?.RecordMetric(name, value);
        }
    }

    /// <summary>
    /// Logger interface
    /// </summary>
    public interface ILogger
    {
        void Debug(string message);
        void Error(string message);
    }

    /// <summary>
    /// Telemetry collector interface
    /// </summary>
    public interface ITelemetryCollector
    {
        void RecordMetric(string name, long value);
    }
}
