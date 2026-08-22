using System;
using System.Collections.Generic;
using System.Diagnostics;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service implementation for theme color caching and persistence.
    /// Handles storage and retrieval of theme colors using LocalStorageService for JSON I/O.
    /// </summary>
    public class ThemeCacheService : IThemeCacheService
    {
        private readonly ILocalStorageService _localStorageService;
        private const string ThemeCacheKey = "theme_colors";

        /// <summary>
        /// Initializes a new instance of ThemeCacheService.
        /// </summary>
        /// <param name="localStorageService">Service for persistent local storage</param>
        /// <exception cref="ArgumentNullException">Thrown if localStorageService is null</exception>
        public ThemeCacheService(ILocalStorageService localStorageService)
        {
            _localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
        }

        /// <summary>
        /// Caches theme colors in persistent storage.
        /// </summary>
        public void CacheThemeColors(Dictionary<string, string> colors)
        {
            if (colors == null)
            {
                Debug.WriteLine("ThemeCacheService.CacheThemeColors: colors dictionary is null, skipping cache");
                return;
            }

            try
            {
                var themeCache = new ThemeCache
                {
                    Colors = new Dictionary<string, string>(colors),
                    CachedAt = DateTime.UtcNow
                };

                _localStorageService.SetItem(ThemeCacheKey, themeCache);
                Debug.WriteLine($"ThemeCacheService: Cached {colors.Count} theme colors");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeCacheService.CacheThemeColors error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves cached theme colors from persistent storage.
        /// </summary>
        public Dictionary<string, string>? GetCachedTheme()
        {
            try
            {
                var cachedTheme = _localStorageService.GetItem<ThemeCache>(ThemeCacheKey);
                if (cachedTheme?.Colors != null && cachedTheme.Colors.Count > 0)
                {
                    Debug.WriteLine($"ThemeCacheService: Retrieved {cachedTheme.Colors.Count} cached theme colors");
                    return cachedTheme.Colors;
                }

                Debug.WriteLine("ThemeCacheService: No theme cache found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeCacheService.GetCachedTheme error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears all cached theme data from persistent storage.
        /// </summary>
        public void ClearThemeCache()
        {
            try
            {
                _localStorageService.RemoveItem(ThemeCacheKey);
                Debug.WriteLine("ThemeCacheService: Cleared theme cache");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeCacheService.ClearThemeCache error: {ex.Message}");
            }
        }
    }
}
