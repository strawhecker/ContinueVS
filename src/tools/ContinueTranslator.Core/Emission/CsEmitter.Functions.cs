using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContinueTranslator.Core.IR;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ContinueTranslator.Core.Emission;

internal sealed partial class CsEmitter
{
    partial void EmitTypeAliases(TsFile[] files, string commonRoot, List<EmittedFile> results)
    {
        foreach (TsFile file in files)
        {
            if (file.TypeAliases.Length == 0) continue;

            string ns = DeriveNamespace(file.FilePath, commonRoot);
            var members = new List<MemberDeclarationSyntax>();

            foreach (TsTypeAlias alias in file.TypeAliases)
            {
                if (HasIgnoreCookie(alias.Cookies)) continue;

                // Emit a placeholder empty static class: valid C# that survives the build.
                // The TODO block comment carries the full TS alias definition for human review.
                // A block comment (/* ... */) is used so that multi-line TypeText values
                // (e.g. TS object literal type bodies) do not leak as bare invalid tokens.
                ClassDeclarationSyntax placeholder = ClassDeclaration(
                        Identifier(alias.Name + "Alias"))
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword));

                string blockComment = $"/* TODO type alias: {alias.Name} = {alias.TypeText} */";
                placeholder = placeholder.WithLeadingTrivia(
                    placeholder.GetLeadingTrivia().InsertRange(0, new[]
                    {
                        Comment(blockComment),
                        ElasticCarriageReturnLineFeed,
                    }));

                members.Add(placeholder);
            }

            if (members.Count == 0) continue;

            string relDir = DeriveRelativePath(ns, string.Empty).TrimEnd('/');
            string fileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".TypeAliases.cs";
            string relativePath = relDir.Length > 0 ? $"{relDir}/{fileName}" : fileName;

            string content = BuildCompilationUnit(ns, members);
            results.Add(new EmittedFile(relativePath, content));
        }
    }

    partial void EmitFunctions(TsFile[] files, string commonRoot, List<EmittedFile> results)
    {
        foreach (TsFile file in files)
        {
            if (file.Functions.Length == 0) continue;

            string ns = DeriveNamespace(file.FilePath, commonRoot);

            // Class name: PascalCase of the filename (without extension) + "Functions".
            string baseName = ToPascalCase(
                Path.GetFileNameWithoutExtension(file.FilePath));
            string className = baseName + "Functions";

            var methodMembers = new List<MemberDeclarationSyntax>();

            foreach (TsFunction func in file.Functions)
            {
                if (HasIgnoreCookie(func.Cookies)) continue;

                string methodName = TryGetRenameCookie(func.Cookies, out string renamed)
                    ? renamed
                    : func.Name;

                MethodDeclarationSyntax methodDecl = BuildFunctionStub(func, methodName, file.FilePath, className);
                methodMembers.Add(methodDecl);
            }

            if (methodMembers.Count == 0) continue;

            ClassDeclarationSyntax classDecl = ClassDeclaration(Identifier(className))
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.StaticKeyword),
                    Token(SyntaxKind.PartialKeyword))
                .WithMembers(List(methodMembers));

            string relDir = DeriveRelativePath(ns, string.Empty).TrimEnd('/');
            string fileName = className + ".cs";
            string relativePath = relDir.Length > 0 ? $"{relDir}/{fileName}" : fileName;

            string content = BuildCompilationUnit(ns, [classDecl]);
            results.Add(new EmittedFile(relativePath, content));
        }
    }

    private MethodDeclarationSyntax BuildFunctionStub(TsFunction func, string methodName, string filePath, string className)
    {
        ParameterListSyntax paramList = BuildParameterList(func.Parameters);

        string stubComment = $"// TODO: {filePath} :: {className}.{methodName}";
        BlockSyntax methodBody = IsBodyEmpty(func.Body)
            ? Block(ParseStatement($"{stubComment}\n"))
            : Block(List(EmitStatementBlock(func.Body, filePath)));

        MethodDeclarationSyntax methodDecl = MethodDeclaration(
                ParseTypeSyntax(func.ReturnType.Text),
                Identifier(methodName))
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.StaticKeyword))
            .WithParameterList(paramList)
            .WithBody(methodBody);

        if (func.IsAsync)
            methodDecl = methodDecl.AddModifiers(Token(SyntaxKind.AsyncKeyword));

        if (func.TypeParameters.Length > 0)
            methodDecl = methodDecl.WithTypeParameterList(BuildTypeParameterList(func.TypeParameters));

        return methodDecl;
    }
}
