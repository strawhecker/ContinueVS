using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for List<T> to T[] conversion in assignment expressions.
/// 
/// In TypeScript, arrays are the primary collection type and can be reassigned freely.
/// In C#, when translating TypeScript variables to C# List<T>, assigning a List<T> to a T[] property
/// requires an explicit .ToArray() conversion.
/// 
/// Examples:
/// 
/// TypeScript Input:
///   const stack = new List<string>();
///   // ... stack.Add(...) operations
///   this.openingBracketsFromLastCompletion = stack;
///   
/// Generated C# Output (BEFORE FIX):
///   var stack = new List<string>();
///   // ... stack.Add(...) operations
///   openingBracketsFromLastCompletion = stack;  // ERROR: Cannot convert List<string> to string[]
///   
/// Generated C# Output (AFTER FIX):
///   var stack = new List<string>();
///   // ... stack.Add(...) operations
///   openingBracketsFromLastCompletion = stack.ToArray();  // OK!
/// </summary>
public class ListToArrayConversionTests
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
    /// Tests that assigning a variable named "stack" to a property containing "bracket"
    /// generates a .ToArray() conversion.
    /// This is the core fix for: this.openingBracketsFromLastCompletion = stack;
    /// </summary>
    [Fact]
    public void StackAssignmentToBracketProperty_GeneratesToArrayConversion()
    {
        // Arrange
        // Simulate: this.openingBracketsFromLastCompletion = stack;
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "openingBracketsFromLastCompletion");
        var stackVar = new TsIdentifierExpression("stack");
        var assignment = new TsBinaryExpression("=", prop, stackVar);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // The assignment should contain the property and stack variable
        Assert.Contains("openingBracketsFromLastCompletion", resultText);
        Assert.Contains("stack", resultText);

        // The critical check: must have .ToArray() conversion
        Assert.Contains("ToArray", resultText);

        // Verify it's a proper method call (has parentheses)
        // Regex to match: stack.ToArray()
        var hasToArrayCall = System.Text.RegularExpressions.Regex.IsMatch(
            resultText, 
            @"stack\s*\.\s*ToArray\s*\(\s*\)");
        Assert.True(hasToArrayCall, "Should generate stack.ToArray() method call");
    }

    /// <summary>
    /// Tests that a variable named "items" assigned to an array property also generates .ToArray().
    /// Verifies the heuristic works for other common list variable names.
    /// </summary>
    [Fact]
    public void ItemsAssignmentToElementProperty_GeneratesToArrayConversion()
    {
        // Arrange
        // Simulate: this.elementNames = items;
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "elementNames");
        var itemsVar = new TsIdentifierExpression("items");
        var assignment = new TsBinaryExpression("=", prop, itemsVar);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // The critical check: must have .ToArray() conversion for "items" → array property
        Assert.Contains("ToArray", resultText);
    }

    /// <summary>
    /// Tests that assignment to an array property with an unknown source type
    /// does NOT generate .ToArray() conversion.
    /// 
    /// This prevents over-aggressive conversion (e.g., wrapping method calls that already return arrays).
    /// </summary>
    [Fact]
    public void UnknownSourceToArrayProperty_NoConversion()
    {
        // Arrange
        // Simulate: this.data = getSomeValue();
        // "getSomeValue" doesn't match common list variable name patterns, and it's a function call
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "data");
        var callExpr = new TsCallExpression(
            new TsIdentifierExpression("getSomeValue"),
            Array.Empty<TsExpression>());
        var assignment = new TsBinaryExpression("=", prop, callExpr);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Should NOT add .ToArray() for function calls
        // (they might already return the correct type, and we exclude calls anyway)
        Assert.DoesNotContain("ToArray", resultText);
    }

    /// <summary>
    /// Tests that assignments to numeric (scalar) properties do NOT generate .ToArray() conversion.
    /// Even if the source variable name matches list patterns (like "stack"), 
    /// assigning to a property that infers to a numeric type (like "count" → int)
    /// should not trigger conversion.
    /// This prevents incorrect conversions for non-array properties.
    /// </summary>
    [Fact]
    public void StackAssignmentToCountProperty_NoConversion()
    {
        // Arrange
        // Simulate: this.count = stack;
        // "count" infers to numeric type (int), not array, so no conversion should happen
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "count");
        var stackVar = new TsIdentifierExpression("stack");
        var assignment = new TsBinaryExpression("=", prop, stackVar);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Should NOT add .ToArray() when target infers to numeric scalar type
        Assert.DoesNotContain("ToArray", resultText);
    }

    /// <summary>
    /// Tests that empty array assignment to a bracket property still uses new string[0]
    /// and does NOT attempt to add .ToArray() (which would be redundant).
    /// </summary>
    [Fact]
    public void EmptyArrayAssignmentToBracketProperty_UsesNewArray()
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

        // Should use new string[0] (inferred from property name)
        var normalizedText = System.Text.RegularExpressions.Regex.Replace(resultText, @"\s+", "");
        Assert.Contains("newstring[0]", normalizedText);

        // Should NOT add .ToArray() to a new array expression
        Assert.DoesNotContain("ToArray", resultText);
    }

    /// <summary>
    /// Tests that non-simple assignment operators (+=, -=, etc.) are not converted.
    /// The conversion is specifically for simple assignment (=) to minimize risk.
    /// </summary>
    [Fact]
    public void CompoundAssignmentToBracketProperty_NoConversion()
    {
        // Arrange
        // Simulate: this.brackets += stack;  (hypothetical compound assignment)
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "brackets");
        var stackVar = new TsIdentifierExpression("stack");
        var assignment = new TsBinaryExpression("+=", prop, stackVar);  // Compound assignment

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Should NOT add .ToArray() for compound assignments
        // (they have different semantics and type safety considerations)
        Assert.DoesNotContain("ToArray", resultText);
    }

    /// <summary>
    /// Tests that member-to-member assignments (e.g., this.a = this.b where b is a list variable)
    /// generate .ToArray() conversion correctly.
    /// </summary>
    [Fact]
    public void MemberExpressionAssignment_GeneratesToArrayConversion()
    {
        // Arrange
        // Simulate: this.items = this.stack;
        var thisId1 = new TsUnknownExpression("this");
        var targetProp = new TsMemberExpression(thisId1, "items");

        var thisId2 = new TsUnknownExpression("this");
        var sourceProp = new TsMemberExpression(thisId2, "stack");

        var assignment = new TsBinaryExpression("=", targetProp, sourceProp);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Should include .ToArray() for member-to-member assignment
        // where source property name suggests List (stack)
        Assert.Contains("ToArray", resultText);
    }

    /// <summary>
    /// Tests that assignment of a literal value (not a variable) does not trigger conversion.
    /// Literals should not be converted (e.g., a string literal assignment).
    /// </summary>
    [Fact]
    public void LiteralAssignmentToBracketProperty_NoConversion()
    {
        // Arrange
        // Simulate: this.bracket = "string_literal";  (nonsensical but tests safety)
        var thisId = new TsUnknownExpression("this");
        var prop = new TsMemberExpression(thisId, "bracket");
        var literal = new TsLiteralExpression("\"value\"");
        var assignment = new TsBinaryExpression("=", prop, literal);

        // Act
        var result = CreateEmitter().EmitExpression(assignment);

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Should NOT add .ToArray() for literals
        // (the type checking at emit time is simple; literals aren't converted)
        Assert.DoesNotContain("ToArray", resultText);
    }
}
