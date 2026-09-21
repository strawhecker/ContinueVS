#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Identifies pure read tools and extracts their file-path identity (gap80).
    ///
    /// Auto-dedup is restricted to pure read tools. Never dedup mutating tools
    /// (edit_file, run_terminal_command, write_file, create_new_file, git_commit,
    /// single_find_and_replace, run_pytest, create_rule_block, create_snippet, open_file):
    /// two edits are not duplicates and merging them would corrupt the change/rollback chain.
    /// </summary>
    public class ReadDeduplicator : IReadDeduplicator
    {
        private static readonly HashSet<string> PureReadTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "read_file",
            "view_file",
            "read_file_range",
            "grep_search",
            "git_status",
            "git_diff",
            "git_log",
            "view_diff",
            "get_problems",
            "read_currently_open_file"
        };

        private static readonly HashSet<string> MutatingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "edit_file",
            "write_file",
            "create_new_file",
            "create_folder",
            "run_terminal_command",
            "run_subprocess",
            "git_commit",
            "single_find_and_replace",
            "run_pytest",
            "create_rule_block",
            "create_snippet",
            "open_file"
        };

        /// <summary>
        /// gap80_1: Tools that return snapshots of mutable directory/glob/search state.
        /// These have no natural single-file successor; their staleness is either structurally
        /// invalidated by a mutation touching the same scope or visibly marked STALE — never
        /// silently removed on time.
        /// </summary>
        private static readonly HashSet<string> DirectorySnapshotTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ls",
            "file_glob_search",
            "search_codebase",
            "grep_search",
            "get_problems",
            "view_diff"
        };

        /// <inheritdoc />
        public bool IsPureReadTool(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
                return false;
            // If it's explicitly a mutating tool, never dedup.
            if (MutatingTools.Contains(toolName))
                return false;
            return PureReadTools.Contains(toolName);
        }

        /// <inheritdoc />
        public bool IsMutatingTool(string toolName)
        {
            return !string.IsNullOrEmpty(toolName) && MutatingTools.Contains(toolName);
        }

        /// <inheritdoc />
        public bool IsDirectorySnapshotTool(string toolName)
        {
            return !string.IsNullOrEmpty(toolName) && DirectorySnapshotTools.Contains(toolName);
        }

        /// <inheritdoc />
        public string? ExtractKeyPath(ToolCall toolCall)
        {
            if (toolCall?.Arguments == null)
                return null;

            // These tools carry a single file identity in "filepath".
            if (toolCall.Arguments.TryGetValue("filepath", out var fp) && fp != null)
            {
                var path = fp.ToString();
                if (!string.IsNullOrWhiteSpace(path))
                    return Normalize(path);
            }

            // read_currently_open_file has no argument; dedup by its literal name (the
            // active file identity changes only via explicit file ops we don't intercept).
            if (string.Equals(toolCall.Name, "read_currently_open_file", StringComparison.OrdinalIgnoreCase))
                return "read_currently_open_file";

            return null;
        }

        /// <inheritdoc />
        public string? ExtractMutationPath(ToolCall toolCall)
        {
            if (toolCall?.Arguments == null)
                return null;
            foreach (var key in new[] { "filepath", "path", "file", "newFilepath", "oldFilepath" })
            {
                if (toolCall.Arguments.TryGetValue(key, out var val) && val != null)
                {
                    var path = val.ToString();
                    if (!string.IsNullOrWhiteSpace(path))
                        return Normalize(path);
                }
            }
            return null;
        }

        /// <inheritdoc />
        public string ExtractCoverage(ToolCall toolCall)
        {
            // read_file / view_file read the whole file -> FULL.
            if (string.Equals(toolCall.Name, "read_file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(toolCall.Name, "view_file", StringComparison.OrdinalIgnoreCase))
            {
                return "FULL";
            }

            // read_file_range -> RANGE(start,end) using the normalized line args.
            if (string.Equals(toolCall.Name, "read_file_range", StringComparison.OrdinalIgnoreCase) &&
                toolCall.Arguments != null)
            {
                var start = toolCall.Arguments.TryGetValue("startLine", out var s) ? s?.ToString() : null;
                var end = toolCall.Arguments.TryGetValue("endLine", out var e) ? e?.ToString() : null;
                return $"RANGE({start},{end})";
            }

            // Everything else -> FULL (best-effort; only read_file/full-id reads are keyed).
            return "FULL";
        }

        private static string Normalize(string path)
        {
            // Treat directory separators equivalently so C:\a\b and C:/a/b key the same.
            var normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            return normalized.Trim();
        }
    }
}
