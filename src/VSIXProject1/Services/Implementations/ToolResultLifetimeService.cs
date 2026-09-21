#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// gap80_1: Tool-result lifetime engine implementing the coverage-aware supersession,
    /// mutation-invalidates-priors, directory-snapshot staleness, and failed-mutation pivot
    /// rules. Mutates <see cref="ChatMessage.FreshnessState"/> / <see cref="ChatMessage.IsDeleted"/>
    /// in place on the collected tool results.
    ///
    /// Governing rule: delete only what has a strict successor or explicit user intent. Never
    /// delete on time alone; staleness without a successor is SIGNALED (Stale marker), never
    /// silently removed.
    /// </summary>
    public class ToolResultLifetimeService : IToolResultLifetimeService
    {
        private readonly IReadDeduplicator _readDeduplicator;

        public ToolResultLifetimeService(IReadDeduplicator? readDeduplicator = null)
        {
            _readDeduplicator = readDeduplicator ?? new ReadDeduplicator();
        }

        /// <inheritdoc />
        public void ApplyLifetime(List<ChatMessage> toolResults)
        {
            if (toolResults == null)
                throw new ArgumentNullException(nameof(toolResults));

            // Pass 1: supersede reads that a FULL read / identical-range repeat replaced.
            ApplyCoverageReadSupersession(toolResults);

            // Pass 2: mutations invalidate the PRECEDING reads of the same path.
            ApplyMutationInvalidatesPriors(toolResults);

            // Pass 3: directory/glob/search snapshots are marked STALE (visible) when a same-scope
            // mutation occurred later in the batch. No silent removal.
            ApplyDirectorySnapshotStaleness(toolResults);

            // Pass 4: failed-mutation pivot — never tombstone the failure signal; only supersede
            // the successful-looking evidence a fallback replaced.
            ApplyFailedMutationPivot(toolResults);
        }

        /// <summary>
        /// Coverage-aware read supersession (gap80_1 rule 1).
        /// Keying is now (path, coverage) at the snapshot store; here we apply the decision to
        /// the collected result messages: a FULL read supersedes every prior read of the path,
        /// a RANGE read supersedes only the identical prior range, and disjoint ranges coexist.
        /// </summary>
        private void ApplyCoverageReadSupersession(List<ChatMessage> results)
        {
            // Supersession is directional and ordered: a read supersedes only PRIOR reads of the
            // path it covers, never later ones (a full read supersedes prior partials/fulls; a
            // range read supersedes only the identical prior range). Iterate by index so we only
            // mark entries BEHIND the current result.
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result.Role != ChatMessageRole.Tool || result.IsDeleted)
                    continue;

                // Only read results carry a CoverageKey. Directory snapshots handled in pass 3.
                if (string.IsNullOrEmpty(result.CoverageKey))
                    continue;

                var (path, coverage) = ParseCoverageKey(result.CoverageKey!);
                if (path == null)
                    continue;

                bool isFull = string.Equals(coverage, "FULL", StringComparison.Ordinal);

                // Only supersede PRIOR results of the same path.
                for (int j = 0; j < i; j++)
                {
                    var prior = results[j];
                    if (prior.Role != ChatMessageRole.Tool || prior.IsDeleted)
                        continue;
                    if (string.IsNullOrEmpty(prior.CoverageKey))
                        continue;

                    var (priorPath, priorCoverage) = ParseCoverageKey(prior.CoverageKey!);
                    if (priorPath == null || !string.Equals(priorPath, path, StringComparison.Ordinal))
                        continue;

                    bool supersedes;
                    if (isFull)
                    {
                        // A full read supersedes EVERY prior read of the path.
                        supersedes = true;
                    }
                    else
                    {
                        // A range read supersedes only the identical range. Disjoint ranges coexist.
                        supersedes = string.Equals(priorCoverage, coverage, StringComparison.Ordinal);
                    }

                    if (supersedes)
                    {
                        // Superseded -> behave like a tombstone for the LLM payload; bytes retained.
                        prior.FreshnessState = FreshnessState.Superseded;
                    }
                }
            }
        }

        /// <summary>
        /// Mutation-invalidates-priors (gap80_1 rule 2).
        /// A mutating tool on path P tombstones the PRECEDING reads of P. The mutation's own
        /// result message stays visible as the freshness signal. A FAILED mutation does NOT
        /// invalidate prior reads (kept visible per rule 4).
        /// </summary>
        private void ApplyMutationInvalidatesPriors(List<ChatMessage> results)
        {
            foreach (var result in results)
            {
                if (result.Role != ChatMessageRole.Tool || result.IsDeleted)
                    continue;
                if (string.IsNullOrEmpty(result.MutationTargetPath))
                    continue;

                var mutatedPath = result.MutationTargetPath!;
                bool mutationFailed = IsFailureSignal(result);

                foreach (var other in results)
                {
                    if (ReferenceEquals(other, result))
                        continue;
                    if (other.Role != ChatMessageRole.Tool || other.IsDeleted)
                        continue;
                    if (string.IsNullOrEmpty(other.CoverageKey))
                        continue;

                    var (otherPath, _) = ParseCoverageKey(other.CoverageKey!);
                    if (otherPath == null || !string.Equals(otherPath, mutatedPath, StringComparison.Ordinal))
                        continue;

                    // A FAILED mutation does NOT invalidate prior reads.
                    if (!mutationFailed)
                    {
                        other.FreshnessState = FreshnessState.Superseded;
                    }
                }
            }
        }

        /// <summary>
        /// Directory-listing / glob / search lifetime (gap80_1 rule 3).
        /// Snapshot tools have no natural single-file successor. When a mutation occurs anywhere
        /// in the batch, a prior snapshot is marked STALE (VISIBLE) — never silently removed.
        /// </summary>
        private void ApplyDirectorySnapshotStaleness(List<ChatMessage> results)
        {
            bool anyMutation = results.Any(r =>
                r.Role == ChatMessageRole.Tool &&
                !r.IsDeleted &&
                _readDeduplicator.IsMutatingTool(r.ToolName ?? string.Empty));

            if (!anyMutation)
                return;

            foreach (var result in results)
            {
                if (result.Role != ChatMessageRole.Tool || result.IsDeleted)
                    continue;
                if (!_readDeduplicator.IsDirectorySnapshotTool(result.ToolName ?? string.Empty))
                    continue;

                // Conservatively mark STALE (visible), never removed — satisfies "never silent".
                result.FreshnessState = FreshnessState.Stale;
                AppendStaleAnnotation(result);
            }
        }

        /// <summary>
        /// Failed-mutation pivot supersession (gap80_1 rule 4).
        /// A failure result is an invalidation signal — keep it visible (never tombstone).
        /// </summary>
        private void ApplyFailedMutationPivot(List<ChatMessage> results)
        {
            foreach (var result in results)
            {
                if (result.Role != ChatMessageRole.Tool)
                    continue;
                if (IsFailureSignal(result))
                {
                    // A failure signal is the invalidation note the LLM must see; never hide it.
                    result.FreshnessState = FreshnessState.Fresh;
                }
            }
        }

        /// <summary>
        /// Returns true when the result message is a failure/invalidation signal.
        /// </summary>
        private static bool IsFailureSignal(ChatMessage result)
        {
            if (result.InvocationStatus == ToolInvocationStatus.Failed)
                return true;
            if (string.IsNullOrEmpty(result.Content))
                return false;
            return result.Content.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                   result.Content.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                   result.Content.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                   result.Content.Contains("rejected", StringComparison.OrdinalIgnoreCase) ||
                   result.Content.Contains("Policy Denied", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Appends a visible "re-read to confirm" annotation so the LLM sees staleness rather
        /// than an absence (never silent).
        /// </summary>
        private static void AppendStaleAnnotation(ChatMessage result)
        {
            if (string.IsNullOrEmpty(result.Content))
            {
                result.Content = "[STALE] Listing/search snapshot may be outdated — re-read to confirm.";
                return;
            }
            if (!result.Content.Contains("[STALE]", StringComparison.Ordinal))
            {
                result.Content = $"{result.Content}\n\n[STALE] This listing/search snapshot may be outdated because files changed — re-read to confirm current state.";
            }
        }

        /// <summary>
        /// Parses a CoverageKey (path|FULL or path|RANGE(start,end)) into (path, coverage).
        /// </summary>
        private static (string? path, string? coverage) ParseCoverageKey(string coverageKey)
        {
            if (string.IsNullOrEmpty(coverageKey))
                return (null, null);
            int idx = coverageKey.IndexOf('|');
            if (idx < 0)
                return (coverageKey, "FULL");
            return (coverageKey.Substring(0, idx), coverageKey.Substring(idx + 1));
        }
    }
}
