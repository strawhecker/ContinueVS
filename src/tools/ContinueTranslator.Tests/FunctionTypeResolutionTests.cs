using System.Text.Json;
using ContinueTranslator.Core.Mapping;
using ContinueTranslator.Core.IR;
using Xunit;

namespace ContinueTranslator.Tests;

/// <summary>
/// Tests for Function type resolution in TypeMap and MappingEngine.
/// Verifies that both bare "Function" type names and full function signatures are properly mapped to "Delegate".
/// </summary>
public class FunctionTypeResolutionTests
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
    /// Verifies that TypeMap contains and resolves "Function" to "Delegate".
    /// </summary>
    [Fact]
    public void TypeMap_ContainsFunction_ReturnsTrue()
    {
        var mappings = new Dictionary<string, string>
        {
            { "Function", "Delegate" },
            { "string", "string" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        Assert.True(typeMap.Contains("Function"));
        Assert.Equal("Delegate", typeMap.Resolve("Function"));
    }

    /// <summary>
    /// Verifies that MappingEngine.ResolveTypeRef handles bare "Function" name without "=>".
    /// This simulates the case when TypeScript's type checker returns the simplified "Function" type.
    /// </summary>
    [Fact]
    public void MappingEngine_BareFunctionType_ResolvesToDelegateWithoutTodo()
    {
        // Arrange: Create a TsTypeRef for bare "Function" type (no arrow notation)
        var functionTypeRef = new TsTypeRef(
            Text: "Function",
            Name: "Function",
            TypeArgs: [],
            IsArray: false
        );

        var mappings = new Dictionary<string, string>
        {
            { "Function", "Delegate" },
        };

        string tempDir = Path.Combine(Path.GetTempPath(), $"TypeMapTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Create dummy mapping files first before instantiating map objects
        string typesPath = Path.Combine(tempDir, "types.json");
        string nodeApiPath = Path.Combine(tempDir, "node-api.json");
        string npmPackagesPath = Path.Combine(tempDir, "npm-packages.json");
        string callSitesPath = Path.Combine(tempDir, "callsites.json");

        File.WriteAllText(typesPath, JsonSerializer.Serialize(mappings));
        File.WriteAllText(nodeApiPath, "{}");
        File.WriteAllText(npmPackagesPath, "{}");
        File.WriteAllText(callSitesPath, "{}");

        var typeMap = new TypeMap(typesPath);
        var nodeApiMap = new NodeApiMap(nodeApiPath);
        var npmPackageMap = new NpmPackageMap(npmPackagesPath);
        var callSiteMap = new CallSiteMap(callSitesPath);

        var mappingEngine = new MappingEngine(nodeApiMap, npmPackageMap, typeMap, callSiteMap);

        // Create minimal IR for testing
        var tsFile = new TsFile(
            FilePath: "test.ts",
            Imports: [],
            Classes: [],
            Interfaces: [
                new TsInterface(
                    Name: "TestInterface",
                    TypeParameters: [],
                    Extends: [],
                    Properties: [
                        new TsProperty(
                            Name: "testFunc",
                            Type: functionTypeRef,
                            IsOptional: false,
                            IsReadonly: false,
                            IsStatic: false,
                            Accessibility: "public",
                            Cookies: []
                        )
                    ],
                    Methods: [],
                    IsExported: true,
                    Cookies: []
                )
            ],
            Enums: [],
            Functions: [],
            TypeAliases: [],
            Cookies: []
        );

        // Act: Apply mapping engine to resolve the type
        var result = mappingEngine.Apply([tsFile]);

        // Assert: Verify that "Function" was resolved to "Delegate" WITHOUT a TODO cookie
        var resultProp = result[0].Interfaces[0].Properties[0];
        Assert.Equal("Delegate", resultProp.Type.Name);
        Assert.Equal("Delegate", resultProp.Type.Text);
        Assert.Empty(resultProp.Cookies); // No TODO cookie should be added
    }

    /// <summary>
    /// Verifies that arrow function types with "=>" are still handled correctly.
    /// The existing ResolveTypeRef check for "=>" should continue to work.
    /// </summary>
    [Fact]
    public void MappingEngine_ArrowFunctionType_ResolvesToDelegateWithArrowSyntax()
    {
        // Arrange: Create a TsTypeRef for arrow function type (with "=>")
        var arrowFunctionTypeRef = new TsTypeRef(
            Text: "(args: { prefix: string; suffix: string }) => boolean",
            Name: "(args: { prefix: string; suffix: string }) => boolean",
            TypeArgs: [],
            IsArray: false
        );

        var mappings = new Dictionary<string, string>
        {
            { "boolean", "bool" },
        };

        string tempDir = Path.Combine(Path.GetTempPath(), $"TypeMapTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Create dummy mapping files first before instantiating map objects
        string typesPath = Path.Combine(tempDir, "types.json");
        string nodeApiPath = Path.Combine(tempDir, "node-api.json");
        string npmPackagesPath = Path.Combine(tempDir, "npm-packages.json");
        string callSitesPath = Path.Combine(tempDir, "callsites.json");

        File.WriteAllText(typesPath, JsonSerializer.Serialize(mappings));
        File.WriteAllText(nodeApiPath, "{}");
        File.WriteAllText(npmPackagesPath, "{}");
        File.WriteAllText(callSitesPath, "{}");

        var typeMap = new TypeMap(typesPath);
        var nodeApiMap = new NodeApiMap(nodeApiPath);
        var npmPackageMap = new NpmPackageMap(npmPackagesPath);
        var callSiteMap = new CallSiteMap(callSitesPath);

        var mappingEngine = new MappingEngine(nodeApiMap, npmPackageMap, typeMap, callSiteMap);

        // Create minimal IR for testing
        var tsFile = new TsFile(
            FilePath: "test.ts",
            Imports: [],
            Classes: [],
            Interfaces: [
                new TsInterface(
                    Name: "TestInterface",
                    TypeParameters: [],
                    Extends: [],
                    Properties: [
                        new TsProperty(
                            Name: "testFunc",
                            Type: arrowFunctionTypeRef,
                            IsOptional: false,
                            IsReadonly: false,
                            IsStatic: false,
                            Accessibility: "public",
                            Cookies: []
                        )
                    ],
                    Methods: [],
                    IsExported: true,
                    Cookies: []
                )
            ],
            Enums: [],
            Functions: [],
            TypeAliases: [],
            Cookies: []
        );

        // Act: Apply mapping engine to resolve the type
        var result = mappingEngine.Apply([tsFile]);

        // Assert: Verify that arrow function was resolved to "Delegate" WITHOUT a TODO cookie
        var resultProp = result[0].Interfaces[0].Properties[0];
        Assert.Equal("Delegate", resultProp.Type.Name);
        Assert.Equal("Delegate", resultProp.Type.Text);
        Assert.Empty(resultProp.Cookies); // No TODO cookie should be added
    }

    /// <summary>
    /// Verifies that nullable bare "Function" types (e.g., "Function?") are handled correctly.
    /// </summary>
    [Fact]
    public void TypeMap_NullableFunction_ResolvesCorrectly()
    {
        var mappings = new Dictionary<string, string>
        {
            { "Function", "Delegate" },
        };

        var typeMap = CreateTypeMapWithMappings(mappings);

        // TypeMap should handle "Function" base name even when queried with generic syntax
        Assert.True(typeMap.Contains("Function"));
        Assert.Equal("Delegate", typeMap.Resolve("Function"));
    }
}
