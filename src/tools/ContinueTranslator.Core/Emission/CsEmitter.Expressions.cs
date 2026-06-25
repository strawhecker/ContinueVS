using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
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
            TsCallExpression call             => EmitCallExpression(call),
            TsConditionalExpression cond      => EmitConditional(cond),
            TsArrowExpression arrow           => EmitArrow(arrow),
            TsObjectLiteralExpression objLit  => EmitObjectLiteral(objLit),
            TsUnknownExpression unknown       => EmitUnknown(unknown),
            _                                 => Placeholder("/* untranslatable expression */"),
        };

    // -------------------------------------------------------------------------
    // Identifier
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitIdentifier(TsIdentifierExpression id) =>
        IdentifierName(id.Name);

    // -------------------------------------------------------------------------
    // Literal
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitLiteral(TsLiteralExpression lit)
    {
        string v = lit.Value;

        if (v == "null")
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
    // Member
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitMemberExpression(TsMemberExpression mem) =>
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            EmitExpression(mem.Obj),
            IdentifierName(mem.Property));

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
            ["==="] = SyntaxKind.EqualsEqualsToken,
            ["!=="] = SyntaxKind.ExclamationEqualsToken,
            ["=="]  = SyntaxKind.EqualsEqualsToken,
            ["!="]  = SyntaxKind.ExclamationEqualsToken,
            ["+"]   = SyntaxKind.PlusToken,
            ["-"]   = SyntaxKind.MinusToken,
            ["*"]   = SyntaxKind.AsteriskToken,
            ["/"]   = SyntaxKind.SlashToken,
            ["%"]   = SyntaxKind.PercentToken,
            ["<"]   = SyntaxKind.LessThanToken,
            [">"]   = SyntaxKind.GreaterThanToken,
            ["<="]  = SyntaxKind.LessThanEqualsToken,
            [">="]  = SyntaxKind.GreaterThanEqualsToken,
            ["&&"]  = SyntaxKind.AmpersandAmpersandToken,
            ["||"]  = SyntaxKind.BarBarToken,
        };

    private ExpressionSyntax EmitBinaryExpression(TsBinaryExpression bin)
    {
        if (!s_binaryOpMap.TryGetValue(bin.Op, out SyntaxKind opKind))
            return Placeholder("/* untranslatable binary op */");

        return BinaryExpression(opKind, EmitExpression(bin.Left), EmitExpression(bin.Right));
    }

    // -------------------------------------------------------------------------
    // Call
    // -------------------------------------------------------------------------

    private ExpressionSyntax EmitCallExpression(TsCallExpression call)
    {
        string calleeChain = BuildCalleeChain(call.Callee);
        ArgumentListSyntax argList = ArgumentList(
            SeparatedList(call.Args.Select(a => Argument(EmitExpression(a)))));

        if (_callSiteMap.TryResolve(calleeChain, out string dotNetCall))
        {
            // Build: <dotNetCall>(<translated args>)
            ExpressionSyntax callee = ParseExpression(dotNetCall);
            return InvocationExpression(callee, argList);
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
            TsIdentifierExpression id  => id.Name,
            TsMemberExpression mem     => BuildCalleeChain(mem.Obj) + "." + mem.Property,
            _                          => string.Empty,
        };
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
            return ParenthesizedLambdaExpression(paramList, null,
                EmitExpression(ret.Expression));
        }

        // Single expression-statement body: () => expr
        if (arrow.Body is [TsExpressionStatement { Expression: not null } exprStmt])
        {
            return ParenthesizedLambdaExpression(paramList, null,
                EmitExpression(exprStmt.Expression));
        }

        return Placeholder("/* untranslatable arrow body */");
    }

    // -------------------------------------------------------------------------
    // Object literal
    // -------------------------------------------------------------------------

    private static ExpressionSyntax EmitObjectLiteral(TsObjectLiteralExpression objLit) =>
        Placeholder("/* untranslatable object literal */");

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
