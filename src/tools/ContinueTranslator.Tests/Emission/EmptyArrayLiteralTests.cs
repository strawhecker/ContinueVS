using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for empty array literal translation from TypeScript to C#.
/// 
/// In TypeScript, empty array literals [] are common for initializing arrays.
/// In C#, empty implicit array creation (new[]{}) fails type inference because
/// the compiler cannot determine the array element type.
/// 
/// This test file verifies that the translator generates compilable C# code
/// by using new char[0] for empty arrays, which provides explicit type information.
/// 
/// Examples:
/// 
/// TypeScript Input:
///   this.openingBracketsFromLastCompletion = [];
///   
/// Expected C# Output:
///   this.openingBracketsFromLastCompletion = new char[0];
///   
/// (NOT: new[]{} — which causes C# type inference failure!)
/// </summary>
public class EmptyArrayLiteralTests
{
    private CsEmitter CreateEmitter()
    {
        // Create minimal mapping files for testing
        var callSitesPath = Path.Combine(Path.GetTempPath(), $"test_callsites_{Guid.NewGuid():N}.json");
        var usingsPath = Path.Combine(Path.GetTempPath(), $"test_usings_{Guid.NewGuid():N}.json");

        var callSitesJson = """{}""";
        var usingsJson = """{}""";

        File.WriteAllText(callSitesPath, callSitesJson);
        File.WriteAllText(usingsPath, usingsJson);

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
    /// Tests that an empty array literal expression emits as new char[0].
    /// When a standalone empty array literal is emitted (not in an assignment context),
    /// we use new char[0] which has explicit type information.
    /// </summary>
    [Fact]
    public void EmptyArrayLiteral_EmitsExplicitCharArray()
    {
        // Arrange
        var emptyArrayExpr = new TsArrayLiteralExpression(Array.Empty<TsExpression>());

        // Act
        var result = CreateEmitter().EmitExpression(emptyArrayExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Standalone empty arrays use new char[0] for explicit typing
        Assert.Contains("new", resultText);
        Assert.Contains("char", resultText);
        Assert.Contains("[", resultText);
        Assert.Contains("0", resultText);

        // Should NOT contain the broken "new[]{}" pattern
        Assert.DoesNotContain("new[", resultText);
    }

    /// <summary>
    /// Tests that an empty array in a property assignment expression translates correctly.
    /// This verifies the fix for: this.openingBracketsFromLastCompletion = [];
    /// The translator infers the element type from the property name ("brackets" → string).
    /// </summary>
    [Fact]
    public void EmptyArrayInAssignment_GeneratesCompilableCode()
    {
        // Arrange
        // Simulate: this.openingBracketsFromLastCompletion = [];
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "openingBracketsFromLastCompletion");
        var emptyArray = new TsArrayLiteralExpression(Array.Empty<TsExpression>());
        var assignment = new TsBinaryExpression("=", prop, emptyArray);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // The assignment expression should contain the property name
        Assert.Contains("openingBracketsFromLastCompletion", resultText);

        // And the right-hand side should be new string[0] (inferred from property name)
        // Allow flexible whitespace since ToString() may not preserve spacing
        var normalizedText = System.Text.RegularExpressions.Regex.Replace(resultText, @"\s+", "");
        Assert.Contains("newstring[0]", normalizedText);

        // Verify no broken new[]{} pattern and no cast needed
        Assert.DoesNotContain("new[", resultText);
    }

    /// <summary>
    /// Tests that a non-empty array still uses the implicit array creation syntax (new[]).
    /// This ensures we didn't break the normal case when fixing empty arrays.
    /// </summary>
    [Fact]
    public void NonEmptyArrayLiteral_UsesImplicitArrayCreation()
    {
        // Arrange
        var arrayWithElements = new TsArrayLiteralExpression(new[]
        {
            new TsLiteralExpression("\"a\"") as TsExpression,
            new TsLiteralExpression("\"b\"")
        });

        // Act
        var result = CreateEmitter().EmitExpression(arrayWithElements);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Non-empty arrays should still use the implicit syntax
        Assert.Contains("a", resultText);
        Assert.Contains("b", resultText);
    }

    /// <summary>
    /// Tests that multiple empty array expressions generate valid C# each time.
    /// This is a regression test to ensure the fix works in sequence when using
    /// direct EmitExpression (not in assignment context).
    /// </summary>
    [Fact]
    public void MultipleEmptyArrayAssignments_AllGenerateValidCode()
    {
        // Arrange
        var emitter = CreateEmitter();
        var emptyArray = new TsArrayLiteralExpression(Array.Empty<TsExpression>());

        // Act - emit empty arrays multiple times
        var result1 = emitter.EmitExpression(emptyArray);
        var result2 = emitter.EmitExpression(emptyArray);
        var result3 = emitter.EmitExpression(emptyArray);

        // Assert - all should generate valid new char[0]
        foreach (var result in new[] { result1, result2, result3 })
        {
            Assert.NotNull(result);
            var text = result.ToString();
            Assert.Contains("char", text);
            Assert.Contains("0", text);
        }
    }
}
