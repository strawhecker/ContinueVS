namespace ContinueTranslator.Core.IR;

/// <summary>Represents a top-level TypeScript variable declaration (const, let, var).</summary>
internal sealed record TsVariable(
    string Name,
    TsTypeRef Type,
    bool IsExported,
    string[] Cookies)
{
    /// <summary>Initialization expression. May be null if no initializer provided.</summary>
    public TsExpression? Initializer { get; init; }

    /// <summary>The kind of declaration: "const", "let", or "var".</summary>
    public string DeclarationKind { get; init; } = "const";
}
