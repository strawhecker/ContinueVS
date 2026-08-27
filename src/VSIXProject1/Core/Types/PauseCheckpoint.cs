using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents an in-memory checkpoint captured when streaming is paused.
    /// Preserves the current LLM response buffer, chunk metadata, and session context snapshot
    /// for potential resume operations or disk persistence (gap31_4).
    /// </summary>
    public class PauseCheckpoint
    {
        /// <summary>
        /// The accumulated streamed text (LLM response) at the moment pause was triggered.
        /// Concatenation of all CompletionChunk.Content values up to pause point.
        /// </summary>
        [JsonProperty("streamedText")]
        public string StreamedText { get; set; } = string.Empty;

        /// <summary>
        /// Number of chunks buffered and included in StreamedText.
        /// </summary>
        [JsonProperty("chunkCount")]
        public int ChunkCount { get; set; }

        /// <summary>
        /// UTC timestamp when pause was triggered (checkpoint captured).
        /// </summary>
        [JsonProperty("pauseTimestamp")]
        public DateTime PauseTimestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Snapshot of available session context items at pause moment.
        /// Maps context item type/label to a brief summary (e.g., "File: Main.cs (200 lines)").
        /// Used for validation and display during resume.
        /// </summary>
        [JsonProperty("sessionContextSnapshot")]
        public Dictionary<string, string> SessionContextSnapshot { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Optional error details if pause was triggered due to an error condition.
        /// </summary>
        [JsonProperty("errorDetails")]
        public string? ErrorDetails { get; set; }
    }
}
