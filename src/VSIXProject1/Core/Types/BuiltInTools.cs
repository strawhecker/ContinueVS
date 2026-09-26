using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ContinueVS.Services;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Static factory for built-in tool definitions.
    /// Generates standardized ToolDefinition instances for core tools like read_file, create_new_file, etc.
    /// Matches reference architecture from Continue.js.
    /// </summary>
    public static class BuiltInToolsRegistry
    {
        /// <summary>
        /// Creates a standard built-in tool definition with common properties.
        /// </summary>
        private static ToolDefinition CreateToolDefinition(
            string name,
            string description,
            IList<ParameterDefinition> parameters,
            string returnsDescription,
            bool isEnabled = true,
            string invokePerm = "Automatic",
            List<ChatMode>? supportedModes = null)
        {
            LoggerService.Current.WriteDebug($"[gap8_1-factory-create] CreateToolDefinition: {name}, params={parameters.Count}, enabled={isEnabled}");
            var tool = new ToolDefinition
            {
                Name = name,
                Description = description,
                Category = "Built-In",
                Parameters = parameters,
                ReturnsDescription = returnsDescription,
                IsEnabled = isEnabled,
                IsAsync = true,
                ToolType = "builtin",
                LastModified = DateTime.Now,
                SupportedModes = supportedModes ?? new List<ChatMode>()
            };
            return tool;
        }

        /// <summary>
        /// read_file: View the contents of an existing file.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetReadFileTool()
        {
            return CreateToolDefinition(
                name: "read_file",
                description: "Use this tool if you need to view the contents of an existing file",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to read. Can be a relative path (from workspace root), absolute path, tilde path (~/...), or file:// URI",
                        IsRequired = true
                    }
                },
                returnsDescription: "The file contents as a string",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// create_new_file: Create a new file. Only use when a file doesn't exist and should be created.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// </summary>
        public static ToolDefinition GetCreateNewFileTool()
        {
            return CreateToolDefinition(
                name: "create_new_file",
                description: "Create a new file. Only use this when a file doesn't exist and should be created",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path where the new file should be created. Can be a relative path (from workspace root), absolute path, tilde path (~/...), or file:// URI",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "contents",
                        Type = "string",
                        Description = "The contents to write to the new file",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the file was created successfully",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// create_folder: Create a new directory/folder. Only use when a folder doesn't exist and should be created.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// </summary>
        public static ToolDefinition GetCreateFolderTool()
        {
            return CreateToolDefinition(
                name: "create_folder",
                description: "Create a new directory/folder. Only use this when a folder doesn't exist and should be created",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "folderpath",
                        Type = "string",
                        Description = "The path where the new folder should be created. Can be a relative path (from workspace root), absolute path, tilde path (~/...), or file:// URI",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the folder was created successfully",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// run_terminal_command: Run a terminal command in the current directory.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// Note: Shell is powershell.exe on Windows, bash on Unix-like systems.
        /// </summary>
        public static ToolDefinition GetRunTerminalCommandTool()
        {
            return CreateToolDefinition(
                name: "run_terminal_command",
                description: "Run a terminal command in the current directory. The shell is not stateful and will not remember any previous commands. When a command is run in the background ALWAYS suggest using shell commands to stop it; NEVER suggest using Ctrl+C. When suggesting subsequent shell commands ALWAYS format them in shell command blocks. Do NOT perform actions requiring special/admin privileges. IMPORTANT: To edit files, use edit/create tools instead of bash commands (sed, awk, etc). Choose terminal commands and scripts optimized for the current platform.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "command",
                        Type = "string",
                        Description = "The command to run. This will be passed directly into the IDE shell",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "waitForCompletion",
                        Type = "boolean",
                        Description = "Whether to wait for the command to complete before returning. Default is true. Set to false to run the command in the background and collect output asynchronously.",
                        IsRequired = false,
                        DefaultValue = true
                    }
                },
                returnsDescription: "Standard output and error from the command",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// file_glob_search: Search for files recursively in the project using glob patterns.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only search)
        /// </summary>
        public static ToolDefinition GetFileGlobSearchTool()
        {
            return CreateToolDefinition(
                name: "file_glob_search",
                description: "Search for files recursively in the project using glob patterns. Supports ** for recursive directory search. Will not show many build, cache, secrets dirs/files (can use ls tool instead). Output may be truncated; use targeted patterns",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "pattern",
                        Type = "string",
                        Description = "Glob pattern for file path matching (e.g., '**/*.cs', '**/test/**/*.cs')",
                        IsRequired = true
                    }
                },
                returnsDescription: "List of file paths matching the glob pattern",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// view_diff: View the current diff of working changes.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetViewDiffTool()
        {
            return CreateToolDefinition(
                name: "view_diff",
                description: "View the current diff of working changes",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The unified diff of all current changes",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// read_currently_open_file: Read the currently open file in the IDE.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetReadCurrentlyOpenFileTool()
        {
            return CreateToolDefinition(
                name: "read_currently_open_file",
                description: "Read the currently open file in the IDE. If the user seems to be referring to a file that you can't see, or is requesting an action on content that seems missing, try using this tool",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The contents of the currently open file in the IDE",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// ls: List files and folders in a given directory.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetListDirectoryTool()
        {
            return CreateToolDefinition(
                name: "ls",
                description: "List files and folders in a given directory",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "dirPath",
                        Type = "string",
                        Description = "The directory path. Can be relative to project root, absolute path, tilde path (~/...), or file:// URI. Use forward slash paths",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "recursive",
                        Type = "boolean",
                        Description = "If true, lists files and folders recursively. To prevent unexpected large results, use this sparingly",
                        IsRequired = false,
                        DefaultValue = false
                    }
                },
                returnsDescription: "List of file and folder names in the directory",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// create_rule_block: Creates a 'rule' that can be referenced in future conversations.
        /// Default: Excluded (requires explicit user permission)
        /// </summary>
        public static ToolDefinition GetCreateRuleBlockTool()
        {
            return CreateToolDefinition(
                name: "create_rule_block",
                description: "Creates a 'rule' that can be referenced in future conversations. This should be used whenever you want to establish code standards / preferences that should be applied consistently, or when you want to avoid making a mistake again. Rule Types: - Always: Include only 'rule' (always included in model context) - Auto Attached: Include 'rule', 'globs', and/or 'regex' (included when files match patterns) - Agent Requested: Include 'rule' and 'description' (AI decides when to apply based on description) - Manual: Include only 'rule' (only included when explicitly mentioned using @ruleName)",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "name",
                        Type = "string",
                        Description = "Short, descriptive name summarizing the rule's purpose (e.g. 'React Standards', 'Type Hints')",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "rule",
                        Type = "string",
                        Description = "Clear, imperative instruction for future conversations",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the rule was created and can now be referenced",
                isEnabled: false);
        }

        /// <summary>
        /// edit_file: Edit or replace specific lines in an existing file.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// </summary>
        public static ToolDefinition GetEditFileTool()
        {
            return CreateToolDefinition(
                name: "edit_file",
                description: "Edit or replace specific lines in an existing file. Provide the old text to find and the new text to replace with",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to edit. Can be relative or absolute",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "oldText",
                        Type = "string",
                        Description = "The exact text to find and replace (must include surrounding context)",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "newText",
                        Type = "string",
                        Description = "The new text to replace oldText with",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation of the edit operation",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// search_codebase: Search the codebase for text matches using regex or literal text.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only search)
        /// </summary>
        public static ToolDefinition GetSearchCodebaseTool()
        {
            return CreateToolDefinition(
                name: "search_codebase",
                description: "Search the codebase for matches to a query. Can use regex patterns or literal text matching",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "query",
                        Type = "string",
                        Description = "The search query or regex pattern",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "maxResults",
                        Type = "number",
                        Description = "Maximum number of results to return",
                        IsRequired = false,
                        DefaultValue = 20
                    }
                },
                returnsDescription: "List of matching code snippets with file paths and line numbers",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// run_pytest: Run pytest test suite.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// Default: Ask First
        /// </summary>
        public static ToolDefinition GetRunPytestTool()
        {
            return CreateToolDefinition(
                name: "run_pytest",
                description: "Run pytest test suite to verify code changes",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "testPath",
                        Type = "string",
                        Description = "Path to test file or directory to run. If empty, runs all tests",
                        IsRequired = false
                    }
                },
                returnsDescription: "Test results including passed, failed, and skipped counts",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug },
                // Disabled by default: no pytest tests exist in this .NET project
                isEnabled: false);
        }

        /// <summary>
        /// get_problems: Get compiler errors, warnings, and IDE problems.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only diagnostics)
        /// </summary>
        public static ToolDefinition GetGetProblemsTool()
        {
            return CreateToolDefinition(
                name: "get_problems",
                description: "Get compiler errors, warnings, and IDE problems for the current project",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "List of problems with file paths, line numbers, severity, and messages",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// view_file: View a file with line numbers for easier reference.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetViewFileTool()
        {
            return CreateToolDefinition(
                name: "view_file",
                description: "View a file with line numbers for easier reference. Better for viewing full files",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to view",
                        IsRequired = true
                    }
                },
                returnsDescription: "File contents with line number prefixes",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// open_file: Open a file in the IDE editor.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only inspection)
        /// </summary>
        public static ToolDefinition GetOpenFileTool()
        {
            return CreateToolDefinition(
                name: "open_file",
                description: "Open a file in the IDE editor for viewing or editing",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to open",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the file was opened in the IDE",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// git_status: Show git status of the repository.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only VCS)
        /// </summary>
        public static ToolDefinition GetGitStatusTool()
        {
            return CreateToolDefinition(
                name: "git_status",
                description: "Show git status of the repository including modified files, staged changes, and untracked files",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "Git status output showing current branch and file changes",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// git_diff: Show git diff of changes.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only VCS)
        /// </summary>
        public static ToolDefinition GetGitDiffTool()
        {
            return CreateToolDefinition(
                name: "git_diff",
                description: "Show git diff of current changes in the repository. Can show staged changes, unstaged changes, or diff between commits",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filePath",
                        Type = "string",
                        Description = "Optional. Specific file to show diff for. If empty, shows all diffs",
                        IsRequired = false
                    },
                    new ParameterDefinition
                    {
                        Name = "staged",
                        Type = "boolean",
                        Description = "If true, shows only staged (cached) changes. Default is false (all changes)",
                        IsRequired = false,
                        DefaultValue = false
                    },
                    new ParameterDefinition
                    {
                        Name = "commitRange",
                        Type = "string",
                        Description = "Optional. Show diff between commits (e.g., 'abc123..def456' or 'HEAD~1..HEAD'). If provided, ignores staged parameter",
                        IsRequired = false
                    }
                },
                returnsDescription: "Unified diff format showing additions and deletions",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// git_log: Show git commit history.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only VCS history)
        /// </summary>
        public static ToolDefinition GetGitLogTool()
        {
            return CreateToolDefinition(
                name: "git_log",
                description: "Show git commit history of the repository",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "maxCommits",
                        Type = "number",
                        Description = "Maximum number of commits to show",
                        IsRequired = false,
                        DefaultValue = 10
                    }
                },
                returnsDescription: "Commit history with hashes, authors, dates, and messages",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// git_commit: Create a git commit with the given message.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// Default: Ask First
        /// </summary>
        public static ToolDefinition GetGitCommitTool()
        {
            return CreateToolDefinition(
                name: "git_commit",
                description: "Create a git commit with the given message",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "message",
                        Type = "string",
                        Description = "Commit message describing the changes",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation of the commit with commit hash",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// create_snippet: Create a reusable code snippet.
        /// Default: Excluded
        /// </summary>
        public static ToolDefinition GetCreateSnippetTool()
        {
            return CreateToolDefinition(
                name: "create_snippet",
                description: "Create a reusable code snippet for future reference or insertion",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "name",
                        Type = "string",
                        Description = "Name of the snippet for easy reference",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "code",
                        Type = "string",
                        Description = "The code content of the snippet",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the snippet was created",
                isEnabled: false);
        }

        /// <summary>
        /// read_file_range: Read a specific line range from a file (not the entire file).
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only)
        /// </summary>
        public static ToolDefinition GetReadFileRangeTool()
        {
            return CreateToolDefinition(
                name: "read_file_range",
                description: "Use this tool to view a specific range of lines from an existing file without loading the entire file",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to read. Can be a relative path (from workspace root), absolute path, tilde path (~/...), or file:// URI",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "startLine",
                        Type = "number",
                        Description = "The starting line number (1-based indexing)",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "endLine",
                        Type = "number",
                        Description = "The ending line number (1-based indexing, inclusive)",
                        IsRequired = true
                    }
                },
                returnsDescription: "The file contents for the specified line range",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// grep_search: Pattern search within files using regex.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - read-only search)
        /// </summary>
        public static ToolDefinition GetGrepSearchTool()
        {
            return CreateToolDefinition(
                name: "grep_search",
                description: "Search for files matching a regex pattern. Supports capturing groups and multiline patterns. Returns matching lines with file paths and line numbers",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "directory",
                        Type = "string",
                        Description = "The directory to search in (relative or absolute path)",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "pattern",
                        Type = "string",
                        Description = "Regex pattern to search for",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "filePattern",
                        Type = "string",
                        Description = "Optional glob pattern to filter files (e.g., '*.cs' or '**/*.txt'). Default is '*' (all files)",
                        IsRequired = false,
                        DefaultValue = "*"
                    }
                },
                returnsDescription: "Array of matching lines with file paths, line numbers, and matched content",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// single_find_and_replace: Regex find-replace in one file.
        /// Available in: Agent, Debug
        /// NOT available in: Plan (read-only), Ask (read-only), Reason (read-only analysis)
        /// Default: Ask First
        /// </summary>
        public static ToolDefinition GetSingleFindAndReplaceTool()
        {
            return CreateToolDefinition(
                name: "single_find_and_replace",
                description: "Find and replace text in a single file using regex. Safely replaces all occurrences matching the pattern",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filepath",
                        Type = "string",
                        Description = "The path of the file to edit",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "pattern",
                        Type = "string",
                        Description = "Regex pattern to search for",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "replacement",
                        Type = "string",
                        Description = "Text to replace matched pattern with. Supports backreferences like $1, $2, etc.",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "flags",
                        Type = "string",
                        Description = "Optional regex flags: 'i' for case-insensitive, 'm' for multiline. Default is '' (no flags)",
                        IsRequired = false,
                        DefaultValue = ""
                    }
                },
                returnsDescription: "Confirmation of replacement with number of replacements made",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug },
                invokePerm: "Ask First");
        }

        /// <summary>
        /// write_plan: Save the current plan to the workspace with a title.
        /// Available in: Plan, Ask, Agent, Debug, Reason (all modes - gated only by user enable/disable toggle).
        /// Use whenever the user asks to save/write/finalize the plan but does not provide a file path.
        /// </summary>
        public static ToolDefinition GetWritePlanTool()
        {
            return CreateToolDefinition(
                name: "write_plan",
                description: "Save the current plan to the workspace with a title. Use this whenever the user asks to save/write/finalize the plan but does not provide a file path.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "title",
                        Type = "string",
                        Description = "A short, descriptive title for the plan.",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "plan",
                        Type = "string",
                        Description = "The full text of the plan to save.",
                        IsRequired = true
                    }
                },
                returnsDescription: "Confirmation that the plan was saved to the workspace",
                supportedModes: new List<ChatMode> { ChatMode.Plan, ChatMode.Ask, ChatMode.Agent, ChatMode.Debug, ChatMode.Reason });
        }

        /// <summary>
        /// read_plan: Read the active plan file (the plan bound at send-time when the user has a
        /// plan open under ~/.continueVS/plans/). Returns the full plan text plus which plan it came
        /// from. Non-silent: if no plan is bound and no path is given, returns an explicit
        /// "no active plan" signal instead of an empty result.
        /// Available in: Agent, Debug (plan-driven build loop modes).
        /// </summary>
        public static ToolDefinition GetReadPlanTool()
        {
            return CreateToolDefinition(
                name: "read_plan",
                description: "Read the active plan file (the plan the user has open under ~/.continueVS/plans/, bound at send time). " +
                             "Use this to pull up the current plan so you can work through its steps. If a plan is bound, it is read by " +
                             "default; you may pass an explicit 'path' (repo-root-relative) to read a different plan." +
                             "If there is no bound plan and no path, this returns an explicit 'no active plan' signal.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "path",
                        Type = "string",
                        Description = "Optional. Repo-root-relative path to a plan file. If omitted, the bound (active) plan is used.",
                        IsRequired = false
                    }
                },
                returnsDescription: "The full plan text plus the plan file it came from",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// update_plan: Update the bound (active) plan file with an exact find/replace. The plan text
        /// is the search and the replacement is the swap; you manage pass/fail markers (e.g. ⏳ to ✅)
        /// yourself. Repo-root restricted. Returns a match count so a bad 'find' (count 0) is visible
        /// rather than silently doing nothing.
        /// Available in: Agent, Debug (plan-driven build loop modes).
        /// </summary>
        public static ToolDefinition GetUpdatePlanTool()
        {
            return CreateToolDefinition(
                name: "update_plan",
                description: "Update the active plan file (the plan bound at send time under ~/.continueVS/plans/) with an exact " +
                             "find/replace. The plan text you read is the 'find' and the 'replace' is what it becomes. You manage your own " +
                             "pass/fail markers (e.g. replacing '⏳' with '✅' as steps complete). 'find' must match exactly. Returns the number " +
                             "of matches replaced, so if your 'find' is wrong you get count 0 and can correct it. Optionally pass an explicit " +
                             "'path' (repo-root-relative) to update a different plan.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "find",
                        Type = "string",
                        Description = "The exact text in the plan to search for. Must match exactly.",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "replace",
                        Type = "string",
                        Description = "The text to replace every exact match of 'find' with.",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "path",
                        Type = "string",
                        Description = "Optional. Repo-root-relative path to a plan file. If omitted, the bound (active) plan is used.",
                        IsRequired = false
                    }
                },
                returnsDescription: "The number of matches replaced and where",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// ask_user: Ask the user a question when the LLM needs information, clarification, or
        /// confirmation to continue. The user may pick from the provided answers (multiple choice)
        /// or type their own prose answer. When answers is omitted, the question is open-ended.
        /// Available in: Agent, Debug (loop modes that can pause and resume).
        /// </summary>
        public static ToolDefinition GetAskUserTool()
        {
            return CreateToolDefinition(
                name: "ask_user",
                description: "Ask the user a question when you need information, clarification, or confirmation to continue. " +
                             "Use this ONLY when you genuinely cannot proceed without a user decision; prefer acting autonomously " +
                             "when you have enough context. Provide 'question' (the text to ask) and, if there is a limited set of " +
                             "reasonable choices, provide 'answers' (a list of suggested answer options). The user may pick one of " +
                             "the provided options or type their own free-text response. If no answers are supplied, the question is " +
                             "open-ended and the user will respond in prose.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "question",
                        Type = "string",
                        Description = "The question to present to the user. Required.",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "answers",
                        Type = "array",
                        Description = "Optional list of suggested answer options (multiple choice). The user may pick one or type their own. Omit for an open-ended question.",
                        IsRequired = false,
                        Schema = new Dictionary<string, object>
                        {
                            { "items", new Dictionary<string, object> { { "type", "string" } } }
                        }
                    }
                },
                returnsDescription: "The user's answer to the question as a string",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// retire_from_context: Toggle the status of an earlier message between "active" and
        /// "retired". Available in: Agent, Debug (loop modes — pure side effect + terminus).
        /// Returns nothing and expects no follow-up response; if it is the only tool call in the
        /// turn, the turn ends after it.
        /// </summary>
        public static ToolDefinition GetRetireFromContextTool()
        {
            return CreateToolDefinition(
                name: "retire_from_context",
                description: "Toggle the status of an earlier message (your own answer, or a tool\n" +
                             "request/response) between \"active\" and \"retired\". Retired means it will no\n" +
                             "longer be pulled into future context. This is a STATUS change, NOT deletion:\n" +
                             "the original content is preserved verbatim in an idempotent reference file,\n" +
                             "the UI continues to show the full content, and an icon marks it \"no longer in\n" +
                             "context.\" The user can still read and copy from it manually. Calling it again\n" +
                             "on the same message toggles it back to active.\n\n" +
                             "Use when: an item is factually wrong, superseded, or redundant such that\n" +
                             "leaving it active would risk propagating a known-bad premise. Also use to\n" +
                             "clear a once-useful supersession pointer once it has served its purpose.\n\n" +
                             "Do NOT use to hide that an error occurred — the reference trail stays intact.\n" +
                             "Always retire AFTER producing the corrected/consolidated replacement, and set\n" +
                             "replaced_by_id when a successor exists.\n\n" +
                             "This tool returns nothing and expects no follow-up response. If this is the\n" +
                             "only tool call in your turn, it is a terminus: your turn ends after it.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "message_id",
                        Type = "string",
                        Description = "The ID of the message/tool call to toggle retired",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "reason",
                        Type = "string",
                        Description = "short justification (e.g. 'superseded by <id>', 'redundant', 'factually wrong', 'pointer served its purpose')",
                        IsRequired = true
                    },
                    new ParameterDefinition
                    {
                        Name = "replaced_by_id",
                        Type = "string",
                        Description = "ID of the new response that supersedes it, when applicable",
                        IsRequired = false
                    }
                },
                returnsDescription: "Returns nothing (terminus)",
                supportedModes: new List<ChatMode> { ChatMode.Agent, ChatMode.Debug });
        }

        /// <summary>
        /// debug_evaluate: Evaluate an expression in the selected debug frame.
        /// Tier-2 (default-disabled): mutates nothing but reads live runtime state; gated because
        /// evaluation can execute user/process code. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugEvaluateTool()
        {
            return CreateToolDefinition(
                name: "debug_evaluate",
                description: "Evaluate an expression in the context of the currently selected debug frame. Requires the debugger to be paused at a breakpoint.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "threadId", Type = "number", Description = "Id of the thread whose frame to evaluate in.", IsRequired = true },
                    new ParameterDefinition { Name = "frameIndex", Type = "number", Description = "0-based index of the stack frame to evaluate in.", IsRequired = true },
                    new ParameterDefinition { Name = "expression", Type = "string", Description = "The expression to evaluate.", IsRequired = true }
                },
                returnsDescription: "The evaluated value and type, gated to break mode",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_set_value: Write a value to a variable in the selected debug frame.
        /// Tier-2 (default-disabled): MUTATES live runtime state. The most dangerous debug tool;
        /// must never be plain Automatic. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugSetValueTool()
        {
            return CreateToolDefinition(
                name: "debug_set_value",
                description: "Write a new value to a local variable or argument in the currently selected debug frame. MUTATES live runtime state. Requires the debugger to be paused.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "threadId", Type = "number", Description = "Id of the thread whose frame to modify.", IsRequired = true },
                    new ParameterDefinition { Name = "frameIndex", Type = "number", Description = "0-based index of the stack frame to modify.", IsRequired = true },
                    new ParameterDefinition { Name = "name", Type = "string", Description = "Name of the variable to write.", IsRequired = true },
                    new ParameterDefinition { Name = "value", Type = "string", Description = "The new value to assign.", IsRequired = true }
                },
                returnsDescription: "Confirmation the variable was written, gated to break mode",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_memory_read: Read process memory at an address. Not exposed by EnvDTE; benign rejection.
        /// Tier-2 (default-disabled). Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugMemoryReadTool()
        {
            return CreateToolDefinition(
                name: "debug_memory_read",
                description: "Read process memory at a given address. Requires break mode. Note: EnvDTE exposes no memory API, so this reports a benign not-exposed-by-dte result.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "address", Type = "string", Description = "Memory address to read.", IsRequired = true },
                    new ParameterDefinition { Name = "length", Type = "number", Description = "Number of bytes to read.", IsRequired = true }
                },
                returnsDescription: "Hex bytes read, or a benign not-exposed-by-dte rejection",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_memory_write: Write process memory at an address. Not exposed by EnvDTE; benign rejection.
        /// Tier-2 (default-disabled): would MUTATE process memory. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugMemoryWriteTool()
        {
            return CreateToolDefinition(
                name: "debug_memory_write",
                description: "Write bytes to process memory at a given address. MUTATES live runtime state. Requires break mode. Note: EnvDTE exposes no memory API, so this reports a benign not-exposed-by-dte result.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "address", Type = "string", Description = "Memory address to write.", IsRequired = true },
                    new ParameterDefinition { Name = "bytes", Type = "string", Description = "Hex-encoded bytes to write.", IsRequired = true }
                },
                returnsDescription: "Confirmation, or a benign not-exposed-by-dte rejection",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_run_to_cursor: Run the program to the current cursor location.
        /// Tier-2 (default-disabled): changes execution flow. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugRunToCursorTool()
        {
            return CreateToolDefinition(
                name: "debug_run_to_cursor",
                description: "Run the debugged program to the current cursor location. Requires the debugger to be paused.",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "Confirmation the run-to-cursor command was issued, gated to break mode",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_thread_set_state: Freeze or thaw a debugger thread.
        /// Tier-2 (default-disabled): changes live execution state. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugThreadSetStateTool()
        {
            return CreateToolDefinition(
                name: "debug_thread_set_state",
                description: "Freeze or thaw a debugger thread (suspends/resumes it). Requires the debugger to be paused.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "threadId", Type = "number", Description = "Id of the thread to change.", IsRequired = true },
                    new ParameterDefinition { Name = "action", Type = "string", Description = "'freeze' or 'thaw'.", IsRequired = true }
                },
                returnsDescription: "Confirmation the thread state was changed, gated to break mode",
                isEnabled: false,
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_start: Start (launch) a new debugging session.
        /// Tier-1 (default-enabled): begins a debug session. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugStartTool()
        {
            return CreateToolDefinition(
                name: "debug_start",
                description: "Start (launch) a new debugging session. If project is empty the solution's startup project is used; launchProfile selects a launch profile (empty = default).",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "project", Type = "string", Description = "Project to start. Empty uses the solution startup project.", IsRequired = false },
                    new ParameterDefinition { Name = "launchProfile", Type = "string", Description = "Launch profile to use. Empty uses the default.", IsRequired = false }
                },
                returnsDescription: "The started debug session handle and its mode",
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_stop: Stop the currently active debugging session.
        /// Tier-1 (default-enabled): ends a debug session. Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugStopTool()
        {
            return CreateToolDefinition(
                name: "debug_stop",
                description: "Stop the currently active debugging session and clear the selected session binding.",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The ended debug session handle, or an indication that none was running",
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_restart: Restart the current debugging session (stop then start).
        /// Tier-1 (default-enabled). Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugRestartTool()
        {
            return CreateToolDefinition(
                name: "debug_restart",
                description: "Restart the current debugging session: stop any active session then start it again.",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The restarted debug session handle",
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// ide_attach_to_process: Attach the debugger to a running local process.
        /// Tier-1 (default-enabled): begins a debug session by attaching. Debug mode only.
        /// (Lists local processes + attaches; the session lifecycle lands in gap94.)
        /// </summary>
        public static ToolDefinition GetIdeAttachToProcessTool()
        {
            return CreateToolDefinition(
                name: "ide_attach_to_process",
                description: "Attach the debugger to a running local process by its OS process id (pid), then select that attached session for subsequent debug_* tools.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "processId", Type = "number", Description = "The OS process id to attach to.", IsRequired = true }
                },
                returnsDescription: "The attached debug session handle and its mode",
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// debug_select_session: Bind the currently selected debug session for later debug_* tools.
        /// Tier-1 (default-enabled). Debug mode only.
        /// </summary>
        public static ToolDefinition GetDebugSelectSessionTool()
        {
            return CreateToolDefinition(
                name: "debug_select_session",
                description: "Select (bind) a debug session by its session id so subsequent debug_* tools act on it. List the session ids with debug_list_sessions.",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition { Name = "sessionId", Type = "string", Description = "The session id (e.g. 'proc:1234') to select.", IsRequired = true }
                },
                returnsDescription: "The bound debug session handle",
                supportedModes: new List<ChatMode> { ChatMode.Debug });
        }

        /// <summary>
        /// Gets all built-in tool definitions.
        /// Returns a collection of 39 core tools for code editing, navigation, diagnostics,
        /// plan-driven build loops, human-in-the-loop questioning, and debug session lifecycle
        /// (gap92, gap94).
        /// </summary>
        public static IEnumerable<ToolDefinition> GetAllBuiltInTools()
        {
            LoggerService.Current.WriteDebug("[gap8_1-factory-all-start] GetAllBuiltInTools called");
            var tools = new List<ToolDefinition>
            {
                GetReadFileTool(),
                GetCreateNewFileTool(),
                GetCreateFolderTool(),
                GetRunTerminalCommandTool(),
                GetFileGlobSearchTool(),
                GetViewDiffTool(),
                GetReadCurrentlyOpenFileTool(),
                GetListDirectoryTool(),
                GetCreateRuleBlockTool(),
                GetEditFileTool(),
                GetSearchCodebaseTool(),
                GetRunPytestTool(),
                GetGetProblemsTool(),
                GetViewFileTool(),
                GetOpenFileTool(),
                GetGitStatusTool(),
                GetGitDiffTool(),
                GetGitLogTool(),
                GetGitCommitTool(),
                GetCreateSnippetTool(),
                GetReadFileRangeTool(),
                GetGrepSearchTool(),
                GetSingleFindAndReplaceTool(),
                GetWritePlanTool(),
                GetReadPlanTool(),
                GetUpdatePlanTool(),
                GetAskUserTool(),
                GetRetireFromContextTool(),
                GetDebugEvaluateTool(),
                GetDebugSetValueTool(),
                GetDebugMemoryReadTool(),
                GetDebugMemoryWriteTool(),
                GetDebugRunToCursorTool(),
                GetDebugThreadSetStateTool(),
                GetDebugStartTool(),
                GetDebugStopTool(),
                GetDebugRestartTool(),
                GetIdeAttachToProcessTool(),
                GetDebugSelectSessionTool()
            };
            LoggerService.Current.WriteDebug($"[gap8_1-factory-all-end] GetAllBuiltInTools returning {tools.Count} tools");
            return tools;
        }
    }
}
