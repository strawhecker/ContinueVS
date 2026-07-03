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
            TsFunctionDeclarationStatement funcDecl => EmitFunctionDeclaration(funcDecl, filePath),
            TsUnknownStatement unknown     => EmitUnknownStatement(unknown),
            _                              => ParseStatement($"// TODO: untranslatable — {filePath}\n"),
        };
    }

    /// <summary>
    /// Emits a sequence of statements and automatically injects any pending local generator methods
    /// at the beginning of the block. This ensures local functions appear before other statements
    /// in accordance with C# scoping rules.
    /// </summary>
    internal IReadOnlyList<StatementSyntax> EmitStatementBlock(TsStatement[] statements, string filePath = "")
    {
        var result = new List<StatementSyntax>();

        // First, emit all statements (which may populate _pendingLocalGenerators)
        var emittedStatements = statements.Select(s => EmitStatement(s, filePath)).ToList();

        // Inject any pending local generator methods at the start of the block
        if (_pendingLocalGenerators.Count > 0)
        {
            result.AddRange(_pendingLocalGenerators.Values);
            _pendingLocalGenerators.Clear();
        }

        // Add the regular statements
        result.AddRange(emittedStatements);

        return result.AsReadOnly();
    }

    private StatementSyntax EmitUnknownStatement(TsUnknownStatement unknown)
    {
        // Try to parse as a simple statement first
        return ParseStatement(unknown.Text);
    }

    private StatementSyntax EmitReturn(TsReturnStatement stmt) =>
        stmt.Expression is not null
            ? ReturnStatement(EmitExpression(stmt.Expression))
            : ReturnStatement();

    private StatementSyntax EmitExpressionStatement(TsExpressionStatement stmt)
    {
        if (stmt.Expression is TsYieldExpression yieldExpr)
        {
            return EmitYieldStatement(yieldExpr);
        }

        return stmt.Expression is not null
            ? ExpressionStatement(EmitExpression(stmt.Expression))
            : ExpressionStatement(EmitExpression(new TsUnknownExpression(string.Empty)));
    }

    private StatementSyntax EmitYieldStatement(TsYieldExpression yieldExpr)
    {
        // Convert yield expression to a yield return statement.
        // JavaScript: yield expr → C#: yield return expr;
        // JavaScript: yield* expr → C#: yield return expr; (C# does not have yield* like Python/JS)

        if (yieldExpr.Expression is not null)
        {
            return YieldStatement(
                SyntaxKind.YieldReturnStatement,
                EmitExpression(yieldExpr.Expression));
        }

        // Bare yield with no expression (rare in TypeScript)
        return ParseStatement("yield break;\n");
    }

    private StatementSyntax EmitThrow(TsThrowStatement stmt) =>
        stmt.Expression is not null
            ? ThrowStatement(EmitExpression(stmt.Expression))
            : ThrowStatement();

    private StatementSyntax EmitVar(TsVarStatement stmt)
    {
        // Handle destructuring patterns (array or object)
        if (stmt.Names is not null && stmt.Names.Length > 0)
        {
            if (stmt.Initializer is null)
            {
                // No initializer, can't emit proper deconstruction
                // Fallback to a comment placeholder
                return ParseStatement($"// TODO: destructuring without initializer for {string.Join(", ", stmt.Names)}\n");
            }

            // Distinguish between array and object destructuring
            if (stmt.PatternKind == "Array")
            {
                // Array destructuring: const [a, b] = expr → var (a, b) = expr;
                string varNames = string.Join(", ", stmt.Names);
                string initText = EmitExpression(stmt.Initializer).NormalizeWhitespace().ToFullString();
                return ParseStatement($"var ({varNames}) = {initText};\n");
            }
            else if (stmt.PatternKind == "Object")
            {
                // Object destructuring: const { config } = expr
                if (stmt.Names.Length == 1)
                {
                    // Single property extraction: const { config } = expr → var config = (expr).config;
                    string propName = stmt.Names[0];
                    string initText = EmitExpression(stmt.Initializer).NormalizeWhitespace().ToFullString();

                    // If the initializer is an await expression, we need to wrap it in parens for property access
                    if (initText.StartsWith("await "))
                    {
                        initText = $"({initText})";
                    }

                    // Convert property name from camelCase to PascalCase for C# convention
                    string csPropertyName = ToPascalCase(propName);
                    return ParseStatement($"var {propName} = {initText}.{csPropertyName};\n");
                }
                else
                {
                    // Multiple property extraction: fallback to TODO comment
                    // This requires complex analysis to determine the source type's properties
                    string propNames = string.Join(", ", stmt.Names);
                    return ParseStatement($"// TODO: Object destructuring {{ {propNames} }} = expr — convert manually to property extraction\n");
                }
            }
            else
            {
                // PatternKind is null/unknown, fallback to TODO comment
                return ParseStatement($"// TODO: Unknown destructuring pattern for {string.Join(", ", stmt.Names)}\n");
            }
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

        // If IsAwait is true, emit as 'await foreach' instead of 'foreach'
        if (stmt.IsAwait)
        {
            return ForEachStatement(typeSyntax, identifier, iterable, body)
                .WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword));
        }

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

        // Emit finally block if present
        FinallyClauseSyntax? finallyClause = null;
        if (stmt.FinallyStatements?.Length > 0)
        {
            BlockSyntax finallyBlock = Block(
                List(stmt.FinallyStatements.Select(s => EmitStatement(s, filePath))));
            finallyClause = FinallyClause(finallyBlock);
        }

        return TryStatement(tryBlock, catchClauses, finallyClause);
    }

    private StatementSyntax EmitFunctionDeclaration(TsFunctionDeclarationStatement funcDecl, string filePath)
    {
        // Emit as a C# local function (C# 7.0+)
        // Local functions are statements that declare and define a nested function.

        // Build parameter list
        var parameters = new List<ParameterSyntax>();
        foreach (TsParameter param in funcDecl.Parameters)
        {
            TypeSyntax paramType = ParseTypeSyntax(param.Type.Name);
            ParameterSyntax paramSyntax = Parameter(Identifier(param.Name))
                .WithType(paramType);

            if (param.IsOptional)
            {
                // Add default value for optional parameters
                paramSyntax = paramSyntax.WithDefault(EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression)));
            }

            parameters.Add(paramSyntax);
        }

        // Build return type
        TypeSyntax returnType = ParseTypeSyntax(funcDecl.ReturnType.Name);

        // Build function body
        BlockSyntax body = Block(List(funcDecl.Body.Select(s => EmitStatement(s, filePath))));

        // Create local function declaration
        LocalFunctionStatementSyntax localFunc = LocalFunctionStatement(
            returnType,
            Identifier(funcDecl.Name))
            .WithParameterList(ParameterList(SeparatedList(parameters)))
            .WithBody(body);

        // Add async modifier if needed
        if (funcDecl.IsAsync)
        {
            localFunc = localFunc.AddModifiers(Token(SyntaxKind.AsyncKeyword));
        }

        return localFunc;
    }
}
