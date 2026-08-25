using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an immutable snapshot of a file's content at a specific point in time.
    /// Baselines are created before each change is applied, enabling per-change rollback.
    /// </summary>
    public class ChangeBaseline
    {
        /// <summary>
        /// Full path to the file this baseline represents.
        /// </summary>
        [JsonProperty("filePath")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// The complete content of the file at the time this baseline was created.
        /// </summary>
        [JsonProperty("baselineContent")]
        public string BaselineContent { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when this baseline was captured.
        /// </summary>
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
