using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContinueTranslator.Core.Emission;

namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Promotes translator-emitted files into the <c>Generated/</c> folder, skipping files that
/// have been hand-edited or that still contain stubs or raw TypeScript type leaks.
/// Rejected files are copied to the <c>rejected/</c> folder for Phase 2 manual review.
/// </summary>
internal static class GeneratedFolderSync
{
    private const string ManifestFileName = ".translator-manifest.json";
    private const string RejectionMetadataSuffix = ".rejection-metadata.json";

    /// <summary>
    /// Compares each file in <paramref name="emittedFiles"/> against the current
    /// <paramref name="generatedDirectory"/> contents and the manifest, then writes
    /// files that are safe to promote. Rejected files are written to <paramref name="rejectedDirectory"/>.
    /// </summary>
    /// <param name="emittedFiles">Files produced by the translator pipeline.</param>
    /// <param name="generatedDirectory">Absolute path to the <c>Generated/</c> folder.</param>
    /// <param name="rejectedDirectory">Absolute path to the <c>rejected/</c> folder for Phase 2 work queue.</param>
    public static SyncResult Sync(
        IReadOnlyList<EmittedFile> emittedFiles,
        string generatedDirectory,
        string rejectedDirectory)
    {
        ArgumentNullException.ThrowIfNull(emittedFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedDirectory);

        string manifestPath = Path.Combine(generatedDirectory, ManifestFileName);
        Dictionary<string, string> manifest = LoadManifest(manifestPath);

        var result = new SyncResult(0, 0);

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
                    result.IncrementSkippedManualEdit();
                    continue;
                }
            }

            // Check for unresolved stubs or raw TS type leaks.
            var rejectionReasons = DetectRejectionReasons(content);
            if (rejectionReasons.Count > 0)
            {
                WriteRejectedFile(file.RelativePath, content, rejectionReasons, rejectedDirectory, result);
                continue;
            }

            // Safe to promote — write file and record hash in manifest.
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            manifest[relativePath] = ComputeSha256(content);
            result.IncrementPromoted();
        }

        if (result.Promoted > 0)
            SaveManifest(manifest, manifestPath);

        return result;
    }

    // -------------------------------------------------------------------------
    // Rejection detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Detects all rejection reasons for a given file content.
    /// Returns an empty list if the file is safe to promote.
    /// </summary>
    private static List<RejectionReason> DetectRejectionReasons(string content)
    {
        var reasons = new List<RejectionReason>();

        // Check for TODO/unknown stubs
        if (HasTodoStubs(content))
        {
            reasons.Add(RejectionReason.HasTodoStub);
        }

        // Check for generic TypeScript types
        if (HasGenericTsTypes(content))
        {
            reasons.Add(RejectionReason.HasGenericTsTypes);
        }

        // Check for bare 'any'
        if (HasAnyType(content))
        {
            reasons.Add(RejectionReason.HasAnyType);
        }

        // Check for bare 'undefined'
        if (HasUndefinedType(content))
        {
            reasons.Add(RejectionReason.HasUndefinedType);
        }

        return reasons;
    }

    private static bool HasTodoStubs(string content) =>
        content.Contains("// TODO", StringComparison.Ordinal) ||
        content.Contains("// @ct:todo", StringComparison.Ordinal) ||
        content.Contains("/* unknown:", StringComparison.Ordinal) ||
        content.Contains("/* untranslatable", StringComparison.Ordinal);

    private static bool HasGenericTsTypes(string content)
    {
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

        return false;
    }

    private static bool HasAnyType(string content) =>
        Regex.IsMatch(content, @"\bany\b");

    private static bool HasUndefinedType(string content) =>
        Regex.IsMatch(content, @"\bundefined\b");

    // -------------------------------------------------------------------------
    // Rejected file writing
    // -------------------------------------------------------------------------

    private static void WriteRejectedFile(
        string relativePath,
        string content,
        List<RejectionReason> reasons,
        string rejectedDirectory,
        SyncResult result)
    {
        var rejectedFile = new RejectedFile(relativePath, content, reasons.ToArray());

        // Write the .cs file
        string rejectedPath = Path.Combine(rejectedDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(rejectedPath)!);
        File.WriteAllText(rejectedPath, content);

        // Write the .rejection-metadata.json sidecar
        string metadataPath = rejectedPath + RejectionMetadataSuffix;
        var metadata = RejectedFileMetadata.FromRejectedFile(rejectedFile);
        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metadataPath, json);

        result.AddRejectedFile(rejectedFile);
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
}
