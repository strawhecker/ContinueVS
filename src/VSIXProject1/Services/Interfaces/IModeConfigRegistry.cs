using System.Collections.Generic;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Registry that provides the <see cref="ModeConfig"/> for each supported <see cref="ChatMode"/>.
    /// The registry is the single source of truth for mode policy; no mode-specific logic
    /// should be scattered across services or view-models.
    /// </summary>
    public interface IModeConfigRegistry
    {
        /// <summary>
        /// Returns the configuration for the specified mode.
        /// </summary>
        /// <param name="mode">The chat mode to look up.</param>
        /// <returns>The <see cref="ModeConfig"/> for that mode.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown when <paramref name="mode"/> is not a recognised <see cref="ChatMode"/> value.
        /// </exception>
        ModeConfig GetConfig(ChatMode mode);

        /// <summary>
        /// Returns configurations for all registered modes.
        /// </summary>
        IReadOnlyList<ModeConfig> GetAllConfigs();
    }
}
