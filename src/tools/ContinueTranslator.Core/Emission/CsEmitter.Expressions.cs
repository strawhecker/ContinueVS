using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    // Set to true the first time EmitSpreadMergeCall is used; CollectResults then emits SpreadMerge.cs.
    private bool _needsSpreadMerge;

    // Set to true the first time the `in` operator is translated; CollectResults then emits HasProperty.cs.
    private bool _needsHasPropertyHelper;

    // Track local generator methods extracted from arrow expressions
    private int _localGeneratorCounter;
    private readonly Dictionary<string, LocalFunctionStatementSyntax> _pendingLocalGenerators = new();

    /// <summary>
    /// Translates a <see cref="TsExpression"/> IR node to a Roslyn <see cref="ExpressionSyntax"/>.
    /// For untranslatable kinds a string-literal comment placeholder is returned to keep the
    /// generated file compilable.
    /// </summary>
    internal ExpressionSyntax EmitExpression(TsExpression expr) =>
        expr switch
        {
            TsIdentifierExpression id         => EmitIdentifier(id),
            TsLiteralExpression lit           => EmitLiteral(lit),
            TsMemberExpression mem            => EmitMemberExpression(mem),
            TsAwaitExpression aw              => EmitAwaitExpression(aw),
            TsBinaryExpression bin            => EmitBinaryExpression(bin),
            TsUnaryExpression unary           => EmitUnaryExpression(unary),
            TsTypeOfExpression typeOf         => EmitTypeOf(typeOf),
            TsCallExpression call             => EmitCallExpression(call),
            TsNewExpression newExpr           => EmitNewExpression(newExpr),
            TsConditionalExpression cond      => EmitConditional(cond),
            TsArrowExpression arrow           => EmitArrow(arrow),
            TsElementAccessExpression elemAccess => EmitElementAccess(elemAccess),
            TsObjectLiteralExpression objLit  => EmitObjectLiteral(objLit),
            TsArrayLiteralExpression arrLit   => EmitArrayLiteral(arrLit),
            TsTemplateExpression tmpl         => EmitTemplateExpression(tmpl),
            TsAsExpression asExpr             => EmitAsExpression(asExpr),
            TsRegexExpression regex           => EmitRegexExpression(regex),
            TsSpreadElement spread            => EmitSpreadElement(spread),
            TsYieldExpression yield           => EmitYield(yield),
            TsUnknownExpression unknown       => EmitUnknown(unknown),
            _                                 => Placeholder("/* untranslatable expression */"),
        };

    // -------------------------------------------------------------------------
    // Identifier
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitIdentifier(TsIdentifierExpression id)
    {
        // TypeScript 'undefined' should map to C# 'null' or 'default' depending on context.
        // For simplicity, emit 'null' as it works for both reference and nullable value types.
        if (id.Name == "undefined")
            return LiteralExpression(SyntaxKind.NullLiteralExpression);

        return IdentifierName(id.Name);
    }

    // -------------------------------------------------------------------------
    // Literal
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitLiteral(TsLiteralExpression lit)
    {
        string v = lit.Value;

        if (v == "null")
            return LiteralExpression(SyntaxKind.NullLiteralExpression);

        if (v == "undefined")
            return LiteralExpression(SyntaxKind.NullLiteralExpression);

        if (v == "true")
            return LiteralExpression(SyntaxKind.TrueLiteralExpression);

        if (v == "false")
            return LiteralExpression(SyntaxKind.FalseLiteralExpression);

        // Quoted string: strip surrounding quotes and unescape.
        if ((v.StartsWith('"') && v.EndsWith('"')) ||
            (v.StartsWith('\'') && v.EndsWith('\'')) ||
            (v.StartsWith('`') && v.EndsWith('`')))
        {
            string inner = v[1..^1];
            return LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal(inner));
        }

        // Numeric literal.
        if (long.TryParse(v, out long intVal))
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intVal));

        if (double.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(dblVal));

        // Fallback: treat as identifier (e.g. enum member reference).
        return IdentifierName(v);
    }

    // -------------------------------------------------------------------------
    // Regex literal
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitRegexExpression(TsRegexExpression regex)
    {
        // Convert TypeScript regex literal to C# Regex constructor call.
        // TypeScript: /pattern/flags → C#: new Regex("pattern", RegexOptions.Flags)

        // Build regex options from flags
        var regexOptions = new List<string>();
        foreach (char flag in regex.Flags)
        {
            switch (flag)
            {
                case 'i':
                    regexOptions.Add("RegexOptions.IgnoreCase");
                    break;
                case 'm':
                    regexOptions.Add("RegexOptions.Multiline");
                    break;
                case 's':
                    regexOptions.Add("RegexOptions.Singleline");
                    break;
                case 'x':
                    regexOptions.Add("RegexOptions.IgnorePatternWhitespace");
                    break;
                // 'g' (global) is handled by the replace method, not the regex itself
                // 'u' (unicode) and 'y' (sticky) don't have direct C# equivalents
            }
        }

        var args = new List<ArgumentSyntax>
        {
            Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(regex.Pattern)))
        };

        // Add regex options if any were specified
        if (regexOptions.Count > 0)
        {
            string optionsExpr = string.Join(" | ", regexOptions);
            args.Add(Argument(ParseExpression(optionsExpr)));
        }

        return ObjectCreationExpression(
            ParseTypeSyntax("System.Text.RegularExpressions.Regex"),
            ArgumentList(SeparatedList(args)),
            initializer: null);
    }

    // -------------------------------------------------------------------------
    // Member
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitMemberExpression(TsMemberExpression mem)
    {
        // TypeScript `this` has no dedicated IR node and arrives as TsUnknownExpression("this").
        // Strip it so `this.foo` emits as just `foo`.
        if (mem.Obj is TsUnknownExpression { Text: "this" })
            return IdentifierName(mem.Property);

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            EmitExpression(mem.Obj),
            IdentifierName(mem.Property));
    }

    // -------------------------------------------------------------------------
    // Element access (arr[i])
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitElementAccess(TsElementAccessExpression elemAccess) =>
        ElementAccessExpression(
            EmitExpression(elemAccess.Obj),
            BracketedArgumentList(
                SingletonSeparatedList(
                    Argument(EmitExpression(elemAccess.Index)))));

    // -------------------------------------------------------------------------
    // Await
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitAwaitExpression(TsAwaitExpression aw) =>
        AwaitExpression(EmitExpression(aw.Expression));

    // -------------------------------------------------------------------------
    // Binary
    // -------------------------------------------------------------------------

    private static readonly Dictionary<string, SyntaxKind> s_binaryOpMap =
        new(StringComparer.Ordinal)
        {
            ["==="] = SyntaxKind.EqualsExpression,
            ["!=="] = SyntaxKind.NotEqualsExpression,
            ["=="]  = SyntaxKind.EqualsExpression,
            ["!="]  = SyntaxKind.NotEqualsExpression,
            ["+"]   = SyntaxKind.AddExpression,
            ["-"]   = SyntaxKind.SubtractExpression,
            ["*"]   = SyntaxKind.MultiplyExpression,
            ["/"]   = SyntaxKind.DivideExpression,
            ["%"]   = SyntaxKind.ModuloExpression,
            ["<"]   = SyntaxKind.LessThanExpression,
            [">"]   = SyntaxKind.GreaterThanExpression,
            ["<="]  = SyntaxKind.LessThanOrEqualExpression,
            [">="]  = SyntaxKind.GreaterThanOrEqualExpression,
            ["&&"]  = SyntaxKind.LogicalAndExpression,
            ["||"]  = SyntaxKind.LogicalOrExpression,
            ["??"]  = SyntaxKind.CoalesceExpression,
            ["|"]   = SyntaxKind.BitwiseOrExpression,
            ["&"]   = SyntaxKind.BitwiseAndExpression,
            ["^"]   = SyntaxKind.ExclusiveOrExpression,
            ["<<"]  = SyntaxKind.LeftShiftExpression,
            [">>"]  = SyntaxKind.RightShiftExpression,
        };

    // ts-morph folds AssignmentExpression into BinaryExpression; Roslyn uses a separate factory.
    private static readonly Dictionary<string, SyntaxKind> s_assignmentOpMap =
        new(StringComparer.Ordinal)
        {
            ["="]   = SyntaxKind.SimpleAssignmentExpression,
            ["+="]  = SyntaxKind.AddAssignmentExpression,
            ["-="]  = SyntaxKind.SubtractAssignmentExpression,
            ["*="]  = SyntaxKind.MultiplyAssignmentExpression,
            ["/="]  = SyntaxKind.DivideAssignmentExpression,
            ["%="]  = SyntaxKind.ModuloAssignmentExpression,
            ["&="]  = SyntaxKind.AndAssignmentExpression,
            ["|="]  = SyntaxKind.OrAssignmentExpression,
            ["^="]  = SyntaxKind.ExclusiveOrAssignmentExpression,
            ["<<="] = SyntaxKind.LeftShiftAssignmentExpression,
            [">>="] = SyntaxKind.RightShiftAssignmentExpression,
            ["??="] = SyntaxKind.CoalesceAssignmentExpression,
        };

    private ExpressionSyntax EmitBinaryExpression(TsBinaryExpression bin)
    {
        if (s_assignmentOpMap.TryGetValue(bin.Op, out SyntaxKind assignKind))
            return AssignmentExpression(assignKind, EmitExpression(bin.Left), EmitExpression(bin.Right));

        // TS `typeof x === "typename"` → C# `x is TypeName`
        if ((bin.Op is "===" or "==") &&
            bin.Left is TsTypeOfExpression typeofExpr &&
            bin.Right is TsLiteralExpression { Value: var typeNameLit })
        {
            string csTypeName = typeNameLit.Trim('"', '\'') switch
            {
                "string"   => "string",
                "number"   => "double",
                "boolean"  => "bool",
                "bigint"   => "System.Numerics.BigInteger",
                "symbol"   => "object",
                "function" => "System.Delegate",
                "object"   => "object",
                var other  => other,
            };
            return IsPatternExpression(
                EmitExpression(typeofExpr.Expression),
                TypePattern(ParseTypeSyntax(csTypeName)));
        }

        // TS `typeof x !== "typename"` → C# `x is not TypeName`
        if ((bin.Op is "!==" or "!=") &&
            bin.Left is TsTypeOfExpression typeofExprNe &&
            bin.Right is TsLiteralExpression { Value: var typeNameLitNe })
        {
            string csTypeName = typeNameLitNe.Trim('"', '\'') switch
            {
                "string"   => "string",
                "number"   => "double",
                "boolean"  => "bool",
                "bigint"   => "System.Numerics.BigInteger",
                "symbol"   => "object",
                "function" => "System.Delegate",
                "object"   => "object",
                var other  => other,
            };
            return IsPatternExpression(
                EmitExpression(typeofExprNe.Expression),
                UnaryPattern(Token(SyntaxKind.NotKeyword),
                    TypePattern(ParseTypeSyntax(csTypeName))));
        }

        // TS `instanceof` → C# `is T` pattern
        if (bin.Op == "instanceof")
        {
            string typeName = bin.Right is TsIdentifierExpression id ? id.Name : "object";
            return IsPatternExpression(
                EmitExpression(bin.Left),
                TypePattern(ParseTypeSyntax(typeName)));
        }

        // TS `"prop" in obj` → C# `HasProperty(obj, "prop")`
        // TS `identifier in obj` → C# `HasProperty(obj, identifier)`
        // This checks for property existence on dynamic objects (JToken, etc.)
        if (bin.Op == "in")
        {
            _needsHasPropertyHelper = true;

            if (bin.Left is TsLiteralExpression { Value: var propName })
            {
                // Extract the property name from the quoted literal
                string cleanPropName = propName.Trim('"', '\'');
                return InvocationExpression(
                    IdentifierName("HasProperty"),
                    ArgumentList(SeparatedList(new[]
                    {
                        Argument(EmitExpression(bin.Right)),
                        Argument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(cleanPropName)))
                    })));
            }
            else
            {
                // For non-literal left operands (identifiers, expressions, etc.), pass the expression as-is
                return InvocationExpression(
                    IdentifierName("HasProperty"),
                    ArgumentList(SeparatedList(new[]
                    {
                        Argument(EmitExpression(bin.Right)),
                        Argument(EmitExpression(bin.Left))
                    })));
            }
        }

        // TS `**` (exponentiation) → C# `Math.Pow(base, exponent)`
        if (bin.Op == "**")
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Math"),
                    IdentifierName("Pow")),
                ArgumentList(SeparatedList(new[]
                {
                    Argument(EmitExpression(bin.Left)),
                    Argument(EmitExpression(bin.Right))
                })));
        }

        if (!s_binaryOpMap.TryGetValue(bin.Op, out SyntaxKind opKind))
            return Placeholder("/* untranslatable binary op */");

        return BinaryExpression(opKind, EmitExpression(bin.Left), EmitExpression(bin.Right));
    }

    // -------------------------------------------------------------------------
    // TypeOf
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fallback for standalone <c>typeof x</c> not consumed by the binary-operator pattern.
    /// Emits <c>x.GetType().Name</c> as a best-effort approximation.
    /// </summary>
    private ExpressionSyntax EmitTypeOf(TsTypeOfExpression typeOf) =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    EmitExpression(typeOf.Expression),
                    IdentifierName("GetType"))),
            IdentifierName("Name"));

    // -------------------------------------------------------------------------
    // As (type cast)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Translates TypeScript `as` type assertions to C# cast syntax.
    /// <c>expr as TypeName</c> → <c>(TypeName)expr</c>
    /// </summary>
    private ExpressionSyntax EmitAsExpression(TsAsExpression asExpr)
    {
        ExpressionSyntax inner = EmitExpression(asExpr.Expression);
        TypeSyntax targetType = ParseTypeSyntax(asExpr.Type);
        return CastExpression(targetType, inner);
    }

    // -------------------------------------------------------------------------
    // Unary
    // -------------------------------------------------------------------------

    private static readonly Dictionary<string, SyntaxKind> s_prefixUnaryOpMap =
        new(StringComparer.Ordinal)
        {
            ["!"]  = SyntaxKind.LogicalNotExpression,
            ["-"]  = SyntaxKind.UnaryMinusExpression,
            ["+"]  = SyntaxKind.UnaryPlusExpression,
            ["~"]  = SyntaxKind.BitwiseNotExpression,
            ["++"] = SyntaxKind.PreIncrementExpression,
            ["--"] = SyntaxKind.PreDecrementExpression,
        };

    private ExpressionSyntax EmitUnaryExpression(TsUnaryExpression unary)
    {
        // Handle postfix operators by extracting the base operator (++ or --)
        if (unary.Op.StartsWith("postfix:", StringComparison.Ordinal))
        {
            string baseOp = unary.Op["postfix:".Length..];
            ExpressionSyntax operandExpr = EmitExpression(unary.Operand);

            return baseOp switch
            {
                "++" => PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, operandExpr),
                "--" => PostfixUnaryExpression(SyntaxKind.PostDecrementExpression, operandExpr),
                "!" => PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, operandExpr),
                _ => Placeholder($"/* untranslatable postfix op: {baseOp} */"),
            };
        }

        // Check for prefix operators
        if (!s_prefixUnaryOpMap.TryGetValue(unary.Op, out SyntaxKind prefixOpKind))
            return Placeholder($"/* untranslatable unary op: {unary.Op} */");

        return PrefixUnaryExpression(prefixOpKind, EmitExpression(unary.Operand));
    }

    // -------------------------------------------------------------------------
    // Call
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitCallExpression(TsCallExpression call)
    {
        string calleeChain = BuildCalleeChain(call.Callee);

        // Check if any arguments are spread elements
        bool hasSpread = call.Args.Any(arg => arg is TsSpreadElement);

        ArgumentListSyntax argList = ArgumentList(
            SeparatedList(call.Args.Select(a => Argument(EmitExpression(a)))));

        if (!string.IsNullOrEmpty(calleeChain) &&
            _callSiteMap.TryResolve(calleeChain, out string dotNetCall))
        {
            // Special handling for array.push(...items) → array.AddRange(items)
            if (hasSpread && calleeChain.EndsWith(".push", StringComparison.Ordinal) && 
                call.Callee is TsMemberExpression memberExpr)
            {
                // Extract the spread argument
                TsSpreadElement? spreadArg = call.Args.OfType<TsSpreadElement>().FirstOrDefault();
                if (spreadArg != null)
                {
                    ExpressionSyntax obj = EmitExpression(memberExpr.Obj);
                    ExpressionSyntax spreadExpr = EmitExpression(spreadArg.Expression);

                    // Convert to AddRange instead of push with spread
                    return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            obj,
                            IdentifierName("AddRange")),
                        ArgumentList(SingletonSeparatedList(Argument(spreadExpr))));
                }
            }

            // Build: <dotNetCall>(<translated args>)
            ExpressionSyntax callee = ParseExpression(dotNetCall);
            return InvocationExpression(callee, argList);
        }

        // Fallback: try array method mapping (e.g., rifs.map → Array.map → System.Linq.Enumerable.Select)
        if (call.Callee is TsMemberExpression memberExpr2)
        {
            string methodName = memberExpr2.Property;
            string arrayMethodKey = "Array." + methodName;

            if (_callSiteMap.TryResolve(arrayMethodKey, out string dotNetArrayMethod))
            {
                // For array methods, apply the mapped method call on the object.
                // Handles both:
                //   - Simple names: "Select" → obj.Select(...)
                //   - Qualified names: "System.Linq.Enumerable.Select" → Enumerable.Select(obj, ...)

                ExpressionSyntax obj = EmitExpression(memberExpr2.Obj);

                // If the mapping is a fully-qualified static method, use it directly
                if (dotNetArrayMethod.Contains('.'))
                {
                    // Parse as full expression (e.g., "System.Linq.Enumerable.Select")
                    ExpressionSyntax callee = ParseExpression(dotNetArrayMethod);

                    // Build a new argument list with the array object as the FIRST argument,
                    // followed by the original arguments (e.g., the lambda for Select).
                    var newArgs = new List<ArgumentSyntax> { Argument(obj) };
                    newArgs.AddRange(argList.Arguments);

                    return InvocationExpression(callee, ArgumentList(SeparatedList(newArgs)));
                }

                // Simple method name - use as instance method call
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        obj,
                        IdentifierName(dotNetArrayMethod)),
                    argList);
            }
        }

        return InvocationExpression(EmitExpression(call.Callee), argList);
    }

    /// <summary>
    /// Walks a callee expression and returns the dot-joined identifier chain
    /// (e.g. <c>fs.readFileSync</c>) used as the <see cref="CallSiteMap"/> lookup key.
    /// </summary>
    private static string BuildCalleeChain(TsExpression expr)
    {
        return expr switch
        {
            TsIdentifierExpression id                                        => id.Name,
            // Strip `this.` — the object part is TsUnknownExpression("this")
            TsMemberExpression { Obj: TsUnknownExpression { Text: "this" } } mem
                                                                             => mem.Property,
            TsMemberExpression mem                                           => BuildCalleeChain(mem.Obj) + "." + mem.Property,
            // Unwrap type assertions (e.g., (expr as any).method) and continue building the chain
            TsAsExpression asExpr                                            => BuildCalleeChain(asExpr.Expression),
            _                                                                => string.Empty,
        };
    }

    /// <summary>
    /// Translates TypeScript <c>new</c> expressions to C# object creation expressions.
    /// Handles special cases like <c>new Date()</c> → <c>System.DateTimeOffset.UtcNow</c>.
    /// </summary>
    private ExpressionSyntax EmitNewExpression(TsNewExpression newExpr)
    {
        string typeName = newExpr.Type switch
        {
            TsIdentifierExpression id => id.Name,
            _ => string.Empty
        };

        // Special handling for Date constructor: `new Date()` → `System.DateTimeOffset.UtcNow`
        if (typeName == "Date")
        {
            return ParseExpression("System.DateTimeOffset.UtcNow");
        }

        // General case: emit as `new TypeName(args)`
        ArgumentListSyntax argList = ArgumentList(
            SeparatedList(newExpr.Args.Select(a => Argument(EmitExpression(a)))));

        TypeSyntax type = ParseTypeSyntax(typeName);
        return ObjectCreationExpression(type, argList, initializer: null);
    }

    // -------------------------------------------------------------------------
    // Conditional
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitConditional(TsConditionalExpression cond) =>
        ConditionalExpression(
            EmitExpression(cond.Condition),
            EmitExpression(cond.WhenTrue),
            EmitExpression(cond.WhenFalse));

    // -------------------------------------------------------------------------
    // Arrow
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitArrow(TsArrowExpression arrow)
    {
        ParameterListSyntax paramList = BuildParameterList(arrow.Parameters);

        // Single return-expression body: () => expr
        if (arrow.Body is [TsReturnStatement { Expression: not null } ret])
        {
            var lambda = ParenthesizedLambdaExpression(paramList, null,
                EmitExpression(ret.Expression));
            return arrow.IsAsync ? lambda.WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword)) : lambda;
        }

        // Single expression-statement body: () => expr
        if (arrow.Body is [TsExpressionStatement { Expression: not null } exprStmt])
        {
            var lambda = ParenthesizedLambdaExpression(paramList, null,
                EmitExpression(exprStmt.Expression));
            return arrow.IsAsync ? lambda.WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword)) : lambda;
        }

        // Single variable-declaration body with initializer: () => { const x = expr; }
        // Extract the initializer expression as the lambda body
        if (arrow.Body is [TsVarStatement { Initializer: not null } varStmt])
        {
            var lambda = ParenthesizedLambdaExpression(paramList, null,
                EmitExpression(varStmt.Initializer));
            return arrow.IsAsync ? lambda.WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword)) : lambda;
        }

        // Two-statement pattern: variable declaration followed by return
        // If the return expression is a simple conditional or if the variable is referenced in the return,
        // emit as a block lambda to preserve variable scope.
        if (arrow.Body is [
            TsVarStatement { Initializer: not null, Name: not null } varStmt2,
            TsReturnStatement { Expression: not null } ret2
        ])
        {
            // For now, emit a block lambda to handle the general case safely
            var statements = new List<StatementSyntax>();

            // Emit the variable declaration
            statements.Add(LocalDeclarationStatement(
                VariableDeclaration(
                    IdentifierName("var"),
                    SingletonSeparatedList(
                        VariableDeclarator(
                            Identifier(varStmt2.Name),
                            null,
                            EqualsValueClause(EmitExpression(varStmt2.Initializer)))))));

            // Emit the return statement
            statements.Add(ReturnStatement(EmitExpression(ret2.Expression)));

            var lambda = ParenthesizedLambdaExpression(paramList, Block(statements));
            return arrow.IsAsync ? lambda.WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword)) : lambda;
        }

        // Generator functions cannot be expressed as C# lambdas and require a method-level declaration.
        // Extract to a local method and return its method group reference.
        if (arrow.IsGenerator)
        {
            return EmitGeneratorAsLocalMethod(arrow);
        }

        // Multi-statement block body: () => { statements }
        // This is the catch-all for arrow bodies that don't match simpler patterns.
        // Emit as a block lambda to preserve complex control flow (for, try-catch, etc.)
        var bodyStatements = EmitStatementBlock(arrow.Body);
        var blockBody = Block(bodyStatements);
        var blockLambda = ParenthesizedLambdaExpression(paramList, blockBody);
        return arrow.IsAsync ? blockLambda.WithAsyncKeyword(Token(SyntaxKind.AsyncKeyword)) : blockLambda;
    }

    /// <summary>
    /// Translates an async/non-async generator arrow function to a local method declaration.
    /// Returns an identifier that references the generated local method.
    /// The method declaration is queued in <see cref="_pendingLocalGenerators"/> to be injected
    /// by the statement emitter.
    /// </summary>
    private ExpressionSyntax EmitGeneratorAsLocalMethod(TsArrowExpression arrow)
    {
        _localGeneratorCounter++;
        string methodName = $"__GeneratorLocal_{_localGeneratorCounter}";

        ParameterListSyntax paramList = BuildParameterList(arrow.Parameters);

        // Build the method body from arrow.Body statements using EmitStatementBlock
        // to ensure any nested local methods are injected correctly
        var bodyStatements = EmitStatementBlock(arrow.Body);
        BlockSyntax methodBody = Block(bodyStatements);

        // Infer the yield type from the body (analyze yield statements for type hints)
        // For now, default to AsyncGenerator<object> or IAsyncEnumerable<object>
        // A more sophisticated implementation would analyze yields to determine the actual type
        string returnType = arrow.IsAsync 
            ? "IAsyncEnumerable<object>"
            : "IEnumerable<object>";

        // Build the local method declaration
        var localMethod = LocalFunctionStatement(
            ParseTypeSyntax(returnType),
            Identifier(methodName))
            .WithParameterList(paramList)
            .WithBody(methodBody);

        // Add async modifier if needed
        if (arrow.IsAsync)
            localMethod = localMethod.AddModifiers(Token(SyntaxKind.AsyncKeyword));

        // Queue this method to be injected before the current statement
        _pendingLocalGenerators[methodName] = localMethod;

        // Return a reference to the local method (method group)
        return IdentifierName(methodName);
    }

    // -------------------------------------------------------------------------
    // Template literal (interpolated string)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Translates a TypeScript template literal, e.g. <c>`Hello ${name}!`</c>,
    /// to a C# interpolated string: <c>$"Hello {name}!"</c>.
    /// </summary>
    private ExpressionSyntax EmitTemplateExpression(TsTemplateExpression tmpl)
    {
        var contents = new List<InterpolatedStringContentSyntax>();

        // Head — literal text before the first ${ }
        if (!string.IsNullOrEmpty(tmpl.Head))
            contents.Add(InterpolatedStringText(
                Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken, tmpl.Head, tmpl.Head, TriviaList())));

        foreach (TsTemplateSpan span in tmpl.Spans)
        {
            // The interpolated expression: {expr}
            contents.Add(Interpolation(EmitExpression(span.Expression)));

            // Literal tail text that follows the closing }
            if (!string.IsNullOrEmpty(span.Tail))
                contents.Add(InterpolatedStringText(
                    Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken, span.Tail, span.Tail, TriviaList())));
        }

        return InterpolatedStringExpression(
            Token(SyntaxKind.InterpolatedStringStartToken),
            List(contents),
            Token(SyntaxKind.InterpolatedStringEndToken));
    }

    // -------------------------------------------------------------------------
    // Object literal
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitObjectLiteral(TsObjectLiteralExpression objLit)
    {
        // Spread properties arrive from the parser with Name="..." and Value = walked IR expression.
        static bool IsSpread(TsObjectProperty p) => p.Name == "..." && p.Value is not null;

        TsObjectProperty[] named   = [.. objLit.Properties.Where(p => !IsSpread(p))];
        TsObjectProperty[] spreads = [.. objLit.Properties.Where(IsSpread)];

        return (spreads.Length, named.Length) switch
        {
            // {}  →  new { }
            (0, 0) => AnonymousObjectCreationExpression(
                           SeparatedList<AnonymousObjectMemberDeclaratorSyntax>()),

            // { ...foo }  →  foo
            (1, 0) => EmitExpression(spreads[0].Value!),

            // { a, b: expr }  →  new { a, b = expr }
            (0, _) => AnonymousObjectCreationExpression(
                           SeparatedList(named.Select(EmitNamedMember))),

            // multi-spread / mixed: SpreadMerge.Merge(a, b, …) — last value wins,
            // null sources silently skipped.  SpreadMerge.cs is auto-emitted into the output.
            _ => EmitSpreadMergeCall(spreads, named),
        };
    }

    private AnonymousObjectMemberDeclaratorSyntax EmitNamedMember(TsObjectProperty p) =>
        p.Value is not null
            ? AnonymousObjectMemberDeclarator(NameEquals(IdentifierName(p.Name)), EmitExpression(p.Value))
            : AnonymousObjectMemberDeclarator(IdentifierName(p.Name));

    private ExpressionSyntax EmitSpreadMergeCall(TsObjectProperty[] spreads, TsObjectProperty[] named)
    {
        _needsSpreadMerge = true;

        IEnumerable<ExpressionSyntax> spreadArgs = spreads.Select(p => EmitExpression(p.Value!));

        // Named props, if any, become a trailing anonymous-object argument.
        IEnumerable<ExpressionSyntax> args = named.Length == 0
            ? spreadArgs
            : spreadArgs.Append(AnonymousObjectCreationExpression(
                  SeparatedList(named.Select(EmitNamedMember))));

        return InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName("SpreadMerge"),
                IdentifierName("Merge")),
            ArgumentList(SeparatedList(args.Select(Argument))));
    }

    // -------------------------------------------------------------------------
    // Array literal
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitArrayLiteral(TsArrayLiteralExpression arrLit)
    {
        // Array literal: [a, b, c] → new[] { a, b, c }
        if (arrLit.Elements.Length == 0)
        {
            // Empty array: [] → Array.Empty<T>() or new T[0]
            // We'll use explicit array initializer syntax for simplicity
            return ImplicitArrayCreationExpression(
                InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList<ExpressionSyntax>()));
        }

        return ImplicitArrayCreationExpression(
            InitializerExpression(
                SyntaxKind.ArrayInitializerExpression,
                SeparatedList(arrLit.Elements.Select(EmitExpression))));
    }

    // -------------------------------------------------------------------------
    // Spread element
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitSpreadElement(TsSpreadElement spread) =>
        // Spread elements are typically handled in the call expression handler.
        // If a spread appears outside of a call context, emit the inner expression.
        EmitExpression(spread.Expression);

    // -------------------------------------------------------------------------
    // Yield expression
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitYield(TsYieldExpression yield)
    {
        // In C#, yield return must be a statement, not an expression.
        // However, we can emit it as an invocation that returns the yielded value for type-checking.
        // The statement emitter will handle the actual yield return statement.
        // For now, when a yield appears as an expression, emit a placeholder that indicates
        // this should have been handled at the statement level.
        if (yield.Expression is not null)
        {
            return Placeholder($"/* yield {(yield.Delegate ? "* " : "")}expr - must be statement */");
        }
        return Placeholder("/* yield - must be statement */");
    }

    // -------------------------------------------------------------------------
    // Unknown / placeholder
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitUnknown(TsUnknownExpression unknown) =>
        Placeholder($"/* unknown: {unknown.Text} */");

    /// <summary>
    /// Returns a string-literal expression whose value is <paramref name="comment"/>.
    /// This keeps the generated file syntactically valid when a real expression cannot be emitted.
    /// </summary>
    private static LiteralExpressionSyntax Placeholder(string comment) =>
        LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(comment));
}
