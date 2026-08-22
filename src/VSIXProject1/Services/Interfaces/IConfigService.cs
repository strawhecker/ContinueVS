using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for configuration management.
    /// Handles loading, persistence, and change notifications for application configuration.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Initializes the configuration service by loading configuration from disk.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InitializeAsync();

        /// <summary>
        /// Gets the current configuration object.
        /// </summary>
        /// <returns>The current ContinueConfig instance.</returns>
        Core.Types.ContinueConfig GetCurrentConfig();

        /// <summary>
        /// Adds a new model to the configuration.
        /// </summary>
        /// <param name="model">The model information to add.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddModelAsync(ModelInfo model);

        /// <summary>
        /// Removes a model from the configuration.
        /// </summary>
        /// <param name="modelId">The ID of the model to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RemoveModelAsync(string modelId);

        /// <summary>
        /// Selects a model as the current active model.
        /// </summary>
        /// <param name="modelId">The ID of the model to select.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SelectModelAsync(string modelId);

        /// <summary>
        /// Gets the currently selected model.
        /// </summary>
        /// <returns>The selected ModelInfo instance.</returns>
        ModelInfo? GetSelectedModel();

        /// <summary>
        /// Gets all enabled tools from the configuration.
        /// </summary>
        /// <returns>An enumerable of enabled ToolDefinition instances.</returns>
        IEnumerable<ToolDefinition> GetEnabledTools();

        /// <summary>
        /// Sets whether a tool is enabled or disabled.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <param name="enabled">True to enable, false to disable.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SetToolEnabledAsync(string toolName, bool enabled);

        /// <summary>
        /// Gets all available profiles.
        /// </summary>
        /// <returns>An enumerable of ProfileInfo instances.</returns>
        IEnumerable<ProfileInfo> GetProfiles();

        /// <summary>
        /// Selects a profile for use.
        /// </summary>
        /// <param name="profileId">The ID of the profile to select.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SelectProfileAsync(string profileId);

        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveConfigAsync();

        /// <summary>
        /// Reloads the configuration from disk, discarding any unsaved changes.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ReloadConfigAsync();

        /// <summary>
        /// Gets the tool override configuration (disable, rename, validate).
        /// Returns null if no overrides are configured.
        /// </summary>
        /// <returns>ToolOverrideConfig instance or null</returns>
        ToolOverrideConfig? GetToolOverrideConfig();

        /// <summary>
        /// Event raised when configuration is changed.
        /// </summary>
        event EventHandler<ConfigChangedEventArgs>? ConfigChanged;
    }
}
