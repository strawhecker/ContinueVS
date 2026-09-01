using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// File-based logger implementation of IBridgeLogger.
    /// Writes log messages to ~/.continueVS/logs/continue-vs-{date}.log
    /// Uses a concurrent queue with background writer thread for non-blocking I/O.
    /// </summary>
    public class FileLogger : IBridgeLogger, IDisposable
    {
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
        private readonly string _logsDirectory;
        private volatile bool _running = true;
        private Thread _writerThread;

        public FileLogger()
        {
            _logsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continueVS",
                "logs"
            );

            // Create logs directory if it doesn't exist
            try
            {
                Directory.CreateDirectory(_logsDirectory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLogger] Failed to create logs directory: {ex.Message}");
            }

            // Rotate old logs (keep last 10 files)
            RotateOldLogs();

            // Start background writer thread
            _writerThread = new Thread(WriterThreadLoop)
            {
                Name = "ContinueVS-FileLogger",
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true
            };
            _writerThread.Start();
        }

        public Task WriteDebugAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            EnqueueMessage(message);
            return Task.CompletedTask;
        }

        public Task WriteInfoAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            EnqueueMessage(message);
            return Task.CompletedTask;
        }

        public Task WriteWarningAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            EnqueueMessage(message);
            return Task.CompletedTask;
        }

        public Task WriteErrorAsync(string message, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null)
        {
            var fullMessage = exception != null 
                ? $"{message} {exception}"
                : message;
            EnqueueMessage(fullMessage);
            return Task.CompletedTask;
        }

        public Task FlushAsync()
        {
            FlushQueue();
            return Task.CompletedTask;
        }

        private void EnqueueMessage(string message)
        {
            if (_running)
            {
                _messageQueue.Enqueue(message);
            }
        }

        private void WriterThreadLoop()
        {
            try
            {
                while (_running)
                {
                    Thread.Sleep(1000);  // Wake up every 1 second
                    FlushQueue();
                }

                // Final flush before shutdown
                FlushQueue();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLogger] Writer thread error: {ex.Message}");
            }
        }

        private void FlushQueue()
        {
            try
            {
                var messages = new List<string>();
                while (_messageQueue.TryDequeue(out var message))
                {
                    messages.Add(message);
                }

                if (messages.Count == 0)
                    return;

                var logPath = Path.Combine(_logsDirectory, $"continue-vs-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllLines(logPath, messages);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLogger] Failed to write logs: {ex.Message}");
            }
        }

        private void RotateOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logsDirectory))
                    return;

                var files = Directory.GetFiles(_logsDirectory, "continue-vs-*.log");
                if (files.Length <= 10)
                    return;

                // Sort files by creation time, delete oldest
                Array.Sort(files, (a, b) => 
                    File.GetCreationTime(a).CompareTo(File.GetCreationTime(b)));

                for (int i = 0; i < files.Length - 10; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileLogger] Failed to delete old log: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileLogger] Failed to rotate logs: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _running = false;

            // Wait for writer thread to finish (up to 5 seconds)
            if (_writerThread?.IsAlive ?? false)
            {
                if (!_writerThread.Join(5000))
                {
                    System.Diagnostics.Debug.WriteLine("[FileLogger] Writer thread did not exit within timeout");
                }
            }

            // Final flush
            FlushQueue();
        }
    }
}
