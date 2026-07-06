using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for destructuring pattern translation from TypeScript to C#.
/// 
/// Covers:
/// 1. Object destructuring: const { config } = expr → var config = (expr).Config;
/// 2. Array destructuring: const [a, b] = expr → var (a, b) = expr;
/// 3. Multiple property destructuring: const { x, y } = expr → TODO comment
/// 4. No initializer handling: const { x } → TODO comment
/// </summary>
public class DestructuringPatternTests
{
    private CsEmitter CreateEmitter()
    {
        // Create minimal mapping files for testing
        var callSitesPath = Path.Combine(Path.GetTempPath(), $"test_callsites_{Guid.NewGuid():N}.json");
        var usingsPath = Path.Combine(Path.GetTempPath(), $"test_usings_{Guid.NewGuid():N}.json");

        File.WriteAllText(callSitesPath, "{}");
        File.WriteAllText(usingsPath, """{"Task":["System.Threading.Tasks"]}""");

        try
        {
            return new CsEmitter(new CallSiteMap(callSitesPath), new UsingsMap(usingsPath));
        }
        finally
        {
            File.Delete(callSitesPath);
            File.Delete(usingsPath);
        }
    }

    /// <summary>
    /// Tests single-property object destructuring emits correct property extraction.
    /// Example: const { config } = await this.configHandler.loadConfig();
    /// Expected: var config = (await this.configHandler.LoadConfig()).Config;
    /// </summary>
    [Fact]
    public void ObjectDestructuring_SingleProperty_EmitsPropertyExtraction()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsCallExpression(
                new TsMemberExpression(
                    new TsUnknownExpression("configHandler"),
                    "loadConfig"),
                new TsExpression[] { }),
            Names: new[] { "config" },
            PatternKind: "Object");

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as LocalDeclarationStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var declarationText = result.ToString();
        Assert.Contains("config", declarationText);
        Assert.Contains("loadConfig()", declarationText);
        // Should NOT emit tuple deconstruction syntax var (...)
        Assert.DoesNotContain("var (", declarationText);
    }

    /// <summary>
    /// Tests array destructuring emits C# tuple deconstruction.
    /// Example: const [a, b, c] = expr;
    /// Expected: var (a, b, c) = expr;
    /// </summary>
    [Fact]
    public void ArrayDestructuring_MultipleElements_EmitsTupleDeconstruction()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsUnknownExpression("getSomeArray()"),
            Names: new[] { "a", "b", "c" },
            PatternKind: "Array");

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("var (a, b, c)", resultText);
        Assert.Contains("getSomeArray()", resultText);
    }

    /// <summary>
    /// Tests multiple-property object destructuring emits a TODO comment.
    /// Example: const { x, y, z } = expr;
    /// Expected: // TODO: Object destructuring { x, y, z } = expr — convert manually...
    /// </summary>
    [Fact]
    public void ObjectDestructuring_MultipleProperties_EmitsTodoComment()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsUnknownExpression("getMultipleProps()"),
            Names: new[] { "x", "y", "z" },
            PatternKind: "Object");

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("// TODO", resultText);
        Assert.Contains("Object destructuring", resultText);
        Assert.Contains("x, y, z", resultText);
    }

    /// <summary>
    /// Tests destructuring without initializer emits a TODO comment.
    /// Example: const { config } = (no initializer)
    /// Expected: // TODO: destructuring without initializer for config
    /// </summary>
    [Fact]
    public void Destructuring_NoInitializer_EmitsTodoComment()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: null,
            Names: new[] { "config" },
            PatternKind: "Object");

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("// TODO", resultText);
        Assert.Contains("destructuring without initializer", resultText);
        Assert.Contains("config", resultText);
    }

    /// <summary>
    /// Tests single-variable declaration emits standard variable declaration.
    /// Example: const x = 5;
    /// Expected: var x = 5;
    /// </summary>
    [Fact]
    public void SingleVariable_EmitsStandardDeclaration()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: "x",
            Initializer: new TsUnknownExpression("5"),
            Names: null,
            PatternKind: null);

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as LocalDeclarationStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("var x", resultText);
        Assert.Contains("5", resultText);
    }

    /// <summary>
    /// Tests that PascalCase conversion works for property names.
    /// Example: const { configHandler } = expr with property Config
    /// Should correctly identify the property name
    /// </summary>
    [Fact]
    public void ObjectDestructuring_CamelCaseProperty_ConvertsToPascalCase()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsUnknownExpression("getConfig()"),
            Names: new[] { "maxRetries" },
            PatternKind: "Object");

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("maxRetries", resultText);
        // Should reference the property with PascalCase conversion
        Assert.Contains(".MaxRetries", resultText);
    }

    /// <summary>
    /// Tests array destructuring with await expression properly parenthesizes.
    /// This ensures property access on await results works correctly.
    /// </summary>
    [Fact]
    public void ObjectDestructuring_WithAwaitInitializer_ParenthesizesCorrectly()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsAwaitExpression(
                new TsCallExpression(
                    new TsMemberExpression(
                        new TsUnknownExpression("configHandler"),
                        "loadConfig"),
                    new TsExpression[] { })),
            Names: new[] { "config" },
            PatternKind: "Object");

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        // Should contain parenthesized await expression for property access
        Assert.Contains("(await", resultText);
        Assert.Contains(").Config", resultText);
    }

    /// <summary>
    /// Tests unknown pattern kind emits a TODO comment.
    /// </summary>
    [Fact]
    public void Destructuring_UnknownPatternKind_EmitsTodoComment()
    {
        // Arrange
        var stmt = new TsVarStatement(
            Name: null,
            Initializer: new TsUnknownExpression("expr()"),
            Names: new[] { "x" },
            PatternKind: "UnknownType"); // Invalid pattern kind

        // Act
        var result = CreateEmitter().EmitStatement(stmt);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("// TODO", resultText);
        Assert.Contains("Unknown destructuring pattern", resultText);
    }
}
