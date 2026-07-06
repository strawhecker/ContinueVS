using System.Text.Json;

namespace ContinueTranslator.Core.Mapping;

/// <summary>
/// Reads <c>usings.json</c> at construction time and resolves C# type names
/// (e.g. <c>Task</c>, <c>List&lt;T&gt;</c>, <c>Dictionary&lt;K,V&gt;</c>) to their required using statements.
/// </summary>
internal sealed class UsingsMap
{
    // Maps C# types (with or without generics) to their required using statements
    private readonly Dictionary<string, IReadOnlyList<string>> _map;

    /// <param name="jsonPath">Absolute path to <c>usings.json</c>.</param>
    public UsingsMap(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        using var stream = File.OpenRead(jsonPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)
            ?? throw new InvalidOperationException($"Failed to deserialize '{jsonPath}'.");

        _map = new Dictionary<string, IReadOnlyList<string>>(raw.Count, StringComparer.Ordinal);

        foreach (var (csType, usings) in raw)
        {
            _map[csType] = Array.AsReadOnly(usings ?? []);
        }
    }

    /// <summary>
    /// Returns the required using statements for the given C# type.
    /// Handles both simple types (e.g., "Task") and generic types (e.g., "Task&lt;T&gt;", "Dictionary&lt;K,V&gt;").
    /// Returns an empty collection if the type is not found in the map.
    /// </summary>
    /// <param name="csType">The C# type name (may include generic parameters).</param>
    /// <returns>Read-only collection of required using namespace names.</returns>
    public IReadOnlyList<string> Resolve(string csType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csType);

        // Fast path: exact match (handles simple types and fully-specified generics)
        if (_map.TryGetValue(csType, out var direct))
            return direct;

        // Generic form: try to extract the base type name
        // e.g., "Task<T>" → "Task", "Dictionary<K,V>" → "Dictionary", "List<T>" → "List"
        int angleIndex = csType.IndexOf('<');
        if (angleIndex > 0)
        {
            string baseName = csType[..angleIndex];
            if (_map.TryGetValue(baseName, out var forBase))
                return forBase;
        }

        // Not found; return empty collection
        return Array.Empty<string>();
    }
}
