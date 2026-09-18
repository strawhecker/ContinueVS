#nullable enable

using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Determines whether a tool name is a pure read tool eligible for auto-dedup (gap80).
    /// </summary>
    public interface IReadDeduplicator
    {
        /// <summary>
        /// Returns true if the named tool is a pure read tool that may be auto-deduped.
        /// Mutating tools (edit_file, run_terminal_command, ...) are NEVER deduped — two edits
        /// are not duplicates and merging them would corrupt the change/rollback chain.
        /// </summary>
        bool IsPureReadTool(string toolName);

        /// <summary>
        /// Extracts the canonical file path used for dedup keying from a tool call's arguments.
        /// Returns null when the tool has no single file-path identity (e.g. directory lists).
        /// </summary>
        string? ExtractKeyPath(ToolCall toolCall);
    }
}
