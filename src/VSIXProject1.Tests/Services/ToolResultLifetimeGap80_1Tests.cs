#nullable enable

using System.Collections.Generic;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap80_1: Tool-Result Lifetime &amp; Coverage Semantics tests — coverage-aware read
    /// supersession, mutation-invalidates-priors, directory-snapshot staleness (visible STALE),
    /// failed-mutation pivot, and the three boundary cases in the spec Notes.
    /// </summary>
    public class ToolResultLifetimeGap80_1Tests
    {
        // ---------------------------------------------------------------
        // Helpers: build tagged tool-result messages the way ChatPageViewModel does.
        // ---------------------------------------------------------------

        private static ChatMessage MakeRead(string path, string coverage, string content = "file-content")
        {
            return new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = "read_file",
                Content = content,
                InvocationStatus = ToolInvocationStatus.Complete,
                CoverageKey = path.Replace('\\', '/') + "|" + coverage
            };
        }

        private static ChatMessage MakeRangeRead(string path, int start, int end, string content = "range-content")
        {
            return new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = "read_file_range",
                Content = content,
                InvocationStatus = ToolInvocationStatus.Complete,
                CoverageKey = path.Replace('\\', '/') + $"|RANGE({start},{end})"
            };
        }

        private static ChatMessage MakeMutation(string path, bool failed = false, string toolName = "edit_file")
        {
            return new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = toolName,
                Content = failed ? "Error: oldText not found" : "File edited",
                InvocationStatus = failed ? ToolInvocationStatus.Failed : ToolInvocationStatus.Complete,
                MutationTargetPath = path.Replace('\\', '/')
            };
        }

        private static ChatMessage MakeDirectorySnapshot(string toolName = "ls", string content = "src/\nbin/\n")
        {
            return new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = toolName,
                Content = content,
                InvocationStatus = ToolInvocationStatus.Complete
            };
        }

        // ---------------------------------------------------------------
        // 1. Coverage-aware read supersession
        // ---------------------------------------------------------------

        [Fact]
        public void DisjointRanges_Coexist_BothRemainFresh()
        {
            var svc = new ToolResultLifetimeService();
            var r1 = MakeRangeRead("a.cs", 1, 50);
            var r2 = MakeRangeRead("a.cs", 51, 100);
            var results = new List<ChatMessage> { r1, r2 };

            svc.ApplyLifetime(results);

            // Disjoint ranges are cumulative evidence — neither is superseded.
            Assert.Equal(FreshnessState.Fresh, r1.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, r2.FreshnessState);
            Assert.False(r1.IsDeleted);
            Assert.False(r2.IsDeleted);
        }

        [Fact]
        public void IdenticalRangeRepeat_MarksPriorSuperseded()
        {
            var svc = new ToolResultLifetimeService();
            var r1 = MakeRangeRead("b.cs", 1, 50);
            var r2 = MakeRangeRead("b.cs", 1, 50, "re-read-content");
            var results = new List<ChatMessage> { r1, r2 };

            svc.ApplyLifetime(results);

            // Only an identical repeat is a duplicate -> prior is superseded (excluded at serialize).
            Assert.Equal(FreshnessState.Superseded, r1.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, r2.FreshnessState);
        }

        [Fact]
        public void PartialThenFull_SupersedesPartial()
        {
            var svc = new ToolResultLifetimeService();
            var partial = MakeRangeRead("c.cs", 1, 50);
            var full = MakeRead("c.cs", "FULL", "whole-file");
            var results = new List<ChatMessage> { partial, full };

            svc.ApplyLifetime(results);

            // FULL read supersedes every prior read of the path (any partial + any full).
            Assert.Equal(FreshnessState.Superseded, partial.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, full.FreshnessState);
        }

        [Fact]
        public void FullTwice_MarksPriorFullSuperseded()
        {
            var svc = new ToolResultLifetimeService();
            var full1 = MakeRead("d.cs", "FULL", "v1");
            var full2 = MakeRead("d.cs", "FULL", "v2");
            var results = new List<ChatMessage> { full1, full2 };

            svc.ApplyLifetime(results);

            Assert.Equal(FreshnessState.Superseded, full1.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, full2.FreshnessState);
        }

        // ---------------------------------------------------------------
        // 2. Mutation-invalidates-priors
        // ---------------------------------------------------------------

        [Fact]
        public void Mutation_Tombstones_PrecedingReadOfSamePath()
        {
            var svc = new ToolResultLifetimeService();
            var read = MakeRead("e.cs", "FULL", "old-content");
            var mutation = MakeMutation("e.cs");
            var results = new List<ChatMessage> { read, mutation };

            svc.ApplyLifetime(results);

            // Mutation invalidates the preceding read; the mutation result stays visible.
            Assert.Equal(FreshnessState.Superseded, read.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, mutation.FreshnessState);
            Assert.False(mutation.IsDeleted);
        }

        [Fact]
        public void Mutation_DoesNotTombstone_UnrelatedPath()
        {
            var svc = new ToolResultLifetimeService();
            var readA = MakeRead("a.cs", "FULL");
            var mutationB = MakeMutation("b.cs");
            var results = new List<ChatMessage> { readA, mutationB };

            svc.ApplyLifetime(results);

            Assert.Equal(FreshnessState.Fresh, readA.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, mutationB.FreshnessState);
        }

        [Fact]
        public void FailedMutation_DoesNotTombstone_PrecedingRead()
        {
            var svc = new ToolResultLifetimeService();
            var read = MakeRead("f.cs", "FULL", "content");
            var failedMutation = MakeMutation("f.cs", failed: true);
            var results = new List<ChatMessage> { read, failedMutation };

            svc.ApplyLifetime(results);

            // A FAILED mutation is an invalidation signal to keep visible — it does NOT invalidate
            // the prior read (no successor was created).
            Assert.Equal(FreshnessState.Fresh, read.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, failedMutation.FreshnessState);
            Assert.False(failedMutation.IsDeleted);
        }

        // ---------------------------------------------------------------
        // 3. Directory-listing / glob / search lifetime (visible STALE)
        // ---------------------------------------------------------------

        [Fact]
        public void DirectorySnapshot_AfterMutation_MarkedStale_VisibleAnnotation()
        {
            var svc = new ToolResultLifetimeService();
            var snapshot = MakeDirectorySnapshot("ls");
            var mutation = MakeMutation("src/a.cs", toolName: "edit_file");
            var results = new List<ChatMessage> { snapshot, mutation };

            svc.ApplyLifetime(results);

            // Snapshot is marked STALE but NOT removed — stays in the payload (visible) so the
            // model re-reads to confirm. Never silent time-based expiry.
            Assert.Equal(FreshnessState.Stale, snapshot.FreshnessState);
            Assert.False(snapshot.IsDeleted);
            Assert.Contains("[STALE]", snapshot.Content);
            Assert.True(snapshot.Content.Contains("re-read to confirm", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void DirectorySnapshot_WithoutMutation_StaysFresh()
        {
            var svc = new ToolResultLifetimeService();
            var snapshot1 = MakeDirectorySnapshot("file_glob_search");
            var snapshot2 = MakeDirectorySnapshot("search_codebase");
            var results = new List<ChatMessage> { snapshot1, snapshot2 };

            svc.ApplyLifetime(results);

            Assert.Equal(FreshnessState.Fresh, snapshot1.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, snapshot2.FreshnessState);
            Assert.DoesNotContain("[STALE]", snapshot1.Content);
        }

        // ---------------------------------------------------------------
        // 4. Failed-mutation pivot supersession
        // ---------------------------------------------------------------

        [Fact]
        public void FailureSignal_IsNeverSuperseded()
        {
            var svc = new ToolResultLifetimeService();
            var read = MakeRead("g.cs", "FULL", "evidence");
            var failure = new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                ToolName = "single_find_and_replace",
                Content = "Error: oldText not found in g.cs",
                InvocationStatus = ToolInvocationStatus.Failed,
                MutationTargetPath = "g.cs"
            };
            var fallback = MakeMutation("g.cs", failed: false, toolName: "write_file");
            var results = new List<ChatMessage> { read, failure, fallback };

            svc.ApplyLifetime(results);

            // The failure signal must STAY visible (not tombstoned), even though a fallback
            // superseded the evidence the failed approach was trying to replace.
            Assert.Equal(FreshnessState.Fresh, failure.FreshnessState);
            Assert.False(failure.IsDeleted);
            // The superseded evidence (old read) is tombstoned by the successful fallback; the
            // fallback result stays visible.
            Assert.Equal(FreshnessState.Superseded, read.FreshnessState);
            Assert.Equal(FreshnessState.Fresh, fallback.FreshnessState);
        }

        // ---------------------------------------------------------------
        // 5. No silent time-based expiry (governing rule)
        // ---------------------------------------------------------------

        [Fact]
        public void NoTimeBasedExpiry_StalenessIsNeverSilent()
        {
            var svc = new ToolResultLifetimeService();
            // Snapshot with no successor and no mutation: must NOT be silently removed.
            var snapshot = MakeDirectorySnapshot("grep_search");
            var results = new List<ChatMessage> { snapshot };

            svc.ApplyLifetime(results);

            Assert.False(snapshot.IsDeleted);
            // No mutation -> no STALE marker either, but crucially never deleted unseen.
            Assert.Equal(FreshnessState.Fresh, snapshot.FreshnessState);
        }
    }
}
