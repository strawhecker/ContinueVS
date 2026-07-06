using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.Mapping;

/// <summary>
/// Loads and manages a whitelist of TypeScript file paths that should be translated.
/// Files not in the whitelist are rejected during translation.
/// Supports glob patterns with ** for matching directories.
/// </summary>
internal sealed class WhitelistMap
{
    private readonly List<string> _whitelistPatterns;

    /// <summary>
    /// Initializes a new instance by loading the whitelist from a JSON file.
    /// </summary>
    /// <param name="whitelistPath">Absolute path to the <c>whitelist.json</c> file.</param>
    /// <exception cref="FileNotFoundException">The whitelist file does not exist.</exception>
    /// <exception cref="JsonException">The whitelist JSON is malformed.</exception>
    public WhitelistMap(string whitelistPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(whitelistPath);

        if (!File.Exists(whitelistPath))
            throw new FileNotFoundException($"Whitelist file not found: {whitelistPath}");

        string json = File.ReadAllText(whitelistPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<WhitelistRoot>(json, options)
            ?? throw new JsonException("Whitelist JSON is empty or malformed");

        _whitelistPatterns = new List<string>(root.Files ?? []);
    }

    /// <summary>
    /// Extracts node_modules directory patterns from the whitelist.
    /// Returns patterns like "core/node_modules/web-tree-sitter" (without the /**)
    /// for use in directory scanning.
    /// </summary>
    public IReadOnlyList<string> GetNodeModulesDirectoryPatterns()
    {
        var nodeModulesPatterns = new List<string>();

        foreach (var pattern in _whitelistPatterns)
        {
            if (pattern.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
            {
                // Remove /** suffix if present
                string dirPattern = pattern.EndsWith("/**", StringComparison.Ordinal)
                    ? pattern.Substring(0, pattern.Length - 3)
                    : pattern;

                nodeModulesPatterns.Add(dirPattern);
            }
        }

        return nodeModulesPatterns;
    }

    /// <summary>
    /// Determines whether the given relative file path is whitelisted.
    /// Paths are normalized to forward slashes and matched against glob patterns.
    /// Supports:
    /// - Exact paths: "core/node_modules/web-tree-sitter/index.d.ts"
    /// - Directory globs: "core/node_modules/web-tree-sitter/**" (matches all files in the directory)
    /// </summary>
    /// <param name="relativePath">Relative path from repo root (e.g., "core/node_modules/web-tree-sitter/index.d.ts").</param>
    /// <returns>True if the file matches the whitelist; false otherwise.</returns>
    public bool IsWhitelisted(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        // Normalize path separators to forward slashes for consistent matching
        string normalized = relativePath.Replace('\\', '/');

        foreach (var pattern in _whitelistPatterns)
        {
            if (MatchesPattern(normalized, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Matches a file path against a glob pattern.
    /// Supports ** for matching any number of directory levels.
    /// </summary>
    private static bool MatchesPattern(string path, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // Normalize pattern to forward slashes
        pattern = pattern.Replace('\\', '/');

        // If pattern doesn't contain **, do simple prefix matching
        if (!pattern.Contains("**"))
        {
            // Exact match or pattern without wildcards
            if (path.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            // If pattern ends with /, treat as directory prefix match
            if (pattern.EndsWith("/", StringComparison.Ordinal))
                return path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        // Handle ** pattern (e.g., "core/node_modules/web-tree-sitter/**")
        // Remove the /** suffix from the pattern
        if (pattern.EndsWith("/**", StringComparison.Ordinal))
        {
            string prefix = pattern.Substring(0, pattern.Length - 3); // Remove "/**"

            // The path must start with the prefix AND have at least one character after it
            // This ensures "core/autocomplete/..." doesn't match "core/node_modules/..."
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                (path.Length == prefix.Length || path[prefix.Length] == '/'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Root structure for whitelist.json deserialization.
    /// </summary>
    private sealed class WhitelistRoot
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("files")]
        public string[]? Files { get; set; }
    }
}
