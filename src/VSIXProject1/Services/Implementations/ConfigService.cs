using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Services.Events;
using ContinueVS.Services.Exceptions;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IConfigService that manages Continue configuration.
    /// Wraps ConfigCache and provides typed access to configuration data.
    /// Loads/saves configuration from ~/.continueVS/continueVS.json.
    /// 
    /// Tools are loaded via two-tier lookup:
    /// 1. Check continueVS.json for user overrides
    /// 2. Load defaults from embedded tools-defaults.json resource
    /// 3. Merge: user overrides + resource defaults
    /// </summary>
    public class ConfigService : IConfigService
    {
        private CoreTypes.ContinueConfig _currentConfig = null!;
        private bool _initialized = false;
        private readonly object _lock = new object();
        private readonly IBridgeLogger? _logger;

        private static readonly string ContinueDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continueVS");

        private static readonly string ConfigFilePath = Path.Combine(ContinueDir, "continueVS.json");

        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

        /// <summary>
        /// Initializes a new instance of ConfigService.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public ConfigService(IBridgeLogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Initializes the configuration service by loading configuration from disk.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_logger != null)
                await _logger.WriteDebugAsync("ConfigService.InitializeAsync (start)");

            lock (_lock)
            {
                if (_initialized)
                {
                    System.Diagnostics.Debug.WriteLine("[ConfigService.InitializeAsync] Already initialized, returning");
                    return;
                }
            }

            await Task.Run(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ConfigService.InitializeAsync] Starting config load...");

                    // Ensure directory exists
                    Directory.CreateDirectory(ContinueDir);
                    System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Config dir: {ContinueDir}");

                    // Load or create default configuration
                    if (File.Exists(ConfigFilePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Config file exists: {ConfigFilePath}");
                        var json = File.ReadAllText(ConfigFilePath);
                        _currentConfig = JsonConvert.DeserializeObject<CoreTypes.ContinueConfig>(json) 
                            ?? (await CreateDefaultConfigAsync());
                        System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Loaded from file. Models: {_currentConfig.Models.Count}, SelectedModelId: {_currentConfig.SelectedModelId ?? "NULL"}");

                        // Migrate/upgrade: populate OllamaModelId for any missing entries
                        bool needsSave = false;
                        foreach (var model in _currentConfig.Models)
                        {
                            if (string.IsNullOrEmpty(model.OllamaModelId) && model.Provider == "ollama" && model.Name == "Llama 3.1 8B Instruct")
                            {
                                System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Migrating model '{model.Name}': setting OllamaModelId");
                                model.OllamaModelId = "hf.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF:Q5_K_M";
                                needsSave = true;
                            }
                        }

                        if (needsSave)
                        {
                            System.Diagnostics.Debug.WriteLine("[ConfigService.InitializeAsync] Config was migrated, saving updated version");
                            SaveConfigSync();
                        }

                        // Merge tools: user overrides + resource defaults
                        await MergeToolsWithResourceAsync(_currentConfig);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Config file does not exist, creating default: {ConfigFilePath}");
                        _currentConfig = await CreateDefaultConfigAsync();
                        System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Created default config. Models: {_currentConfig.Models.Count}, SelectedModelId: {_currentConfig.SelectedModelId ?? "NULL"}");
                        SaveConfigSync();
                        System.Diagnostics.Debug.WriteLine("[ConfigService.InitializeAsync] Saved default config to disk");
                    }

                    _currentConfig.ConfigFilePath = ConfigFilePath;
                    _currentConfig.LastModified = DateTime.UtcNow;
                    System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] Final state - SelectedModelId: {_currentConfig.SelectedModelId ?? "NULL"}, Models: {string.Join(", ", _currentConfig.Models.Select(m => m.Name))}");

                    lock (_lock)
                    {
                        _initialized = true;
                    }
                    System.Diagnostics.Debug.WriteLine("[ConfigService.InitializeAsync] Initialization complete");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService.InitializeAsync] ERROR: {ex.Message}, StackTrace: {ex.StackTrace}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Gets the current configuration object.
        /// </summary>
        public CoreTypes.ContinueConfig GetCurrentConfig()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException("ConfigService has not been initialized. Call InitializeAsync() first.");
                }
                return _currentConfig;
            }
        }

        /// <summary>
        /// Adds a new model to the configuration.
        /// </summary>
        public async Task AddModelAsync(CoreTypes.ModelInfo model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    _currentConfig.Models.Add(model);
                    _currentConfig.LastModified = DateTime.UtcNow;
                }
            });

            await SaveConfigAsync();
            OnConfigChanged("models", null, model);
        }

        /// <summary>
        /// Removes a model from the configuration.
        /// </summary>
        public async Task RemoveModelAsync(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or whitespace.", nameof(modelId));
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    var model = _currentConfig.Models.FirstOrDefault(m => m.Id == modelId);
                    if (model != null)
                    {
                        _currentConfig.Models.Remove(model);
                        _currentConfig.LastModified = DateTime.UtcNow;
                    }
                }
            });

            await SaveConfigAsync();
            OnConfigChanged("models", modelId, null);
        }

        /// <summary>
        /// Selects a model as the current active model.
        /// </summary>
        public async Task SelectModelAsync(string modelId)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException("Model ID cannot be null or whitespace.", nameof(modelId));
            }

            string? oldModelId = null;
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    oldModelId = _currentConfig.SelectedModelId;
                    _currentConfig.SelectedModelId = modelId;
                    _currentConfig.LastModified = DateTime.UtcNow;
                }
            });

            await SaveConfigAsync();
            OnConfigChanged("selectedModelId", oldModelId, modelId);
        }

        /// <summary>
        /// Gets the currently selected model.
        /// </summary>
        public CoreTypes.ModelInfo? GetSelectedModel()
        {
            lock (_lock)
            {
                ThrowIfNotInitialized();
                System.Diagnostics.Debug.WriteLine($"[ConfigService.GetSelectedModel] SelectedModelId: {_currentConfig.SelectedModelId ?? "NULL"}");
                System.Diagnostics.Debug.WriteLine($"[ConfigService.GetSelectedModel] Available models: {string.Join(", ", _currentConfig.Models.Select(m => $"{m.Name}(Id:{m.Id})"))}");

                if (string.IsNullOrEmpty(_currentConfig.SelectedModelId))
                {
                    System.Diagnostics.Debug.WriteLine("[ConfigService.GetSelectedModel] SelectedModelId is null/empty");

                    // Auto-select first model if none selected but models exist
                    if (_currentConfig.Models.Count > 0)
                    {
                        var firstModel = _currentConfig.Models.First();
                        System.Diagnostics.Debug.WriteLine($"[ConfigService.GetSelectedModel] Auto-selecting first model: {firstModel.Name} (Id:{firstModel.Id})");
                        _currentConfig.SelectedModelId = firstModel.Id;
                        return firstModel;
                    }

                    System.Diagnostics.Debug.WriteLine("[ConfigService.GetSelectedModel] No models available, returning null");
                    return null;
                }

                var selected = _currentConfig.Models.FirstOrDefault(m => m.Id == _currentConfig.SelectedModelId);
                if (selected != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService.GetSelectedModel] Found model: {selected.Name} (Id:{selected.Id})");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService.GetSelectedModel] Model not found for SelectedModelId: {_currentConfig.SelectedModelId}");
                }
                return selected;
            }
        }

        /// <summary>
        /// Gets all enabled tools from the configuration.
        /// </summary>
        public IEnumerable<CoreTypes.ToolDefinition> GetEnabledTools()
        {
            lock (_lock)
            {
                ThrowIfNotInitialized();
                var enabledTools = _currentConfig.Tools.Where(t => t.IsEnabled).ToList();
                Debug.WriteLine($"[gap8_1-configsvc-enabled] GetEnabledTools: {enabledTools.Count} enabled out of {_currentConfig.Tools.Count} total");
                return enabledTools;
            }
        }

        /// <summary>
        /// Sets whether a tool is enabled or disabled.
        /// </summary>
        public async Task SetToolEnabledAsync(string toolName, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(toolName));
            }

            bool? oldValue = null;
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    var tool = _currentConfig.Tools.FirstOrDefault(t => t.Name == toolName);
                    if (tool != null)
                    {
                        oldValue = tool.IsEnabled;
                        tool.IsEnabled = enabled;
                        _currentConfig.LastModified = DateTime.UtcNow;
                    }
                }
            });

            await SaveConfigAsync();
            OnConfigChanged($"tools.{toolName}.isEnabled", oldValue, enabled);
        }

        /// <summary>
        /// Gets all available profiles.
        /// </summary>
        public IEnumerable<CoreTypes.ProfileInfo> GetProfiles()
        {
            lock (_lock)
            {
                ThrowIfNotInitialized();
                return _currentConfig.Profiles.ToList();
            }
        }

        /// <summary>
        /// Selects a profile for use.
        /// </summary>
        public async Task SelectProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                throw new ArgumentException("Profile ID cannot be null or whitespace.", nameof(profileId));
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    var profile = _currentConfig.Profiles.FirstOrDefault(p => p.Id == profileId);
                    if (profile != null)
                    {
                        _currentConfig.CustomSettings["selectedProfileId"] = profileId;
                        _currentConfig.LastModified = DateTime.UtcNow;
                    }
                }
            });

            await SaveConfigAsync();
            OnConfigChanged("selectedProfileId", null, profileId);
        }

        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        public async Task SaveConfigAsync()
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    SaveConfigSync();
                }
            });
        }

        /// <summary>
        /// Reloads the configuration from disk, discarding any unsaved changes.
        /// </summary>
        public async Task ReloadConfigAsync()
        {
            await Task.Run(async () =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    if (File.Exists(ConfigFilePath))
                    {
                        var json = File.ReadAllText(ConfigFilePath);
                        _currentConfig = JsonConvert.DeserializeObject<CoreTypes.ContinueConfig>(json) 
                            ?? null!;
                        _currentConfig.ConfigFilePath = ConfigFilePath;
                        _currentConfig.LastModified = DateTime.UtcNow;
                    }
                }

                // Merge tools after reload
                await MergeToolsWithResourceAsync(_currentConfig);
            });

            OnConfigChanged("*", null, _currentConfig);
        }

        /// <summary>
        /// Gets the tool override configuration (disable, rename, validate).
        /// Returns null if no overrides are configured.
        /// </summary>
        /// <returns>ToolOverrideConfig instance or null for no overrides</returns>
        public ToolOverrideConfig? GetToolOverrideConfig()
        {
            lock (_lock)
            {
                ThrowIfNotInitialized();

                // For now, return null (no overrides configured)
                // This can be extended to load overrides from config file in the future
                return null;
            }
        }

        /// <summary>
        /// Creates a default configuration object with predefined Ollama model.
        /// Tools populated from embedded resource via MergeToolsWithResourceAsync.
        /// </summary>
        private async Task<CoreTypes.ContinueConfig> CreateDefaultConfigAsync()
        {
            System.Diagnostics.Debug.WriteLine("[ConfigService.CreateDefaultConfig] Creating default config...");

            var models = new List<CoreTypes.ModelInfo>
            {
                new CoreTypes.ModelInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Llama 3.1 8B Instruct",
                    Provider = "ollama",
                    ApiKey = null,
                    BaseUrl = "http://localhost:11434",
                    ContextWindow = 8192,
                    SupportsFunctionCalling = false,
                    SupportedToolFormats = new List<string>(),
                    OllamaModelId = "hf.co/bartowski/Meta-Llama-3.1-8B-Instruct-GGUF:Q5_K_M"
                }
            };

            var config = new CoreTypes.ContinueConfig
            {
                Models = models,
                Tools = new List<CoreTypes.ToolDefinition>(),
                Profiles = new List<CoreTypes.ProfileInfo>(),
                CustomSettings = new Dictionary<string, object>(),
                ConfigFilePath = ConfigFilePath,
                LastModified = DateTime.UtcNow,
                SelectedModelId = models[0].Id
            };

            // Load tools from resource
            await MergeToolsWithResourceAsync(config);

            System.Diagnostics.Debug.WriteLine($"[ConfigService.CreateDefaultConfig] Created config with SelectedModelId: {config.SelectedModelId}, Model: {models[0].Name} (Id: {models[0].Id}), Tools: {config.Tools.Count}");
            return config;
        }

        /// <summary>
        /// Merges tools from embedded resource with user overrides in config.
        /// Applies lightweight ToolOverride entries to full ToolDefinition instances.
        /// </summary>
        private async Task MergeToolsWithResourceAsync(CoreTypes.ContinueConfig config)
        {
            Debug.WriteLine("[gap8_1-configsvc-merge-tools] MergeToolsWithResourceAsync starting");

            // Load all defaults from resource
            var defaultTools = await ToolsResourceLoader.LoadDefaultToolsAsync();
            Debug.WriteLine($"[gap8_1-configsvc-merge-tools] Loaded {defaultTools.Count()} tools from resource");

            if (config.ToolOverrides == null)
                config.ToolOverrides = new List<CoreTypes.ToolOverride>();

            // Build a map of user overrides by name
            var overridesByName = config.ToolOverrides.ToDictionary(o => o.Name);

            // Merge: For each default tool, apply override if exists
            var mergedTools = new List<CoreTypes.ToolDefinition>();
            foreach (var defaultTool in defaultTools)
            {
                var toolCopy = new CoreTypes.ToolDefinition
                {
                    Name = defaultTool.Name,
                    Description = defaultTool.Description,
                    Category = defaultTool.Category,
                    Parameters = defaultTool.Parameters,
                    ReturnsDescription = defaultTool.ReturnsDescription,
                    IsAsync = defaultTool.IsAsync,
                    ToolType = defaultTool.ToolType,
                    McpServerId = defaultTool.McpServerId,
                    HttpEndpoint = defaultTool.HttpEndpoint,
                    LastModified = defaultTool.LastModified,
                    IsEnabled = defaultTool.IsEnabled // Start with default
                };

                // Apply override if exists
                if (overridesByName.TryGetValue(defaultTool.Name, out var overrideTool))
                {
                    Debug.WriteLine($"[gap8_1-configsvc-merge-tools] Applying override for tool: {defaultTool.Name}, IsEnabled: {overrideTool.IsEnabled}");
                    toolCopy.IsEnabled = overrideTool.IsEnabled;
                }
                else
                {
                    Debug.WriteLine($"[gap8_1-configsvc-merge-tools] Using resource default for tool: {defaultTool.Name}, IsEnabled: {defaultTool.IsEnabled}");
                }

                mergedTools.Add(toolCopy);
            }

            // Assign merged tools back to config
            config.Tools = mergedTools;
            Debug.WriteLine($"[gap8_1-configsvc-merge-tools] MergeToolsWithResourceAsync complete: {config.Tools.Count} tools in config");
        }

        /// <summary>
        /// Filters tools to only include those with non-default enabled state.
        /// Returns a list of ToolOverride objects (name + isEnabled only) for lightweight persistence.
        /// </summary>
        private List<CoreTypes.ToolOverride> FilterToolsByDelta(List<CoreTypes.ToolDefinition> tools)
        {
            Debug.WriteLine("[gap8_1-configsvc-filter-start] FilterToolsByDelta: start filtering");

            // Get all default tools from registry
            var defaultTools = CoreTypes.BuiltInToolsRegistry.GetAllBuiltInTools()
                .ToDictionary(t => t.Name);

            var overrides = new List<CoreTypes.ToolOverride>();
            int excludedCount = 0;
            int includedCount = 0;

            foreach (var tool in tools)
            {
                if (defaultTools.TryGetValue(tool.Name, out var defaultTool))
                {
                    // Compare IsEnabled; only include if different from default
                    if (tool.IsEnabled != defaultTool.IsEnabled)
                    {
                        Debug.WriteLine($"[gap8_1-configsvc-filter-keep] Tool '{tool.Name}': IsEnabled={tool.IsEnabled} (differs from default={defaultTool.IsEnabled}), KEEPING in JSON");
                        overrides.Add(new CoreTypes.ToolOverride 
                        { 
                            Name = tool.Name, 
                            IsEnabled = tool.IsEnabled 
                        });
                        includedCount++;
                    }
                    else
                    {
                        Debug.WriteLine($"[gap8_1-configsvc-filter-exclude] Tool '{tool.Name}': IsEnabled={tool.IsEnabled} (matches default), EXCLUDING from JSON");
                        excludedCount++;
                    }
                }
                else
                {
                    // Custom (non-built-in) tool; always include with all properties
                    Debug.WriteLine($"[gap8_1-configsvc-filter-custom] Tool '{tool.Name}': custom tool, KEEPING in JSON");
                    overrides.Add(new CoreTypes.ToolOverride 
                    { 
                        Name = tool.Name, 
                        IsEnabled = tool.IsEnabled 
                    });
                    includedCount++;
                }
            }

            Debug.WriteLine($"[gap8_1-configsvc-filter-end] FilterToolsByDelta: input={tools.Count}, excluded={excludedCount}, kept={includedCount}");
            return overrides;
        }

        /// <summary>
        /// Synchronously saves configuration to disk. Must be called within lock.
        /// Converts full ToolDefinition list to lightweight ToolOverride list for persistence.
        /// </summary>
        private void SaveConfigSync()
        {
            try
            {
                Directory.CreateDirectory(ContinueDir);

                // Convert full tool list to lightweight overrides for JSON persistence
                var toolOverrides = FilterToolsByDelta(_currentConfig.Tools);
                _currentConfig.ToolOverrides = toolOverrides;

                Debug.WriteLine($"[gap8_1-configsvc-save] SaveConfigSync: Persisting {toolOverrides.Count} tool overrides (from {_currentConfig.Tools.Count} full tools)");

                var json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);

                Debug.WriteLine($"[gap8_1-configsvc-save] SaveConfigSync: Config persisted successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving config: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises the ConfigChanged event.
        /// </summary>
        private void OnConfigChanged(string? configKey, object? oldValue, object? newValue)
        {
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                ConfigKey = configKey,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Throws InvalidOperationException if service not initialized.
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("ConfigService has not been initialized. Call InitializeAsync() first.");
            }
        }

        /// <summary>
        /// Gets the current UI state (tool policies, rule settings, etc.) from configuration.
        /// Returns an empty UIState if no UI state has been saved yet.
        /// </summary>
        public async Task<CoreTypes.UIState> GetUIStateAsync()
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();

                    const string uiStateKey = "ui.state";
                    if (_currentConfig.CustomSettings.TryGetValue(uiStateKey, out var uiStateObj))
                    {
                        try
                        {
                            // Handle both JSON string and already-deserialized object
                            if (uiStateObj is string jsonString)
                            {
                                var uiState = JsonConvert.DeserializeObject<CoreTypes.UIState>(jsonString);
                                if (uiState != null)
                                {
                                    System.Diagnostics.Debug.WriteLine("[ConfigService.GetUIStateAsync] Loaded UIState from JSON string");
                                    return uiState;
                                }
                            }
                            else if (uiStateObj is CoreTypes.UIState uiState)
                            {
                                System.Diagnostics.Debug.WriteLine("[ConfigService.GetUIStateAsync] UIState already deserialized");
                                return uiState;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ConfigService.GetUIStateAsync] Error deserializing UIState: {ex.Message}");
                        }
                    }

                    // Return empty UIState if key missing or deserialization failed
                    System.Diagnostics.Debug.WriteLine("[ConfigService.GetUIStateAsync] Returning empty UIState");
                    return new CoreTypes.UIState();
                }
            });
        }

        /// <summary>
        /// Saves the UI state (tool policies, rule settings, etc.) to configuration and disk.
        /// Serializes UIState to JSON string and stores in CustomSettings["ui.state"],
        /// then calls SaveConfigAsync() to persist to disk.
        /// </summary>
        public async Task SaveUIStateAsync(CoreTypes.UIState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();

                    const string uiStateKey = "ui.state";
                    var jsonString = JsonConvert.SerializeObject(state, Formatting.Indented);
                    _currentConfig.CustomSettings[uiStateKey] = jsonString;
                    _currentConfig.LastModified = DateTime.UtcNow;

                    System.Diagnostics.Debug.WriteLine($"[ConfigService.SaveUIStateAsync] Saved UIState to CustomSettings[\"{uiStateKey}\"]");
                }
            });

            // Persist to disk
            await SaveConfigAsync();
        }
    }
}
