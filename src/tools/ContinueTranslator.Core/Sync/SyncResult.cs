using System.Text;

namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Results produced by <see cref="GeneratedFolderSync.Sync"/> after a single promotion pass.
/// </summary>
internal sealed class SyncResult
{
    /// <summary>Number of files written to the Generated/ folder.</summary>
    public int Promoted { get; private set; }

    /// <summary>Number of files skipped because the Generated/ copy was hand-edited.</summary>
    public int SkippedManualEdit { get; private set; }

    /// <summary>Rejected files grouped by rejection reason.</summary>
    public Dictionary<RejectionReason, List<RejectedFile>> RejectedByReason { get; } =
        new();

    /// <summary>Total number of rejected files.</summary>
    public int TotalRejected => RejectedByReason.Values.Sum(list => list.Count);

    public SyncResult(int promoted, int skippedManualEdit)
    {
        Promoted = promoted;
        SkippedManualEdit = skippedManualEdit;
    }

    /// <summary>Increments the promoted file count.</summary>
    public void IncrementPromoted() => Promoted++;

    /// <summary>Increments the skipped manual edit count.</summary>
    public void IncrementSkippedManualEdit() => SkippedManualEdit++;

    public void AddRejectedFile(RejectedFile rejectedFile)
    {
        ArgumentNullException.ThrowIfNull(rejectedFile);

        // Add to each reason bucket
        foreach (var reason in rejectedFile.Reasons)
        {
            if (!RejectedByReason.ContainsKey(reason))
            {
                RejectedByReason[reason] = [];
            }
            RejectedByReason[reason].Add(rejectedFile);
        }
    }

    /// <summary>
    /// Generates a formatted completion report suitable for CLI output.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=== PHASE 1 COMPLETION REPORT ===");
        sb.AppendLine();

        int emitted = Promoted + SkippedManualEdit + TotalRejected;
        sb.AppendLine($"Emitted:        {emitted,3} files");
        sb.AppendLine($"Promoted:       {Promoted,3} → src/VSIXProject1/Generated/");
        sb.AppendLine($"Hand-edits:     {SkippedManualEdit,3} (skipped, unchanged)");
        sb.AppendLine();

        if (TotalRejected > 0)
        {
            sb.AppendLine($"Rejected:       {TotalRejected,3} → src/rejected/");
            sb.AppendLine();

            foreach (var (reason, files) in RejectedByReason.OrderByDescending(kvp => kvp.Value.Count))
            {
                sb.AppendLine($"  • {reason.GetDescription(),-50} {files.Count,2}");

                // Show up to 3 example files
                foreach (var file in files.Take(3))
                {
                    sb.AppendLine($"      - {file.RelativePath}");
                }

                if (files.Count > 3)
                {
                    sb.AppendLine($"      - ... and {files.Count - 3} more");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Next steps:");
            sb.AppendLine("  1. Review files in src/rejected/");
            sb.AppendLine("  2. Check .rejection-metadata.json for specific issues");
            sb.AppendLine("  3. Hand-edit or improve translator mappings");
            sb.AppendLine("  4. Re-run translator with updated mappings");
        }
        else
        {
            sb.AppendLine("Status: ALL FILES PROMOTED ✓");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
