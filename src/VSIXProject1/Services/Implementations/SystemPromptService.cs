using System;
using System.Collections.Generic;
using System.IO;
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

        private SystemPromptConfig? _config;
        private bool _isLoaded;

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

        private static string GetDefaultPromptForMode(string mode)
        {
            switch (mode.ToLowerInvariant())
            {
                case "agent":
                    return "You are in agent mode. Use multiple tools simultaneously if needed. Always include the language and file path in the info string when you write code blocks. " +
                           "For implementation, use edit tools (not suggestion blocks). Use abbreviated syntax for larger files (// ... existing code ...).";
                case "plan":
                    return "You are in plan mode, in which you help the user understand and construct a plan. Only use read-only tools. Do not use any tools that would write to non-temporary files. " +
                           "If the user wants to make changes, offer that they can switch to Agent Mode to give you access to write tools to make the suggested updates. " +
                           "Always include the language and file name in the info string when you write code blocks. For planning purposes only, output code blocks for suggestion and planning. When ready to implement, request to switch to Agent Mode.";
                default:
                    return "You are in chat mode. If the user asks to make changes to files, offer that they can use the Apply Button on the code block, or suggest switching to Agent Mode to make updates automatically. " +
                           "Always include the language and file name in the info string when you write code blocks. For larger blocks (>20 lines), use abbreviated placeholders like `// ... existing code ...` at the beginning, middle, or end. " +
                           "Concisely explain changes unless the user asks for code only.";
            }
        }
    }
}

