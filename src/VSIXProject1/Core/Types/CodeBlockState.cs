#nullable enable

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents the state and metadata of a code block for gap53 per-block actions.
    /// </summary>
    public class CodeBlockState
    {
        /// <summary>
        /// Unique identifier for this code block (index or GUID).
        /// </summary>
        public string BlockId { get; set; } = string.Empty;

        /// <summary>
        /// Programming language or code block language hint (e.g., "python", "csharp", "bash").
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Raw code content of the block.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Currently selected action for this block: "Copy" or "Apply".
        /// </summary>
        public string SelectedAction { get; set; } = "Copy";

        /// <summary>
        /// Timestamp when this block was last actioned (ISO 8601 format).
        /// </summary>
        public string? LastActionTime { get; set; }
    }
}
