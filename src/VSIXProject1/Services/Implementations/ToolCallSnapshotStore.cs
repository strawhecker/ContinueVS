#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Version-retaining read snapshot store (gap80 / gap80_1).
    ///
    /// Retains each read's byte snapshots via ChangeBaseline (gap29_8_2 shape) so the
    /// future restore path already has them. On a repeated read it marks the earlier versions
    /// Deleted for the LLM (excluded at serialize time) while keeping them accessible.
    ///
    /// gap80_1: keys by (path, coverage) instead of bare path so coverage-aware supersession
    /// holds: a FULL read supersedes every prior read of the path, while a RANGE read supersedes
    /// only the identical prior range (disjoint ranges coexist as cumulative evidence).
    /// </summary>
    public class ToolCallSnapshotStore : IToolCallSnapshotStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<ChangeBaseline>> _versionsByCoverage =
            new Dictionary<string, List<ChangeBaseline>>(StringComparer.Ordinal);
        private readonly ReadDeduplicator _readDeduplicator = new ReadDeduplicator();

        /// <inheritdoc />
        public ReadDedupDecision EvaluateRead(ToolCall toolCall, string retrievedContent)
        {
            lock (_gate)
            {
                var keyPath = _readDeduplicator.ExtractKeyPath(toolCall);
                if (keyPath == null)
                {
                    // Not a single-file read identity; treat as a plain read (no dedup).
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.FirstRead,
                        KeyPath = null,
                        Coverage = _readDeduplicator.ExtractCoverage(toolCall),
                        Version = 0
                    };
                }

                var coverage = _readDeduplicator.ExtractCoverage(toolCall);
                var key = BuildCoverageKey(keyPath, coverage);

                if (!_versionsByCoverage.TryGetValue(key, out var versions) || versions.Count == 0)
                {
                    // First read of this (path, coverage) — v1.
                    _versionsByCoverage[key] = new List<ChangeBaseline>
                    {
                        new ChangeBaseline
                        {
                            FilePath = keyPath,
                            BaselineContent = retrievedContent ?? string.Empty,
                            CreatedAt = DateTime.Now
                        }
                    };
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.FirstRead,
                        KeyPath = keyPath,
                        Coverage = coverage,
                        Version = 1
                    };
                }

                // Repeated read of the same (path, coverage). Compare to the most recent retained version.
                var latest = versions[versions.Count - 1];
                if (string.Equals(latest.BaselineContent, retrievedContent ?? string.Empty, StringComparison.Ordinal))
                {
                    // Identical content -> duplicate. Soft-delete the prior version from the
                    // LLM payload; the retained bytes stay accessible (version retention).
                    return new ReadDedupDecision
                    {
                        Verdict = ReadDedupVerdict.DuplicateSoftDeleted,
                        KeyPath = keyPath,
                        Coverage = coverage,
                        Version = versions.Count
                    };
                }

                // Content differs -> retain as a new version; prior versions marked Deleted.
                versions.Add(new ChangeBaseline
                {
                    FilePath = keyPath,
                    BaselineContent = retrievedContent ?? string.Empty,
                    CreatedAt = DateTime.Now
                });
                return new ReadDedupDecision
                {
                    Verdict = ReadDedupVerdict.NewVersionRetained,
                    KeyPath = keyPath,
                    Coverage = coverage,
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
                if (!_versionsByCoverage.TryGetValue(keyPath, out var versions))
                {
                    versions = new List<ChangeBaseline>();
                    _versionsByCoverage[keyPath] = versions;
                }
                versions.Add(new ChangeBaseline
                {
                    FilePath = keyPath,
                    BaselineContent = content ?? string.Empty,
                    CreatedAt = DateTime.Now
                });
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            lock (_gate)
            {
                _versionsByCoverage.Clear();
            }
        }

        /// <summary>
        /// gap80_1: Builds the (path, coverage) dictionary key. Coverage is normalized so
        /// identical range reads key identically.
        /// </summary>
        private static string BuildCoverageKey(string keyPath, string coverage)
        {
            return keyPath.Replace('\\', '/') + "|" + (coverage ?? "FULL");
        }
    }
}
