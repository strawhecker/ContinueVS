using System;
using Newtonsoft.Json;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Enumeration of context item types.
    /// </summary>
    public enum ContextItemType
    {
        /// <summary>
        /// Context from a source file.
        /// </summary>
        File,

        /// <summary>
        /// Context from a code symbol (class, function, etc.).
        /// </summary>
        Symbol,

        /// <summary>
        /// Context from documentation.
        /// </summary>
        Documentation,

        /// <summary>
        /// Context from a recent file.
        /// </summary>
        Recent,

        /// <summary>
        /// Custom or manual context.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Represents a context item for inclusion in LLM prompts.
    /// </summary>
    public class ContextItem
    {
        /// <summary>
        /// Unique identifier for this context item.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of context item.
        /// </summary>
        [JsonProperty("type")]
        public ContextItemType Type { get; set; }

        /// <summary>
        /// File path associated with this context.
        /// </summary>
        [JsonProperty("filePath")]
        public string? FilePath { get; set; }

        /// <summary>
        /// Starting line number in the file (1-based, optional).
        /// </summary>
        [JsonProperty("startLine")]
        public int? StartLine { get; set; }

        /// <summary>
        /// Ending line number in the file (1-based, optional).
        /// </summary>
        [JsonProperty("endLine")]
        public int? EndLine { get; set; }

        /// <summary>
        /// The actual content of this context item.
        /// </summary>
        [JsonProperty("content")]
        public string? Content { get; set; }

        /// <summary>
        /// Relevance score (0.0 to 1.0, where 1.0 is most relevant).
        /// </summary>
        [JsonProperty("relevance")]
        public double Relevance { get; set; } = 0.5;

        /// <summary>
        /// Source or origin of this context item.
        /// </summary>
        [JsonProperty("source")]
        public string? Source { get; set; }

        /// <summary>
        /// Timestamp when this context was added.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
