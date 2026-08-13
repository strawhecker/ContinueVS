using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for theme management and resource resolution.
    /// Provides theme loading, switching, and brush resource access.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Loads a theme by name asynchronously.
        /// </summary>
        /// <param name="themeName">The name of the theme (e.g., "dark", "light")</param>
        /// <returns>A task representing the async operation</returns>
        Task LoadThemeAsync(string themeName);

        /// <summary>
        /// Sets the current theme to the theme with the specified name.
        /// </summary>
        /// <param name="themeName">The name of the theme to set as current</param>
        void SetCurrentTheme(string themeName);

        /// <summary>
        /// Gets the name of the currently active theme.
        /// </summary>
        /// <returns>The name of the current theme</returns>
        string GetCurrentThemeName();

        /// <summary>
        /// Gets a brush resource by key from the current theme.
        /// </summary>
        /// <param name="key">The resource key for the brush</param>
        /// <returns>A SolidColorBrush if found; otherwise a default brush</returns>
        SolidColorBrush GetBrush(string key);

        /// <summary>
        /// Gets a color resource by key from the current theme.
        /// </summary>
        /// <param name="key">The resource key for the color</param>
        /// <returns>A Color value if found; otherwise a default color</returns>
        Color GetColor(string key);

        /// <summary>
        /// Gets a list of all available theme names.
        /// </summary>
        /// <returns>An enumerable of available theme names</returns>
        IEnumerable<string> GetAvailableThemes();

        /// <summary>
        /// Fired when the current theme changes.
        /// </summary>
        event EventHandler<ThemeChangedEventArgs>? ThemeChanged;
    }

    /// <summary>
    /// Event arguments for theme change notifications.
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the name of the previous theme.
        /// </summary>
        public string PreviousThemeName { get; set; }

        /// <summary>
        /// Gets the name of the new theme.
        /// </summary>
        public string NewThemeName { get; set; }

        /// <summary>
        /// Initializes a new instance of the ThemeChangedEventArgs class.
        /// </summary>
        public ThemeChangedEventArgs(string previousThemeName, string newThemeName)
        {
            PreviousThemeName = previousThemeName;
            NewThemeName = newThemeName;
        }
    }
}
