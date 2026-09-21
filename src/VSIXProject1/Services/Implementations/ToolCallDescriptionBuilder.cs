using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// gap87: Fabricates a short, factual, human-readable description of what a tool call
    /// actually did, derived ONLY from the tool-call arguments — never from LLM prose, never
    /// from an LLM-fillable parameter. This is used to render compact tool-call bubbles in the
    /// chat so the user can tell what happened well enough to evaluate / prune the entry.
    ///
    /// The description is display-only: it is never written into <see cref="ChatMessage.Content"/>,
    /// so it is never serialized back into the LLM payload regardless of length.
    /// </summary>
    public static class ToolCallDescriptionBuilder
    {
        /// <summary>
        /// Builds a description for a tool call. Guards against null/missing args and never throws
        /// for unknown tools (compact fallback).
        /// </summary>
        public static string Build(ToolCall toolCall)
        {
            if (toolCall == null || string.IsNullOrWhiteSpace(toolCall.Name))
                return "tool call";

            return Build(toolCall.Name, toolCall.Arguments);
        }

        /// <summary>
        /// Builds a description for a tool by name + arguments. Missing/atypical args degrade to a
        /// compact snapshot that never throws.
        /// </summary>
        public static string Build(string toolName, IDictionary<string, object>? arguments)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return "tool call";

            var args = arguments ?? new Dictionary<string, object>();

            switch (toolName)
            {
                case "read_file":
                case "view_file":
                case "open_file":
                case "read_currently_open_file":
                    return $"{ToolVerb(toolName)} {GetString(args, "filepath", "path", "file", "filename")}";

                case "read_file_range":
                    {
                        string file = GetString(args, "filepath", "path", "file", "filename");
                        string start = GetString(args, "startLine");
                        string end = GetString(args, "endLine");
                        if (!string.IsNullOrEmpty(start) || !string.IsNullOrEmpty(end))
                            return $"read {file} lines {First(start, "?")}-{First(end, "?")}";
                        return $"read {file} (range)";
                    }

                case "run_terminal_command":
                case "run_subprocess":
                    {
                        string command = GetString(args, "command");
                        if (string.IsNullOrEmpty(command))
                            return $"run: {GetString(args)}";
                        return $"run: {Truncate(command, 80)}";
                    }

                case "edit_file":
                case "edit_existing_file":
                case "single_find_and_replace":
                    return $"edit {GetString(args, "filepath", "path", "file", "filename")} (§ old\u2192new)";

                case "create_new_file":
                case "write_file":
                case "create_folder":
                    return $"create {GetString(args, "filepath", "path", "folderpath", "dir")}";

                case "ls":
                    {
                        string dir = GetString(args, "dirPath", "dir", "path");
                        bool recursive = GetBool(args, "recursive");
                        if (string.IsNullOrEmpty(dir))
                            dir = "(current dir)";
                        return recursive ? $"list {dir} (recursive)" : $"list {dir}";
                    }

                case "file_glob_search":
                    return $"glob {GetString(args, "pattern")}";

                case "search_codebase":
                case "grep_search":
                    return $"search for \"{Truncate(GetString(args, "query", "pattern"), 60)}\"";

                case "git_status":
                    return "git status";
                case "git_diff":
                    return "git diff";
                case "git_log":
                    return "git log";
                case "git_commit":
                    return "git commit";
                case "view_diff":
                    return "view diff";
                case "get_problems":
                    return "fetch compiler problems";
                case "run_pytest":
                    return "run tests";
                case "write_plan":
                    return $"write plan: {Truncate(GetString(args, "title"), 40)}";
                case "ask_user":
                    return $"ask user: {Truncate(GetString(args, "question"), 60)}";
                case "create_rule_block":
                    return "create rule block";
                case "create_snippet":
                    return "create snippet";
                case "read_skill":
                    return $"read skill {GetString(args, "skillName")}";
                case "search_web":
                    return $"web search: {Truncate(GetString(args, "query"), 60)}";
                case "fetch_url_content":
                    return $"fetch {GetString(args, "url")}";
                case "request_rule":
                    return $"request rule {GetString(args, "name")}";

                default:
                    return BuildFallback(toolName, args);
            }
        }

        private static string ToolVerb(string toolName) =>
            toolName switch
            {
                "view_file" => "view",
                "open_file" => "open",
                "read_currently_open_file" => "read open file",
                _ => toolName == "read_file" ? "read" : toolName
            };

        /// <summary>
        /// Compact, deterministic fallback for unknown/atypical tools. Never throws.
        /// </summary>
        private static string BuildFallback(string toolName, IDictionary<string, object> args)
        {
            var parts = new List<string>();
            foreach (var pair in args
                .OrderBy(k => k.Key, StringComparer.Ordinal)
                .Where(k => k.Value != null)
                .Take(3))
            {
                string value = pair.Value?.ToString() ?? string.Empty;
                if (value.Length > 40)
                    value = value.Substring(0, 40) + "\u2026";
                parts.Add($"{pair.Key}={value}");
            }

            return parts.Count > 0 ? $"{toolName} ({string.Join(", ", parts)})" : toolName;
        }

        private static string GetString(IDictionary<string, object> args, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (args.TryGetValue(key, out var val) && val != null)
                {
                    string s = val.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            return string.Empty;
        }

        private static bool GetBool(IDictionary<string, object> args, string key)
        {
            if (args.TryGetValue(key, out var val) && val != null)
            {
                if (val is bool b) return b;
                if (bool.TryParse(val.ToString(), out bool parsed)) return parsed;
            }
            return false;
        }

        private static string First(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.Length <= max)
                return value;
            return value.Substring(0, max) + "\u2026";
        }
    }
}
