using System.Reflection;
using ContinueTranslator.Core.Emission;
using ContinueTranslator.Core.IR;
using ContinueTranslator.Core.Mapping;
using ContinueTranslator.Core.Parsing;
using ContinueTranslator.Core.Sync;

namespace ContinueTranslator.Cli;

/// <summary>
/// Constructs all pipeline dependencies and runs the full TypeScript-to-C# translation
/// for a given <see cref="TranslationOptions"/>.
/// </summary>
internal sealed class PipelineRunner
{
    private readonly MappingEngine _mappingEngine;
    private readonly NuGetPackageResolver _nugetResolver;
    private readonly TsParser _tsParser;
    private readonly CsEmitter _csEmitter;
    private readonly ProjectEmitter _projectEmitter;
    private readonly WhitelistMap _whitelist;

    /// <summary>
    /// Initialises all pipeline components. The mappings folder is resolved relative to the running assembly.
    /// For net10.0 builds, it's in the same directory as the executable.
    /// </summary>
    public PipelineRunner()
    {
        string assemblyDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

        // First try the mappings directory alongside the assembly (for net10.0)
        string mappingsDir = Path.Combine(assemblyDir, "mappings");
        if (!Directory.Exists(mappingsDir))
        {
            // Fall back to one level up (for development/debugging scenarios)
            mappingsDir = Path.Combine(assemblyDir, "..", "mappings");
        }

        string nodeApiPath = Path.GetFullPath(Path.Combine(mappingsDir, "node-api.json"));
        string npmPackagesPath = Path.GetFullPath(Path.Combine(mappingsDir, "npm-packages.json"));
        string typesPath = Path.GetFullPath(Path.Combine(mappingsDir, "types.json"));
        string callSitesPath = Path.GetFullPath(Path.Combine(mappingsDir, "callsites.json"));
        string whitelistPath = Path.GetFullPath(Path.Combine(mappingsDir, "whitelist.json"));
        string usingsPath = Path.GetFullPath(Path.Combine(mappingsDir, "usings.json"));

        var nodeApiMap = new NodeApiMap(nodeApiPath);
        var npmPackageMap = new NpmPackageMap(npmPackagesPath);
        var typeMap = new TypeMap(typesPath);
        var callSiteMap = new CallSiteMap(callSitesPath);
        var usingsMap = new UsingsMap(usingsPath);
        _whitelist = new WhitelistMap(whitelistPath);

        _mappingEngine = new MappingEngine(nodeApiMap, npmPackageMap, typeMap, callSiteMap);
        _nugetResolver = new NuGetPackageResolver(npmPackageMap);
        _tsParser = new TsParser();
        _csEmitter = new CsEmitter(callSiteMap, usingsMap);
        _projectEmitter = new ProjectEmitter(_nugetResolver);
    }

    /// <summary>
    /// Runs the full pipeline: checkout → scan → parse → map → emit → write.
    /// In single-file mode, skips checkout/scan and processes only the specified file.
    /// </summary>
    /// <param name="options">Validated CLI arguments.</param>
    public void Run(TranslationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<string> tsPaths;
        IReadOnlySet<string> phase1Files = new HashSet<string>();

        // Single-file mode: skip cleanup and repo scanning
        if (options.SingleFilePath is not null)
        {
            if (!File.Exists(options.SingleFilePath))
                throw new InvalidOperationException($"Single-file path does not exist: {options.SingleFilePath}");

            tsPaths = new[] { options.SingleFilePath };
        }
        else
        {
            // Standard mode: clean directories and scan repo
            if (string.IsNullOrWhiteSpace(options.RepoPath) || string.IsNullOrWhiteSpace(options.Tag))
                throw new InvalidOperationException("Standard mode requires --repo and --tag arguments.");

            // 0. Clean output directories to ensure a fresh start.
            CleanDirectory(options.OutDirectory);
            if (options.GeneratedDirectory is not null)
            {
                CleanDirectory(options.GeneratedDirectory);

                // If rejected directory is not explicitly provided, clean the auto-determined path.
                if (options.RejectedDirectory is null)
                {
                    string autoRejectedDir = Path.Combine(
                        Path.GetDirectoryName(options.GeneratedDirectory) ?? options.OutDirectory, "..", "rejected");
                    CleanDirectory(autoRejectedDir);
                }
                else
                {
                    CleanDirectory(options.RejectedDirectory);
                }
            }
            else if (options.RejectedDirectory is not null)
            {
                CleanDirectory(options.RejectedDirectory);
            }

            // 1. Checkout and scan (optimized two-pass: node_modules first, then source).
            var scanner = new RepoScanner();
            var nodeModulesPatterns = _whitelist.GetNodeModulesDirectoryPatterns();

            if (nodeModulesPatterns.Count > 0)
            {
                // Use two-pass scan: whitelisted node_modules first, then source excluding node_modules
                (tsPaths, phase1Files) = scanner.CheckoutAndScanTwoPass(options.RepoPath, options.Tag, nodeModulesPatterns);
            }
            else
            {
                // No node_modules in whitelist, use standard full core/ scan
                tsPaths = scanner.CheckoutAndScan(options.RepoPath, options.Tag);
            }
        }

        // 2. Parse.
        TsFile[] parsed = _tsParser.Parse(tsPaths);

        // 2a. Filter by whitelist: files not in the whitelist are rejected immediately.
        // In single-file mode, use the file's directory as the base; in standard mode, use the repo path.
        string basePathForWhitelist = options.RepoPath ?? Path.GetDirectoryName(options.SingleFilePath) ?? Directory.GetCurrentDirectory();
        TsFile[] whitelistedFiles = FilterByWhitelist(parsed, basePathForWhitelist, phase1Files);

        // 3. Apply mappings.
        TsFile[] mapped = _mappingEngine.Apply(whitelistedFiles);

        // 3a. Separate accepted and rejected files.
        TsFile[] acceptedFiles = mapped.Where(f => f.RejectionReasons.Count == 0).ToArray();
        TsFile[] rejectedFiles = mapped.Where(f => f.RejectionReasons.Count > 0).ToArray();

        // 4. Emit C# source files (only for accepted files).
        IReadOnlyList<EmittedFile> emitted = _csEmitter.Emit(mapped, options.OutDirectory);
        var files = new List<EmittedFile>(emitted);

        // 4a. Generate RequireShim helper class.
        EmittedFile requireShim = GenerateRequireShim();
        files.Add(requireShim);

        // 5. Emit project file and append.
        EmittedFile csproj = _projectEmitter.Emit(acceptedFiles, "net10.0");
        files.Add(csproj);

        // 6. Write all files to disk.
        foreach (EmittedFile file in files)
        {
            string fullPath = Path.Combine(options.OutDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
        }

        Console.WriteLine($"Wrote {files.Count} file(s) to '{options.OutDirectory}'.");

        // 6a. Write rejected files (in single-file mode, write to output directory as-is; in standard mode, write to rejected/).
        if (rejectedFiles.Length > 0)
        {
            string rejectedDir;
            if (options.SingleFilePath is null)
            {
                // Standard mode: use explicit or auto-determined rejected directory
                rejectedDir = options.RejectedDirectory
                    ?? Path.Combine(Path.GetDirectoryName(options.GeneratedDirectory ?? options.OutDirectory) ?? options.OutDirectory, "..", "rejected");
            }
            else
            {
                // Single-file mode: write to output directory
                rejectedDir = options.OutDirectory;
            }

            WriteRejectedFiles(rejectedFiles, rejectedDir);
            Console.WriteLine($"Wrote {rejectedFiles.Length} rejected file(s) to '{rejectedDir}'.");
        }

        // 7. Optionally promote translated files to the Generated/ folder (skipped in single-file mode).
        if (options.SingleFilePath is null && options.GeneratedDirectory is not null)
        {
            string rejectedDir = options.RejectedDirectory
                ?? Path.Combine(Path.GetDirectoryName(options.GeneratedDirectory) ?? options.OutDirectory, "..", "rejected");

            SyncResult syncResult = GeneratedFolderSync.Sync(files, options.GeneratedDirectory, rejectedDir);
            Console.WriteLine(syncResult);

            // 8. Optionally clean the Generated/ folder after translation (useful for testing).
            if (options.PostTranslateCleanGenerated)
            {
                CleanDirectory(options.GeneratedDirectory);
            }
        }
    }

    /// <summary>
    /// Generates the RequireShim.cs helper class.
    /// </summary>
    private static EmittedFile GenerateRequireShim()
    {
        const string shimContent = @"namespace ContinueTranslator.Generated;

/// <summary>
/// Shim for Node.js require/CommonJS patterns translated to C#.
/// Provides methods to load dynamic modules and manage module caching semantics.
/// 
/// In Node.js, require() loads modules with caching by default, and delete require.cache[]
/// clears entries so the next require() loads fresh. In C#/.NET, we simulate this by:
/// - Loading directly from files each time (no persistent cache)
/// - Returning deserialized objects (for .json) or dynamic types (for .js files)
/// </summary>
public static class RequireShim
{
    /// <summary>
    /// Loads and parses a JavaScript or JSON module from the specified path.
    /// Always loads fresh from disk (no caching).
    /// </summary>
    /// <param name=""modulePath"">Path to the module file (.js, .mjs, .json, etc.)</param>
    /// <returns>
    /// For .json files: a dynamic object (parsed JSON structure)
    /// For .js files: null (JS files cannot be directly executed in C#)
    /// </returns>
    public static dynamic? Import(string modulePath)
    {
        if (!File.Exists(modulePath))
            throw new FileNotFoundException($""Module not found: {modulePath}"");

        string ext = Path.GetExtension(modulePath).ToLowerInvariant();

        if (ext == "".json"")
        {
            string json = File.ReadAllText(modulePath);
            return System.Text.Json.JsonDocument.Parse(json).RootElement;
        }

        // For .js/.mjs files, return null (JS code cannot be directly executed in C#)
        // Callers should use a different pattern (e.g., RPC, subprocess, Roslyn)
        return null;
    }

    /// <summary>
    /// Clears the cached entry for a module, ensuring the next Import() call loads fresh.
    /// In C#, this is a no-op since Import() always loads fresh from disk.
    /// </summary>
    /// <param name=""modulePath"">Path to the module to clear from cache</param>
    public static void ClearCache(string modulePath)
    {
        // No-op: C# file I/O doesn't have a persistent module cache like Node.js
    }

    /// <summary>
    /// Resolves a module path to an absolute file path.
    /// Simulates Node.js require.resolve() for basic cases.
    /// </summary>
    /// <param name=""modulePath"">Module path (relative or absolute)</param>
    /// <returns>Absolute path to the module</returns>
    public static string Resolve(string modulePath)
    {
        return Path.GetFullPath(modulePath);
    }
}
";

        return new EmittedFile("RequireShim.cs", shimContent);
    }

    /// <summary>
    /// Writes rejected files (marked with rejection reasons) to the rejected directory,
    /// along with metadata sidecar files explaining why they were rejected.
    /// </summary>
    private static void WriteRejectedFiles(TsFile[] rejectedFiles, string rejectedDirectory)
    {
        ArgumentNullException.ThrowIfNull(rejectedFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedDirectory);

        Directory.CreateDirectory(rejectedDirectory);

        foreach (var tsFile in rejectedFiles)
        {
            // Use the original .ts filename but write as .cs.rejected for clarity.
            string fileName = Path.GetFileNameWithoutExtension(tsFile.FilePath);
            string relativePath = fileName + ".cs.rejected";

            // Create a placeholder file with rejection info.
            var reasons = tsFile.RejectionReasons;
            var reasonDescriptions = string.Join("\n// ", reasons.Select(r => r.GetDescription()));

            string placeholder = $@"// ===== FILE REJECTED FROM TRANSLATION =====
// This file was rejected during translation for the following reason(s):
// {reasonDescriptions}
//
// Source: {tsFile.FilePath}
// Rejected at: {DateTime.UtcNow:O}
//
// To fix this, either:
// 1. Add the missing npm dependency to npm-packages.json
// 2. Rewrite the TypeScript to avoid the problematic import
// 3. Use @ct:map= pragma on the import (if supported)
//
// ==========================================
";

            // Write the placeholder file.
            string rejectedPath = Path.Combine(rejectedDirectory, relativePath);
            File.WriteAllText(rejectedPath, placeholder);

            // Write the metadata sidecar.
            var rejectedFile = new RejectedFile(
                relativePath,
                placeholder,
                reasons.ToArray());

            string metadataPath = rejectedPath + ".rejection-metadata.json";
            var metadata = RejectedFileMetadata.FromRejectedFile(rejectedFile);
            string json = System.Text.Json.JsonSerializer.Serialize(
                metadata,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metadataPath, json);
        }
    }

    /// <summary>
    /// Filters parsed files against the whitelist.
    /// Only files matching whitelist patterns are accepted; all others are rejected.
    /// The whitelist typically contains patterns like "core/node_modules/package-name/**"
    /// 
    /// When two-pass scanning is used (phase1Files is provided), only Phase 1 files (from node_modules)
    /// are filtered by whitelist. Phase 2 files (source code) are accepted without filtering.
    /// In single-file mode (phase1Files is null), all files are filtered by whitelist as before.
    /// </summary>
    /// <param name="parsed">All parsed TypeScript files.</param>
    /// <param name="repoPath">Repository root path (used to compute relative paths for whitelist matching).</param>
    /// <param name="phase1Files">Set of files from Phase 1 (node_modules). If null, all files are whitelist-filtered.</param>
    /// <returns>Array of files (both accepted and rejected, with rejection reason set on rejected ones).</returns>
    private TsFile[] FilterByWhitelist(TsFile[] parsed, string repoPath, IReadOnlySet<string> phase1Files)
    {
        var result = new List<TsFile>();
        int acceptedCount = 0, rejectedCount = 0;

        foreach (var file in parsed)
        {
            string relativePath = ComputeRelativePath(file.FilePath, repoPath);

            // If phase1Files is provided, only filter Phase 1 files. Accept all Phase 2 files (source code).
            bool isPhase1File = phase1Files.Contains(file.FilePath);

            if (phase1Files.Count > 0 && !isPhase1File)
            {
                // Phase 2 file (source code): accept without whitelist filtering
                result.Add(file with { IsWhitelisted = true });
                acceptedCount++;
            }
            else if (_whitelist.IsWhitelisted(relativePath))
            {
                // File matches whitelist; mark as whitelisted and include as-is
                result.Add(file with { IsWhitelisted = true });
                acceptedCount++;
            }
            else
            {
                // File does not match whitelist; reject
                result.Add(file with { RejectionReasons = [RejectionReason.NotWhitelisted] });
                rejectedCount++;
            }
        }

        Console.WriteLine($"[WHITELIST FILTER] Accepted: {acceptedCount}, Rejected: {rejectedCount}");

        return result.ToArray();
    }

    /// <summary>
    /// Computes the relative path from the repo root to a file path.
    /// Normalizes backslashes to forward slashes for consistent matching.
    /// </summary>
    /// <param name="filePath">Absolute file path.</param>
    /// <param name="repoPath">Repository root path.</param>
    /// <returns>Relative path from repo root.</returns>
    private static string ComputeRelativePath(string filePath, string repoPath)
    {
        // Normalize both paths to absolute, full paths.
        string fullFilePath = Path.GetFullPath(filePath);
        string fullRepoPath = Path.GetFullPath(repoPath);

        // Ensure repo path ends with a separator for proper relative path computation.
        if (!fullRepoPath.EndsWith(Path.DirectorySeparatorChar))
            fullRepoPath += Path.DirectorySeparatorChar;

        if (fullFilePath.StartsWith(fullRepoPath, StringComparison.OrdinalIgnoreCase))
        {
            string relative = fullFilePath.Substring(fullRepoPath.Length);
            // Normalize to forward slashes
            return relative.Replace('\\', '/');
        }

        // If the file path is not within the repo, just return the file name.
        return Path.GetFileName(filePath).Replace('\\', '/');
    }

    /// <summary>
    /// Recursively deletes all files and subdirectories in the specified directory.
    /// If the directory does not exist, this method returns silently.
    /// </summary>
    /// <param name="directoryPath">Path to the directory to clean.</param>
    private static void CleanDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return;

        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
                Console.WriteLine($"Cleaned directory: {directoryPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to clean directory '{directoryPath}': {ex.Message}");
        }
    }
}
