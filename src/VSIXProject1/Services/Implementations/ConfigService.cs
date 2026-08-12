using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IConfigService that manages Continue configuration.
    /// Wraps ConfigCache and provides typed access to configuration data.
    /// Loads/saves configuration from ~/.continue/config.json.
    /// </summary>
    public class ConfigService : IConfigService
    {
        private CoreTypes.ContinueConfig _currentConfig = null!;
        private bool _initialized = false;
        private readonly object _lock = new object();

        private static readonly string ContinueDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".continue");

        private static readonly string ConfigFilePath = Path.Combine(ContinueDir, "config.json");

        public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

        /// <summary>
        /// Initializes the configuration service by loading configuration from disk.
        /// </summary>
        public async Task InitializeAsync()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    return;
                }
            }

            await Task.Run(() =>
            {
                try
                {
                    // Ensure directory exists
                    Directory.CreateDirectory(ContinueDir);

                    // Load or create default configuration
                    if (File.Exists(ConfigFilePath))
                    {
                        var json = File.ReadAllText(ConfigFilePath);
                        _currentConfig = JsonConvert.DeserializeObject<CoreTypes.ContinueConfig>(json) 
                            ?? CreateDefaultConfig();
                    }
                    else
                    {
                        _currentConfig = CreateDefaultConfig();
                        SaveConfigSync();
                    }

                    _currentConfig.ConfigFilePath = ConfigFilePath;
                    _currentConfig.LastModified = DateTime.UtcNow;

                    lock (_lock)
                    {
                        _initialized = true;
                    }
                }
                catch (Exception ex)
                {
                    _currentConfig = CreateDefaultConfig();
                    _currentConfig.ConfigFilePath = ConfigFilePath;
                    lock (_lock)
                    {
                        _initialized = true;
                    }
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
                if (string.IsNullOrEmpty(_currentConfig.SelectedModelId))
                {
                    return null;
                }

                return _currentConfig.Models.FirstOrDefault(m => m.Id == _currentConfig.SelectedModelId);
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
                return _currentConfig.Tools.Where(t => t.IsEnabled).ToList();
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
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    ThrowIfNotInitialized();
                    if (File.Exists(ConfigFilePath))
                    {
                        var json = File.ReadAllText(ConfigFilePath);
                        _currentConfig = JsonConvert.DeserializeObject<CoreTypes.ContinueConfig>(json) 
                            ?? CreateDefaultConfig();
                        _currentConfig.ConfigFilePath = ConfigFilePath;
                        _currentConfig.LastModified = DateTime.UtcNow;
                    }
                }
            });

            OnConfigChanged("*", null, _currentConfig);
        }

        /// <summary>
        /// Creates a default configuration object.
        /// </summary>
        private static CoreTypes.ContinueConfig CreateDefaultConfig()
        {
            return new CoreTypes.ContinueConfig
            {
                Models = new List<CoreTypes.ModelInfo>(),
                Tools = new List<CoreTypes.ToolDefinition>(),
                Profiles = new List<CoreTypes.ProfileInfo>(),
                CustomSettings = new Dictionary<string, object>(),
                ConfigFilePath = ConfigFilePath,
                LastModified = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Synchronously saves configuration to disk. Must be called within lock.
        /// </summary>
        private void SaveConfigSync()
        {
            try
            {
                Directory.CreateDirectory(ContinueDir);
                var json = JsonConvert.SerializeObject(_currentConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);
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
    }
}
