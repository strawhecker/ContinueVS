using System;
using System.Diagnostics;
using GalaSoft.MvvmLight;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.ViewModels
{
    /// <summary>
    /// ViewModel for user settings (Chat, Appearance, Autocomplete, Experimental preferences).
    /// Binds settings to the SettingsControl UI and coordinates with ConfigService for persistence.
    /// Settings are stored as flattened key-value pairs in ContinueConfig.CustomSettings.
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly IConfigService _configService;

        // Chat settings
        private bool _showSessionTabs;
        private bool _wrapCodeblocks;
        private bool _showChatScrollbar;
        private bool _textToSpeechEnabled;
        private bool _enableSessionTitles;
        private bool _formatMarkdown;

        // Appearance settings
        private int _fontSize;

        // Autocomplete settings
        private string _multilineMode;
        private int _autocompleteTimeoutMs;
        private int _autocompleteDebounceMs;
        private string _disableAutocompleteInFiles;

        // Experimental settings
        private bool _addCurrentFileByDefault;
        private bool _enableExperimentalTools;
        private bool _onlyUseSystemMessageTools;
        private bool _codebaseUseToolCallingOnly;
        private bool _streamAfterToolRejection;

        // Chat Properties
        public bool ShowSessionTabs
        {
            get => _showSessionTabs;
            set => Set(ref _showSessionTabs, value);
        }

        public bool WrapCodeblocks
        {
            get => _wrapCodeblocks;
            set => Set(ref _wrapCodeblocks, value);
        }

        public bool ShowChatScrollbar
        {
            get => _showChatScrollbar;
            set => Set(ref _showChatScrollbar, value);
        }

        public bool TextToSpeechEnabled
        {
            get => _textToSpeechEnabled;
            set => Set(ref _textToSpeechEnabled, value);
        }

        public bool EnableSessionTitles
        {
            get => _enableSessionTitles;
            set => Set(ref _enableSessionTitles, value);
        }

        public bool FormatMarkdown
        {
            get => _formatMarkdown;
            set => Set(ref _formatMarkdown, value);
        }

        // Appearance Properties
        public int FontSize
        {
            get => _fontSize;
            set => Set(ref _fontSize, value);
        }

        // Autocomplete Properties
        public string MultilineMode
        {
            get => _multilineMode;
            set => Set(ref _multilineMode, value);
        }

        public bool MultilineModeAuto
        {
            get => _multilineMode == "auto";
            set { if (value) MultilineMode = "auto"; }
        }

        public bool MultilineModeAlways
        {
            get => _multilineMode == "always";
            set { if (value) MultilineMode = "always"; }
        }

        public bool MultilineModeNever
        {
            get => _multilineMode == "never";
            set { if (value) MultilineMode = "never"; }
        }

        public int AutocompleteTimeoutMs
        {
            get => _autocompleteTimeoutMs;
            set => Set(ref _autocompleteTimeoutMs, value);
        }

        public int AutocompleteDebounceMs
        {
            get => _autocompleteDebounceMs;
            set => Set(ref _autocompleteDebounceMs, value);
        }

        public string DisableAutocompleteInFiles
        {
            get => _disableAutocompleteInFiles;
            set => Set(ref _disableAutocompleteInFiles, value ?? string.Empty);
        }

        // Experimental Properties
        public bool AddCurrentFileByDefault
        {
            get => _addCurrentFileByDefault;
            set => Set(ref _addCurrentFileByDefault, value);
        }

        public bool EnableExperimentalTools
        {
            get => _enableExperimentalTools;
            set => Set(ref _enableExperimentalTools, value);
        }

        public bool OnlyUseSystemMessageTools
        {
            get => _onlyUseSystemMessageTools;
            set => Set(ref _onlyUseSystemMessageTools, value);
        }

        public bool CodebaseUseToolCallingOnly
        {
            get => _codebaseUseToolCallingOnly;
            set => Set(ref _codebaseUseToolCallingOnly, value);
        }

        public bool StreamAfterToolRejection
        {
            get => _streamAfterToolRejection;
            set => Set(ref _streamAfterToolRejection, value);
        }

        public SettingsViewModel(IConfigService configService)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));

            Debug.WriteLine("[SettingsViewModel-ctor] SettingsViewModel CONSTRUCTOR CALLED");

            _configService = configService;

            // Initialize all settings to defaults
            var defaults = UserSettings.GetDefaults();

            _showSessionTabs = GetBool(UserSettings.Chat_ShowSessionTabs, defaults);
            _wrapCodeblocks = GetBool(UserSettings.Chat_WrapCodeblocks, defaults);
            _showChatScrollbar = GetBool(UserSettings.Chat_ShowChatScrollbar, defaults);
            _textToSpeechEnabled = GetBool(UserSettings.Chat_TextToSpeechEnabled, defaults);
            _enableSessionTitles = GetBool(UserSettings.Chat_EnableSessionTitles, defaults);
            _formatMarkdown = GetBool(UserSettings.Chat_FormatMarkdown, defaults);

            _fontSize = GetInt(UserSettings.Appearance_FontSize, defaults);

            _multilineMode = GetString(UserSettings.Autocomplete_MultilineMode, defaults);
            _autocompleteTimeoutMs = GetInt(UserSettings.Autocomplete_TimeoutMs, defaults);
            _autocompleteDebounceMs = GetInt(UserSettings.Autocomplete_DebounceMs, defaults);
            _disableAutocompleteInFiles = GetString(UserSettings.Autocomplete_DisableInFiles, defaults);

            _addCurrentFileByDefault = GetBool(UserSettings.Experimental_AddCurrentFileByDefault, defaults);
            _enableExperimentalTools = GetBool(UserSettings.Experimental_EnableExperimentalTools, defaults);
            _onlyUseSystemMessageTools = GetBool(UserSettings.Experimental_OnlyUseSystemMessageTools, defaults);
            _codebaseUseToolCallingOnly = GetBool(UserSettings.Experimental_CodebaseUseToolCallingOnly, defaults);
            _streamAfterToolRejection = GetBool(UserSettings.Experimental_StreamAfterToolRejection, defaults);

            Debug.WriteLine("[SettingsViewModel-ctor] SettingsViewModel CONSTRUCTOR COMPLETE");
        }

        /// <summary>
        /// Loads all settings from ConfigService.CustomSettings, applying defaults for missing keys.
        /// </summary>
        public void LoadSettings()
        {
            try
            {
                Debug.WriteLine("[SettingsViewModel.LoadSettings] Starting load...");

                var config = _configService.GetCurrentConfig();
                if (config?.CustomSettings == null)
                {
                    Debug.WriteLine("[SettingsViewModel.LoadSettings] Config or CustomSettings is null");
                    return;
                }

                var defaults = UserSettings.GetDefaults();

                // Load Chat settings
                ShowSessionTabs = GetBoolFromConfig(UserSettings.Chat_ShowSessionTabs, config.CustomSettings, defaults);
                WrapCodeblocks = GetBoolFromConfig(UserSettings.Chat_WrapCodeblocks, config.CustomSettings, defaults);
                ShowChatScrollbar = GetBoolFromConfig(UserSettings.Chat_ShowChatScrollbar, config.CustomSettings, defaults);
                TextToSpeechEnabled = GetBoolFromConfig(UserSettings.Chat_TextToSpeechEnabled, config.CustomSettings, defaults);
                EnableSessionTitles = GetBoolFromConfig(UserSettings.Chat_EnableSessionTitles, config.CustomSettings, defaults);
                FormatMarkdown = GetBoolFromConfig(UserSettings.Chat_FormatMarkdown, config.CustomSettings, defaults);

                // Load Appearance settings
                FontSize = GetIntFromConfig(UserSettings.Appearance_FontSize, config.CustomSettings, defaults);

                // Load Autocomplete settings
                MultilineMode = GetStringFromConfig(UserSettings.Autocomplete_MultilineMode, config.CustomSettings, defaults);
                AutocompleteTimeoutMs = GetIntFromConfig(UserSettings.Autocomplete_TimeoutMs, config.CustomSettings, defaults);
                AutocompleteDebounceMs = GetIntFromConfig(UserSettings.Autocomplete_DebounceMs, config.CustomSettings, defaults);
                DisableAutocompleteInFiles = GetStringFromConfig(UserSettings.Autocomplete_DisableInFiles, config.CustomSettings, defaults);

                // Load Experimental settings
                AddCurrentFileByDefault = GetBoolFromConfig(UserSettings.Experimental_AddCurrentFileByDefault, config.CustomSettings, defaults);
                EnableExperimentalTools = GetBoolFromConfig(UserSettings.Experimental_EnableExperimentalTools, config.CustomSettings, defaults);
                OnlyUseSystemMessageTools = GetBoolFromConfig(UserSettings.Experimental_OnlyUseSystemMessageTools, config.CustomSettings, defaults);
                CodebaseUseToolCallingOnly = GetBoolFromConfig(UserSettings.Experimental_CodebaseUseToolCallingOnly, config.CustomSettings, defaults);
                StreamAfterToolRejection = GetBoolFromConfig(UserSettings.Experimental_StreamAfterToolRejection, config.CustomSettings, defaults);

                Debug.WriteLine("[SettingsViewModel.LoadSettings] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel.LoadSettings] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves all current settings to ConfigService.CustomSettings and persists to disk.
        /// </summary>
        public async System.Threading.Tasks.Task SaveSettingsAsync()
        {
            try
            {
                Debug.WriteLine("[SettingsViewModel.SaveSettingsAsync] Starting save...");

                var config = _configService.GetCurrentConfig();
                if (config?.CustomSettings == null)
                {
                    Debug.WriteLine("[SettingsViewModel.SaveSettingsAsync] Config or CustomSettings is null");
                    return;
                }

                // Save Chat settings
                config.CustomSettings[UserSettings.Chat_ShowSessionTabs] = ShowSessionTabs;
                config.CustomSettings[UserSettings.Chat_WrapCodeblocks] = WrapCodeblocks;
                config.CustomSettings[UserSettings.Chat_ShowChatScrollbar] = ShowChatScrollbar;
                config.CustomSettings[UserSettings.Chat_TextToSpeechEnabled] = TextToSpeechEnabled;
                config.CustomSettings[UserSettings.Chat_EnableSessionTitles] = EnableSessionTitles;
                config.CustomSettings[UserSettings.Chat_FormatMarkdown] = FormatMarkdown;

                // Save Appearance settings
                config.CustomSettings[UserSettings.Appearance_FontSize] = FontSize;

                // Save Autocomplete settings
                config.CustomSettings[UserSettings.Autocomplete_MultilineMode] = MultilineMode;
                config.CustomSettings[UserSettings.Autocomplete_TimeoutMs] = AutocompleteTimeoutMs;
                config.CustomSettings[UserSettings.Autocomplete_DebounceMs] = AutocompleteDebounceMs;
                config.CustomSettings[UserSettings.Autocomplete_DisableInFiles] = DisableAutocompleteInFiles;

                // Save Experimental settings
                config.CustomSettings[UserSettings.Experimental_AddCurrentFileByDefault] = AddCurrentFileByDefault;
                config.CustomSettings[UserSettings.Experimental_EnableExperimentalTools] = EnableExperimentalTools;
                config.CustomSettings[UserSettings.Experimental_OnlyUseSystemMessageTools] = OnlyUseSystemMessageTools;
                config.CustomSettings[UserSettings.Experimental_CodebaseUseToolCallingOnly] = CodebaseUseToolCallingOnly;
                config.CustomSettings[UserSettings.Experimental_StreamAfterToolRejection] = StreamAfterToolRejection;

                // Persist to disk
                await _configService.SaveConfigAsync();
                Debug.WriteLine("[SettingsViewModel.SaveSettingsAsync] Settings saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel.SaveSettingsAsync] Error: {ex.Message}");
            }
        }

        // Helper methods for type conversion
        private bool GetBool(string key, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (defaults.TryGetValue(key, out var value) && value is bool boolValue)
                return boolValue;
            return false;
        }

        private int GetInt(string key, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (defaults.TryGetValue(key, out var value))
            {
                if (value is int intValue) return intValue;
                if (int.TryParse(value?.ToString(), out int parsed)) return parsed;
            }
            return 0;
        }

        private string GetString(string key, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (defaults.TryGetValue(key, out var value) && value is string strValue)
                return strValue;
            return string.Empty;
        }

        private bool GetBoolFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value is bool boolValue) return boolValue;
                if (bool.TryParse(value?.ToString(), out bool parsed)) return parsed;
            }
            return GetBool(key, defaults);
        }

        private int GetIntFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value is int intValue) return intValue;
                if (int.TryParse(value?.ToString(), out int parsed)) return parsed;
            }
            return GetInt(key, defaults);
        }

        private string GetStringFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config, System.Collections.Generic.Dictionary<string, object> defaults)
        {
            if (config.TryGetValue(key, out var value) && value is string strValue)
                return strValue;
            return GetString(key, defaults);
        }
    }
}
