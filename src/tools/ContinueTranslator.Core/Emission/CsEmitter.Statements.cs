using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    /// <summary>
    /// Translates a <see cref="TsStatement"/> IR node to a Roslyn <see cref="StatementSyntax"/>.
    /// For untranslatable kinds a <c>// TODO: untranslatable</c> comment statement is returned.
    /// </summary>
    /// <param name="stmt">The IR statement node to translate.</param>
    /// <param name="filePath">The source TypeScript file path, used in fallback comments.</param>
    internal StatementSyntax EmitStatement(TsStatement stmt, string filePath = "")
    {
        return stmt switch
        {
            TsReturnStatement ret          => EmitReturn(ret),
            TsExpressionStatement exprStmt => EmitExpressionStatement(exprStmt),
            TsThrowStatement thr           => EmitThrow(thr),
            TsVarStatement varStmt         => EmitVar(varStmt),
            TsIfStatement ifStmt           => EmitIf(ifStmt, filePath),
            TsWhileStatement whileStmt     => EmitWhile(whileStmt, filePath),
            TsForStatement forStmt         => EmitFor(forStmt, filePath),
            TsForOfStatement forOfStmt     => EmitForOf(forOfStmt, filePath),
            TsTryStatement tryStmt         => EmitTry(tryStmt, filePath),
            _                              => ParseStatement($"// TODO: untranslatable — {filePath}\n"),
        };
    }

    private StatementSyntax EmitReturn(TsReturnStatement stmt) =>
        stmt.Expression is not null
            ? ReturnStatement(EmitExpression(stmt.Expression))
            : ReturnStatement();

    private StatementSyntax EmitExpressionStatement(TsExpressionStatement stmt) =>
        stmt.Expression is not null
            ? ExpressionStatement(EmitExpression(stmt.Expression))
            : ExpressionStatement(EmitExpression(new TsUnknownExpression(string.Empty)));

    private StatementSyntax EmitThrow(TsThrowStatement stmt) =>
        stmt.Expression is not null
            ? ThrowStatement(EmitExpression(stmt.Expression))
            : ThrowStatement();

    private StatementSyntax EmitVar(TsVarStatement stmt)
    {
        // Handle destructuring patterns (array or object)
        if (stmt.Names is not null && stmt.Names.Length > 0)
        {
            // Destructuring: const [a, b] = expr → var (a, b) = expr;
            // or: const {x, y} = expr → var (x, y) = expr;

            if (stmt.Initializer is null)
            {
                // No initializer, can't emit proper deconstruction
                // Fallback to a comment placeholder
                return ParseStatement($"// TODO: destructuring without initializer for {string.Join(", ", stmt.Names)}\n");
            }

            // Generate the tuple deconstruction pattern text
            string varNames = string.Join(", ", stmt.Names);
            string initText = EmitExpression(stmt.Initializer).NormalizeWhitespace().ToFullString();

            // Emit: var (a, b, c) = expr;
            return ParseStatement($"var ({varNames}) = {initText};\n");
        }

        // Handle regular single variable declaration
        // TsVarStatement carries no declared type; always use var.
        TypeSyntax typeSyntax = IdentifierName("var");

        VariableDeclaratorSyntax declarator = VariableDeclarator(Identifier(stmt.Name ?? "_"));
        if (stmt.Initializer is not null)
            declarator = declarator.WithInitializer(
                EqualsValueClause(EmitExpression(stmt.Initializer)));

        return LocalDeclarationStatement(
            VariableDeclaration(typeSyntax)
                .WithVariables(SingletonSeparatedList(declarator)));
    }

    private StatementSyntax EmitIf(TsIfStatement stmt, string filePath)
    {
        ExpressionSyntax condition = stmt.Condition is not null
            ? EmitExpression(stmt.Condition)
            : LiteralExpression(SyntaxKind.TrueLiteralExpression);

        BlockSyntax thenBlock = Block(
            List(stmt.ThenStatements.Select(s => EmitStatement(s, filePath))));

        ElseClauseSyntax? elseClause = null;
        if (stmt.ElseStatements.Length > 0)
        {
            BlockSyntax elseBlock = Block(
                List(stmt.ElseStatements.Select(s => EmitStatement(s, filePath))));
            elseClause = ElseClause(elseBlock);
        }

        return IfStatement(condition, thenBlock, elseClause);
    }

    private StatementSyntax EmitWhile(TsWhileStatement stmt, string filePath)
    {
        ExpressionSyntax condition = stmt.Condition is not null
            ? EmitExpression(stmt.Condition)
            : LiteralExpression(SyntaxKind.TrueLiteralExpression);

        BlockSyntax body = Block(
            List(stmt.Statements.Select(s => EmitStatement(s, filePath))));

        return WhileStatement(condition, body);
    }

    private StatementSyntax EmitFor(TsForStatement stmt, string filePath)
    {
        // Initializer is a raw string fragment (e.g. "let i = 0"), parse it directly.
        VariableDeclarationSyntax? initDecl = null;
        if (!string.IsNullOrWhiteSpace(stmt.Initializer))
        {
            // Parse the initializer text as a local-declaration statement to extract
            // the VariableDeclaration (the for-statement API expects the declaration node).
            string initText = stmt.Initializer.TrimEnd(';');
            // Strip leading let/const/var keyword so we can parse as a declaration.
            foreach (string kw in new[] { "let ", "const ", "var " })
            {
                if (initText.StartsWith(kw, StringComparison.Ordinal))
                {
                    initText = initText[kw.Length..];
                    break;
                }
            }

            // Try to parse as a C# variable declaration (e.g. "i = 0" → "var i = 0").
            LocalDeclarationStatementSyntax? parsed =
                ParseStatement("var " + initText + ";") as LocalDeclarationStatementSyntax;
            initDecl = parsed?.Declaration;
        }

        ExpressionSyntax? condition = stmt.Condition is not null
            ? EmitExpression(stmt.Condition)
            : null;

        SeparatedSyntaxList<ExpressionSyntax> incrementors = stmt.Incrementor is not null
            ? SingletonSeparatedList(EmitExpression(stmt.Incrementor))
            : default;

        BlockSyntax body = Block(
            List(stmt.Statements.Select(s => EmitStatement(s, filePath))));

        ForStatementSyntax forStmt = ForStatement(body)
            .WithCondition(condition)
            .WithIncrementors(incrementors);

        if (initDecl is not null)
            forStmt = forStmt.WithDeclaration(initDecl);

        return forStmt;
    }

    private StatementSyntax EmitForOf(TsForOfStatement stmt, string filePath)
    {
        // TsForOfStatement carries no declared type for the loop variable; always use var.
        TypeSyntax typeSyntax = IdentifierName("var");

        SyntaxToken identifier = Identifier(stmt.Variable ?? "_");

        ExpressionSyntax iterable = stmt.Expression is not null
            ? EmitExpression(stmt.Expression)
            : LiteralExpression(SyntaxKind.NullLiteralExpression);

        BlockSyntax body = Block(
            List(stmt.Statements.Select(s => EmitStatement(s, filePath))));

        return ForEachStatement(typeSyntax, identifier, iterable, body);
    }

    private StatementSyntax EmitTry(TsTryStatement stmt, string filePath)
    {
        BlockSyntax tryBlock = Block(
            List(stmt.TryStatements.Select(s => EmitStatement(s, filePath))));

        // Use the catch variable name from the TypeScript source.
        // The parser always extracts this from the catch clause parameter.
        string catchVarName = stmt.CatchVariableName ?? "e";

        SyntaxList<CatchClauseSyntax> catchClauses = default;
        if (stmt.CatchStatements.Length > 0)
        {
            BlockSyntax catchBlock = Block(
                List(stmt.CatchStatements.Select(s => EmitStatement(s, filePath))));

            CatchDeclarationSyntax catchDecl = CatchDeclaration(
                ParseTypeSyntax("Exception"),
                Identifier(catchVarName));

            catchClauses = SingletonList(CatchClause(catchDecl, null, catchBlock));
        }

        return TryStatement(tryBlock, catchClauses, null);
    }
}
