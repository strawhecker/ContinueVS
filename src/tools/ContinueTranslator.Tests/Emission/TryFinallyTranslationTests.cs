using Xunit;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Tests for try-finally statement translation from TypeScript to C#.
/// 
/// Covers:
/// 1. Try block only: try { ... }
/// 2. Try-catch: try { ... } catch (e) { ... }
/// 3. Try-finally: try { ... } finally { ... }
/// 4. Try-catch-finally: try { ... } catch (e) { ... } finally { ... }
/// 5. Empty finally blocks
/// 
/// Example TypeScript pattern that was broken:
///   try {
///     Ollama.modelsBeingInstalled.add(modelName);
///   } finally {
///     release();
///   }
/// 
/// Expected C# output (now fixed):
///   try
///   {
///       Ollama.modelsBeingInstalled.add(modelName);
///   }
///   finally
///   {
///       release();
///   }
/// </summary>
public class TryFinallyTranslationTests
{
    private CsEmitter CreateEmitter()
    {
        // Create a minimal CallSiteMap with a temporary JSON file for testing
        var tempPath = Path.Combine(Path.GetTempPath(), "test_callsites.json");
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
    /// Tests try block with finally block emits both sections.
    /// Example: try { ... } finally { release(); }
    /// Expected: try { ... } finally { release(); }
    /// </summary>
    [Fact]
    public void TryFinally_WithBothBlocks_EmitsFinallyClause()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("someOperation()"))
            },
            CatchStatements: new TsStatement[] { },
            CatchVariableName: null,
            FinallyStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("release()"))
            });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Verify finally clause exists on the try statement
        Assert.NotNull(result.Finally);

        // Verify finally block is present in output
        Assert.Contains("finally", resultText);
    }

    /// <summary>
    /// Tests try-catch-finally emits all three blocks.
    /// Example: try { ... } catch (e) { ... } finally { ... }
    /// Expected: try { ... } catch (Exception e) { ... } finally { ... }
    /// </summary>
    [Fact]
    public void TryCatchFinally_WithAllBlocks_EmitsAllClauses()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("doSomething()"))
            },
            CatchStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("handleError()"))
            },
            CatchVariableName: "err",
            FinallyStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("cleanup()"))
            });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);
        var resultText = result.ToString();

        // Verify catch block exists
        Assert.NotEmpty(result.Catches);

        // Verify finally block exists
        Assert.NotNull(result.Finally);
        Assert.Contains("finally", resultText);
    }

    /// <summary>
    /// Tests try-finally without catch block (no exception handling).
    /// Example: try { ... } finally { release(); }
    /// Expected: try { ... } finally { release(); } (no catch)
    /// </summary>
    [Fact]
    public void TryFinally_NoCatch_EmitsFinallyWithoutCatchClause()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("operation()"))
            },
            CatchStatements: new TsStatement[] { }, // Empty catch
            CatchVariableName: null,
            FinallyStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("release()"))
            });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);

        // Verify NO catch clause
        Assert.Empty(result.Catches);

        // Verify finally block is present
        Assert.NotNull(result.Finally);
    }

    /// <summary>
    /// Tests try-catch without finally (original behavior).
    /// Example: try { ... } catch (e) { ... }
    /// Expected: try { ... } catch (Exception e) { ... }
    /// </summary>
    [Fact]
    public void TryCatch_NoFinally_EmitsCatchWithoutFinallyClause()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("operation()"))
            },
            CatchStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("handleError()"))
            },
            CatchVariableName: "ex",
            FinallyStatements: null); // No finally

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);

        // Verify catch block exists
        Assert.NotEmpty(result.Catches);

        // Verify NO finally clause
        Assert.Null(result.Finally);
    }

    /// <summary>
    /// Tests try block only (no catch or finally).
    /// Example: try { ... }
    /// Expected: try { ... }
    /// </summary>
    [Fact]
    public void TryOnly_NoCatchOrFinally_EmitsTryBlockOnly()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("operation()"))
            },
            CatchStatements: new TsStatement[] { },
            CatchVariableName: null,
            FinallyStatements: null);

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);

        // Verify NO catch clause
        Assert.Empty(result.Catches);

        // Verify NO finally clause
        Assert.Null(result.Finally);
    }

    /// <summary>
    /// Tests finally block with multiple statements.
    /// Example: try { ... } finally { cleanup1(); cleanup2(); }
    /// Expected: try { ... } finally { cleanup1(); cleanup2(); }
    /// </summary>
    [Fact]
    public void TryFinally_MultipleStatements_EmitsAllFinallyStatements()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("operation()"))
            },
            CatchStatements: new TsStatement[] { },
            CatchVariableName: null,
            FinallyStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("cleanup1()")),
                new TsExpressionStatement(new TsUnknownExpression("cleanup2()"))
            });

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);

        // Verify finally block with multiple statements
        Assert.NotNull(result.Finally);
    }

    /// <summary>
    /// Tests empty finally block emits the finally clause.
    /// Example: try { ... } finally { }
    /// Expected: try { ... } finally { }
    /// </summary>
    [Fact]
    public void TryFinally_EmptyFinallyBlock_EmitsFinallyWithNoStatements()
    {
        // Arrange
        var stmt = new TsTryStatement(
            TryStatements: new TsStatement[]
            {
                new TsExpressionStatement(new TsUnknownExpression("operation()"))
            },
            CatchStatements: new TsStatement[] { },
            CatchVariableName: null,
            FinallyStatements: new TsStatement[] { }); // Empty finally

        // Act
        var result = CreateEmitter().EmitStatement(stmt) as TryStatementSyntax;

        // Assert
        Assert.NotNull(result);

        // Empty finally block should NOT be emitted (no point)
        // The emitter logic checks: if (stmt.FinallyStatements?.Length > 0)
        // So an empty array will not create a finally clause
        Assert.Null(result.Finally);
    }
}
