using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    partial void EmitInterfaces(TsFile[] files, string commonRoot, List<EmittedFile> results)
    {
        foreach (TsFile file in files)
        {
            if (file.Interfaces.Length == 0) continue;

            string ns = DeriveNamespace(file.FilePath, commonRoot);
            var members = new List<MemberDeclarationSyntax>();

            foreach (TsInterface tsIface in file.Interfaces)
            {
                if (HasIgnoreCookie(tsIface.Cookies)) continue;

                string ifaceName = TryGetRenameCookie(tsIface.Cookies, out string renamed)
                    ? renamed
                    : tsIface.Name;

                InterfaceDeclarationSyntax ifaceDecl = BuildInterfaceDeclaration(tsIface, ifaceName);
                members.Add(ifaceDecl);
            }

            if (members.Count == 0) continue;

            string relDir = DeriveRelativePath(ns, string.Empty).TrimEnd('/');
            string fileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".Interfaces.cs";
            string relativePath = relDir.Length > 0 ? $"{relDir}/{fileName}" : fileName;

            string content = BuildCompilationUnit(ns, members);
            results.Add(new EmittedFile(relativePath, content));
        }
    }

    private static InterfaceDeclarationSyntax BuildInterfaceDeclaration(
        TsInterface tsIface, string ifaceName)
    {
        var memberSyntaxList = new List<MemberDeclarationSyntax>();

        // Properties
        foreach (TsProperty prop in tsIface.Properties)
        {
            if (HasIgnoreCookie(prop.Cookies)) continue;

            PropertyDeclarationSyntax propDecl = BuildInterfaceProperty(prop);
            memberSyntaxList.Add(propDecl);
        }

        // Methods
        foreach (TsMethod method in tsIface.Methods)
        {
            if (HasIgnoreCookie(method.Cookies)) continue;

            MethodDeclarationSyntax methodDecl = BuildInterfaceMethod(method);
            memberSyntaxList.Add(methodDecl);
        }

        InterfaceDeclarationSyntax ifaceDecl = InterfaceDeclaration(Identifier(ifaceName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithMembers(List(memberSyntaxList));

        // Base interfaces (extends)
        if (tsIface.Extends.Length > 0)
        {
            BaseTypeSyntax[] baseTypes = tsIface.Extends
                .Select(e => (BaseTypeSyntax)SimpleBaseType(ParseTypeName(e)))
                .ToArray();
            ifaceDecl = ifaceDecl.WithBaseList(BaseList(SeparatedList(baseTypes)));
        }

        return ifaceDecl;
    }

    private static PropertyDeclarationSyntax BuildInterfaceProperty(TsProperty prop)
    {
        string typeText = prop.IsOptional && !prop.Type.Text.EndsWith('?')
            ? prop.Type.Text + "?"
            : prop.Type.Text;

        // Detect and handle index signatures: { [key: string]: ValueType }
        // Convert to Dictionary<string, ValueType>
        bool isIndexSig = TryExtractIndexSignatureValueType(typeText, out string valueType);
        if (isIndexSig)
        {
            typeText = $"Dictionary<string, {valueType}>";
        }

        PropertyDeclarationSyntax propDecl = PropertyDeclaration(
                ParseTypeSyntax(typeText),
                Identifier(prop.Name))
            .WithAccessorList(AccessorList(List(new[]
            {
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
            })));

        if (HasTodoCookie(prop.Cookies) || isIndexSig)
            propDecl = WithLeadingLineComment(propDecl, "TODO");

        return propDecl;
    }

    private static MethodDeclarationSyntax BuildInterfaceMethod(TsMethod method)
    {
        ParameterListSyntax paramList = BuildParameterList(method.Parameters);

        MethodDeclarationSyntax methodDecl = MethodDeclaration(
                ParseTypeSyntax(method.ReturnType.Text),
                Identifier(method.Name))
            .WithParameterList(paramList)
            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

        if (method.TypeParameters.Length > 0)
            methodDecl = methodDecl.WithTypeParameterList(BuildTypeParameterList(method.TypeParameters));

        if (HasTodoCookie(method.Cookies))
            methodDecl = WithLeadingLineComment(methodDecl, "TODO");

        return methodDecl;
    }

    // -------------------------------------------------------------------------
    // Shared parameter / type-parameter helpers (used by Interfaces + Classes)
    // -------------------------------------------------------------------------

    internal static ParameterListSyntax BuildParameterList(TsParameter[] parameters)
    {
        var paramSyntaxes = new List<ParameterSyntax>();

        foreach (TsParameter p in parameters)
        {
            string typeText = p.Type.Text;

            // Detect and handle union types (e.g., "ChatMessage[] | string" → "object")
            bool isUnionType = typeText.Contains(" | ", StringComparison.Ordinal);
            if (isUnionType)
            {
                typeText = "object";
            }

            // Apply optional modifier if needed (but skip if already nullable)
            if (p.IsOptional && !typeText.EndsWith("?"))
            {
                typeText = typeText + "?";
            }

            // Detect and handle index signatures using the shared helper
            bool isIndexSig = TryExtractIndexSignatureValueType(typeText, out string valueType);
            if (isIndexSig)
            {
                typeText = $"Dictionary<string, {valueType}>";
            }

            ParameterSyntax paramSyntax = Parameter(Identifier(p.Name))
                .WithType(ParseTypeSyntax(typeText));

            // Add TODO comment if union type or index signature
            if (isUnionType)
                paramSyntax = WithLeadingLineComment(paramSyntax, "@ct:todo=union-type");
            else if (isIndexSig)
                paramSyntax = WithLeadingLineComment(paramSyntax, "@ct:todo");

            // Apply rest parameter modifier if needed
            if (p.IsRest)
                paramSyntax = paramSyntax.AddModifiers(Token(SyntaxKind.ParamsKeyword));

            // Apply default value if parameter has initializer
            if (p.HasInitializer)
            {
                // Heuristic default values based on type
                EqualsValueClauseSyntax defaultValue = GetDefaultValueForType(typeText);
                paramSyntax = paramSyntax.WithDefault(defaultValue);
            }

            paramSyntaxes.Add(paramSyntax);
        }

        return ParameterList(SeparatedList(paramSyntaxes));
    }

    /// <summary>
    /// Returns a heuristic default value for a given C# type.
    /// Used when a TypeScript parameter has a default value but the specific value is not captured.
    /// </summary>
    private static EqualsValueClauseSyntax GetDefaultValueForType(string typeText)
    {
        // Strip trailing '?' for nullable types
        string baseType = typeText.TrimEnd('?');

        // Common string types → empty string
        if (baseType is "string" or "String")
            return EqualsValueClause(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("")));

        // Common bool types → false
        if (baseType is "bool" or "boolean" or "Boolean")
            return EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression));

        // Common numeric types → 0
        if (baseType is "int" or "long" or "float" or "double" or "decimal" or "byte" or "short")
            return EqualsValueClause(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)));

        // Default to null for reference types
        return EqualsValueClause(LiteralExpression(SyntaxKind.NullLiteralExpression));
    }

    internal static TypeParameterListSyntax BuildTypeParameterList(string[] typeParams)
    {
        TypeParameterSyntax[] tpSyntaxes = typeParams
            .Select(tp => TypeParameter(Identifier(tp)))
            .ToArray();
        return TypeParameterList(SeparatedList(tpSyntaxes));
    }

    /// <summary>
    /// Detects and extracts the value type from a TypeScript index signature.
    /// Index signatures have the form: { [key: string]: ValueType } or { [key: SomeType]: ValueType }
    /// Extracts "ValueType" from the signature and returns it as the Dictionary value type.
    /// </summary>
    /// <param name="typeText">The type text to check (e.g., "{ [key: string]: RangeInFileWithContents[] }")</param>
    /// <param name="valueType">The extracted value type (e.g., "RangeInFileWithContents[]"), empty if not an index signature</param>
    /// <returns>True if typeText is an index signature, false otherwise</returns>
    private static bool TryExtractIndexSignatureValueType(string typeText, out string valueType)
    {
        valueType = string.Empty;

        string trimmed = typeText.TrimStart();
        if (!trimmed.StartsWith('{')) return false;
        if (!trimmed.Contains('[')) return false;
        if (!trimmed.Contains("]:")) return false;

        // Find the closing } to isolate the signature
        int closingBrace = trimmed.LastIndexOf('}');
        if (closingBrace < 0) return false;

        // Extract content between { and }
        int openBrace = trimmed.IndexOf('{');
        string signature = trimmed.Substring(openBrace + 1, closingBrace - openBrace - 1);

        // Find ]: to locate where the value type starts
        int colonIdx = signature.IndexOf("]:");
        if (colonIdx < 0) return false;

        // Extract value type after ]:
        valueType = signature.Substring(colonIdx + 2).Trim();
        return !string.IsNullOrEmpty(valueType);
    }
}
