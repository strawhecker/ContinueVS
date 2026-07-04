using Xunit;
using ContinueTranslator.Core.Emission;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContinueTranslator.Tests.Emission;

/// <summary>
/// Diagnostic test to isolate the exact regex in FixAnonymousObjectFormatting
/// that corrupts ternary colons while fixing object literals.
///
/// This test captures:
/// 1. Raw Roslyn-emitted text (after NormalizeWhitespace but before FixAnonymousObjectFormatting)
/// 2. Post-processed text (after FixAnonymousObjectFormatting)
/// 3. Individual regex step results to identify which one damages the ternary
/// </summary>
public class ColonHandlingDiagnosticTests
{
    /// <summary>
    /// Tests that NameEquals in Roslyn produces '=' not ':' at the syntax tree level.
    /// </summary>
    [Fact]
    public void RawRoslyn_AnonymousObject_NameEqualsProducesEquals()
    {
        // Arrange: Create a compilation unit with an anonymous object
        var objExpr = AnonymousObjectCreationExpression(
            Token(SyntaxKind.NewKeyword),
            Token(SyntaxKind.OpenBraceToken),
            SeparatedList(new[]
            {
                AnonymousObjectMemberDeclarator(
                    NameEquals(IdentifierName("x")),
                    LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1))),
            }),
            Token(SyntaxKind.CloseBraceToken));

        var returnStmt = ReturnStatement(objExpr);
        var methodDecl = MethodDeclaration(ParseTypeName("object"), "TestMethod")
            .WithBody(Block(returnStmt));

        var nsDecl = FileScopedNamespaceDeclaration(ParseName("Test"))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(methodDecl));

        var cu = CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(nsDecl));

        // Act: Convert to text
        var result = cu.ToFullString();

        // Assert: Check that the NameEquals syntax uses = (Roslyn emits NameEquals with =, not :)
        // Even without whitespace, the result should have the structure "x=1" (no space before equals due to minification)
        // The key is that NameEquals ALWAYS uses = in Roslyn, never :
        Assert.Matches(@"x\s*=\s*1", result);
        Assert.DoesNotMatch(@"x\s*:\s*1", result);
    }

    /// <summary>
    /// Tests that ConditionalExpression in Roslyn produces '?' and ':' at the syntax tree level.
    /// </summary>
    [Fact]
    public void RawRoslyn_ConditionalExpression_ProducesQuestionAndColon()
    {
        // Arrange: Create a compilation unit with a ternary expression
        var condExpr = ConditionalExpression(
            BinaryExpression(SyntaxKind.EqualsExpression,
                IdentifierName("x"),
                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(5))),
            IdentifierName("a"),
            IdentifierName("b"));

        var varDecl = VariableDeclaration(ParseTypeName("var"))
            .WithVariables(SingletonSeparatedList(
                VariableDeclarator(Identifier("result"))
                    .WithInitializer(EqualsValueClause(condExpr))));

        var methodDecl = MethodDeclaration(ParseTypeName("void"), "TestMethod")
            .WithBody(Block(LocalDeclarationStatement(varDecl)));

        var nsDecl = FileScopedNamespaceDeclaration(ParseName("Test"))
            .WithMembers(SingletonList<MemberDeclarationSyntax>(methodDecl));

        var cu = CompilationUnit()
            .WithMembers(SingletonList<MemberDeclarationSyntax>(nsDecl));

        // Act: Convert to text
        var result = cu.ToFullString();

        // Assert: Should have '?' and ':'
        Assert.Contains("?", result);
        Assert.Contains(":", result);
        var qIdx = result.IndexOf('?');
        var cIdx = result.LastIndexOf(':');
        Assert.True(cIdx > qIdx, $"Colon should follow question mark in: {result}");
    }

    /// <summary>
    /// Tests each regex in FixAnonymousObjectFormatting independently
    /// to identify which one damages the ternary colon.
    /// </summary>
    public class RegexDiagnosticsTests
    {
        private const string TestCode = @"var obj = new
{
    line : 0L,
};
var result = flag ? trueVal : falseVal;";

        [Fact]
        public void Regex_SpacesBeforeComma_DoesNotAffectTernary()
        {
            // Act: Apply the first regex: \s+, → ,
            string result = System.Text.RegularExpressions.Regex.Replace(
                TestCode, @"\s+,", ",");

            // Assert
            Assert.Contains("? trueVal :", result); // Ternary should be unaffected
        }

        [Fact]
        public void Regex_SpacesBeforeBrace_MayDamageWhitespace()
        {
            // Act: Apply the second regex: \s+} → }
            string result = System.Text.RegularExpressions.Regex.Replace(
                TestCode, @"\s+}", "}");

            // Assert
            Assert.Contains("? trueVal :", result); // Should still be OK
        }

        [Fact]
        public void Regex_MultipleBlankLines_DoesNotAffectColons()
        {
            // Act: Apply the third regex: collapse multiple blank lines
            string result = System.Text.RegularExpressions.Regex.Replace(
                TestCode, @"\n\s*\n\s*\n", "\n\n");

            // Assert
            Assert.Contains("? trueVal :", result); // Should be unaffected
        }

        [Fact]
        public void Regex_InjectNewKeyword_MayAffectNearbyTokens()
        {
            // The "inject new keyword" regex uses negative lookbehind for "new"
            // and positive lookahead for "{". Verify it doesn't accidentally
            // match or replace nearby tokens.

            string result = System.Text.RegularExpressions.Regex.Replace(
                TestCode,
                @"(?<!new\s)=\s*\{",
                "= new {",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            // Assert
            Assert.Contains("? trueVal :", result); // Ternary unaffected
            Assert.Contains("= new {", result);    // Nested object gets new
        }

        [Fact]
        public void Regex_CollapseRedundantLineBreaks_SuspectOfDamage()
        {
            // This regex: (\s*=\s*)\n\s+([^\n{}\[\]]+,) → $1 $2
            // Collapses line breaks around single-line property values.
            // It operates on lines with '=' followed by non-brace content and commas.
            // Need to check if it interferes with ternary ':' tokens.

            string result = System.Text.RegularExpressions.Regex.Replace(
                TestCode,
                @"(\s*=\s*)\n\s+([^\n{}\[\]]+,)",
                "$1 $2");

            // Assert
            Assert.Contains("? trueVal :", result); // Should be unaffected
            // But if it IS damaged, we've found the culprit
        }
    }
}
