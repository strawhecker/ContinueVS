namespace ContinueTranslator.Cli;

/// <summary>
/// Validated argument bag passed from <c>Program.cs</c> to the translation pipeline.
/// </summary>
internal sealed record TranslationOptions(
    /// <summary>Absolute path to the local clone of the forked Continue repository.</summary>
    string? RepoPath,
    /// <summary>Git tag or branch to check out before scanning.</summary>
    string? Tag,
    /// <summary>Absolute path to the output directory for generated C# files.</summary>
    string OutDirectory,
    /// <summary>Optional absolute path to the Generated/ folder to promote translated files into.</summary>
    string? GeneratedDirectory = null,
    /// <summary>Optional absolute path to the rejected/ folder for Phase 2 work queue.</summary>
    string? RejectedDirectory = null,
    /// <summary>When true, delete the Generated/ folder after translation completes. Useful for testing to prevent folder pollution.</summary>
    bool PostTranslateCleanGenerated = false,
    /// <summary>Optional: full path to a single .ts file to translate. When set, all repo/tag parameters are ignored and no cleanup is performed.</summary>
    string? SingleFilePath = null);

