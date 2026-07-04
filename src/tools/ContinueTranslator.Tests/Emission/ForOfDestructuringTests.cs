using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for for-of loop destructuring pattern translation from TypeScript to C#.
/// 
/// In TypeScript, for-of with array destructuring uses square brackets:
///   for (const [a, b] of items)
///
/// In C#, foreach with tuple deconstruction uses parentheses:
///   foreach (var (a, b) in items)
///
/// This test verifies the translator correctly converts square brackets to parentheses.
/// </summary>
public class ForOfDestructuringTests
{
    private CsEmitter CreateEmitter()
    {
        // Create a minimal CallSiteMap with a temporary JSON file for testing
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_callsites_{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, "{}");
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
    /// Tests for-of with array destructuring translates square brackets to parentheses.
    /// 
    /// TypeScript: for (const [filepath, typs] of ctx.relevantTypes.entries())
    /// C#: foreach (var (filepath, typs) in ctx.relevantTypes.entries())
    /// </summary>
    [Fact]
    public void ForOfDestructuring_WithArrayPattern_EmitsParenthesesNotBrackets()
    {
        // Arrange
        var stmt = new TsForOfStatement(
            Variable: "[filepath, typs]",
            Expression: new TsCallExpression(
                new TsMemberExpression(
                    new TsUnknownExpression("ctx.relevantTypes"),
                    "entries"),
                new TsExpression[] { }),
            Statements: new TsStatement[] { });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as ForEachStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Must contain the parenthesized tuple deconstruction pattern
        Assert.Contains("(filepath", resultText);
        Assert.Contains("typs)", resultText);

        // Must NOT contain the invalid square bracket pattern
        Assert.DoesNotContain("[filepath", resultText);
        Assert.DoesNotContain("typs]", resultText);
    }

    /// <summary>
    /// Tests for-of with single variable (no destructuring) still works.
    /// 
    /// TypeScript: for (const item of items)
    /// C#: foreach (var item in items)
    /// </summary>
    [Fact]
    public void ForOfSingleVariable_WithoutDestructuring_EmitsSimpleVariable()
    {
        // Arrange
        var stmt = new TsForOfStatement(
            Variable: "item",
            Expression: new TsUnknownExpression("items"),
            Statements: new TsStatement[] { });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as ForEachStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Must contain the simple variable
        Assert.Contains("item", resultText);

        // Must NOT contain parentheses or brackets around the variable
        Assert.DoesNotContain("(item)", resultText);
        Assert.DoesNotContain("[item]", resultText);
    }

        /// <summary>
        /// Tests for-of with multiple destructured variables.
        /// 
        /// TypeScript: for (const [a, b, c] of items)
        /// C#: foreach (var (a, b, c) in items)
        /// </summary>
        [Fact]
        public void ForOfDestructuring_WithMultipleElements_EmitsCorrectParentheses()
        {
            // Arrange
            var stmt = new TsForOfStatement(
                Variable: "[a, b, c]",
                Expression: new TsUnknownExpression("items"),
                Statements: new TsStatement[] { });

            // Act
            var result = CreateEmitter().EmitStatement(stmt) as ForEachStatementSyntax;

            // Assert
            Assert.NotNull(result);
            var resultText = result.ToString();

            // Must contain the parenthesized tuple deconstruction
            Assert.Contains("(a, b, c)", resultText);

            // Must NOT contain the invalid square bracket pattern
            Assert.DoesNotContain("[a, b, c]", resultText);
        }

        /// <summary>
        /// Tests for-of with object destructuring pattern translates curly braces to parentheses.
        /// 
        /// TypeScript: for (const { name, node } of identifiers)
        /// C#: foreach (var (name, node) in identifiers)
        /// </summary>
        [Fact]
        public void ForOfDestructuring_WithObjectPattern_EmitsParenthesesNotBraces()
        {
            // Arrange
            var stmt = new TsForOfStatement(
                Variable: "{ name, node }",
                Expression: new TsUnknownExpression("identifiers"),
                Statements: new TsStatement[] { });

            // Act
            var result = CreateEmitter().EmitStatement(stmt) as ForEachStatementSyntax;

            // Assert
            Assert.NotNull(result);
            var resultText = result.ToString();

            // Must contain the parenthesized tuple deconstruction with extracted names
            Assert.Contains("(", resultText);
            Assert.Contains("name", resultText);
            Assert.Contains("node", resultText);

            // Must NOT contain the destructuring curly braces in the loop variable pattern
            // (the block body will have {} but not the variable pattern)
            Assert.DoesNotContain("{ name", resultText);
        }
    }
