#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Interfaces
{
    /// <summary>
    /// Service interface for aggregating streaming tool call fragments into complete, validated tool calls.
    /// Buffers raw JSON text fragments until completion signal arrives, then parses and validates once.
    /// </summary>
    public interface IToolCallAggregator
    {
        /// <summary>
        /// Accumulates a tool call JSON fragment into the buffer.
        /// Concatenates fragments without attempting to parse until completion signal received.
        /// </summary>
        /// <param name="toolCallsJsonFragment">Raw JSON text fragment (or complete JSON string) to append to buffer</param>
        void AccumulateToolCallsText(string? toolCallsJsonFragment);

        /// <summary>
        /// Checks if the completion signal has been received.
        /// </summary>
        /// <param name="doneReason">The finish_reason value from the LLM response (e.g., "stop", "length", "tool_calls")</param>
        /// <returns>True if doneReason equals "tool_calls", indicating tool calls are the completion signal</returns>
        bool CheckCompletion(string? doneReason);

        /// <summary>
        /// Attempts to parse and validate accumulated tool call JSON into a list of ToolCallSchema objects.
        /// Only succeeds if accumulated buffer contains valid JSON matching the ToolCallSchema format.
        /// </summary>
        /// <param name="validToolCalls">Output list of parsed and validated ToolCallSchema objects, or null if parsing failed</param>
        /// <returns>True if parsing succeeded and ToolCalls are valid; false if JSON is malformed or invalid</returns>
        bool TryGetCompleteToolCalls(out List<ToolCallSchema>? validToolCalls);

        /// <summary>
        /// Resets the internal buffer and completion state.
        /// Called when user cancels operation or needs to start fresh aggregation.
        /// </summary>
        void Clear();

        /// <summary>
        /// gap80: Resets the per-action ID allocator base and the version-retaining snapshot
        /// store. Called ONLY on a real user Send (per-action scope, matching the tool budget).
        /// </summary>
        void ResetPerAction();

        /// <summary>
        /// gap80: Evaluates a completed pure-read invocation; returns true when the read should
        /// be suppressed from the LLM payload (duplicate, or an older retained version) while its
        /// bytes remain accessible. Never applied to mutating tools.
        /// </summary>
        bool ShouldSuppressReadResult(Core.Types.ToolCall toolCall, string retrievedContent);
    }
}
