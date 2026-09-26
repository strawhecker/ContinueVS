using System;
using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Registry of all user-configurable settings with static key constants and defaults.
    /// Settings are stored as flattened key-value pairs in ContinueConfig.CustomSettings.
    /// </summary>
    public static class UserSettings
    {
        // Chat Settings
        public const string Chat_ShowSessionTabs = "chat.showSessionTabs";
        public const string Chat_WrapCodeblocks = "chat.wrapCodeblocks";
        public const string Chat_ShowChatScrollbar = "chat.showChatScrollbar";
        public const string Chat_TextToSpeechEnabled = "chat.textToSpeechEnabled";
        public const string Chat_EnableSessionTitles = "chat.enableSessionTitles";
        public const string Chat_FormatMarkdown = "chat.formatMarkdown";
        public const string Chat_ShowThinkingAfterStreaming = "chat.showThinkingAfterStreaming";

        // Appearance Settings
        public const string Appearance_FontSize = "appearance.fontSize";

        // Autocomplete Settings
        public const string Autocomplete_MultilineMode = "autocomplete.multilineMode";
        public const string Autocomplete_TimeoutMs = "autocomplete.timeoutMs";
        public const string Autocomplete_DebounceMs = "autocomplete.debounceMs";
        public const string Autocomplete_DisableInFiles = "autocomplete.disableInFiles";

        // Experimental Settings
        public const string Experimental_AddCurrentFileByDefault = "experimental.addCurrentFileByDefault";
        public const string Experimental_EnableExperimentalTools = "experimental.enableExperimentalTools";
        public const string Experimental_OnlyUseSystemMessageTools = "experimental.onlyUseSystemMessageTools";
        public const string Experimental_CodebaseUseToolCallingOnly = "experimental.codebaseUseToolCallingOnly";
        public const string Experimental_StreamAfterToolRejection = "experimental.streamAfterToolRejection";
        public const string Experimental_DumpContextBeforeSend = "experimental.dumpContextBeforeSend";
        public const string Experimental_DumpResponseAfterReceive = "experimental.dumpResponseAfterReceive";
        // Experimental gate for the agent self-diagnostics pipeline (gap23_4_1). Disabled by default:
        // when off, the plan-execution gathering/analyzing/reporting pipeline (InstructionExecutorService
        // and its phase generators/executors) never runs.
        public const string Experimental_EnableAgentDebug = "experimental.enableAgentDebug";

        // Agent/Tool Settings
        public const string Agent_MaxToolCallsPerAction = "agent.maxToolCallsPerAction";

        // gap80: Configurable tool-loop iteration limits
        /// <summary>
        /// Broad failure gate (default 10): stops the tool loop when things are failing so we
        /// don't accumulate LLM/token time. Intentionally generous.
        /// </summary>
        public const string Agent_MaxToolFailureGate = "agent.maxToolFailureGate";

        /// <summary>
        /// Recursion-depth bound (default 5): caps the Ollama continuation loop depth.
        /// Most fixes converge by 2-3; 5 leaves headroom for cascading fixes.
        /// </summary>
        public const string Agent_MaxToolRecursionDepth = "agent.maxToolRecursionDepth";

        // Tool-Specific Enabled/Disabled Settings (Read-Only Tools - All MODES)
        public const string Tool_ReadFileEnabled = "tool.readFileEnabled";
        public const string Tool_ReadFileRangeEnabled = "tool.readFileRangeEnabled";
        public const string Tool_ListDirectoryEnabled = "tool.listDirectoryEnabled";
        public const string Tool_FileGlobSearchEnabled = "tool.fileGlobSearchEnabled";
        public const string Tool_SearchCodeEnabled = "tool.searchCodeEnabled";
        public const string Tool_GrepSearchEnabled = "tool.grepSearchEnabled";
        public const string Tool_ViewDiffEnabled = "tool.viewDiffEnabled";
        public const string Tool_GitStatusEnabled = "tool.gitStatusEnabled";
        public const string Tool_GitDiffEnabled = "tool.gitDiffEnabled";
        public const string Tool_GitLogEnabled = "tool.gitLogEnabled";
        public const string Tool_GetProblemsEnabled = "tool.getProblemsEnabled";
        public const string Tool_ViewFileEnabled = "tool.viewFileEnabled";
        public const string Tool_ReadCurrentlyOpenFileEnabled = "tool.readCurrentlyOpenFileEnabled";

        // Tool-Specific Enabled/Disabled Settings (Write Tools - Agent + Debug ONLY)
        public const string Tool_EditFileEnabled = "tool.editFileEnabled";
        public const string Tool_WriteFileEnabled = "tool.writeFileEnabled";
        public const string Tool_CreateNewFileEnabled = "tool.createNewFileEnabled";
        public const string Tool_RunTerminalCommandEnabled = "tool.runTerminalCommandEnabled";
        public const string Tool_GitCommitEnabled = "tool.gitCommitEnabled";
        public const string Tool_CreateFolderEnabled = "tool.createFolderEnabled";
        public const string Tool_CreateRuleBlockEnabled = "tool.createRuleBlockEnabled";
        public const string Tool_CreateSnippetEnabled = "tool.createSnippetEnabled";
        public const string Tool_OpenFileEnabled = "tool.openFileEnabled";
        public const string Tool_SingleFindAndReplaceEnabled = "tool.singleFindAndReplaceEnabled";
        public const string Tool_RunPytestEnabled = "tool.runPytestEnabled";
        public const string Tool_WritePlanEnabled = "tool.writePlanEnabled";
        public const string Tool_AskUserEnabled = "tool.askUserEnabled";
        // Context pruning & error supersession tool (retire_from_context). Enabled by default.
        public const string Tool_RetireFromContextEnabled = "tool.retireFromContextEnabled";
        // Active-plan binding + read_plan/update_plan tools. Enabled by default (the beneficiary
        // wants it on); any new user may change it themselves.
        public const string Tool_PlanToolsEnabled = "tool.planToolsEnabled";

        // gap92_3: Tier-2 debug evaluate/mutate tools. ALL default-disabled — the LLM can never
        // freely evaluate or mutate a live process without explicit human opt-in. Never plain Automatic.
        public const string Tool_DebugEvaluateEnabled = "tool.debugEvaluateEnabled";
        public const string Tool_DebugSetValueEnabled = "tool.debugSetValueEnabled";
        public const string Tool_DebugMemoryReadEnabled = "tool.debugMemoryReadEnabled";
        public const string Tool_DebugMemoryWriteEnabled = "tool.debugMemoryWriteEnabled";
        public const string Tool_DebugRunToCursorEnabled = "tool.debugRunToCursorEnabled";
        public const string Tool_DebugThreadStateEnabled = "tool.debugThreadStateEnabled";

        /// <summary>
        /// Returns a dictionary of all default settings values.
        /// </summary>
        public static Dictionary<string, object> GetDefaults()
        {
            return new Dictionary<string, object>
            {
                // Chat defaults
                { Chat_ShowSessionTabs, false },
                { Chat_WrapCodeblocks, false },
                { Chat_ShowChatScrollbar, true },
                { Chat_TextToSpeechEnabled, false },
                { Chat_EnableSessionTitles, true },
                { Chat_FormatMarkdown, true },
                { Chat_ShowThinkingAfterStreaming, true },

                // Appearance defaults
                { Appearance_FontSize, 14 },

                // Autocomplete defaults
                { Autocomplete_MultilineMode, "auto" },
                { Autocomplete_TimeoutMs, 150 },
                { Autocomplete_DebounceMs, 250 },
                { Autocomplete_DisableInFiles, "" },

                // Experimental defaults
                { Experimental_AddCurrentFileByDefault, false },
                { Experimental_EnableExperimentalTools, true },
                { Experimental_OnlyUseSystemMessageTools, false },
                { Experimental_CodebaseUseToolCallingOnly, false },
                { Experimental_StreamAfterToolRejection, false },
                { Experimental_DumpContextBeforeSend, false },
                { Experimental_DumpResponseAfterReceive, false },
                { Experimental_EnableAgentDebug, false },

                // Agent/Tool defaults
                { Agent_MaxToolCallsPerAction, 100 },
                // gap80: tool-loop iteration limits
                { Agent_MaxToolFailureGate, 10 },
                { Agent_MaxToolRecursionDepth, 5 },

                // Tool-Specific defaults (Read-Only - default true, safe to auto-execute)
                { Tool_ReadFileEnabled, true },
                { Tool_ReadFileRangeEnabled, true },
                { Tool_ListDirectoryEnabled, true },
                { Tool_FileGlobSearchEnabled, true },
                { Tool_SearchCodeEnabled, true },
                { Tool_GrepSearchEnabled, true },
                { Tool_ViewDiffEnabled, true },
                { Tool_GitStatusEnabled, false },
                { Tool_GitDiffEnabled, false },
                { Tool_GitLogEnabled, false },
                { Tool_GetProblemsEnabled, true },
                { Tool_ViewFileEnabled, true },
                { Tool_ReadCurrentlyOpenFileEnabled, true },

                // Tool-Specific defaults (Write Tools - default true, mode registry gates to Agent+Debug)
                { Tool_EditFileEnabled, true },
                { Tool_WriteFileEnabled, true },
                { Tool_CreateNewFileEnabled, true },
                { Tool_RunTerminalCommandEnabled, true },
                { Tool_GitCommitEnabled, false },
                { Tool_CreateFolderEnabled, true },
                { Tool_CreateRuleBlockEnabled, true },
                { Tool_CreateSnippetEnabled, true },
                { Tool_OpenFileEnabled, true },
                { Tool_SingleFindAndReplaceEnabled, true },
                // run_pytest is disabled by default: no pytest tests exist in this .NET project
                { Tool_RunPytestEnabled, false },
                { Tool_WritePlanEnabled, true },
                { Tool_AskUserEnabled, true },
                { Tool_PlanToolsEnabled, true },
                { Tool_RetireFromContextEnabled, true },

                // gap92_3 Tier-2 debug evaluate/mutate tools — disabled by default
                { Tool_DebugEvaluateEnabled, false },
                { Tool_DebugSetValueEnabled, false },
                { Tool_DebugMemoryReadEnabled, false },
                { Tool_DebugMemoryWriteEnabled, false },
                { Tool_DebugRunToCursorEnabled, false },
                { Tool_DebugThreadStateEnabled, false }
            };
        }

        /// <summary>
        /// Gets the default value for a setting key by comparing against GetDefaults().
        /// Used during load/save to apply delta-based persistence (store only non-default values).
        /// </summary>
        public static object? GetDefault(string key)
        {
            var defaults = GetDefaults();
            defaults.TryGetValue(key, out var value);
            return value;
        }

        /// <summary>
        /// Reads an integer setting from a CustomSettings dictionary, returning the
        /// supplied fallback when the key is absent, the value is not an integer,
        /// or the dictionary is null. Shared by ToolService and ChatPageViewModel
        /// so both enforce the same per-action budget (gap79).
        /// </summary>
        public static int DefaultsAsInt(Dictionary<string, object>? customSettings, string key, int fallback)
        {
            if (customSettings == null || !customSettings.TryGetValue(key, out var raw))
                return fallback;

            // Values may be stored as int, long, or numeric string after JSON round-trip.
            switch (raw)
            {
                case int i:
                    return i;
                case long l:
                    return (int)l;
                case short s:
                    return s;
                case byte b:
                    return b;
                case double d:
                    return (int)d;
                case float f:
                    return (int)f;
                case string str when int.TryParse(str, out var parsed):
                    return parsed;
                default:
                    return fallback;
            }
        }
    }
}
