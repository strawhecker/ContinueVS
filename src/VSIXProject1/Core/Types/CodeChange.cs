using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an immutable atomic source code modification.
    /// Each change tracks the file path, original content, new content, and a baseline snapshot.
    /// </summary>
    public class CodeChange
    {
        /// <summary>
        /// Unique identifier for this change.
        /// </summary>
        [JsonProperty("changeId")]
        public string ChangeId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Full path to the file being modified.
        /// </summary>
        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// The original content of the file before this change was applied.
        /// </summary>
        [JsonProperty("oldContent")]
        public string OldContent { get; set; } = string.Empty;

        /// <summary>
        /// The new content after this change is applied.
        /// </summary>
        [JsonProperty("newContent")]
        public string NewContent { get; set; } = string.Empty;

        /// <summary>
        /// The baseline snapshot taken before this change was applied.
        /// This allows per-change rollback without affecting earlier changes.
        /// </summary>
        [JsonProperty("baseline")]
        public ChangeBaseline? Baseline { get; set; } = null;

        /// <summary>
        /// Timestamp when this change was created.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Description of what this change accomplishes.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
    }
}
