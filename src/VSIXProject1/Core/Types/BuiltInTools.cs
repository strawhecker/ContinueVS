using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            string invokePerm = "Automatic")
        {
            Debug.WriteLine($"[gap8_1-factory-create] CreateToolDefinition: {name}, params={parameters.Count}, enabled={isEnabled}");
            return new ToolDefinition
            {
                Name = name,
                Description = description,
                Category = "Built-In",
                Parameters = parameters,
                ReturnsDescription = returnsDescription,
                IsEnabled = isEnabled,
                IsAsync = true,
                ToolType = "builtin",
                LastModified = DateTime.UtcNow
            };
        }

        /// <summary>
        /// read_file: View the contents of an existing file.
        /// Default: Automatic
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
                returnsDescription: "The file contents as a string");
        }

        /// <summary>
        /// create_new_file: Create a new file. Only use when a file doesn't exist and should be created.
        /// Default: Ask First
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
                returnsDescription: "Confirmation that the file was created successfully");
        }

        /// <summary>
        /// run_terminal_command: Run a terminal command in the current directory.
        /// Default: Ask First
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
                returnsDescription: "Standard output and error from the command");
        }

        /// <summary>
        /// file_glob_search: Search for files recursively in the project using glob patterns.
        /// Default: Automatic
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
                returnsDescription: "List of file paths matching the glob pattern");
        }

        /// <summary>
        /// view_diff: View the current diff of working changes.
        /// Default: Automatic
        /// </summary>
        public static ToolDefinition GetViewDiffTool()
        {
            return CreateToolDefinition(
                name: "view_diff",
                description: "View the current diff of working changes",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The unified diff of all current changes");
        }

        /// <summary>
        /// read_currently_open_file: Read the currently open file in the IDE.
        /// Default: Ask First
        /// </summary>
        public static ToolDefinition GetReadCurrentlyOpenFileTool()
        {
            return CreateToolDefinition(
                name: "read_currently_open_file",
                description: "Read the currently open file in the IDE. If the user seems to be referring to a file that you can't see, or is requesting an action on content that seems missing, try using this tool",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "The contents of the currently open file in the IDE");
        }

        /// <summary>
        /// ls: List files and folders in a given directory.
        /// Default: Automatic
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
                returnsDescription: "List of file and folder names in the directory");
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
        /// Default: Ask First
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
                returnsDescription: "Confirmation of the edit operation");
        }

        /// <summary>
        /// search_codebase: Search the codebase for text matches using regex or literal text.
        /// Default: Automatic
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
                returnsDescription: "List of matching code snippets with file paths and line numbers");
        }

        /// <summary>
        /// run_pytest: Run pytest test suite.
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
                returnsDescription: "Test results including passed, failed, and skipped counts");
        }

        /// <summary>
        /// get_problems: Get compiler errors, warnings, and IDE problems.
        /// Default: Automatic
        /// </summary>
        public static ToolDefinition GetGetProblemsTool()
        {
            return CreateToolDefinition(
                name: "get_problems",
                description: "Get compiler errors, warnings, and IDE problems for the current project",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "List of problems with file paths, line numbers, severity, and messages");
        }

        /// <summary>
        /// view_file: View a file with line numbers for easier reference.
        /// Default: Automatic
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
                returnsDescription: "File contents with line number prefixes");
        }

        /// <summary>
        /// open_file: Open a file in the IDE editor.
        /// Default: Automatic
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
                returnsDescription: "Confirmation that the file was opened in the IDE");
        }

        /// <summary>
        /// git_status: Show git status of the repository.
        /// Default: Automatic
        /// </summary>
        public static ToolDefinition GetGitStatusTool()
        {
            return CreateToolDefinition(
                name: "git_status",
                description: "Show git status of the repository including modified files, staged changes, and untracked files",
                parameters: new List<ParameterDefinition>(),
                returnsDescription: "Git status output showing current branch and file changes");
        }

        /// <summary>
        /// git_diff: Show git diff of changes.
        /// Default: Automatic
        /// </summary>
        public static ToolDefinition GetGitDiffTool()
        {
            return CreateToolDefinition(
                name: "git_diff",
                description: "Show git diff of current changes in the repository",
                parameters: new List<ParameterDefinition>
                {
                    new ParameterDefinition
                    {
                        Name = "filePath",
                        Type = "string",
                        Description = "Optional. Specific file to show diff for. If empty, shows all diffs",
                        IsRequired = false
                    }
                },
                returnsDescription: "Unified diff format showing additions and deletions");
        }

        /// <summary>
        /// git_log: Show git commit history.
        /// Default: Automatic
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
                returnsDescription: "Commit history with hashes, authors, dates, and messages");
        }

        /// <summary>
        /// git_commit: Create a git commit with the given message.
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
                returnsDescription: "Confirmation of the commit with commit hash");
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
        /// Gets all built-in tool definitions.
        /// Returns a collection of 19 core tools for code editing, navigation, and diagnostics.
        /// </summary>
        public static IEnumerable<ToolDefinition> GetAllBuiltInTools()
        {
            Debug.WriteLine("[gap8_1-factory-all-start] GetAllBuiltInTools called");
            var tools = new List<ToolDefinition>
            {
                GetReadFileTool(),
                GetCreateNewFileTool(),
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
                GetCreateSnippetTool()
            };
            Debug.WriteLine($"[gap8_1-factory-all-end] GetAllBuiltInTools returning {tools.Count} tools");
            return tools;
        }
    }
}
