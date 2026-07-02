using System.Text.Json;
using ContinueTranslator.Core.Mapping;
using Xunit;

namespace ContinueTranslator.Tests;

/// <summary>
/// Tests for TypeMap.Contains() and identity-mapped type resolution.
/// Verifies that types mapping to themselves (e.g., "string" → "string") are correctly
/// recognized as resolved, not marked as unresolved TODO items.
/// </summary>
public class TypeMapIdentityResolutionTests
{
    /// <summary>
    /// Creates a temporary types.json file with the given mappings and returns a TypeMap instance.
    /// </summary>
    private static TypeMap CreateTypeMapWithMappings(Dictionary<string, string> mappings)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"TypeMapTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string typesPath = Path.Combine(tempDir, "types.json");
        string json = JsonSerializer.Serialize(mappings);
        File.WriteAllText(typesPath, json);

        return new TypeMap(typesPath);
    }

    /// <summary>
    /// Verifies that Contains returns true for identity-mapped types (type → same type).
    /// This is critical for primitives like "string" and "void" that map to themselves.
    /// </summary>
    [Fact]
    public void Contains_IdentityMappedType_ReturnsTrue()
    {
        var mappings = new Dictionary<string, string>
        {
            { "string", "string" },
            { "void", "void" },
            { "boolean", "bool" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.True(typeMap.Contains("string"));
        Assert.True(typeMap.Contains("void"));
        Assert.True(typeMap.Contains("boolean"));
    }

    /// <summary>
    /// Verifies that Contains returns true for non-identity-mapped types.
    /// Examples: "number" → "double", "Promise" → "Task".
    /// </summary>
    [Fact]
    public void Contains_NonIdentityMappedType_ReturnsTrue()
    {
        var mappings = new Dictionary<string, string>
        {
            { "number", "double" },
            { "Promise<T>", "Task<T>" },
            { "Array<T>", "List<T>" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.True(typeMap.Contains("number"));
        Assert.True(typeMap.Contains("Promise"));
        Assert.True(typeMap.Contains("Array"));
    }

    /// <summary>
    /// Verifies that Contains returns the correct mapped value for identity-mapped types.
    /// </summary>
    [Fact]
    public void Resolve_IdentityMappedType_ReturnsIdenticalValue()
    {
        var mappings = new Dictionary<string, string>
        {
            { "string", "string" },
            { "void", "void" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.Equal("string", typeMap.Resolve("string"));
        Assert.Equal("void", typeMap.Resolve("void"));
    }

    /// <summary>
    /// Verifies that Contains returns false for unknown types.
    /// </summary>
    [Fact]
    public void Contains_UnknownType_ReturnsFalse()
    {
        var mappings = new Dictionary<string, string>
        {
            { "string", "string" },
            { "number", "double" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.False(typeMap.Contains("FooBarUnknown"));
        Assert.False(typeMap.Contains("UnmappedCustomType"));
    }

    /// <summary>
    /// Verifies that Contains handles generic base names correctly.
    /// "Set<T>" should be recognized even when split into base "Set" and args "<T>".
    /// </summary>
    [Fact]
    public void Contains_GenericBase_ReturnsTrue()
    {
        var mappings = new Dictionary<string, string>
        {
            { "Set<T>", "HashSet<T>" },
            { "Map<K,V>", "Dictionary<K,V>" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        // Should find the base "Set" when checking "Set<string>"
        Assert.True(typeMap.Contains("Set<string>"));
        Assert.True(typeMap.Contains("Map<string,int>"));
    }

    /// <summary>
    /// Verifies that Resolve handles generic base names correctly.
    /// When "Set<string>" is queried, should return "HashSet<string>".
    /// </summary>
    [Fact]
    public void Resolve_GenericBase_ReturnsWithTypeArgsAttached()
    {
        var mappings = new Dictionary<string, string>
        {
            { "Set<T>", "HashSet<T>" },
            { "Map<K,V>", "Dictionary<K,V>" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.Equal("HashSet<string>", typeMap.Resolve("Set<string>"));
        Assert.Equal("Dictionary<string,int>", typeMap.Resolve("Map<string,int>"));
    }

    /// <summary>
    /// Verifies that Contains returns false for null and whitespace inputs.
    /// This prevents null-reference exceptions when resolving edge-case types.
    /// </summary>
    [Fact]
    public void Contains_NullOrWhitespace_ReturnsFalse()
    {
        var mappings = new Dictionary<string, string>
        {
            { "string", "string" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.False(typeMap.Contains(null!));
        Assert.False(typeMap.Contains(""));
        Assert.False(typeMap.Contains("   "));
    }

    /// <summary>
    /// Integration test: Verifies that a type found by Contains always has a valid Resolve result.
    /// This ensures the two methods work in tandem correctly.
    /// </summary>
    [Fact]
    public void Contains_And_Resolve_AreConsistent()
    {
        var mappings = new Dictionary<string, string>
        {
            { "string", "string" },
            { "number", "double" },
            { "Promise<T>", "Task<T>" },
            { "Set<T>", "HashSet<T>" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        // For each type we know about, Contains and Resolve should work together
        foreach (var type in new[] { "string", "number", "Promise<string>", "Set<int>" })
        {
            // If Contains returns true, Resolve should return a non-null, non-empty string
            if (typeMap.Contains(type))
            {
                var result = typeMap.Resolve(type);
                Assert.NotNull(result);
                Assert.NotEmpty(result);
            }
        }

        // For unknown types, Contains should return false
        foreach (var type in new[] { "UnknownType", "FakePromise<T>" })
        {
            Assert.False(typeMap.Contains(type));
        }
    }
}
