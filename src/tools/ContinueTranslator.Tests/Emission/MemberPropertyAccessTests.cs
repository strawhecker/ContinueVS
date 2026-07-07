using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for TypeScript member property access translation to C#.
/// 
/// Bug: TypeScript `.length` property (lowercase) must be translated to C# `.Length` (uppercase)
/// for built-in types like strings and arrays. Other properties preserve their original case
/// to avoid breaking anonymous types and user-defined classes.
/// </summary>
public class MemberPropertyAccessTests
{
    private CsEmitter CreateEmitter()
    {
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
    /// Verifies that .length property is capitalized to .Length.
    /// This is the core fix for the bug reported in BracketMatchingService.ts line 23.
    /// 
    /// TypeScript: completion.length
    /// Expected C#: completion.Length
    /// </summary>
    [Fact]
    public void EmitExpression_WithLengthProperty_CapitalizesToPascalCase()
    {
        // Arrange
        var arrayId = new TsIdentifierExpression("completion");
        var memberExpr = new TsMemberExpression(arrayId, "length");

        // Act
        var result = CreateEmitter().EmitExpression(memberExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("completion.Length", resultText);
        Assert.DoesNotContain("completion.length", resultText);
    }

    /// <summary>
    /// Verifies that non-standard properties preserve their original case.
    /// For properties like .name on anonymous types, we preserve the case
    /// because the property may be defined in lowercase.
    /// 
    /// TypeScript: obj.name (where obj is an anonymous type)
    /// Expected C#: obj.name (preserve case)
    /// </summary>
    [Fact]
    public void EmitExpression_WithNameProperty_PreservesOriginalCase()
    {
        // Arrange
        var objId = new TsIdentifierExpression("obj");
        var memberExpr = new TsMemberExpression(objId, "name");

        // Act
        var result = CreateEmitter().EmitExpression(memberExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        // Should preserve lowercase for user-defined properties
        Assert.Contains("obj.name", resultText);
    }

    /// <summary>
    /// Verifies that .message property preserves case for user-defined types.
    /// 
    /// TypeScript: error.message
    /// Expected C#: error.message (preserve case - may be on anonymous type)
    /// </summary>
    [Fact]
    public void EmitExpression_WithMessageProperty_PreservesOriginalCase()
    {
        // Arrange
        var errorId = new TsIdentifierExpression("error");
        var memberExpr = new TsMemberExpression(errorId, "message");

        // Act
        var result = CreateEmitter().EmitExpression(memberExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("error.message", resultText);
    }

    /// <summary>
    /// Verifies that property access in a for-loop condition works correctly.
    /// This is the exact regression case from BracketMatchingService.ts line 15.
    /// 
    /// TypeScript: for (let i = 0; i < completion.length; i++)
    /// Expected C#: for (var i = 0; i < completion.Length; i++)
    /// </summary>
    [Fact]
    public void EmitExpression_WithLengthInForLoopCondition_CapitalizesProperty()
    {
        // Arrange: build the condition expression "i < completion.length"
        var iId = new TsIdentifierExpression("i");
        var completionId = new TsIdentifierExpression("completion");
        var lengthProp = new TsMemberExpression(completionId, "length");
        var lessThanExpr = new TsBinaryExpression("<", iId, lengthProp);

        // Act
        var result = CreateEmitter().EmitExpression(lessThanExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("completion.Length", resultText);
        Assert.DoesNotContain("completion.length", resultText);
    }

    /// <summary>
    /// Verifies that .value property preserves case for user-defined types.
    /// 
    /// TypeScript: input.value
    /// Expected C#: input.value (preserve case)
    /// </summary>
    [Fact]
    public void EmitExpression_WithValueProperty_PreservesOriginalCase()
    {
        // Arrange
        var inputId = new TsIdentifierExpression("input");
        var memberExpr = new TsMemberExpression(inputId, "value");

        // Act
        var result = CreateEmitter().EmitExpression(memberExpr);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();
        Assert.Contains("input.value", resultText);
    }
}
