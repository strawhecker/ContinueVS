#nullable enable

using System;
using System.Collections.Generic;
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
        private string _buffer = string.Empty;

        /// <summary>
        /// Accumulates a tool call JSON fragment into the buffer.
        /// </summary>
        public void AccumulateToolCallsText(string? toolCallsJsonFragment)
        {
            if (string.IsNullOrEmpty(toolCallsJsonFragment))
                return;

            _buffer += toolCallsJsonFragment;
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

            if (string.IsNullOrWhiteSpace(_buffer))
                return false;

            try
            {
                // Attempt to parse the buffer as a JSON array
                var parsed = JsonConvert.DeserializeObject<List<ToolCallSchema>>(_buffer);

                if (parsed == null || parsed.Count == 0)
                    return false;

                // Validate each tool call has a valid name
                foreach (var toolCall in parsed)
                {
                    if (toolCall?.Function == null || string.IsNullOrWhiteSpace(toolCall.Function.Name))
                    {
                        LoggerService.Current.WriteWarning(
                            $"[gap78-aggregator] Invalid tool call: null function or empty name. Rejecting batch.");
                        return false;
                    }
                }

                validToolCalls = parsed;
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
            _buffer = string.Empty;
        }
    }
}
