using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of indexing status values.
    /// </summary>
    public enum IndexingStatus
    {
        /// <summary>
        /// Indexing is idle and not running.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// Indexing is currently in progress.
        /// </summary>
        Indexing = 1,

        /// <summary>
        /// Indexing has been paused.
        /// </summary>
        Paused = 2,

        /// <summary>
        /// Indexing encountered an error.
        /// </summary>
        Error = 3,

        /// <summary>
        /// Indexing has been cancelled.
        /// </summary>
        Cancelled = 4
    }

    /// <summary>
    /// Represents progress updates for the indexing operation.
    /// </summary>
    public class IndexingProgressUpdate
    {
        /// <summary>
        /// Current status of the indexing operation.
        /// </summary>
        [JsonProperty("status")]
        public IndexingStatus Status { get; set; } = IndexingStatus.Idle;

        /// <summary>
        /// Number of files processed so far.
        /// </summary>
        [JsonProperty("filesProcessed")]
        public int FilesProcessed { get; set; }

        /// <summary>
        /// Total number of files to process.
        /// </summary>
        [JsonProperty("totalFiles")]
        public int TotalFiles { get; set; }

        /// <summary>
        /// Path of the file currently being processed.
        /// </summary>
        [JsonProperty("currentFile")]
        public string? CurrentFile { get; set; }

        /// <summary>
        /// Percentage of indexing completion (0-100).
        /// </summary>
        [JsonProperty("percentComplete")]
        public double PercentComplete { get; set; }

        /// <summary>
        /// Time elapsed since indexing started.
        /// </summary>
        [JsonProperty("elapsedTime")]
        public TimeSpan? ElapsedTime { get; set; }

        /// <summary>
        /// Estimated time remaining until completion.
        /// </summary>
        [JsonProperty("estimatedTimeRemaining")]
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// Timestamp when this update was generated.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
