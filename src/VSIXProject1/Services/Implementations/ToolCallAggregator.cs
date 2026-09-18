#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IToolCallAggregator that buffers raw JSON text fragments and parses when complete.
    /// gap80: assigns deterministic own IDs and routes pure-read results through the version-retention store.
    /// </summary>
    public class ToolCallAggregator : IToolCallAggregator
    {
        private readonly Dictionary<int, ToolCallSchema> _toolCallsByIndex = new Dictionary<int, ToolCallSchema>();

        // gap80: deterministic own-ID allocator (injected; shared default if none supplied)
        private readonly IToolCallIdAllocator _idAllocator;

        // gap80: version-retaining snapshot store for pure-read dedup
        private readonly IToolCallSnapshotStore _snapshotStore;

        // gap80: classifies pure-read tools (never dedups mutating tools)
        private readonly IReadDeduplicator _readDeduplicator;

        /// <summary>
        /// Creates the aggregator. When gap80 services are absent (unit tests, legacy resolver),
        /// falls back to default implementations.
        /// </summary>
        public ToolCallAggregator(
            IToolCallIdAllocator? idAllocator = null,
            IToolCallSnapshotStore? snapshotStore = null,
            IReadDeduplicator? readDeduplicator = null)
        {
            _idAllocator = idAllocator ?? new ToolCallIdAllocator();
            _snapshotStore = snapshotStore ?? new ToolCallSnapshotStore();
            _readDeduplicator = readDeduplicator ?? new ReadDeduplicator();
        }

        /// <summary>
        /// Accumulates a tool call JSON fragment into the buffer.
        /// </summary>
        public void AccumulateToolCallsText(string? toolCallsJsonFragment)
        {
            if (string.IsNullOrEmpty(toolCallsJsonFragment))
                return;

            try
            {
                var fragmentArray = JsonConvert.DeserializeObject<List<JObject>>(toolCallsJsonFragment!);
                if (fragmentArray == null) return;

                foreach (var obj in fragmentArray)
                {
                    int index = obj["index"]?.Value<int>() ?? 0;

                    if (!_toolCallsByIndex.ContainsKey(index))
                    {
                        // First fragment for this index: create new
                        _toolCallsByIndex[index] = obj.ToObject<ToolCallSchema>() ?? new ToolCallSchema();
                    }
                    else
                    {
                        // Existing index: merge only the data fields (preserve id)
                        if (obj["function"]?["arguments"] is JValue argsVal)
                        {
                            var existing = _toolCallsByIndex[index];
                            existing.Function ??= new ToolCallFunction();
                            existing.Function.Arguments = (existing.Function.Arguments ?? "") + argsVal.Value<string>();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteWarning($"[gap78-accumulate] {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the completion signal indicates tool calls are done.
        /// </summary>
        public bool CheckCompletion(string? doneReason)
        {
            return doneReason == "tool_calls";
        }

        /// <summary>
        /// Attempts to parse accumulated JSON buffer into ToolCallSchema objects.
        /// Validates that tool names are non-empty and well-formed.
        /// gap80: assigns deterministic own IDs at close and replaces the LLM random ID.
        /// </summary>
        public bool TryGetCompleteToolCalls(out List<ToolCallSchema>? validToolCalls)
        {
            validToolCalls = null;

            if (_toolCallsByIndex.Count == 0)
                return false;

            try
            {
                var mergedCalls = _toolCallsByIndex.OrderBy(x => x.Key).Select(x => x.Value).ToList();

                LoggerService.Current.WriteInfo($"[gap78-tool_calls] Merged {_toolCallsByIndex.Count} tool calls");

                // Validate each tool call has a valid name
                foreach (var toolCall in mergedCalls)
                {
                    if (toolCall?.Function == null || string.IsNullOrWhiteSpace(toolCall.Function.Name))
                    {
                        LoggerService.Current.WriteWarning(
                            $"[gap78-aggregator] Invalid tool call: null function or empty name. Rejecting batch.");
                        return false;
                    }
                }

                // ---- gap80 Step 2: deterministic own IDs end-to-end ----
                // Assign our own deterministic ID to every surviving call, replacing reliance
                // on the LLM's random Id for correlation/pruning. Base was Reset at action start
                // (ResetToolCallLimitForAction on real Send), so IDs are per-action deterministic.
                foreach (var toolCall in mergedCalls)
                {
                    toolCall.OwnId = _idAllocator.Next();
                    toolCall.Id = toolCall.OwnId; // own ID is the canonical correlation key
                }

                validToolCalls = mergedCalls;
                return true;
            }
            catch (JsonException ex)
            {
                LoggerService.Current.WriteWarning(
                    $"[gap78-aggregator] Failed to parse accumulated tool calls JSON: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteWarning(
                    $"[gap78-aggregator] Unexpected error parsing tool calls: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets the buffer and state.
        /// </summary>
        public void Clear()
        {
            _toolCallsByIndex.Clear();
        }

        /// <inheritdoc />
        public void ResetPerAction()
        {
            _idAllocator.Reset();
            _snapshotStore.Reset();
        }

        /// <inheritdoc />
        public bool ShouldSuppressReadResult(Core.Types.ToolCall toolCall, string retrievedContent)
        {
            if (toolCall?.Name == null)
                return false;
            if (!_readDeduplicator.IsPureReadTool(toolCall.Name))
                return false;
            var decision = _snapshotStore.EvaluateRead(toolCall, retrievedContent);
            return decision.SuppressFromLlm;
        }
    }
}
