#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap80: Smart Tool Calls Management tests — deterministic own IDs, configurable
    /// loop limits, soft-delete tombstone, and version-retaining read dedup.
    /// </summary>
    public class ToolCallManagementGap80Tests
    {
        // ---------------------------------------------------------------
        // Step 2: Deterministic tool-call IDs (own IDs end-to-end)
        // ---------------------------------------------------------------

        [Fact]
        public void IdAllocator_FirstCall_UsesTimeOfDayBase()
        {
            var allocator = new ToolCallIdAllocator();
            var first = allocator.Next();

            Assert.StartsWith("tc-", first);
            Assert.Contains("-0000", first); // first call in a batch uses increment 0000
        }

        [Fact]
        public void IdAllocator_ParallelBatch_IncrementsUniqueIds()
        {
            var allocator = new ToolCallIdAllocator();
            var ids = new HashSet<string>();
            for (int i = 0; i < 10; i++)
            {
                Assert.True(ids.Add(allocator.Next()), $"id #{i} was duplicated");
            }
            Assert.Equal(10, ids.Count);
        }

        [Fact]
        public void IdAllocator_Reset_StartsNewBase()
        {
            var allocator = new ToolCallIdAllocator();
            var a = allocator.Next();
            allocator.Reset();
            var b = allocator.Next();
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void Aggregator_AssignsOwnId_OverwritingLlmRandomId()
        {
            var aggregator = new ToolCallAggregator(new ToolCallIdAllocator(), new ToolCallSnapshotStore(), new ReadDeduplicator());
            aggregator.AccumulateToolCallsText(
                "[{\"index\":0,\"id\":\"llm-random-abc\",\"function\":{\"name\":\"read_file\",\"arguments\":\"{\\\"filepath\\\":\\\"a.cs\\\"}\"}}]");

            Assert.True(aggregator.TryGetCompleteToolCalls(out var calls));
            Assert.NotNull(calls);
            var call = Assert.Single(calls!);
            Assert.NotNull(call.OwnId);
            // Own ID is the canonical correlation key, replacing the LLM random id.
            Assert.Equal(call.OwnId, call.Id);
            Assert.NotEqual("llm-random-abc", call.OwnId);
        }

        // ---------------------------------------------------------------
        // Step 3: Configurable tool-loop iteration limits
        // ---------------------------------------------------------------

        [Fact]
        public void UserSettings_Defaults_FailureGateAndRecursionDepth()
        {
            var defaults = UserSettings.GetDefaults();

            Assert.Equal(10, Convert.ToInt32(defaults[UserSettings.Agent_MaxToolFailureGate]));
            Assert.Equal(5, Convert.ToInt32(defaults[UserSettings.Agent_MaxToolRecursionDepth]));
        }

        [Fact]
        public void UserSettings_GetDefault_ReturnsValues()
        {
            Assert.Equal(10, Convert.ToInt32(UserSettings.GetDefault(UserSettings.Agent_MaxToolFailureGate)));
            Assert.Equal(5, Convert.ToInt32(UserSettings.GetDefault(UserSettings.Agent_MaxToolRecursionDepth)));
        }

        // ---------------------------------------------------------------
        // Step 4: Soft-delete tombstone primitive
        // ---------------------------------------------------------------

        [Fact]
        public void ChatMessage_SetsAndRetainsIsDeletedTombstone()
        {
            var msg = new ChatMessage { Role = ChatMessageRole.Tool, Content = "read result" };
            Assert.False(msg.IsDeleted);
            msg.IsDeleted = true;
            Assert.True(msg.IsDeleted);
            // Bytes/content remain retained even when soft-deleted.
            Assert.Equal("read result", msg.Content);
        }

        // ---------------------------------------------------------------
        // Step 5: Version-retaining read dedup
        // ---------------------------------------------------------------

        [Fact]
        public void ReadDeduplicator_MarksMutatingTools_NotPureRead()
        {
            var dedup = new ReadDeduplicator();
            Assert.False(dedup.IsPureReadTool("edit_file"));
            Assert.False(dedup.IsPureReadTool("run_terminal_command"));
            Assert.False(dedup.IsPureReadTool("write_file"));
            Assert.False(dedup.IsPureReadTool("single_find_and_replace"));
        }

        [Fact]
        public void ReadDeduplicator_MarksReadTools_PureRead()
        {
            var dedup = new ReadDeduplicator();
            Assert.True(dedup.IsPureReadTool("read_file"));
            Assert.True(dedup.IsPureReadTool("view_file"));
            Assert.True(dedup.IsPureReadTool("grep_search"));
        }

        [Fact]
        public void ReadDedup_FirstRead_StoresVersion1()
        {
            var store = new ToolCallSnapshotStore();
            var call = new ToolCall { Name = "read_file", Arguments = new Dictionary<string, object> { ["filepath"] = "a.cs" } };

            var decision = store.EvaluateRead(call, "content-v1");

            Assert.Equal(ReadDedupVerdict.FirstRead, decision.Verdict);
            Assert.Equal(1, decision.Version);
            Assert.False(decision.SuppressFromLlm);
        }

        [Fact]
        public void ReadDedup_IdenticalRepeat_MarksDuplicateSoftDeleted()
        {
            var store = new ToolCallSnapshotStore();
            var call = new ToolCall { Name = "read_file", Arguments = new Dictionary<string, object> { ["filepath"] = "a.cs" } };

            store.EvaluateRead(call, "same");
            var decision = store.EvaluateRead(call, "same");

            Assert.Equal(ReadDedupVerdict.DuplicateSoftDeleted, decision.Verdict);
            Assert.True(decision.SuppressFromLlm);
        }

        [Fact]
        public void ReadDedup_ChangedContent_RetainsNewVersion_BesidePrior()
        {
            var store = new ToolCallSnapshotStore();
            var call = new ToolCall { Name = "read_file", Arguments = new Dictionary<string, object> { ["filepath"] = "b.cs" } };

            var v1 = store.EvaluateRead(call, "old");
            var v2 = store.EvaluateRead(call, "new");

            // New version retained; earlier version marked Deleted for the LLM.
            Assert.Equal(ReadDedupVerdict.NewVersionRetained, v2.Verdict);
            Assert.Equal(2, v2.Version);
            Assert.True(v2.SuppressFromLlm);

            // Both versions remain retained/accessible (version retention, not hard remove).
            var snapshots = GetSnapshots(store, "b.cs");
            Assert.Equal(2, snapshots.Count);
            Assert.Equal("old", snapshots[0].BaselineContent);
            Assert.Equal("new", snapshots[1].BaselineContent);
        }

        [Fact]
        public void ReadDedup_Reset_ClearsRetainedVersions()
        {
            var store = new ToolCallSnapshotStore();
            var call = new ToolCall { Name = "read_file", Arguments = new Dictionary<string, object> { ["filepath"] = "c.cs" } };

            store.EvaluateRead(call, "x");
            store.Reset();

            var decision = store.EvaluateRead(call, "x");
            Assert.Equal(ReadDedupVerdict.FirstRead, decision.Verdict);
            Assert.Equal(1, decision.Version);
        }

        // ---------------------------------------------------------------
        // Step 6: Buffer ownership (no double-clear)
        // ---------------------------------------------------------------

        [Fact]
        public void Aggregator_Clear_EmptiesBuffer()
        {
            var aggregator = new ToolCallAggregator();
            aggregator.AccumulateToolCallsText(
                "[{\"index\":0,\"function\":{\"name\":\"read_file\",\"arguments\":\"{}\"}}]");
            Assert.True(aggregator.TryGetCompleteToolCalls(out _));
            aggregator.Clear();
            Assert.False(aggregator.TryGetCompleteToolCalls(out _));
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static List<ChangeBaseline> GetSnapshots(ToolCallSnapshotStore store, string keyPath)
        {
            // gap80_1: store is keyed by (path, coverage). A read_file call is FULL coverage,
            // so the map key is "path|FULL".
            var field = typeof(ToolCallSnapshotStore)
                .GetField("_versionsByCoverage", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field); // _versionsByCoverage should exist
            var dict = (Dictionary<string, List<ChangeBaseline>>?)field!.GetValue(store);
            Assert.NotNull(dict);
            Assert.True(dict!.TryGetValue(keyPath + "|FULL", out var list));
            return list!;
        }
    }
}
