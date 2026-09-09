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

        // Agent/Tool Settings
        public const string Agent_MaxToolCallsPerSession = "agent.maxToolCallsPerSession";

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

                // Agent/Tool defaults
                { Agent_MaxToolCallsPerSession, 100 },

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
                { Tool_RunPytestEnabled, true }
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
    }
}
