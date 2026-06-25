namespace ContinueTranslator.Core.IR;

/// <summary>Represents a top-level TypeScript function declaration.</summary>
internal sealed record TsFunction(
    string Name,
    TsTypeRef ReturnType,
    TsParameter[] Parameters,
    string[] TypeParameters,
    bool IsAsync,
    bool IsExported,
    string[] Cookies)
{
    /// <summary>Function body statements. Empty when body not yet parsed.</summary>
    public TsStatement[] Body { get; init; } = [];
}

