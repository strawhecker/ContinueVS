using System.Text.Json.Serialization;

namespace ContinueTranslator.Core.Sync;

/// <summary>
/// Represents a translator-emitted file that was rejected and not promoted to Generated/.
/// Includes metadata about why the file was rejected.
/// </summary>
internal sealed class RejectedFile
{
    /// <summary>
    /// Relative path from the output root, e.g., "Protocol/Llm.cs".
    /// </summary>
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// The full content of the rejected file.
    /// </summary>
    [JsonIgnore]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the file content at rejection time.
    /// </summary>
    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Reasons why this file was rejected (may contain multiple).
    /// </summary>
    [JsonPropertyName("reasons")]
    public RejectionReason[] Reasons { get; set; } = [];

    /// <summary>
    /// Human-readable summary of rejection reasons, e.g., "TODO stub, Promise< type".
    /// </summary>
    [JsonPropertyName("reasonSummary")]
    public string ReasonSummary { get; set; } = string.Empty;

    /// <summary>
    /// ISO 8601 timestamp when the file was rejected.
    /// </summary>
    [JsonPropertyName("rejectedAt")]
    public string RejectedAt { get; set; } = string.Empty;

    public RejectedFile()
    {
    }

    public RejectedFile(string relativePath, string content, RejectionReason[] reasons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(reasons);

        RelativePath = relativePath;
        Content = content;
        Reasons = reasons;
        ContentHash = ComputeSha256(content);
        ReasonSummary = string.Join(", ", reasons.Select(r => r.GetShortLabel()).Distinct());
        RejectedAt = DateTime.UtcNow.ToString("O");
    }

    private static string ComputeSha256(string content)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Serializable metadata for a rejected file (without content).
/// Written as a sidecar .rejection-metadata.json alongside the .cs file.
/// </summary>
internal sealed class RejectedFileMetadata
{
    [JsonPropertyName("relativePath")]
    public string RelativePath { get; set; } = string.Empty;

    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;

    [JsonPropertyName("reasons")]
    public RejectionReason[] Reasons { get; set; } = [];

    [JsonPropertyName("reasonSummary")]
    public string ReasonSummary { get; set; } = string.Empty;

    [JsonPropertyName("rejectedAt")]
    public string RejectedAt { get; set; } = string.Empty;

    public static RejectedFileMetadata FromRejectedFile(RejectedFile file) =>
        new()
        {
            RelativePath = file.RelativePath,
            ContentHash = file.ContentHash,
            Reasons = file.Reasons,
            ReasonSummary = file.ReasonSummary,
            RejectedAt = file.RejectedAt
        };
}
