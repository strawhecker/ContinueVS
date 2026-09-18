#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Version-retaining read snapshot store (gap80).
    ///
    /// Retains each read path's byte snapshots via ChangeBaseline (gap29_8_2 shape) so the
    /// future restore path already has them. On a repeated read it marks the earlier versions
    /// Deleted for the LLM (excluded at serialize time) while keeping them accessible.
    /// </summary>
    public class ToolCallSnapshotStore : IToolCallSnapshotStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<ChangeBaseline>> _versionsByPath =
            new Dictionary<string, List<ChangeBaseline>>(StringComparer.Ordinal);

        /// <inheritdoc />
        public ReadDedupDecision EvaluateRead(ToolCall toolCall, string retrievedContent)
        {
            lock (_gate)
            {
                var keyPath = new ReadDeduplicator().ExtractKeyPath(toolCall);
                if (keyPath == null)
                {
                    // Not a single-file read identity; treat as a plain read (no dedup).
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.FirstRead,
                        KeyPath = null,
                        Version = 0
                    };
                }

                if (!_versionsByPath.TryGetValue(keyPath, out var versions) || versions.Count == 0)
                {
                    // First read of this path — v1.
                    _versionsByPath[keyPath] = new List<ChangeBaseline>
                    {
                        new ChangeBaseline
                        {
                            FilePath = keyPath,
                            BaselineContent = retrievedContent ?? string.Empty,
                            CreatedAt = DateTime.UtcNow
                        }
                    };
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.FirstRead,
                        KeyPath = keyPath,
                        Version = 1
                    };
                }

                // Repeated read. Compare to the most recent retained version.
                var latest = versions[versions.Count - 1];
                if (string.Equals(latest.BaselineContent, retrievedContent ?? string.Empty, StringComparison.Ordinal))
                {
                    // Identical content -> duplicate. Soft-delete the prior version from the
                    // LLM payload; the retained bytes stay accessible (version retention).
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.DuplicateSoftDeleted,
                        KeyPath = keyPath,
                        Version = versions.Count
                    };
                }

                // Content differs -> retain as a new version; prior versions marked Deleted.
                versions.Add(new ChangeBaseline
                {
                    FilePath = keyPath,
                    BaselineContent = retrievedContent ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
                return new ReadDedupDecision
                {
                    Verdict = ReadDedupVerdict.NewVersionRetained,
                    KeyPath = keyPath,
                    Version = versions.Count
                };
            }
        }

        /// <inheritdoc />
        public void RecordSnapshot(string keyPath, string content)
        {
            if (string.IsNullOrEmpty(keyPath))
                return;

            lock (_gate)
            {
                if (!_versionsByPath.TryGetValue(keyPath, out var versions))
                {
                    versions = new List<ChangeBaseline>();
                    _versionsByPath[keyPath] = versions;
                }
                versions.Add(new ChangeBaseline
                {
                    FilePath = keyPath,
                    BaselineContent = content ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            lock (_gate)
            {
                _versionsByPath.Clear();
            }
        }
    }
}
