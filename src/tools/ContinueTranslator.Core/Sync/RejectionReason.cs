namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Categorizes why a translator-emitted file was rejected and not promoted to Generated/.
/// </summary>
internal enum RejectionReason
{
    /// <summary>File contains <c>// TODO</c>, <c>// @ct:todo</c>, <c>/* unknown:</c>, or <c>/* untranslatable</c> markers.</summary>
    HasTodoStub,

    /// <summary>File contains generic TypeScript types: <c>Promise&lt;</c>, <c>Observable&lt;</c>, <c>ReadonlyArray&lt;</c>, etc.</summary>
    HasGenericTsTypes,

    /// <summary>File contains bare <c>any</c> identifier (likely raw TypeScript type leak).</summary>
    HasAnyType,

    /// <summary>File contains bare <c>undefined</c> identifier (likely raw TypeScript type leak).</summary>
    HasUndefinedType,

    /// <summary>File imports npm packages with no C# mapping in <c>npm-packages.json</c> (e.g., <c>web-tree-sitter</c>).</summary>
    UnmappedNpmImport,

    /// <summary>File path is not in the whitelist (<c>whitelist.json</c>). Only whitelisted files are emitted.</summary>
    NotWhitelisted,
}

/// <summary>
/// Extension methods for <see cref="RejectionReason"/>.
/// </summary>
internal static class RejectionReasonExtensions
{
    /// <summary>
    /// Returns a human-readable description of the rejection reason.
    /// </summary>
    public static string GetDescription(this RejectionReason reason) => reason switch
    {
        RejectionReason.HasTodoStub => "Contains unresolved // TODO or /* unknown */ stub markers",
        RejectionReason.HasGenericTsTypes => "Contains raw TypeScript generic types (Promise<, Observable<, etc.)",
        RejectionReason.HasAnyType => "Contains bare 'any' identifier",
        RejectionReason.HasUndefinedType => "Contains bare 'undefined' identifier",
        RejectionReason.UnmappedNpmImport => "Imports npm packages with no C# mapping",
        RejectionReason.NotWhitelisted => "File path not in whitelist",
        _ => "Unknown rejection reason"
    };

    /// <summary>
    /// Returns a short summary suitable for file metadata.
    /// </summary>
    public static string GetShortLabel(this RejectionReason reason) => reason switch
    {
        RejectionReason.HasTodoStub => "TODO stub",
        RejectionReason.HasGenericTsTypes => "TS generic types",
        RejectionReason.HasAnyType => "any type",
        RejectionReason.HasUndefinedType => "undefined type",
        RejectionReason.UnmappedNpmImport => "Unmapped npm import",
        RejectionReason.NotWhitelisted => "Not whitelisted",
        _ => "unknown"
    };
}
