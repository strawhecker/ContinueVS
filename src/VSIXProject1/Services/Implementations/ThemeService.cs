using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service implementation for theme management and resource resolution.
    /// Loads theme ResourceDictionaries and exposes brush/color access.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private string _currentThemeName = "dark";
        private ResourceDictionary _currentThemeResources;
        private readonly Dictionary<string, ResourceDictionary> _loadedThemes;
        private static readonly object _syncLock = new object();

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        /// <summary>
        /// Initializes a new instance of the ThemeService class.
        /// </summary>
        public ThemeService()
        {
            _loadedThemes = new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase);
            _currentThemeResources = new ResourceDictionary();
        }

        /// <summary>
        /// Loads a theme by name asynchronously.
        /// </summary>
        public async Task LoadThemeAsync(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                throw new ArgumentNullException(nameof(themeName));
            }

            await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    if (!_loadedThemes.ContainsKey(themeName))
                    {
                        var xamlPath = GetThemeXamlPath(themeName);
                        if (!File.Exists(xamlPath))
                        {
                            throw new FileNotFoundException($"Theme file not found: {xamlPath}");
                        }

                        var resourceDictionary = LoadResourceDictionaryFromPath(xamlPath);
                        _loadedThemes[themeName] = resourceDictionary;
                    }
                }
            });
        }

        /// <summary>
        /// Sets the current theme to the theme with the specified name.
        /// </summary>
        public void SetCurrentTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
            {
                throw new ArgumentNullException(nameof(themeName));
            }

            lock (_syncLock)
            {
                if (!_loadedThemes.ContainsKey(themeName))
                {
                    throw new KeyNotFoundException($"Theme '{themeName}' has not been loaded. Call LoadThemeAsync first.");
                }

                string previousThemeName = _currentThemeName;
                _currentThemeName = themeName;
                _currentThemeResources = _loadedThemes[themeName];

                OnThemeChanged(new ThemeChangedEventArgs(previousThemeName, themeName));
            }
        }

        /// <summary>
        /// Gets the name of the currently active theme.
        /// </summary>
        public string GetCurrentThemeName()
        {
            lock (_syncLock)
            {
                return _currentThemeName;
            }
        }

        /// <summary>
        /// Gets a brush resource by key from the current theme.
        /// </summary>
        public SolidColorBrush GetBrush(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            lock (_syncLock)
            {
                if (_currentThemeResources.Contains(key))
                {
                    var resource = _currentThemeResources[key];
                    if (resource is SolidColorBrush brush)
                    {
                        return brush;
                    }
                }

                // Return default brush if not found
                return new SolidColorBrush(Colors.Gray);
            }
        }

        /// <summary>
        /// Gets a color resource by key from the current theme.
        /// </summary>
        public Color GetColor(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            lock (_syncLock)
            {
                if (_currentThemeResources.Contains(key))
                {
                    var resource = _currentThemeResources[key];
                    if (resource is SolidColorBrush brush)
                    {
                        return brush.Color;
                    }
                    if (resource is Color color)
                    {
                        return color;
                    }
                }

                // Return default color if not found
                return Colors.Gray;
            }
        }

        /// <summary>
        /// Gets a list of all available theme names.
        /// </summary>
        public IEnumerable<string> GetAvailableThemes()
        {
            lock (_syncLock)
            {
                return _loadedThemes.Keys.ToList();
            }
        }

        /// <summary>
        /// Raises the ThemeChanged event.
        /// </summary>
        protected virtual void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ThemeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Gets the file path for a theme XAML file.
        /// Handles both development (relative path from bin) and deployed (VSIX extension folder) scenarios.
        /// </summary>
        private static string GetThemeXamlPath(string themeName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                throw new InvalidOperationException("Unable to determine assembly location.");
            }

            var themeFileName = $"Theme{char.ToUpper(themeName[0])}{themeName.Substring(1)}.xaml";

            // Try to find theme in relative path first (development scenario: bin\net472)
            var relativePath = Path.Combine(assemblyLocation, "..", "..", "..", "UI", "Styles", "Themes", themeFileName);
            var normalizedPath = Path.GetFullPath(relativePath);

            if (File.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            // Fall back to direct subdirectory relative to assembly (deployed VSIX scenario)
            // The VSIX extension folder structure: extension_folder\UI\Styles\Themes\ThemeDark.xaml
            var deployedPath = Path.Combine(assemblyLocation, "UI", "Styles", "Themes", themeFileName);
            if (File.Exists(deployedPath))
            {
                return deployedPath;
            }

            // Last resort: try pack URI for embedded or resource-based loading
            // This path will fail gracefully if file doesn't exist, which is caught by LoadThemeAsync
            return deployedPath;
        }

        /// <summary>
        /// Loads a ResourceDictionary from a XAML file path.
        /// </summary>
        private static ResourceDictionary LoadResourceDictionaryFromPath(string xamlPath)
        {
            try
            {
                var uri = new Uri(xamlPath, UriKind.Absolute);
                var resourceDictionary = new ResourceDictionary { Source = uri };
                return resourceDictionary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load theme from '{xamlPath}': {ex.Message}", ex);
            }
        }
    }
}
