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
