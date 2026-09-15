#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Implementation of IToolCallAggregator that buffers raw JSON text fragments and parses when complete.
    /// </summary>
    public class ToolCallAggregator : IToolCallAggregator
    {
        private Dictionary<int, ToolCallSchema> _toolCallsByIndex = new Dictionary<int, ToolCallSchema>();

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
    }
}
