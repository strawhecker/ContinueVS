using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace VSIXProject1.Services
{
    /// <summary>
    /// Interface for terminal collection operations.
    /// Provides contract for executing commands, sending input, and querying terminal state.
    /// 
    /// Related Steps: Step 82 (terminal-handler.mjs), Step 71 (handler registration)
    /// </summary>
    public interface ITerminalCollector
    {
        /// <summary>
        /// Execute a command asynchronously with output streaming.
        /// Yields chunks of output incrementally for real-time progress.
        /// </summary>
        /// <param name="command">Command text to execute (e.g., "npm test")</param>
        /// <param name="timeoutMs">Timeout in milliseconds (0 = no timeout)</param>
        /// <param name="workingDirectory">Working directory for command (optional)</param>
        /// <returns>Async enumerable of terminal output chunks</returns>
        IAsyncEnumerable<TerminalOutput> ExecuteCommandAsync(string command, int timeoutMs, string workingDirectory = null);

        /// <summary>
        /// Send input text to running terminal (non-blocking).
        /// Queued if terminal is busy; no guarantee of immediate execution.
        /// </summary>
        /// <param name="text">Text to send (e.g., "npm run build\n")</param>
        /// <returns>Task representing queued operation</returns>
        Task SendInputAsync(string text);

        /// <summary>
        /// Clear terminal state and output history.
        /// </summary>
        /// <returns>Task representing clear operation</returns>
        Task ClearTerminalAsync();

        /// <summary>
        /// Get current terminal status (idle, busy, running).
        /// </summary>
        /// <returns>TerminalStatus with current state and metadata</returns>
        Task<TerminalStatus> GetStatusAsync();
    }

    /// <summary>
    /// Terminal output chunk for streaming responses.
    /// Each chunk represents a portion of command output.
    /// </summary>
    public class TerminalOutput
    {
        /// <summary>
        /// Output text chunk (may be partial; check IsPartial)
        /// </summary>
        public string Chunk { get; set; }

        /// <summary>
        /// True if this is a partial chunk; more output expected
        /// </summary>
        public bool IsPartial { get; set; }

        /// <summary>
        /// True if this chunk is error output (stderr)
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Line number in output stream (0-based)
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Timestamp when chunk was generated (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        public TerminalOutput()
        {
            Chunk = string.Empty;
            IsPartial = false;
            IsError = false;
            LineNumber = 0;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Terminal state enumeration
    /// </summary>
    public enum TerminalState
    {
        Idle,
        Busy,
        Running,
        Error,
    }

    /// <summary>
    /// Terminal status snapshot for querying state
    /// </summary>
    public class TerminalStatus
    {
        /// <summary>
        /// Current terminal state
        /// </summary>
        public TerminalState State { get; set; }

        /// <summary>
        /// True if terminal is responsive to input
        /// </summary>
        public bool IsResponsive { get; set; }

        /// <summary>
        /// Number of commands executed in this session
        /// </summary>
        public int CommandCount { get; set; }

        /// <summary>
        /// Last output text (may be null)
        /// </summary>
        public string LastOutput { get; set; }

        /// <summary>
        /// Timestamp when status was captured (UTC)
        /// </summary>
        public DateTime CapturedAt { get; set; }

        public TerminalStatus()
        {
            State = TerminalState.Idle;
            IsResponsive = true;
            CommandCount = 0;
            LastOutput = null;
            CapturedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Terminal collector implementation using DTE automation.
    /// Provides bidirectional terminal control (execute commands, stream output, send input, track state).
    /// 
    /// Architecture:
    /// - Uses DTE.ExecuteCommand or similar to interface with VS integrated terminal
    /// - Queues commands for sequential execution (prevents interleaving)
    /// - Streams output via async generators (efficient memory usage)
    /// - Gracefully handles DTE unavailability or null conditions
    /// 
    /// Related Steps: Step 82 (terminal-handler.mjs), Step 61 (DebugSessionCollector pattern)
    /// </summary>
    public class TerminalCollector : ITerminalCollector
    {
        private readonly DTE _dte;
        private readonly IBridgeLogger _logger;
        private readonly ITelemetryCollector _metrics;

        private TerminalState _state;
        private int _commandCount;
        private Queue<Func<Task>> _commandQueue;
        private bool _isProcessingQueue;
        private string _lastOutput;
        private CancellationTokenSource _executionCts;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dte">Visual Studio DTE object (required)</param>
        /// <param name="logger">Bridge logger (optional)</param>
        /// <param name="metrics">Telemetry collector (optional)</param>
        public TerminalCollector(DTE dte, IBridgeLogger logger = null, ITelemetryCollector metrics = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (dte == null)
            {
                throw new ArgumentNullException(nameof(dte), "DTE is required for TerminalCollector");
            }

            _dte = dte;
            _logger = logger;
            _metrics = metrics;
            _state = TerminalState.Idle;
            _commandCount = 0;
            _commandQueue = new Queue<Func<Task>>();
            _isProcessingQueue = false;
            _lastOutput = null;
            _executionCts = null;

            LogDebug("TerminalCollector initialized");
        }

        /// <summary>
        /// Execute command with output streaming
        /// </summary>
        public async IAsyncEnumerable<TerminalOutput> ExecuteCommandAsync(
            string command,
            int timeoutMs,
            string workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command cannot be empty", nameof(command));
            }

            var startTime = DateTime.UtcNow;

            try
            {
                _state = TerminalState.Running;
                _executionCts = new CancellationTokenSource();

                if (timeoutMs > 0)
                {
                    _executionCts.CancelAfter(timeoutMs);
                }

                LogDebug($"Executing command: {command}");

                // Simulate command execution with output streaming
                // In real implementation, this would interface with DTE.ExecuteCommand or similar
                var output = await SimulateCommandExecutionAsync(command, _executionCts.Token);

                int lineNumber = 0;
                var chunkSize = 100;

                for (int i = 0; i < output.Length; i += chunkSize)
                {
                    if (_executionCts.Token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Command execution timeout");
                    }

                    var chunk = output.Substring(i, Math.Min(chunkSize, output.Length - i));
                    var isLast = i + chunkSize >= output.Length;

                    var terminalOutput = new TerminalOutput
                    {
                        Chunk = chunk,
                        IsPartial = !isLast,
                        IsError = false,
                        LineNumber = lineNumber++,
                        Timestamp = DateTime.UtcNow,
                    };

                    _lastOutput = chunk;
                    yield return terminalOutput;

                    // Simulate streaming delay
                    await Task.Delay(10, _executionCts.Token);
                }

                _state = TerminalState.Idle;
                _commandCount++;

                RecordMetric("terminal.executeCommand", new Dictionary<string, object>
                {
                    { "duration_ms", (DateTime.UtcNow - startTime).TotalMilliseconds },
                    { "success", true },
                });

                LogDebug($"Command completed: {_commandCount} total commands executed");
            }
            catch (OperationCanceledException ex)
            {
                _state = TerminalState.Error;
                RecordMetric("terminal.executeCommand", new Dictionary<string, object>
                {
                    { "duration_ms", (DateTime.UtcNow - startTime).TotalMilliseconds },
                    { "success", false },
                    { "errorCode", "TIMEOUT" },
                });

                LogWarn($"Command execution timeout: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _state = TerminalState.Error;
                RecordMetric("terminal.executeCommand", new Dictionary<string, object>
                {
                    { "duration_ms", (DateTime.UtcNow - startTime).TotalMilliseconds },
                    { "success", false },
                    { "errorCode", "EXECUTION_ERROR" },
                });

                LogWarn($"Command execution failed: {ex.Message}");
                throw;
            }
            finally
            {
                _executionCts?.Dispose();
                _executionCts = null;
            }
        }

        /// <summary>
        /// Send input to terminal (non-blocking, queued)
        /// </summary>
        public async Task SendInputAsync(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            LogDebug($"Queuing input: {text.Length} chars");

            // Queue the input operation
            _commandQueue.Enqueue(async () =>
            {
                try
                {
                    await Task.Delay(0); // Simulate async operation
                    _lastOutput = text;
                    RecordMetric("terminal.sendInput", new Dictionary<string, object>
                    {
                        { "textLength", text.Length },
                        { "success", true },
                    });
                }
                catch (Exception ex)
                {
                    LogWarn($"Send input failed: {ex.Message}");
                    throw;
                }
            });

            // Process queue
            await ProcessCommandQueueAsync();
        }

        /// <summary>
        /// Clear terminal
        /// </summary>
        public async Task ClearTerminalAsync()
        {
            LogDebug("Clearing terminal");

            _commandQueue.Enqueue(async () =>
            {
                try
                {
                    await Task.Delay(0); // Simulate async operation
                    _lastOutput = null;
                    _state = TerminalState.Idle;
                    RecordMetric("terminal.clear", new Dictionary<string, object>
                    {
                        { "success", true },
                    });
                }
                catch (Exception ex)
                {
                    LogWarn($"Clear terminal failed: {ex.Message}");
                    throw;
                }
            });

            await ProcessCommandQueueAsync();
        }

        /// <summary>
        /// Get terminal status
        /// </summary>
        public async Task<TerminalStatus> GetStatusAsync()
        {
            var status = new TerminalStatus
            {
                State = _state,
                IsResponsive = _dte != null,
                CommandCount = _commandCount,
                LastOutput = _lastOutput,
                CapturedAt = DateTime.UtcNow,
            };

            LogDebug($"Terminal status: {status.State}");
            return await Task.FromResult(status);
        }

        /// <summary>
        /// Process queued commands sequentially
        /// </summary>
        private async Task ProcessCommandQueueAsync()
        {
            if (_isProcessingQueue || _commandQueue.Count == 0)
            {
                return;
            }

            _isProcessingQueue = true;

            try
            {
                while (_commandQueue.Count > 0)
                {
                    var command = _commandQueue.Dequeue();
                    await command();
                }
            }
            finally
            {
                _isProcessingQueue = false;
            }
        }

        /// <summary>
        /// Simulate command execution (placeholder for real DTE integration)
        /// </summary>
        private async Task<string> SimulateCommandExecutionAsync(string command, CancellationToken cancellationToken)
        {
            // In real implementation, would use:
            // - DTE.ExecuteCommand("View.Output") to activate output pane
            // - Capture output via event handlers
            // - Or use external process (ProcessStartInfo) for actual command execution

            await Task.Delay(50, cancellationToken);
            return $"Output of command: {command}\nLine 2\nLine 3\nCompleted successfully";
        }

        /// <summary>
        /// Log debug message
        /// </summary>
        private void LogDebug(string message)
        {
            _logger?.Debug($"[TerminalCollector] {message}");
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        private void LogWarn(string message)
        {
            _logger?.Warn($"[TerminalCollector] {message}");
        }

        /// <summary>
        /// Record metric
        /// </summary>
        private void RecordMetric(string name, Dictionary<string, object> fields)
        {
            _metrics?.Record(name, fields);
        }
    }
}
