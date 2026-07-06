namespace ContinueTranslator.Core.IR;

using ContinueTranslator.Core.Sync;

/// <summary>Root IR node representing one parsed TypeScript source file.</summary>
internal sealed record TsFile(
    /// <summary>Absolute path to the <c>.ts</c> source file.</summary>
    string FilePath,
    TsImport[] Imports,
    TsClass[] Classes,
    TsInterface[] Interfaces,
    TsEnum[] Enums,
    TsFunction[] Functions,
    TsTypeAlias[] TypeAliases,
    string[] Cookies)
{
    /// <summary>Rejection reasons indicating why this file cannot be translated to C#.</summary>
    public List<RejectionReason> RejectionReasons { get; init; } = [];

    /// <summary>
    /// Indicates whether this file matches a whitelist pattern.
    /// If true, unmapped npm imports are allowed and will not cause rejection.
    /// </summary>
    public bool IsWhitelisted { get; init; } = false;
}

