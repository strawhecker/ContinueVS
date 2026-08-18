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
                { Experimental_StreamAfterToolRejection, false }
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
