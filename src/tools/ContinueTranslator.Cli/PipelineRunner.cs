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

    /// <summary>
    /// Initialises all pipeline components. The mappings folder is resolved as one directory
    /// above the directory containing the running assembly.
    /// </summary>
    public PipelineRunner()
    {
        string assemblyDir = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

        string mappingsDir = Path.Combine(assemblyDir, "..", "mappings");

        string nodeApiPath = Path.GetFullPath(Path.Combine(mappingsDir, "node-api.json"));
        string npmPackagesPath = Path.GetFullPath(Path.Combine(mappingsDir, "npm-packages.json"));
        string typesPath = Path.GetFullPath(Path.Combine(mappingsDir, "types.json"));
        string callSitesPath = Path.GetFullPath(Path.Combine(mappingsDir, "callsites.json"));

        var nodeApiMap = new NodeApiMap(nodeApiPath);
        var npmPackageMap = new NpmPackageMap(npmPackagesPath);
        var typeMap = new TypeMap(typesPath);
        var callSiteMap = new CallSiteMap(callSitesPath);

        _mappingEngine = new MappingEngine(nodeApiMap, npmPackageMap, typeMap, callSiteMap);
        _nugetResolver = new NuGetPackageResolver(npmPackageMap);
        _tsParser = new TsParser();
        _csEmitter = new CsEmitter(callSiteMap);
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

            // 1. Checkout and scan.
            var scanner = new RepoScanner();
            tsPaths = scanner.CheckoutAndScan(options.RepoPath, options.Tag);
        }

        // 2. Parse.
        TsFile[] parsed = _tsParser.Parse(tsPaths);

        // 3. Apply mappings.
        TsFile[] mapped = _mappingEngine.Apply(parsed);

        // 4. Emit C# source files.
        IReadOnlyList<EmittedFile> emitted = _csEmitter.Emit(mapped, options.OutDirectory);
        var files = new List<EmittedFile>(emitted);

        // 4a. Generate RequireShim helper class.
        EmittedFile requireShim = GenerateRequireShim();
        files.Add(requireShim);

        // 5. Emit project file and append.
        EmittedFile csproj = _projectEmitter.Emit(mapped, "net10.0");
        files.Add(csproj);

        // 6. Write all files to disk.
        foreach (EmittedFile file in files)
        {
            string fullPath = Path.Combine(options.OutDirectory, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Content);
        }

        Console.WriteLine($"Wrote {files.Count} file(s) to '{options.OutDirectory}'.");

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
