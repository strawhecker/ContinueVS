using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    partial void EmitEnums(TsFile[] files, string commonRoot, List<EmittedFile> results)
    {
        foreach (TsFile file in files)
        {
            if (file.Enums.Length == 0) continue;

            string ns = DeriveNamespace(file.FilePath, commonRoot);
            var members = new List<MemberDeclarationSyntax>();

            foreach (TsEnum tsEnum in file.Enums)
            {
                if (HasIgnoreCookie(tsEnum.Cookies)) continue;

                string enumName = TryGetRenameCookie(tsEnum.Cookies, out string renamed)
                    ? renamed
                    : tsEnum.Name;

                EnumDeclarationSyntax enumDecl = BuildEnumDeclaration(tsEnum, enumName);
                members.Add(enumDecl);
            }

            if (members.Count == 0) continue;

            string relDir = DeriveRelativePath(ns, string.Empty).TrimEnd('/');
            string fileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".Enums.cs";
            string relativePath = relDir.Length > 0 ? $"{relDir}/{fileName}" : fileName;

            string content = BuildCompilationUnit(ns, members, file.Imports);
            results.Add(new EmittedFile(relativePath, content));
        }
    }

    private static EnumDeclarationSyntax BuildEnumDeclaration(TsEnum tsEnum, string enumName)
    {
        var enumMembers = new List<EnumMemberDeclarationSyntax>();

        foreach (TsEnumMember member in tsEnum.Members)
        {
            if (HasIgnoreCookie(member.Cookies)) continue;

            EnumMemberDeclarationSyntax memberDecl = EnumMemberDeclaration(
                Identifier(member.Name));

            if (member.Value is not null)
            {
                // TryParseEnumMemberValue returns null for string-literal TS values
                // (e.g. "API Key") that are not valid C# enum initialisers.
                // In that case we omit the initialiser and add a comment instead.
                ExpressionSyntax? initExpr = TryParseEnumMemberValue(member.Value);
                if (initExpr is not null)
                {
                    memberDecl = memberDecl.WithEqualsValue(EqualsValueClause(initExpr));
                }
                else
                {
                    memberDecl = WithLeadingLineComment(memberDecl, $"TS value: {member.Value}");
                }
            }

            enumMembers.Add(memberDecl);
        }

        EnumDeclarationSyntax enumDecl = EnumDeclaration(Identifier(enumName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithMembers(SeparatedList(enumMembers));

        if (tsEnum.IsConst)
        {
            enumDecl = WithLeadingLineComment(enumDecl, "TS const enum");
        }

        return enumDecl;
    }

    /// <summary>
    /// Parses a TypeScript enum member value into a C# <see cref="ExpressionSyntax"/>.
    /// Returns <see langword="null"/> for quoted string-literal values (e.g. <c>"API Key"</c>)
    /// because C# enums require integer-backed initialisers.
    /// </summary>
    private static ExpressionSyntax? TryParseEnumMemberValue(string value)
    {
        // Numeric literal (int).
        if (int.TryParse(value, out int intVal))
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intVal));

        // Hex literal.
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
        {
            if (long.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out long hexVal))
                return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(hexVal));
        }

        // String literal (quoted) — not valid as a C# enum initialiser.
        // Return null so the caller omits the EqualsValue clause entirely.
        if (value.Length >= 2 &&
            ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        {
            return null;
        }

        // Fall back to identifier reference (e.g. another enum member name).
        return IdentifierName(value);
    }
}
