using System.Collections.Generic;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for theme color caching and persistence.
    /// Manages cache of theme colors to avoid recalculation and enable theme persistence across sessions.
    /// </summary>
    public interface IThemeCacheService
    {
        /// <summary>
        /// Caches theme colors in persistent storage.
        /// Stores a dictionary of CSS variable names to hex color values.
        /// </summary>
        /// <param name="colors">Dictionary mapping CSS variable names to hex color values (e.g., "--vscode-editor-background" -> "#1e1e1e")</param>
        void CacheThemeColors(Dictionary<string, string> colors);

        /// <summary>
        /// Retrieves cached theme colors from persistent storage.
        /// Returns null if no cache exists or cache is invalid.
        /// </summary>
        /// <returns>Dictionary of CSS variable names to hex color values, or null if no cache available</returns>
        Dictionary<string, string>? GetCachedTheme();

        /// <summary>
        /// Clears all cached theme data from persistent storage.
        /// Used when theme cache becomes stale or user explicitly resets theme settings.
        /// </summary>
        void ClearThemeCache();
    }
}
