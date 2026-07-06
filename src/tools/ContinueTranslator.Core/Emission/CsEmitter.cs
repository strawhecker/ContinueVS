using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;

namespace ContinueTranslator.Core.Emission;

/// <summary>
/// Converts an array of mapped <see cref="TsFile"/> IR nodes into emitted C# source files
/// using Roslyn SyntaxFactory. Each conceptual group is implemented in a dedicated partial file.
/// </summary>
internal sealed partial class CsEmitter
{
    private readonly CallSiteMap _callSiteMap;

    internal CsEmitter(CallSiteMap callSiteMap)
    {
        ArgumentNullException.ThrowIfNull(callSiteMap);
        _callSiteMap = callSiteMap;
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Emits C# source files for all IR nodes in <paramref name="files"/>.
    /// Files with rejection reasons (e.g., unmapped npm imports) are skipped.
    /// </summary>
    /// <param name="files">Mapped IR nodes (MappingEngine must have already run).</param>
    /// <param name="outputDirectory">
    /// Absolute path to the output root used to derive relative paths for each emitted file.
    /// </param>
    /// <returns>Deterministically ordered list of emitted files.</returns>
    public IReadOnlyList<EmittedFile> Emit(TsFile[] files, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        // Filter out files that have been marked for rejection (e.g., due to unmapped imports).
        TsFile[] acceptedFiles = files.Where(f => f.RejectionReasons.Count == 0).ToArray();

        string commonRoot = ResolveCommonRoot(acceptedFiles);
        var results = new List<EmittedFile>();

        EmitEnums(acceptedFiles, commonRoot, results);
        EmitInterfaces(acceptedFiles, commonRoot, results);
        EmitTypeAliases(acceptedFiles, commonRoot, results);
        EmitFunctions(acceptedFiles, commonRoot, results);
        EmitClasses(acceptedFiles, commonRoot, results);
        CollectResults(results);

        return results.AsReadOnly();
    }

    // -------------------------------------------------------------------------
    // Partial method declarations (implemented in dedicated partial files)
    // -------------------------------------------------------------------------

    partial void EmitEnums(TsFile[] files, string commonRoot, List<EmittedFile> results);
    partial void EmitInterfaces(TsFile[] files, string commonRoot, List<EmittedFile> results);
    partial void EmitTypeAliases(TsFile[] files, string commonRoot, List<EmittedFile> results);
    partial void EmitFunctions(TsFile[] files, string commonRoot, List<EmittedFile> results);
    partial void EmitClasses(TsFile[] files, string commonRoot, List<EmittedFile> results);

    /// <summary>Post-processes <paramref name="results"/> in-place (sorting, de-dup, etc.).</summary>
    partial void CollectResults(List<EmittedFile> results);
}
