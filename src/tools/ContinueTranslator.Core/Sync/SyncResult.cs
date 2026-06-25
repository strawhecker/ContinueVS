namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Counts produced by <see cref="GeneratedFolderSync.Sync"/> after a single promotion pass.
/// </summary>
internal sealed record SyncResult(
    /// <summary>Number of files written to the Generated/ folder.</summary>
    int Promoted,
    /// <summary>Number of files skipped because the Generated/ copy was hand-edited.</summary>
    int SkippedManualEdit,
    /// <summary>Number of files skipped because the content still contains stubs or raw TS type leaks.</summary>
    int SkippedHasStubs);
