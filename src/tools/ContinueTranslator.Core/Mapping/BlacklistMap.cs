using System.Text.Json;
using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.Mapping;

/// <summary>
/// Loads and manages a blacklist of TypeScript file paths that should NOT be translated.
/// Files matching blacklist patterns are rejected during translation.
/// Supports glob patterns with ** for matching directories.
/// </summary>
internal sealed class BlacklistMap
{
    private readonly List<string> _blacklistPatterns;

    /// <summary>
    /// Initializes a new instance by loading the blacklist from a JSON file.
    /// </summary>
    /// <param name="blacklistPath">Absolute path to the <c>blacklist.json</c> file.</param>
    /// <exception cref="FileNotFoundException">The blacklist file does not exist.</exception>
    /// <exception cref="JsonException">The blacklist JSON is malformed.</exception>
    public BlacklistMap(string blacklistPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blacklistPath);

        if (!File.Exists(blacklistPath))
            throw new FileNotFoundException($"Blacklist file not found: {blacklistPath}");

        string json = File.ReadAllText(blacklistPath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var root = JsonSerializer.Deserialize<BlacklistRoot>(json, options)
            ?? throw new JsonException("Blacklist JSON is empty or malformed");

        _blacklistPatterns = new List<string>(root.Files ?? []);
    }

    /// <summary>
    /// Determines whether the given relative file path is blacklisted.
    /// Paths are normalized to forward slashes and matched against glob patterns.
    /// Supports:
    /// - Exact paths: "core/node_modules/web-tree-sitter/index.d.ts"
    /// - Directory globs: "core/node_modules/web-tree-sitter/**" (matches all files in the directory)
    /// </summary>
    /// <param name="relativePath">Relative path from repo root (e.g., "core/node_modules/web-tree-sitter/index.d.ts").</param>
    /// <returns>True if the file matches the blacklist; false otherwise.</returns>
    public bool IsBlacklisted(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        // Normalize path separators to forward slashes for consistent matching
        string normalized = relativePath.Replace('\\', '/');

        foreach (var pattern in _blacklistPatterns)
        {
            if (MatchesPattern(normalized, pattern))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Matches a file path against a glob pattern.
    /// Supports * for matching files in a directory and ** for matching any number of directory levels.
    /// </summary>
    private static bool MatchesPattern(string path, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // Normalize pattern to forward slashes
        pattern = pattern.Replace('\\', '/');

        // Handle ** pattern (e.g., "core/node_modules/web-tree-sitter/**")
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

        // Handle * pattern (e.g., "core/node_modules/web-tree-sitter/*")
        // This matches files directly in the specified directory, not in subdirectories.
        if (pattern.EndsWith("/*", StringComparison.Ordinal))
        {
            string dirPrefix = pattern.Substring(0, pattern.Length - 2); // Remove "/*"

            // The path must start with the directory prefix and have a file name after it (no subdirs).
            // Check: path starts with "core/node_modules/web-tree-sitter/" and has no more "/" after that.
            if (path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure the character after the prefix is "/" and there are no more "/" after that.
                if (path.Length > dirPrefix.Length && path[dirPrefix.Length] == '/')
                {
                    string afterPrefix = path.Substring(dirPrefix.Length + 1);
                    // If there's no "/" in the remainder, it's a direct file in this directory.
                    if (!afterPrefix.Contains("/"))
                    {
                        return true;
                    }
                }
            }
        }

        // Handle non-wildcard patterns (exact match or directory prefix)
        // If pattern doesn't contain *, do simple prefix matching
        if (!pattern.Contains("*"))
        {
            // Exact match or pattern without wildcards
            if (path.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                return true;

            // If pattern ends with /, treat as directory prefix match
            if (pattern.EndsWith("/", StringComparison.Ordinal))
                return path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        return false;
    }

    /// <summary>
    /// Root structure for blacklist.json deserialization.
    /// </summary>
    private sealed class BlacklistRoot
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("files")]
        public string[]? Files { get; set; }
    }
}
