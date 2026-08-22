using System;
using System.Collections.Generic;

namespace ContinueVS.Core.Types
{
    /// <summary>
    /// Represents a cached theme with color values and metadata.
    /// Used to persist theme colors in localStorage to avoid recalculation on each session.
    /// </summary>
    public class ThemeCache
    {
        /// <summary>
        /// Dictionary mapping CSS variable names to hex color values.
        /// Example: "--vscode-editor-background" -> "#1e1e1e"
        /// </summary>
        public Dictionary<string, string> Colors { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Timestamp when the theme was cached.
        /// Used to track cache staleness and enable cache invalidation if needed.
        /// </summary>
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}
