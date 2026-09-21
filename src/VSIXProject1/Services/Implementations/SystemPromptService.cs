using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using EnvDTE;
using Microsoft.VisualStudio.OLE.Interop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Joins;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
        private readonly IBridgeLogger? _logger;

        private SystemPromptConfig? _config;
        private bool _isLoaded;

        /// <summary>Initializes with optional workspace stats service for runtime context injection.</summary>
        public SystemPromptService(IBridgeLogger? logger = null, IWorkspaceStatsService? statsService = null)
        {
            _logger = logger;
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
                _logger?.WriteError($"[SystemPromptService.LoadAsync] Error loading config: {ex.Message}. Using defaults.", ex);
                _config = new SystemPromptConfig();
                _isLoaded = true;
            }
        }

        public string GetPromptForMode(string mode)
        {
            if (!_isLoaded)
            {
                _logger?.WriteDebug("[SystemPromptService.GetPromptForMode] Config not loaded yet. Please call LoadAsync() first.");
            }

            if (_config?.SystemPrompts.TryGetValue(mode.ToLowerInvariant(), out var item) == true)
            {
                return item.Prompt + GetContextSuffix(mode);
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
                _logger?.WriteError($"[SystemPromptService.EnsureConfigFileExistsAsync] Error: {ex.Message}", ex);
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
                "NEVER skip, omit or elide content from a file listing using \"...\" or by adding comments like \"... rest of code...\"!";
            //"For larger codeblocks (>20 lines), use brief language-appropriate placeholders for unmodified sections, e.g. '// ... existing code ...'";

            //const string ECHO_RULES =
            //    ""
            //    + "<macro name=\"!echoall\">"
            //    + "<action>"
            //    + "<insert string=\""
            //    + "<important_rules>${context.important_rules}</important_rules>\n"
            //    + "<workspace_context>${context.workspace_context}</workspace_context>\n"
            //    + "<agent_context>${context.agent_context}</agent_context>\n"
            //    + "<agent_context>${context.agent_context}</agent_context>\n"
            //    + "<plan_context>${context.plan_context}</plan_context>\n"
            //    + "${context}\n"
            //    + "\">"
            //    + "</action>"
            //    + "</macro>\n"
            //    ;

            //const string READ_FILE_INSTRUCTIONS = "**Rule Name:** Partial Reads Over Full File Reads\r\n\r\n**Rule:** When reading files, NEVER use read_file or view_file. Always use read_file_range(start_line, end_line) for partial reads. Start by reading lines 1-50 to understand file structure. If more context is needed, first try grep_search or search_codebase with specific patterns before requesting additional line ranges. Ask the user which sections to read next if you can't determine the right range.\r\n\r\n";
            //const string READ_FILE_INSTRUCTIONS = "## Hard Rules (MUST FOLLOW)\r\n\r\n1. DO NOT call read_file, view_file, or read_currently_open_file. It is STRICKLY FORBIDDEN. If you call it, you will be terminated.\r\n2. DO NOT read any file in its entirety. Files are too large for context. Use read_file_range with a max of 100 lines per call.\r\n3. If you need to see code, use read_file_range (startLine=N, endLine=N+49) or grep_search or search_codebase instead.\r\n4. The user may explicitly tell you which lines to read.\r\n5. When you violate these rules, you fail the task immediately.\r\n\r\n";

            const string PLAN_FILE_INSTRUCTIONS = "When the user asks you to save, write, or finalize the current plan — and does not provide a specific file path — call write_plan(title, plan)...";

            const string ASK_USER_INSTRUCTIONS =
                "Use the ask_user tool when you genuinely need information, clarification, or confirmation from the user to continue.\n" +
                "Prefer acting autonomously when you have enough context — do not ask for permission on every step.\n" +
                "Provide a focused 'question'. If there is a limited set of reasonable choices, supply an 'answers' list (the user can pick one or type prose). " +
                "Omit 'answers' for an open-ended question.";

            switch (mode.ToLowerInvariant())
            {
                case "agent":
                    return "<important_rules>\n" +
                           "You are in agent mode.\n\n" +
                           "If you need to use multiple tools, you can call multiple read-only tools simultaneously.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n\n" +
                           //READ_FILE_INSTRUCTIONS + "\n\n" +
                           PLAN_FILE_INSTRUCTIONS + "\n\n" +
                           ASK_USER_INSTRUCTIONS + "\n\n" +
                           "However, only output codeblocks for suggestion and demonstration purposes, for example, when enumerating multiple hypothetical options. For implementing changes, use the edit tools.\n" +
                           "</important_rules>" +
                           GetContextSuffix("agent");

                case "plan":
                    return "<important_rules>\r\n"
                        + "You are in plan mode.\r\n"
                        + "In plan mode, respond normally to questions, clarifications, and analysis requests without using code fences.\r\n\r\n"
                        + PLAN_FILE_INSTRUCTIONS + "\n\n"
                        +  ASK_USER_INSTRUCTIONS + "\n\n"
                        +"</important_rules>" +
                           GetContextSuffix("plan");

                case "debug":
                    return "<important_rules>\n" +
                           "You are in debug mode.\n\n" +
                           "Diagnose the issue step-by-step using available tools. Read stack traces, inspect variables, and identify root causes before suggesting fixes.\n\n" +
                           "You operate as in agent mode so all tools are available. prompt user for changes, on accept, make the changes.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n" +
                           //READ_FILE_INSTRUCTIONS + "\n\n" +
                           PLAN_FILE_INSTRUCTIONS + "\n\n" +
                           ASK_USER_INSTRUCTIONS + "\n\n" +
                           "</important_rules>" +
                           GetContextSuffix("debug");

                case "reason":
                    return "<important_rules>\n" +
                           "You are in reason mode.\n\n" +
                           "Think step-by-step before providing a final answer. Show your reasoning explicitly, working through the problem in structured stages. Withhold a definitive conclusion until your reasoning is complete.\n\n" +
                           "Show code only as required for logical points or references. The user wants your reasoning in place of code.\n" +
                           "Only use read-only tools. If the user wants changes implemented, suggest switching to Agent mode.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n" +
                           //READ_FILE_INSTRUCTIONS + "\n\n" +
                           PLAN_FILE_INSTRUCTIONS + "\n\n" +
                           ASK_USER_INSTRUCTIONS + "\n\n" +
                           "</important_rules>" +
                           GetContextSuffix("reason");

                default:  // chat/ask mode
                    return "<important_rules>\n" +
                           "You are in chat mode.\n\n" +
                           "If the user asks to make changes to files offer that they can use the Apply Button on the code block, or switch to Agent Mode to make the suggested updates automatically.\n" +
                           "If needed concisely explain to the user they can switch to agent mode using the Mode Selector dropdown and provide no other details.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n" +
                           //READ_FILE_INSTRUCTIONS + "\n\n" +
                           PLAN_FILE_INSTRUCTIONS + "\n\n" +
                           ASK_USER_INSTRUCTIONS + "\n\n" +
                           "</important_rules>" +
                           GetContextSuffix("ask");
                    //EDIT_CODE_INSTRUCTIONS + "\n" +
            }
        }

        //paste to see: echo <important_rules>${context.important_rules}</important_rules> <workspace_context>${context.workspace_context}</workspace_context> <agent_context>${context.agent_context}</agent_context> <agent_context>${context.agent_context}</agent_context> <plan_context>${context.plan_context}</plan_context>context
        // echo <important_rules>${context.important_rules}</important_rules>
        // echo <workspace_context>${context.workspace_context}</workspace_context>
        // echo <agent_context>${context.agent_context}</agent_context>
        // echo <agent_context>${context.agent_context}</agent_context>
        // echo <plan_context>${context.plan_context}</plan_context>
        // echocontext

        private string GetContextSuffix(string mode)
        {
            if (_statsService == null)
            {
                _logger?.WriteDebug("[SystemPromptService] GetContextSuffix: _statsService is NULL — workspace context will not be injected");
                return string.Empty;
            }

            try
            {
                // Refresh stats to ensure latest workspace state is captured
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

                var suffix = sb.ToString();
                _logger?.WriteDebug($"[SystemPromptService] GetContextSuffix({mode}) produced {suffix.Length} chars");
                return suffix;
            }
            catch (Exception ex)
            {
                _logger?.WriteError($"[SystemPromptService] GetContextSuffix failed: {ex.Message}", ex);
                return string.Empty;
            }
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

