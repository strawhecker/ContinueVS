using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    partial void EmitVariables(TsFile[] files, string commonRoot, List<EmittedFile> results)
    {
        foreach (TsFile file in files)
        {
            if (file.Variables.Length == 0) continue;

            // Skip if no primary class (variables at module level with no associated class)
            if (string.IsNullOrEmpty(file.PrimaryClassName)) continue;

            string ns = DeriveNamespace(file.FilePath, commonRoot);
            string className = file.PrimaryClassName;

            var fieldMembers = new List<MemberDeclarationSyntax>();

            foreach (TsVariable variable in file.Variables)
            {
                if (HasIgnoreCookie(variable.Cookies)) continue;

                string fieldName = variable.Name;

                // Map TypeScript type to C#, handling index signatures
                string typeText = MapVariableType(variable.Type.Text);

                // Build field declaration: public static readonly <type> <name> = <initializer>;
                EqualsValueClauseSyntax? initializer = null;
                if (variable.Initializer != null)
                {
                    initializer = EqualsValueClause(
                        TryConvertExpression(variable.Initializer, file.FilePath));
                }

                var fieldDecl = FieldDeclaration(
                    VariableDeclaration(
                        ParseTypeName(typeText))
                    .WithVariables(
                        SingletonSeparatedList(
                            VariableDeclarator(Identifier(fieldName))
                            .WithInitializer(initializer))))
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword),
                        Token(SyntaxKind.ReadOnlyKeyword));

                fieldMembers.Add(fieldDecl);
            }

            if (fieldMembers.Count == 0) continue;

            // Emit as a partial class that extends the primary class
            ClassDeclarationSyntax partialClass = ClassDeclaration(className)
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.PartialKeyword))
                .AddMembers(fieldMembers.ToArray());

            string relDir = DeriveRelativePath(ns, string.Empty).TrimEnd('/');
            string fileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".Variables.cs";
            string relativePath = relDir.Length > 0 ? $"{relDir}/{fileName}" : fileName;

            var standardUsings = CollectRequiredUsings(fieldMembers, _usingsMap);
            string content = BuildCompilationUnit(ns, new[] { partialClass }, file.Imports, standardUsings);
            results.Add(new EmittedFile(relativePath, content));
        }
    }

    /// <summary>
    /// Maps TypeScript types to C# equivalents.
    /// Handles index signatures like { [key: string]: string } → Dictionary&lt;string, string&gt;.
    /// </summary>
    private string MapVariableType(string tsType)
    {
        if (string.IsNullOrWhiteSpace(tsType))
            return "object";

        // Pattern match for index signatures: { [key: KeyType]: ValueType }
        // This is a simplified heuristic: look for [ and match the pattern
        if (tsType.Contains("[") && tsType.Contains(":") && tsType.Contains("]"))
        {
            // Try to extract the key and value types from the index signature
            var match = System.Text.RegularExpressions.Regex.Match(
                tsType,
                @"\{\s*\[\s*\w+\s*:\s*(\w+)\s*\]\s*:\s*(\w+)\s*\}");

            if (match.Success)
            {
                string keyType = match.Groups[1].Value;
                string valueType = match.Groups[2].Value;
                return $"Dictionary<{keyType}, {valueType}>";
            }
        }

        return tsType;
    }

    /// <summary>
    /// Attempts to convert a TsExpression to a C# ExpressionSyntax.
    /// Falls back to a comment if conversion fails.
    /// </summary>
    private ExpressionSyntax TryConvertExpression(TsExpression? expr, string filePath)
    {
        if (expr == null)
        {
            return LiteralExpression(SyntaxKind.NullLiteralExpression);
        }

        try
        {
            return ConvertExpression(expr);
        }
        catch
        {
            // Fallback: return a TODO comment
            return LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal($"TODO: {expr.GetType().Name}"));
        }
    }

    /// <summary>
    /// Converts a TsExpression to a C# ExpressionSyntax.
    /// </summary>
    private ExpressionSyntax ConvertExpression(TsExpression expr)
    {
        return expr switch
        {
            TsLiteralExpression lit => LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal(lit.Value ?? "")),
            TsIdentifierExpression id => IdentifierName(id.Name),
            TsObjectLiteralExpression obj => ConvertObjectLiteral(obj),
            TsCallExpression call => ConvertCallExpression(call),
            TsMemberExpression member => ConvertMemberExpression(member),
            TsArrayLiteralExpression arr => ConvertArrayLiteral(arr),
            _ => LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal($"TODO: {expr.GetType().Name}"))
        };
    }

    /// <summary>
    /// Converts a TsObjectLiteralExpression to C# syntax.
    /// For simple string->string maps (like BRACKETS), emits a dictionary initializer.
    /// </summary>
    private ExpressionSyntax ConvertObjectLiteral(TsObjectLiteralExpression obj)
    {
        if (obj.Properties.Length == 0)
        {
            // Empty object literal: new Dictionary<string, string>()
            return ObjectCreationExpression(
                GenericName(Identifier("Dictionary"),
                    TypeArgumentList(
                        SeparatedList(new TypeSyntax[] {
                            ParseTypeName("string"),
                            ParseTypeName("string")
                        }))))
                .WithArgumentList(ArgumentList());
        }

        // Check if all properties have string literal values (for BRACKETS pattern)
        bool isStringStringMap = obj.Properties.All(p =>
            p.Value is TsLiteralExpression);

        if (isStringStringMap)
        {
            // Create dictionary initializer: new Dictionary<string, string> { { "key", "value" }, ... }
            var initializers = obj.Properties
                .Select(p =>
                {
                    var keyExpr = LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal(p.Name));
                    var valExpr = ConvertExpression(p.Value ?? new TsLiteralExpression(""));
                    return InitializerExpression(
                        SyntaxKind.ComplexElementInitializerExpression,
                        SeparatedList<ExpressionSyntax>(new ExpressionSyntax[] { keyExpr, valExpr }));
                })
                .ToArray();

            return ObjectCreationExpression(
                GenericName(Identifier("Dictionary"),
                    TypeArgumentList(
                        SeparatedList(new TypeSyntax[] {
                            ParseTypeName("string"),
                            ParseTypeName("string")
                        }))))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.CollectionInitializerExpression,
                        SeparatedList<ExpressionSyntax>(initializers)));
        }

        // Fallback: emit a comment indicating unsupported syntax
        return LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal("TODO: complex object literal"));
    }

    /// <summary>
    /// Converts a TsArrayLiteralExpression to C# array initializer syntax.
    /// </summary>
    private ExpressionSyntax ConvertArrayLiteral(TsArrayLiteralExpression arr)
    {
        if (arr.Elements.Length == 0)
        {
            return ArrayCreationExpression(
                ArrayType(ParseTypeName("string"),
                    SingletonList(ArrayRankSpecifier())))
                .WithInitializer(InitializerExpression(SyntaxKind.ArrayInitializerExpression));
        }

        var elements = arr.Elements.Select(ConvertExpression).ToArray();
        return ArrayCreationExpression(
            ArrayType(ParseTypeName("string"),
                SingletonList(ArrayRankSpecifier())))
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    SeparatedList(elements)));
    }

    /// <summary>
    /// Converts a call expression to C# syntax.
    /// </summary>
    private ExpressionSyntax ConvertCallExpression(TsCallExpression call)
    {
        var callee = ConvertExpression(call.Callee);
        var args = call.Args
            .Select(a => Argument(ConvertExpression(a)))
            .ToArray();

        return InvocationExpression((ExpressionSyntax)callee)
            .WithArgumentList(ArgumentList(SeparatedList(args)));
    }

    /// <summary>
    /// Converts a member access expression to C# syntax.
    /// </summary>
    private ExpressionSyntax ConvertMemberExpression(TsMemberExpression member)
    {
        var obj = ConvertExpression(member.Obj);
        var prop = member.Property;

        return MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            (ExpressionSyntax)obj,
            IdentifierName(prop));
    }
}
