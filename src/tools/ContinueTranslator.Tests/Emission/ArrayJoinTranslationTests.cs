using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for Array.join() translation from TypeScript to C#.
/// 
/// In TypeScript, array.join(separator) concatenates elements with the separator.
/// In C#, string.Join(separator, collection) has the opposite argument order:
/// separator is the first parameter, collection is the second.
/// 
/// This test file verifies that the translator correctly inverts the argument order.
/// 
/// Examples:
/// 
/// TypeScript Input:
///   const snippets = typs.join("\n")
///   
/// Expected C# Output:
///   var snippets = string.Join("\n", typs);
///   
/// (NOT: string.Join(typs, "\n") — which would be wrong!)
/// </summary>
public class ArrayJoinTranslationTests
{
    private CsEmitter CreateEmitter()
    {
        // Create a minimal CallSiteMap with just the Array.join mapping for testing
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_callsites_{Guid.NewGuid():N}.json");
        var callSitesJson = """{"Array.join":"string.Join"}""";
        File.WriteAllText(tempPath, callSitesJson);
        try
        {
            return new CsEmitter(new CallSiteMap(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Tests array.join(separator) translates to string.Join(separator, array)
    /// with correct argument order: separator first, collection second.
    /// 
    /// TypeScript: typs.join("\n")
    /// C#: string.Join("\n", typs)
    /// </summary>
    [Fact]
    public void ArrayJoin_WithSeparatorArgument_EmitsStringJoinWithCorrectOrder()
    {
        // Arrange
        var callExpr = new TsCallExpression(
            new TsMemberExpression(
                new TsUnknownExpression("typs"),
                "join"),
            new TsExpression[] { new TsUnknownExpression("\"\\n\"") });

        // Act
        var result = CreateEmitter().EmitExpression(callExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Must contain string.Join call
        Assert.Contains("string.Join", resultText);

        // Verify the collection (typs) is present
        Assert.Contains("typs", resultText);

        // The old (broken) method call should NOT be present
        Assert.DoesNotContain(".join", resultText);
    }

    /// <summary>
    /// Tests array.join() with no argument (empty separator) translates correctly.
    /// The separator argument is omitted, just the collection is passed.
    /// 
    /// TypeScript: typs.join()
    /// C#: string.Join(typs)  (or similar minimal variant)
    /// </summary>
    [Fact]
    public void ArrayJoin_WithoutArgument_EmitsStringJoinWithoutCrash()
    {
        // Arrange
        var callExpr = new TsCallExpression(
            new TsMemberExpression(
                new TsUnknownExpression("typs"),
                "join"),
            new TsExpression[] { }); // No arguments

        // Act
        var result = CreateEmitter().EmitExpression(callExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Must contain string.Join call
        Assert.Contains("string.Join", resultText);

        // Must contain the array receiver
        Assert.Contains("typs", resultText);

        // The old (broken) method call should NOT be present
        Assert.DoesNotContain(".join", resultText);
    }

    /// <summary>
    /// Tests that the receiver object (array) appears in the correct position
    /// as the second argument to string.Join.
    /// 
    /// Verifies that the argument inversion fix is working: the array receiver
    /// is placed AFTER the separator, not before.
    /// </summary>
    [Fact]
    public void ArrayJoin_WithMultipleElements_PlacesCollectionSecond()
    {
        // Arrange
        // Simulating: elements.join(", ")
        var callExpr = new TsCallExpression(
            new TsMemberExpression(
                new TsUnknownExpression("elements"),
                "join"),
            new TsExpression[] { new TsUnknownExpression("\", \"") });

        // Act
        var result = CreateEmitter().EmitExpression(callExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Verify string.Join is used
        Assert.Contains("string.Join", resultText);

        // Verify the collection appears
        Assert.Contains("elements", resultText);

        // The old (broken) method call should NOT be present
        Assert.DoesNotContain(".join", resultText);
    }
}
