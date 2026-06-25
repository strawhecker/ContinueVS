using System.Text.Json;

namespace ContinueTranslator.Core.Mapping;

/// <summary>
/// Reads <c>callsites.json</c> at construction time and resolves Node.js call expressions
/// (e.g. <c>fs.readFileSync</c>) to their .NET fully-qualified equivalents.
/// </summary>
internal sealed class CallSiteMap
{
    private readonly Dictionary<string, string> _map;

    /// <param name="jsonPath">Absolute path to <c>callsites.json</c>.</param>
    public CallSiteMap(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        using var stream = File.OpenRead(jsonPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Failed to deserialize '{jsonPath}'.");

        _map = new Dictionary<string, string>(raw, StringComparer.Ordinal);
    }

    /// <summary>
    /// Looks up <paramref name="callee"/> (e.g. <c>fs.readFileSync</c>) and returns
    /// the mapped .NET call expression when found.
    /// </summary>
    /// <returns><see langword="true"/> when a mapping exists.</returns>
    public bool TryResolve(string callee, out string dotNetCall)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callee);
        return _map.TryGetValue(callee, out dotNetCall!);
    }
}
