using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Handler Metrics Snapshot Model (Step 109)
    /// Serializable container for handler metrics captured at a point in time.
    /// </summary>
    public class HandlerMetricsSnapshot
    {
        public DateTime Timestamp { get; set; }

        public HandlerMetric[]? Handlers { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Individual handler metric data.
    /// </summary>
    public class HandlerMetric
    {
        public string? Name { get; set; }

        public LatencyMetric? Latency { get; set; }

        public double ErrorRate { get; set; }

        public int RequestCount { get; set; }

        public int TimeoutCount { get; set; }

        public double? CacheHitRate { get; set; }
    }

    /// <summary>
    /// Latency percentiles in milliseconds.
    /// </summary>
    public class LatencyMetric
    {
        public double P50 { get; set; }

        public double P95 { get; set; }

        public double P99 { get; set; }
    }

    /// <summary>
    /// Custom exception for metrics collection errors.
    /// </summary>
    public class MetricsCollectionException : Exception
    {
        public string Code { get; }

        public MetricsCollectionException(string message, string code = "METRICS_ERROR", Exception? innerException = null)
            : base(message, innerException)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Host-side metrics collector for ContinueVS Bridge (Step 109)
    /// 
    /// Optional integration with Node.js aggregator for enhanced metrics.
    /// Provides host-level metrics (memory, CPU, process state).
    /// Gracefully degrades if bridge unavailable.
    /// </summary>
    public class HandlerMetricsCollector
    {
        private readonly object? _bridgeServiceProvider;
        private readonly object? _logger;
        private readonly string _storagePath;

        /// <summary>
        /// Initialize metrics collector.
        /// </summary>
        public HandlerMetricsCollector(
            object? bridgeServiceProvider = null,
            object? logger = null)
        {
            _bridgeServiceProvider = bridgeServiceProvider;
            _logger = logger;
            _storagePath = GetStoragePath() ?? string.Empty;
        }

        /// <summary>
        /// Get default metrics storage path: ~/.continue/metrics/
        /// </summary>
        public static string GetStoragePath()
        {
            string home = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(home, ".continue", "metrics");
        }

        /// <summary>
        /// Create snapshot with current handler metrics and host-level data.
        /// </summary>
        public async Task<HandlerMetricsSnapshot> CreateSnapshotAsync()
        {
            try
            {
                var snapshot = new HandlerMetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Handlers = Array.Empty<HandlerMetric>(),
                    Metadata = new Dictionary<string, object>()
                };

                // Add host-level metrics
                try
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    snapshot.Metadata["ProcessMemoryMB"] = process.WorkingSet64 / 1024 / 1024;
                    snapshot.Metadata["ProcessCpuUsage"] = process.TotalProcessorTime.TotalMilliseconds;
                    snapshot.Metadata["ThreadCount"] = process.Threads.Count;
                }
                catch (Exception)
                {
                    // Log warning silently
                }

                // Try to get bridge metrics if available
                if (_bridgeServiceProvider != null)
                {
                    try
                    {
                        // This would call bridge:getProfilerData if implemented
                        // For now, leave handlers empty - will be populated by Node.js aggregator
                    }
                    catch (Exception)
                    {
                        // Log warning silently
                    }
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                throw new MetricsCollectionException(
                    $"Failed to create snapshot: {ex.Message}",
                    "SNAPSHOT_FAILED",
                    ex);
            }
        }

        /// <summary>
        /// Persist snapshot to local storage.
        /// </summary>
        public async Task PersistSnapshotAsync(HandlerMetricsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new MetricsCollectionException("Snapshot cannot be null", "NULL_SNAPSHOT");
            }

            try
            {
                // Ensure directory exists
                Directory.CreateDirectory(_storagePath);

                // Create filename: metrics-YYYY-MM-DD.jsonl
                string filename = $"metrics-{snapshot.Timestamp:yyyy-MM-dd}.jsonl";
                string filepath = Path.Combine(_storagePath, filename);

                // Serialize snapshot to JSON
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    WriteIndented = false
                };

                string json = JsonSerializer.Serialize(snapshot, options);
                string line = json + "\n";

                // Append to file (atomically via FileStream)
                using (var stream = new FileStream(filepath, FileMode.Append, FileAccess.Write))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        await writer.WriteAsync(line);
                        await writer.FlushAsync();
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new MetricsCollectionException(
                    $"Permission denied writing to metrics storage: {ex.Message}",
                    "PERMISSION_DENIED",
                    ex);
            }
            catch (IOException ex)
            {
                throw new MetricsCollectionException(
                    $"I/O error persisting snapshot: {ex.Message}",
                    "IO_ERROR",
                    ex);
            }
            catch (Exception ex)
            {
                throw new MetricsCollectionException(
                    $"Failed to persist snapshot: {ex.Message}",
                    "PERSIST_FAILED",
                    ex);
            }
        }

        /// <summary>
        /// Create and persist a snapshot in one operation.
        /// </summary>
        public async Task<HandlerMetricsSnapshot> CaptureAndPersistAsync()
        {
            var snapshot = await CreateSnapshotAsync();
            await PersistSnapshotAsync(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Get disk usage statistics for metrics storage.
        /// </summary>
        public StorageStats? GetStorageStats()
        {
            try
            {
                var dirInfo = new DirectoryInfo(_storagePath);
                if (!dirInfo.Exists)
                {
                    return new StorageStats { TotalSizeBytes = 0, FileCount = 0 };
                }

                var files = dirInfo.GetFiles("metrics-*.jsonl");
                long totalBytes = files.Sum(f => f.Length);

                return new StorageStats
                {
                    Directory = _storagePath,
                    FileCount = files.Length,
                    TotalSizeBytes = totalBytes,
                    TotalSizeMB = totalBytes / 1024.0 / 1024.0
                };
            }
            catch (Exception ex)
            {
                _logger?.GetType().GetMethod("LogWarning")?.Invoke(_logger, new object[] { $"Failed to get storage stats: {ex.Message}" });
                return null;
            }
        }

        /// <summary>
        /// Clean up old snapshot files.
        /// </summary>
        public async Task CleanupOldSnapshotsAsync(int retentionDays = 7)
        {
            try
            {
                var dirInfo = new DirectoryInfo(_storagePath);
                if (!dirInfo.Exists)
                {
                    return;
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var oldFiles = dirInfo.GetFiles("metrics-*.jsonl")
                    .Where(f => f.LastWriteTimeUtc < cutoffDate)
                    .ToList();

                foreach (var file in oldFiles)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception)
                    {
                        // Ignore deletion errors
                    }
                }
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Storage statistics container.
    /// </summary>
    public class StorageStats
    {
        public string? Directory { get; set; }

        public int FileCount { get; set; }

        public long TotalSizeBytes { get; set; }

        public double TotalSizeMB { get; set; }
    }
}
