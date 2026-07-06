using System.Diagnostics;

namespace ContinueTranslator.Cli;

/// <summary>
/// Checks out a specific git tag in a repository and enumerates TypeScript source files
/// under the <c>core/</c> subdirectory, excluding test files, the <c>extensions</c> tree,
/// and the <c>gui</c> tree.
/// </summary>
internal sealed class RepoScanner
{
    /// <summary>
    /// Checks out <paramref name="tag"/> in <paramref name="repoPath"/> and returns all
    /// relevant <c>.ts</c> file paths under <c>&lt;repoPath&gt;/core/</c>.
    /// </summary>
    /// <param name="repoPath">Absolute path to the local git repository.</param>
    /// <param name="tag">Git tag or branch name to check out.</param>
    /// <returns>Filtered list of absolute <c>.ts</c> file paths.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>git checkout</c> exits with a non-zero code.
    /// </exception>
    public IReadOnlyList<string> CheckoutAndScan(string repoPath, string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        RunGitCheckout(repoPath, tag);
        return ScanCoreFiles(repoPath);
    }

    /// <summary>
    /// Two-pass scan optimization: first scans only whitelisted node_modules directories,
    /// then scans the rest of core/ excluding node_modules.
    /// This drastically reduces file enumeration time by processing node_modules separately
    /// per whitelist entry rather than scanning all files at once.
    /// </summary>
    /// <param name="repoPath">Absolute path to the local git repository.</param>
    /// <param name="tag">Git tag or branch name to check out.</param>
    /// <param name="nodeModulesPatterns">Directory patterns to scan in phase 1 (e.g., "core/node_modules/web-tree-sitter").</param>
    /// <returns>Tuple of (combined filtered list of .ts file paths from both phases sorted, set of Phase 1 file paths).</returns>
    public (IReadOnlyList<string> allFiles, IReadOnlySet<string> phase1Files) CheckoutAndScanTwoPass(string repoPath, string tag, IReadOnlyList<string> nodeModulesPatterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(nodeModulesPatterns);

        RunGitCheckout(repoPath, tag);

        var allFiles = new List<string>();
        var phase1FilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: scan whitelisted node_modules directories
        if (nodeModulesPatterns.Count > 0)
        {
            Console.WriteLine($"[SCAN] Phase 1: Scanning {nodeModulesPatterns.Count} whitelisted node_modules pattern(s)...");
            var nodeModulesFiles = ScanSpecificDirectories(repoPath, nodeModulesPatterns);
            allFiles.AddRange(nodeModulesFiles);
            foreach (var file in nodeModulesFiles)
            {
                phase1FilesSet.Add(file);
            }
            Console.WriteLine($"[SCAN] Phase 1: Found {nodeModulesFiles.Count} file(s) in node_modules.");
        }

        // Second pass: scan core/ excluding node_modules
        Console.WriteLine($"[SCAN] Phase 2: Scanning core/ excluding node_modules...");
        var sourceFiles = ScanCoreFilesExcludingNodeModules(repoPath);
        allFiles.AddRange(sourceFiles);
        Console.WriteLine($"[SCAN] Phase 2: Found {sourceFiles.Count} file(s) in source code.");

        return ([.. allFiles.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)], phase1FilesSet);
    }

    /// <summary>
    /// Scans specific directories under the repository root for TypeScript files.
    /// Used to scan whitelisted node_modules directories without scanning the entire core/.
    /// Does not run git checkout (assumes repo is already at the desired state).
    /// </summary>
    /// <param name="repoPath">Absolute path to the local git repository.</param>
    /// <param name="relativeDirPatterns">Relative directory paths (e.g., "core/node_modules/web-tree-sitter").</param>
    /// <returns>Filtered list of absolute .ts file paths in those directories.</returns>
    public IReadOnlyList<string> ScanSpecificDirectories(string repoPath, IEnumerable<string> relativeDirPatterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        ArgumentNullException.ThrowIfNull(relativeDirPatterns);

        var results = new List<string>();

        foreach (var pattern in relativeDirPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            // Convert pattern like "core/node_modules/web-tree-sitter" to absolute path
            string absolutePath = Path.Combine(repoPath, pattern.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(absolutePath))
            {
                Console.WriteLine($"[WARN] Directory not found: {absolutePath}");
                continue;
            }

            // Enumerate all .ts files in this directory (non-recursive for now, can be recursive)
            var files = Directory
                .EnumerateFiles(absolutePath, "*.ts", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

            results.AddRange(files);
        }

        return results;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void RunGitCheckout(string repoPath, string tag)
    {
        var psi = new ProcessStartInfo("git", $"checkout {tag}")
        {
            WorkingDirectory = repoPath,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");

        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git checkout {tag} failed with exit code {process.ExitCode}. stderr: {stderr}");
    }

    private static IReadOnlyList<string> ScanCoreFiles(string repoPath)
    {
        string coreDir = Path.Combine(repoPath, "core");

        if (!Directory.Exists(coreDir))
            throw new InvalidOperationException(
                $"Expected 'core' subdirectory not found under '{repoPath}'.");

        return [.. Directory
            .EnumerateFiles(coreDir, "*.ts", SearchOption.AllDirectories)
            .Where(IsIncluded)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Scans the core directory EXCLUDING node_modules subdirectories.
    /// Used for the source code pass after node_modules have been processed separately.
    /// </summary>
    private static IReadOnlyList<string> ScanCoreFilesExcludingNodeModules(string repoPath)
    {
        string coreDir = Path.Combine(repoPath, "core");

        if (!Directory.Exists(coreDir))
            throw new InvalidOperationException(
                $"Expected 'core' subdirectory not found under '{repoPath}'.");

        return [.. Directory
            .EnumerateFiles(coreDir, "*.ts", SearchOption.AllDirectories)
            .Where(p => !p.Contains("node_modules", StringComparison.OrdinalIgnoreCase) && IsIncluded(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    internal static bool IsIncluded(string path)
    {
        // Exclude test files by file name pattern.
        if (path.IndexOf(".test.ts", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (path.IndexOf(".vitest.ts", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (path.IndexOf("__tests__", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        // Normalize path to forward slashes for consistent matching on all OSes.
        string normalizedPath = path.Replace('\\', '/');

        // Exclude top-level "test" or "tests" directories within core/,
        // but not arbitrary directories named Test nested within a component structure.
        // Match patterns like "/core/test/" and "/core/tests/" but not "/RootPathContext/Test/".
        if (normalizedPath.IndexOf("/core/test/", StringComparison.InvariantCultureIgnoreCase) >= 0
            || normalizedPath.IndexOf("/core/tests/", StringComparison.InvariantCultureIgnoreCase) >= 0)
            return false;

        // Examine individual path segments for excluded directories.
        string[] segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        // Exclude specific directory names that indicate non-source content.
        foreach (string segment in segments)
        {
            if (segment.Equals("extensions", StringComparison.OrdinalIgnoreCase))
                return false;
            if (segment.Equals("gui", StringComparison.OrdinalIgnoreCase))
                return false;
            if (segment.Equals("vendor", StringComparison.OrdinalIgnoreCase))
                return false;
            if (segment.Equals("__tests__", StringComparison.OrdinalIgnoreCase))
                return false;
            if (segment.Equals("test", StringComparison.InvariantCultureIgnoreCase))
                return false;
        }

        return true;
    }
}
