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
            const string CODEBLOCK_FORMATTING_INSTRUCTIONS =
                "Always include the language and file name in the info string when you write code blocks.\n" +
                "If you are editing \"src/main.py\" for example, your code block should start with '```python src/main.py'";

            const string EDIT_CODE_INSTRUCTIONS =
                "When addressing code modification requests, present a concise code snippet that\n" +
                "emphasizes only the necessary changes and uses abbreviated placeholders for\n" +
                "unmodified sections. For example:\n\n" +
                "```language /path/to/file\n" +
                "// ... existing code ...\n" +
                "{{ modified code here }}\n" +
                "// ... existing code ...\n" +
                "{{ another modification }}\n" +
                "// ... rest of code ...\n" +
                "```\n\n" +
                "In existing files, you should always restate the function or class that the snippet belongs to:\n\n" +
                "```language /path/to/file\n" +
                "// ... existing code ...\n\n" +
                "function exampleFunction() {\n" +
                "  // ... existing code ...\n\n" +
                "  {{ modified code here }}\n\n" +
                "  // ... rest of function ...\n" +
                "}\n\n" +
                "// ... rest of code ...\n" +
                "```\n\n" +
                "Since users have access to their complete file, they prefer reading only the\n" +
                "relevant modifications. It's perfectly acceptable to omit unmodified portions\n" +
                "at the beginning, middle, or end of files using these \"lazy\" comments. Only\n" +
                "provide the complete file when explicitly requested. Include a concise explanation\n" +
                "of changes unless the user specifically asks for code only.";

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
                           "</important_rules>";

                case "plan":
                    return "<important_rules>\n" +
                           "You are in plan mode, in which you help the user understand and construct a plan.\n" +
                           "Only use read-only tools. Do not use any tools that would write to non-temporary files.\n" +
                           "If the user wants to make changes, offer that they can switch to Agent mode to give you access to write tools to make the suggested updates.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n\n" +
                           BRIEF_LAZY_INSTRUCTIONS + "\n\n" +
                           "However, only output codeblocks for suggestion and planning purposes. When ready to implement changes, request to switch to Agent mode.\n\n" +
                           "In plan mode, only write code when directly suggesting changes. Prioritize understanding and developing a plan.\n" +
                           "</important_rules>";

                default:  // chat/ask mode
                    return "<important_rules>\n" +
                           "You are in chat mode.\n\n" +
                           "If the user asks to make changes to files offer that they can use the Apply Button on the code block, or switch to Agent Mode to make the suggested updates automatically.\n" +
                           "If needed concisely explain to the user they can switch to agent mode using the Mode Selector dropdown and provide no other details.\n\n" +
                           CODEBLOCK_FORMATTING_INSTRUCTIONS + "\n" +
                           EDIT_CODE_INSTRUCTIONS + "\n" +
                           "</important_rules>";
            }
        }
    }
}

