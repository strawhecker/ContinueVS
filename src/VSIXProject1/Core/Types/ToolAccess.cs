using System;
using System.Collections.Generic;
using System.Linq;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Authoritative access classification for tools, keyed by tool name. Replaces the per-tool
    /// SupportedModes mode matrix (which was stripped by the config/resource pipeline and leaked
    /// write/command tools into read-only modes like Bare).
    ///
    /// Capability tiers are monotonic: Debug ⊇ Write ⊇ Read. A mode's tool set is the union of
    /// every tier at or below it:
    ///   - Ask, Bare, Plan, Reason → Read only
    ///   - Agent → Read ∪ Write
    ///   - Debug → Read ∪ Write ∪ Debug
    ///
    /// FAIL-CLOSED: a tool whose name is in no list is classified <see cref="ToolAccessLevel.None"/>
    /// and is NOT returned for any mode. Nothing reaches the LLM roster through a non-list channel.
    /// </summary>
    public enum ToolAccessLevel
    {
        /// <summary>Not classified. Never exposed to any mode (fail-closed).</summary>
        None = 0,
        /// <summary>Observation / bounded non-mutating access. Available to all read-only modes.</summary>
        Read = 1,
        /// <summary>Mutating / arbitrary-effect access. Available to Agent and Debug.</summary>
        Write = 2,
        /// <summary>Debugger / process-lifecycle access. Available to Debug only.</summary>
        Debug = 3
    }

    public static class ToolAccess
    {
        /// <summary>Observation-only tools, available in Ask, Bare, Plan, and Reason.</summary>
        public static readonly IReadOnlyCollection<string> Read = new HashSet<string>
        {
            "read_file",
            "view_file",
            "read_file_range",
            "read_currently_open_file",
            "search_codebase",
            "grep_search",
            "file_glob_search",
            "ls",
            "get_problems",
            "view_diff",
            "ide_active_document",
            "ide_open_file",
            "ide_output_pane",
            "ide_navigate_to",
            "ide_goto_definition",
            "ide_find_symbol",
            "ide_build_configuration",
            "ide_launch_profile",
            "open_file",
            "read_plan",
            "update_plan",
            "write_plan",
            "ask_user",
            "retire_from_context",
            "git_status",
            "git_diff",
            "git_log"
        };

        /// <summary>Mutating / arbitrary-effect tools. Superset of <see cref="Read"/>.</summary>
        public static readonly IReadOnlyCollection<string> Write = new HashSet<string>
        {
            "run_terminal_command",
            "create_new_file",
            "create_folder",
            "edit_file",
            "single_find_and_replace",
            "ide_build",
            "create_rule_block",
            "create_snippet",
            "run_pytest",
            "git_commit"
        };

        /// <summary>Debugger / process-lifecycle tools. Superset of <see cref="Write"/>.</summary>
        public static readonly IReadOnlyCollection<string> Debug = new HashSet<string>
        {
            "debug_start",
            "debug_stop",
            "debug_restart",
            "debug_select_session",
            "debug_evaluate",
            "debug_set_value",
            "debug_memory_read",
            "debug_memory_write",
            "debug_run_to_cursor",
            "debug_thread_set_state",
            "ide_attach_to_process"
        };

        /// <summary>
        /// Classifies a tool. Returns the highest tier the tool belongs to, or
        /// <see cref="ToolAccessLevel.None"/> if the tool is in no list (fail-closed).
        /// </summary>
        public static ToolAccessLevel LevelFor(string toolName)
        {
            if (Debug.Contains(toolName))
                return ToolAccessLevel.Debug;
            if (Write.Contains(toolName))
                return ToolAccessLevel.Write;
            if (Read.Contains(toolName))
                return ToolAccessLevel.Read;
            return ToolAccessLevel.None;
        }

        /// <summary>
        /// Whether the tool should be exposed in the given mode under the monotonic tier model.
        /// Unclassified tools (<see cref="ToolAccessLevel.None"/>) are never exposed.
        /// </summary>
        public static bool IsAvailableInMode(string toolName, ChatMode mode)
        {
            var level = LevelFor(toolName);
            if (level == ToolAccessLevel.None)
                return false;

            return mode switch
            {
                ChatMode.Ask or ChatMode.Bare or ChatMode.Plan or ChatMode.Reason => level == ToolAccessLevel.Read,
                ChatMode.Agent => level == ToolAccessLevel.Read || level == ToolAccessLevel.Write,
                ChatMode.Debug => level >= ToolAccessLevel.Read, // Read ∪ Write ∪ Debug
                _ => false
            };
        }
    }
}
