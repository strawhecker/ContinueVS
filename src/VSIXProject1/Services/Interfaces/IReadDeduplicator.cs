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

        /// <summary>
        /// gap80_1: Returns true when the named tool is a mutating tool (edit_file, write_file,
        /// single_find_and_replace, create_new_file, ...). Mutating tools never get merged, but
        /// they INVALIDATE the preceding reads of the path they touch.
        /// </summary>
        bool IsMutatingTool(string toolName);

        /// <summary>
        /// gap80_1: Returns true when the named tool produces a snapshot of mutable directory/
        /// glob/search state (ls, file_glob_search, search_codebase, grep_search, get_problems,
        /// view_diff). Such snapshots have no natural single-file successor; they are either
        /// structurally invalidated by a same-scope mutation or visibly marked STALE.
        /// </summary>
        bool IsDirectorySnapshotTool(string toolName);

        /// <summary>
        /// gap80_1: Extracts the canonical path a mutating tool touches from its arguments.
        /// Returns null when the mutation has no clear single path identity.
        /// </summary>
        string? ExtractMutationPath(ToolCall toolCall);

        /// <summary>
        /// gap80_1: Classifies a read tool call's coverage as "FULL" (read_file/view_file) or
        /// "RANGE(start,end)" (read_file_range). Used as the coverage half of the
        /// (path, coverage) key for coverage-aware read supersession.
        /// </summary>
        string ExtractCoverage(ToolCall toolCall);
    }
}
