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
        private bool _dumpContextBeforeSend;
        private bool _dumpResponseAfterReceive;

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

        public bool DumpContextBeforeSend
        {
            get => _dumpContextBeforeSend;
            set => Set(ref _dumpContextBeforeSend, value);
        }

        public bool DumpResponseAfterReceive
        {
            get => _dumpResponseAfterReceive;
            set => Set(ref _dumpResponseAfterReceive, value);
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
            _dumpContextBeforeSend = GetBool(UserSettings.Experimental_DumpContextBeforeSend, defaults);
            _dumpResponseAfterReceive = GetBool(UserSettings.Experimental_DumpResponseAfterReceive, defaults);

            Debug.WriteLine("[SettingsViewModel-ctor] SettingsViewModel CONSTRUCTOR COMPLETE");
        }

        /// <summary>
        /// Loads all settings from ConfigService.CustomSettings, applying defaults for missing keys.
        /// Two-tier lookup: first check CustomSettings (file), fall back to UserSettings.GetDefault() (code).
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

                // Load Chat settings using two-tier lookup
                ShowSessionTabs = GetBoolFromConfig(UserSettings.Chat_ShowSessionTabs, config.CustomSettings);
                WrapCodeblocks = GetBoolFromConfig(UserSettings.Chat_WrapCodeblocks, config.CustomSettings);
                ShowChatScrollbar = GetBoolFromConfig(UserSettings.Chat_ShowChatScrollbar, config.CustomSettings);
                TextToSpeechEnabled = GetBoolFromConfig(UserSettings.Chat_TextToSpeechEnabled, config.CustomSettings);
                EnableSessionTitles = GetBoolFromConfig(UserSettings.Chat_EnableSessionTitles, config.CustomSettings);
                FormatMarkdown = GetBoolFromConfig(UserSettings.Chat_FormatMarkdown, config.CustomSettings);

                // Load Appearance settings
                FontSize = GetIntFromConfig(UserSettings.Appearance_FontSize, config.CustomSettings);

                // Load Autocomplete settings
                MultilineMode = GetStringFromConfig(UserSettings.Autocomplete_MultilineMode, config.CustomSettings);
                AutocompleteTimeoutMs = GetIntFromConfig(UserSettings.Autocomplete_TimeoutMs, config.CustomSettings);
                AutocompleteDebounceMs = GetIntFromConfig(UserSettings.Autocomplete_DebounceMs, config.CustomSettings);
                DisableAutocompleteInFiles = GetStringFromConfig(UserSettings.Autocomplete_DisableInFiles, config.CustomSettings);

                // Load Experimental settings
                AddCurrentFileByDefault = GetBoolFromConfig(UserSettings.Experimental_AddCurrentFileByDefault, config.CustomSettings);
                EnableExperimentalTools = GetBoolFromConfig(UserSettings.Experimental_EnableExperimentalTools, config.CustomSettings);
                OnlyUseSystemMessageTools = GetBoolFromConfig(UserSettings.Experimental_OnlyUseSystemMessageTools, config.CustomSettings);
                CodebaseUseToolCallingOnly = GetBoolFromConfig(UserSettings.Experimental_CodebaseUseToolCallingOnly, config.CustomSettings);
                StreamAfterToolRejection = GetBoolFromConfig(UserSettings.Experimental_StreamAfterToolRejection, config.CustomSettings);
                DumpContextBeforeSend = GetBoolFromConfig(UserSettings.Experimental_DumpContextBeforeSend, config.CustomSettings);
                DumpResponseAfterReceive = GetBoolFromConfig(UserSettings.Experimental_DumpResponseAfterReceive, config.CustomSettings);

                Debug.WriteLine("[SettingsViewModel.LoadSettings] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel.LoadSettings] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves all current settings to ConfigService.CustomSettings and persists to disk.
        /// Implements delta-based persistence: only stores settings that differ from defaults.
        /// Settings equal to their defaults are removed from CustomSettings to keep continueVS.json clean.
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

                // Helper to set or remove based on default comparison
                Action<string, object> SetOrRemove = (key, value) =>
                {
                    var defaultValue = UserSettings.GetDefault(key);
                    if (Equals(value, defaultValue))
                    {
                        config.CustomSettings.Remove(key);
                        Debug.WriteLine($"[SettingsViewModel.SaveSettingsAsync] Removed {key} (equals default)");
                    }
                    else
                    {
                        config.CustomSettings[key] = value;
                        Debug.WriteLine($"[SettingsViewModel.SaveSettingsAsync] Saved {key} = {value}");
                    }
                };

                // Save Chat settings (only if different from defaults)
                SetOrRemove(UserSettings.Chat_ShowSessionTabs, ShowSessionTabs);
                SetOrRemove(UserSettings.Chat_WrapCodeblocks, WrapCodeblocks);
                SetOrRemove(UserSettings.Chat_ShowChatScrollbar, ShowChatScrollbar);
                SetOrRemove(UserSettings.Chat_TextToSpeechEnabled, TextToSpeechEnabled);
                SetOrRemove(UserSettings.Chat_EnableSessionTitles, EnableSessionTitles);
                SetOrRemove(UserSettings.Chat_FormatMarkdown, FormatMarkdown);

                // Save Appearance settings
                SetOrRemove(UserSettings.Appearance_FontSize, FontSize);

                // Save Autocomplete settings
                SetOrRemove(UserSettings.Autocomplete_MultilineMode, MultilineMode);
                SetOrRemove(UserSettings.Autocomplete_TimeoutMs, AutocompleteTimeoutMs);
                SetOrRemove(UserSettings.Autocomplete_DebounceMs, AutocompleteDebounceMs);
                SetOrRemove(UserSettings.Autocomplete_DisableInFiles, DisableAutocompleteInFiles);

                // Save Experimental settings
                SetOrRemove(UserSettings.Experimental_AddCurrentFileByDefault, AddCurrentFileByDefault);
                SetOrRemove(UserSettings.Experimental_EnableExperimentalTools, EnableExperimentalTools);
                SetOrRemove(UserSettings.Experimental_OnlyUseSystemMessageTools, OnlyUseSystemMessageTools);
                SetOrRemove(UserSettings.Experimental_CodebaseUseToolCallingOnly, CodebaseUseToolCallingOnly);
                SetOrRemove(UserSettings.Experimental_StreamAfterToolRejection, StreamAfterToolRejection);
                SetOrRemove(UserSettings.Experimental_DumpContextBeforeSend, DumpContextBeforeSend);
                SetOrRemove(UserSettings.Experimental_DumpResponseAfterReceive, DumpResponseAfterReceive);

                // Persist to disk
                await _configService.SaveConfigAsync();
                Debug.WriteLine("[SettingsViewModel.SaveSettingsAsync] Settings saved successfully (delta-based)");
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

        private bool GetBoolFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value is bool boolValue) return boolValue;
                if (bool.TryParse(value?.ToString(), out bool parsed)) return parsed;
            }
            // Fall back to default from UserSettings
            var defaultValue = UserSettings.GetDefault(key);
            if (defaultValue is bool defaultBool) return defaultBool;
            return false;
        }

        private int GetIntFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config)
        {
            if (config.TryGetValue(key, out var value))
            {
                if (value is int intValue) return intValue;
                if (int.TryParse(value?.ToString(), out int parsed)) return parsed;
            }
            // Fall back to default from UserSettings
            var defaultValue = UserSettings.GetDefault(key);
            if (defaultValue is int defaultInt) return defaultInt;
            return 0;
        }

        private string GetStringFromConfig(string key, System.Collections.Generic.Dictionary<string, object> config)
        {
            if (config.TryGetValue(key, out var value) && value is string strValue)
                return strValue;
            // Fall back to default from UserSettings
            var defaultValue = UserSettings.GetDefault(key);
            if (defaultValue is string defaultStr) return defaultStr;
            return string.Empty;
        }
    }
}
