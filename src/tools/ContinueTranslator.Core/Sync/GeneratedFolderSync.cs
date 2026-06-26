using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContinueTranslator.Core.Emission;

namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Promotes translator-emitted files into the <c>Generated/</c> folder, skipping files that
/// have been hand-edited or that still contain stubs or raw TypeScript type leaks.
/// </summary>
internal static class GeneratedFolderSync
{
    private const string ManifestFileName = ".translator-manifest.json";

    /// <summary>
    /// Compares each file in <paramref name="emittedFiles"/> against the current
    /// <paramref name="generatedDirectory"/> contents and the manifest, then writes
    /// files that are safe to promote.
    /// </summary>
    /// <param name="emittedFiles">Files produced by the translator pipeline.</param>
    /// <param name="generatedDirectory">Absolute path to the <c>Generated/</c> folder.</param>
    public static SyncResult Sync(IReadOnlyList<EmittedFile> emittedFiles, string generatedDirectory)
    {
        ArgumentNullException.ThrowIfNull(emittedFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedDirectory);

        string manifestPath = Path.Combine(generatedDirectory, ManifestFileName);
        Dictionary<string, string> manifest = LoadManifest(manifestPath);

        int promoted = 0;
        int skippedManualEdit = 0;
        int skippedHasStubs = 0;

        foreach (EmittedFile file in emittedFiles)
        {
            string relativePath = file.RelativePath;
            string content = file.Content;
            string fullPath = Path.Combine(generatedDirectory, relativePath);

            // Check for hand-edits: compare the Generated/ copy's SHA-256 against the manifest entry.
            if (File.Exists(fullPath))
            {
                string existingContent = File.ReadAllText(fullPath);
                string existingHash = ComputeSha256(existingContent);

                if (manifest.TryGetValue(relativePath, out string? manifestHash) &&
                    !string.Equals(existingHash, manifestHash, StringComparison.OrdinalIgnoreCase))
                {
                    skippedManualEdit++;
                    continue;
                }
            }

            // Check for unresolved stubs or raw TS type leaks.
            if (HasStubs(content) || HasTsTypeLeaks(content))
            {
                skippedHasStubs++;
                continue;
            }

            // Safe to promote — write file and record hash in manifest.
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            manifest[relativePath] = ComputeSha256(content);
            promoted++;
        }

        if (promoted > 0)
            SaveManifest(manifest, manifestPath);

        return new SyncResult(promoted, skippedManualEdit, skippedHasStubs);
    }

    // -------------------------------------------------------------------------
    // Manifest helpers
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> LoadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void SaveManifest(Dictionary<string, string> manifest, string manifestPath)
    {
        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(manifestPath, json);
    }

    // -------------------------------------------------------------------------
    // SHA-256 helper
    // -------------------------------------------------------------------------

    private static string ComputeSha256(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    // -------------------------------------------------------------------------
    // Skip predicates
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="content"/> contains a <c>// TODO</c> stub marker
    /// or an emitter placeholder comment (<c>/* unknown:</c> / <c>/* untranslatable</c>).
    /// </summary>
    private static bool HasStubs(string content) =>
        content.Contains("// TODO", StringComparison.Ordinal) ||
        content.Contains("/* unknown:", StringComparison.Ordinal) ||
        content.Contains("/* untranslatable", StringComparison.Ordinal);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="content"/> contains raw TypeScript type names
    /// (generic open-bracket form or bare <c>any</c>/<c>undefined</c> identifiers).
    /// </summary>
    private static bool HasTsTypeLeaks(string content)
    {
        // Generic TS utility types followed by '<'
        ReadOnlySpan<string> genericTypes =
        [
            "Promise<", "Observable<", "ReadonlyArray<",
            "Partial<", "Required<", "Readonly<", "Record<",
            "Pick<", "Omit<", "Extract<", "Exclude<", "NonNullable<",
        ];

        foreach (string t in genericTypes)
        {
            if (content.Contains(t, StringComparison.Ordinal))
                return true;
        }

        // Bare 'any' or 'undefined' as standalone C# identifiers
        return Regex.IsMatch(content, @"\bany\b") ||
               Regex.IsMatch(content, @"\bundefined\b");
    }
}
