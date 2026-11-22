#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;

namespace ContinueVS.Services.Utilities
{
    /// <summary>
    /// gap99: Enforces atomic, ordered Assistant tool request / Tool result pairs on the
    /// outbound LLM message list.
    ///
    /// Rules (exact, no fabrication):
    /// - A tool call is sent only when BOTH its request (Assistant.ToolCalls[n].Id) and its
    ///   result (Tool.ToolCallId) are live. Deleted (IsDeleted) or Superseded members
    ///   invalidate the whole pair; neither half is sent.
    /// - Results are emitted immediately after their request group, in request order.
    /// - Missing results produce NO placeholder; the call is simply not sent.
    /// - Orphan, duplicate, or id-less Tool messages are silently dropped.
    /// - When an Assistant tool-call message loses every call and has no prose, the whole
    ///   group is dropped together with the Thinking entries immediately preceding it.
    /// - All other messages keep their literal content and relative order.
    /// Inputs are never mutated; a partially surviving Assistant is emitted as a shallow copy.
    /// </summary>
    public static class ToolCallPairing
    {
        /// <summary>
        /// Returns a new list containing only valid, ordered tool request/result pairs plus all
        /// other live messages in their original order.
        /// </summary>
        public static List<ChatMessage> FilterPairs(IEnumerable<ChatMessage>? messages)
        {
            var output = new List<ChatMessage>();
            if (messages == null)
                return output;

            var source = messages.Where(m => m != null).ToList();
            var pendingThinking = new List<ChatMessage>();

            int i = 0;
            while (i < source.Count)
            {
                var msg = source[i];

                bool isToolCallAssistant = msg.Role == ChatMessageRole.Assistant &&
                                           msg.ToolCalls != null &&
                                           msg.ToolCalls.Count > 0;

                // A deleted tool-call request is still processed as a group below so that its
                // results and preceding Thinking are dropped with it.
                if (msg.IsDeleted && !isToolCallAssistant)
                {
                    i++;
                    continue;
                }

                if (msg.Role == ChatMessageRole.Thinking)
                {
                    pendingThinking.Add(msg);
                    i++;
                    continue;
                }

                if (msg.Role == ChatMessageRole.Tool)
                {
                    // Not consumed by a preceding request group: orphan. Dropped silently.
                    i++;
                    continue;
                }

                if (!isToolCallAssistant)
                {
                    output.AddRange(pendingThinking);
                    pendingThinking.Clear();
                    output.Add(msg);
                    i++;
                    continue;
                }

                // Collect the contiguous Tool segment that answers this request group.
                int segmentStart = i + 1;
                int segmentEnd = segmentStart;
                while (segmentEnd < source.Count && source[segmentEnd].Role == ChatMessageRole.Tool)
                    segmentEnd++;

                bool requestDead = !IsLive(msg);

                // First result seen for an id decides that pair; later ones are duplicates.
                var resultById = new Dictionary<string, ChatMessage>(StringComparer.Ordinal);
                for (int k = segmentStart; k < segmentEnd; k++)
                {
                    var result = source[k];
                    if (string.IsNullOrWhiteSpace(result.ToolCallId))
                        continue;
                    if (!resultById.ContainsKey(result.ToolCallId!))
                        resultById[result.ToolCallId!] = result;
                }

                var survivingCalls = new List<ToolCall>();
                var survivingResults = new List<ChatMessage>();
                if (!requestDead)
                {
                    var seenCallIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var call in msg.ToolCalls!)
                    {
                        if (call == null || string.IsNullOrWhiteSpace(call.Id))
                            continue;
                        if (!seenCallIds.Add(call.Id!))
                            continue;
                        if (!resultById.TryGetValue(call.Id!, out var result))
                            continue;
                        if (!IsLive(result))
                            continue;

                        survivingCalls.Add(call);
                        survivingResults.Add(result);
                    }
                }

                if (survivingCalls.Count > 0)
                {
                    output.AddRange(pendingThinking);
                    pendingThinking.Clear();
                    output.Add(survivingCalls.Count == msg.ToolCalls!.Count
                        ? msg
                        : CopyWithToolCalls(msg, survivingCalls));
                    output.AddRange(survivingResults);
                }
                else if (!requestDead && !string.IsNullOrWhiteSpace(msg.Content))
                {
                    // Prose survives without any tool call; its reasoning stays with it.
                    output.AddRange(pendingThinking);
                    pendingThinking.Clear();
                    output.Add(CopyWithToolCalls(msg, null));
                }
                else
                {
                    // Entire group dropped, including the Thinking that produced it.
                    pendingThinking.Clear();
                }

                i = segmentEnd;
            }

            // Trailing Thinking is literal data; the provider serializer decides what to do with it.
            output.AddRange(pendingThinking);
            return output;
        }

        private static bool IsLive(ChatMessage message)
        {
            return !message.IsDeleted && message.FreshnessState != FreshnessState.Superseded;
        }

        private static ChatMessage CopyWithToolCalls(ChatMessage source, List<ToolCall>? toolCalls)
        {
            return new ChatMessage
            {
                Id = source.Id,
                Role = source.Role,
                Content = source.Content ?? string.Empty,
                ToolCalls = toolCalls,
                ToolCallId = source.ToolCallId,
                Timestamp = source.Timestamp,
                FreshnessState = source.FreshnessState,
                ToolName = source.ToolName,
                IsThinking = source.IsThinking
            };
        }
    }
}
