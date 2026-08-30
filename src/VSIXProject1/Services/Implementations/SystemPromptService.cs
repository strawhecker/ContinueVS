using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of ISystemPromptService that loads and manages system prompts
    /// from ~/.continueVS/system-prompts.json with fallback to hardcoded defaults.
    /// </summary>
    public class SystemPromptService : ISystemPromptService
    {
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continueVS");

        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "system-prompts.json");

        private readonly IWorkspaceStatsService? _statsService;

        private SystemPromptConfig? _config;
        private bool _isLoaded;

        /// <summary>Initializes with optional workspace stats service for runtime context injection.</summary>
        public SystemPromptService(IWorkspaceStatsService? statsService = null)
        {
            _statsService = statsService;
        }

        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    await EnsureConfigFileExistsAsync();
                }

                var json = File.ReadAllText(ConfigFilePath);
                _config = JsonConvert.DeserializeObject<SystemPromptConfig>(json);

                if (_config == null)
                {
                    _config = new SystemPromptConfig();
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemPromptService.LoadAsync] Error loading config: {ex.Message}. Using defaults.");
                _config = new SystemPromptConfig();
                _isLoaded = true;
            }
        }

        public string GetPromptForMode(string mode)
        {
            if (!_isLoaded)
            {
                System.Diagnostics.Debug.WriteLine("[SystemPromptService.GetPromptForMode] Config not loaded yet. Please call LoadAsync() first.");
            }

            if (_config?.SystemPrompts.TryGetValue(mode.ToLowerInvariant(), out var item) == true)
            {
                return item.Prompt;
            }

            return GetDefaultPromptForMode(mode);
        }

        public async Task ReloadAsync()
        {
            _isLoaded = false;
            _config = null;
            await LoadAsync();
        }

        public async Task EnsureConfigFileExistsAsync()
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                if (!File.Exists(ConfigFilePath))
                {
                    var defaultConfig = new SystemPromptConfig
                    {
                        SystemPrompts = new Dictionary<string, SystemPromptItem>
                        {
                            ["ask"] = new SystemPromptItem
                            {
                                Prompt = GetDefaultPromptForMode("ask"),
                                Description = "Read-only analysis mode; offer Apply Button or Agent Mode switch for code changes"
                            },
                            ["agent"] = new SystemPromptItem
                            {
                                Prompt = GetDefaultPromptForMode("agent"),
                                Description = "Full tool calling enabled; use edit tools for implementation"
                            },
                            ["plan"] = new SystemPromptItem
                            {
                                Prompt = GetDefaultPromptForMode("plan"),
                                Description = "Read-only planning tool; suggest Agent Mode for implementation"
                            },
                            ["debug"] = new SystemPromptItem
                            {
                                Prompt = GetDefaultPromptForMode("debug"),
                                Description = "Instrumentation-driven error diagnosis; use read-only tools and identify root causes before suggesting fixes"
                            },
                            ["reason"] = new SystemPromptItem
                            {
                                Prompt = GetDefaultPromptForMode("reason"),
                                Description = "Structured chain-of-thought reasoning; LLM thinks step-by-step before answering"
                            }
                        }
                    };

                    var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                    File.WriteAllText(ConfigFilePath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SystemPromptService.EnsureConfigFileExistsAsync] Error: {ex.Message}");
            }
        }

        private string GetDefaultPromptForMode(string mode)
        {
            const string CODEBLOCK_FORMATTING_INSTRUCTIONS =
                "Always include the language and file name in the info string when you write code blocks.\n" +
                "If you are editing \"src/main.py\" for example, your code block should start with '```python src/main.py'";

            //const string EDIT_CODE_INSTRUCTIONS =
            //    "When addressing code modification requests, present a concise code snippet that\n" +
            //    "emphasizes only the necessary changes and uses abbreviated placeholders for\n" +
            //    "unmodified sections. For example:\n\n" +
            //    "```language /path/to/file\n" +
            //    "// ... existing code ...\n" +
            //    "{{ modified code here }}\n" +
            //    "// ... existing code ...\n" +
            //    "{{ another modification }}\n" +
            //    "// ... rest of code ...\n" +
            //    "```\n\n" +
            //    "In existing files, you should always restate the function or class that the snippet belongs to:\n\n" +
            //    "```language /path/to/file\n" +
            //    "// ... existing code ...\n\n" +
            //    "function exampleFunction() {\n" +
            //    "  // ... existing code ...\n\n" +
            //    "  {{ modified code here }}\n\n" +
            //    "  // ... rest of function ...\n" +
            //    "}\n\n" +
            //    "// ... rest of code ...\n" +
            //    "```\n\n" +
            //    "Since users have access to their complete file, they prefer reading only the\n" +
            //    "relevant modifications. It's perfectly acceptable to omit unmodified portions\n" +
            //    "at the beginning, middle, or end of files using these \"lazy\" comments. Only\n" +
            //    "provide the complete file when explicitly requested. Include a concise explanation\n" +
            //    "of changes unless the user specifically asks for code only.";

            const string BRIEF_LAZY_INSTRUCTIONS =
                "For larger codeblocks (>20 lines), use brief language-appropriate placeholders for unmodified sections, e.g. '// ... existing code ...'";

            switch (mode.ToLowerInvariant())
            {
                case "agent":
                    return "<important_rules>\n" +
                           "You are in agent mode.\n\n" +
                           "If you need to use multiple tools, you can call multiple read-only tools simultaneously.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n\n" +
                           "However, only output codeblocks for suggestion and demonstration purposes, for example, when enumerating multiple hypothetical options. For implementing changes, use the edit tools.\n" +
                           "</important_rules>" + GetContextSuffix("agent");

                case "plan":
                    return "<important_rules>\n" +
                           "You are in plan mode, in which you help the user understand and construct a plan.\n" +
                           "Only use read-only tools. Do not use any tools that would write to non-temporary files.\n" +
                           "If the user wants to make changes, offer that they can switch to Agent mode to give you access to write tools to make the suggested updates.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n\n" +
                           "However, only output codeblocks for suggestion and planning purposes. When ready to implement changes, request to switch to Agent mode.\n\n" +
                           "In plan mode, only write code when directly suggesting changes. Prioritize understanding and developing a plan.\n" +
                           "</important_rules>" + GetContextSuffix("plan");

                case "debug":
                    return "<important_rules>\n" +
                           "You are in debug mode.\n\n" +
                           "Diagnose the issue step-by-step using available tools. Read stack traces, inspect variables, and identify root causes before suggesting fixes.\n\n" +
                           "You operate as in agent mode so all tools are available. prompt user for changes, on accept, make the changes.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n" +
                           "</important_rules>" + GetContextSuffix("debug");

                case "reason":
                    return "<important_rules>\n" +
                           "You are in reason mode.\n\n" +
                           "Think step-by-step before providing a final answer. Show your reasoning explicitly, working through the problem in structured stages. Withhold a definitive conclusion until your reasoning is complete.\n\n" +
                           "Show code only as required for logical points or references. The user wants your reasoning in place of code.\n" +
                           "Only use read-only tools. If the user wants changes implemented, suggest switching to Agent mode.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n" +
                           "</important_rules>" + GetContextSuffix("reason");

                default:  // chat/ask mode
                    return "<important_rules>\n" +
                           "You are in chat mode.\n\n" +
                           "If the user asks to make changes to files offer that they can use the Apply Button on the code block, or switch to Agent Mode to make the suggested updates automatically.\n" +
                           "If needed concisely explain to the user they can switch to agent mode using the Mode Selector dropdown and provide no other details.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n" +
                           //EDIT_CODE_INSTRUCTIONS + "\n" +
                           "</important_rules>" + GetContextSuffix("ask");
            }
        }

        private string GetContextSuffix(string mode)
        {
            if (_statsService == null)
                return string.Empty;

            _statsService.Refresh();
            var s = _statsService.GetStats();

            var sb = new StringBuilder();
            sb.Append("\n").Append(BuildWorkspaceContextBlock(s, mode));

            switch (mode)
            {
                case "agent":
                    var agentBlock = BuildAgentContextBlock(s);
                    if (!string.IsNullOrEmpty(agentBlock)) sb.Append("\n").Append(agentBlock);
                    break;
                case "plan":
                    var planBlock = BuildPlanContextBlock(s);
                    if (!string.IsNullOrEmpty(planBlock)) sb.Append("\n").Append(planBlock);
                    break;
                case "debug":
                    var debugBlock = BuildDebugContextBlock(s);
                    if (!string.IsNullOrEmpty(debugBlock)) sb.Append("\n").Append(debugBlock);
                    break;
            }

            return sb.ToString();
        }

        private static string BuildWorkspaceContextBlock(WorkspaceStats s, string mode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<workspace_context>");
            AppendField(sb, "active_file", s.ActiveFile);
            AppendField(sb, "git_branch", s.GitBranch);
            AppendField(sb, "solution_path", s.SolutionPath);
            // chat_mode reflects the mode string being built
            if (!IsEmpty(mode)) sb.AppendLine($"  <chat_mode>{mode}</chat_mode>");
            sb.Append("</workspace_context>");
            return sb.ToString();
        }

        private static string BuildAgentContextBlock(WorkspaceStats s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<agent_context>");
            AppendField(sb, "target_frameworks", s.TargetFrameworks);
            AppendField(sb, "git_remote", s.GitRemote);
            AppendField(sb, "shell", s.Shell);
            sb.Append("</agent_context>");
            return HasAnyContent(sb) ? sb.ToString() : string.Empty;
        }

        private static string BuildPlanContextBlock(WorkspaceStats s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<plan_context>");
            AppendField(sb, "target_frameworks", s.TargetFrameworks);
            AppendField(sb, "git_remote", s.GitRemote);
            AppendField(sb, "completed_gaps", s.CompletedGaps);
            sb.Append("</plan_context>");
            return HasAnyContent(sb) ? sb.ToString() : string.Empty;
        }

        private static string BuildDebugContextBlock(WorkspaceStats s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<debug_context>");
            AppendField(sb, "target_frameworks", s.TargetFrameworks);
            AppendField(sb, "git_remote", s.GitRemote);
            AppendField(sb, "debug_mode", s.DebugMode);
            AppendField(sb, "break_location", s.BreakLocation);
            sb.Append("</debug_context>");
            return HasAnyContent(sb) ? sb.ToString() : string.Empty;
        }

        private static void AppendField(StringBuilder sb, string name, string value)
        {
            if (!IsEmpty(value))
                sb.AppendLine($"  <{name}>{value}</{name}>");
        }

        private static bool IsEmpty(string value)
            => string.IsNullOrWhiteSpace(value)
               || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);

        // Returns true when the StringBuilder contains at least one field element between the outer tags
        private static bool HasAnyContent(StringBuilder sb)
            => sb.ToString().Contains("  <");
    }
}

