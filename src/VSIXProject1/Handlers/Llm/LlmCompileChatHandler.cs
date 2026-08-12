using ContinueVS.IPC;
using ContinueVS.Services.Interfaces;
using ContinueVS.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Llm
{
    internal sealed class LlmCompileChatHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;
        private readonly IToolService? _toolService;

        /// <summary>
        /// Configuration helper for context token limits.
        /// Reads from environment variables with sensible defaults.
        /// 
        /// Environment Variables:
        /// - CONTINUE_MAX_CONTEXT_TOKENS: Total context window size (default: 4000)
        /// - CONTINUE_RESERVE_TOKENS: Tokens reserved for model response (default: 1000)
        /// - CONTINUE_CHARS_PER_TOKEN: Estimated characters per token for calculation (default: 4)
        /// 
        /// Example (PowerShell):
        ///   $env:CONTINUE_MAX_CONTEXT_TOKENS = "131072"  # 128 KiB
        ///   $env:CONTINUE_RESERVE_TOKENS = "8192"        # Reserve 8K for response
        /// </summary>
        private static class ContextConfig
        {
            /// <summary>
            /// Maximum total context tokens (includes reserve).
            /// Default: 4000 tokens (conservative for most models)
            /// Set via: CONTINUE_MAX_CONTEXT_TOKENS environment variable
            /// </summary>
            public static int MaxContextTokens
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_MAX_CONTEXT_TOKENS");
                    if (int.TryParse(envVar, out var value) && value > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b24-CONFIG] Using CONTINUE_MAX_CONTEXT_TOKENS={value}");
                        return value;
                    }
                    return 4000; // Default fallback
                }
            }

            /// <summary>
            /// Tokens to reserve for the model's response (don't use for input).
            /// Default: 1000 tokens
            /// Set via: CONTINUE_RESERVE_TOKENS environment variable
            /// </summary>
            public static int ReserveForResponse
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_RESERVE_TOKENS");
                    if (int.TryParse(envVar, out var value) && value >= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b24-CONFIG] Using CONTINUE_RESERVE_TOKENS={value}");
                        return value;
                    }
                    return 1000; // Default fallback
                }
            }

            /// <summary>
            /// Estimated characters per token (for rough token counting).
            /// Default: 4 characters per token (typical for English)
            /// Set via: CONTINUE_CHARS_PER_TOKEN environment variable
            /// </summary>
            public static int CharsPerToken
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_CHARS_PER_TOKEN");
                    if (int.TryParse(envVar, out var value) && value > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b24-CONFIG] Using CONTINUE_CHARS_PER_TOKEN={value}");
                        return value;
                    }
                    return 4; // Default fallback
                }
            }

            /// <summary>
            /// Usable context tokens (max - reserve). This is what's available for input messages.
            /// </summary>
            public static int UsableContextTokens => MaxContextTokens - ReserveForResponse;
        }

        public LlmCompileChatHandler(ContinueToolWindowControl control, IToolService? toolService = null)
        {
            _control = control;
            _toolService = toolService;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine("[b24-COMPILE-CHAT-HANDLER] llm/compileChat invoked");

            try
            {
                // Extract the messages array from the request
                // Note: message.Data is JToken, must cast to JObject first to access properties
                var dataObj = message.Data as JObject;
                var messages = dataObj?["messages"] as JArray ?? new JArray();
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] Received {messages.Count} messages to compile");

                if (messages.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[b24-COMPILE-CHAT] No messages to compile, returning empty array");
                    var emptyResponse = new
                    {
                        compiledChatMessages = new object[0],
                        contextPercentage = 0,
                        didPrune = false
                    };
                    _control.SendReplyToGui(message.MessageType, message.MessageId, emptyResponse);
                    return Task.CompletedTask;
                }

                // Log configuration being used
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-CONFIG] MaxContextTokens={ContextConfig.MaxContextTokens}, ReserveForResponse={ContextConfig.ReserveForResponse}, UsableContextTokens={ContextConfig.UsableContextTokens}, CharsPerToken={ContextConfig.CharsPerToken}");

                // Calculate token usage for all messages
                var totalTokens = EstimateTokens(messages);
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] Estimated total tokens: {totalTokens}");

                // Prune messages if necessary (keep recent messages, remove oldest)
                var (compiledMessages, didPrune) = PruneMessagesIfNeeded(messages, totalTokens);

                // Recalculate tokens after pruning
                var finalTokens = EstimateTokens(compiledMessages);
                var contextPercentage = ContextConfig.UsableContextTokens > 0 
                    ? Math.Min(100, (finalTokens * 100) / ContextConfig.UsableContextTokens)
                    : 0;

                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] After pruning: {compiledMessages.Count} messages, {finalTokens} tokens, {contextPercentage}% context used, didPrune={didPrune}");

                if (contextPercentage > 100)
                {
                    System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] WARNING: Context still exceeds 100% ({contextPercentage}%)");
                }

                // Normalize messages: convert array-based content to strings for provider compatibility
                var normalizedMessages = new JArray();
                foreach (var msg in compiledMessages)
                {
                    var msgObj = msg as JObject;
                    if (msgObj != null)
                    {
                        var normalized = new JObject(msgObj); // Clone
                        var contentToken = msgObj["content"];
                        if (contentToken is JArray contentArray)
                        {
                            // Convert array of blocks to plain string
                            var textParts = new List<string>();
                            foreach (var item in contentArray)
                            {
                                if (item is JObject block)
                                {
                                    var text = block["text"]?.Value<string>();
                                    if (text != null)
                                    {
                                        textParts.Add(text);
                                    }
                                }
                            }
                            normalized["content"] = string.Join(" ", textParts);
                            System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-NORMALIZE] Converted array content to string for message");
                        }
                        normalizedMessages.Add(normalized);
                    }
                    else
                    {
                        normalizedMessages.Add(msg);
                    }
                }

                // Return the compiled messages in the expected format
                // Convert JArray to List for JSON serialization
                var response = new
                {
                    compiledChatMessages = normalizedMessages.ToList(),
                    contextPercentage = contextPercentage,
                    didPrune = didPrune
                };

                _control.SendReplyToGui(message.MessageType, message.MessageId, response);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] Stack: {ex.StackTrace}");

                // Return error response
                var errorResponse = new
                {
                    error = ex.Message,
                    compiledChatMessages = new object[0],
                    contextPercentage = 0,
                    didPrune = false
                };
                _control.SendReplyToGui(message.MessageType, message.MessageId, errorResponse);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Estimate token count for a single message.
        /// Content can be a string or an array of content blocks (from v2.0.0 format).
        /// </summary>
        private int EstimateTokensForMessage(JToken msg)
        {
            try
            {
                if (msg is JObject jMsg)
                {
                    var role = jMsg["role"]?.Value<string>() ?? "";
                    var contentToken = jMsg["content"];

                    string contentText = "";

                    // Handle both string and array content formats
                    if (contentToken is JValue jValue)
                    {
                        // Simple string format
                        contentText = jValue.Value<string>() ?? "";
                    }
                    else if (contentToken is JArray contentArray)
                    {
                        // Array format: [{"text": "...", "type": "text"}, ...]
                        var textParts = new List<string>();
                        foreach (var item in contentArray)
                        {
                            if (item is JObject contentBlock)
                            {
                                var text = contentBlock["text"]?.Value<string>();
                                if (text != null)
                                {
                                    textParts.Add(text);
                                }
                            }
                        }
                        contentText = string.Join(" ", textParts);
                    }

                    // Rough estimate: role (4 tokens) + content (chars/CharsPerToken)
                    return 4 + (contentText.Length / ContextConfig.CharsPerToken);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] EstimateTokensForMessage error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] EstimateTokensForMessage stack: {ex.StackTrace}");
            }
            return 0;
        }

        /// <summary>
        /// Estimate token count for messages using rough character-to-token conversion.
        /// </summary>
        private int EstimateTokens(JArray messages)
        {
            int totalTokens = 0;

            if (messages == null || messages.Count == 0)
                return 0;

            try
            {
                foreach (var msg in messages)
                {
                    if (msg != null)
                    {
                        totalTokens += EstimateTokensForMessage(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] EstimateTokens error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT] EstimateTokens stack: {ex.StackTrace}");
            }

            return totalTokens;
        }

        /// <summary>
        /// Prune messages to fit within context window, keeping most recent messages.
        /// Strategy: Keep first message (system) + most recent messages that fit.
        /// </summary>
        private (JArray, bool) PruneMessagesIfNeeded(JArray messages, int totalTokens)
        {
            if (totalTokens <= ContextConfig.UsableContextTokens)
            {
                // No pruning needed
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-PRUNE] No pruning needed: {totalTokens} <= {ContextConfig.UsableContextTokens}");
                return (messages, false);
            }

            System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-PRUNE] Context overflow: {totalTokens} > {ContextConfig.UsableContextTokens}, pruning messages");

            // Keep the last N messages that fit within budget
            var prunedMessages = new JArray();
            var runningTokens = 0;

            // Always keep the first message (usually system message)
            if (messages.Count > 0)
            {
                var firstMsg = messages[0];
                var firstTokens = EstimateTokensForMessage(firstMsg);
                prunedMessages.Add(firstMsg);
                runningTokens += firstTokens;
                System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-PRUNE] Kept first message (system): {firstTokens} tokens");
            }

            // Add messages from the end backwards (most recent first) until we exceed limit
            for (int i = messages.Count - 1; i >= 1; i--)
            {
                var msg = messages[i];
                var msgTokens = EstimateTokensForMessage(msg);

                if (runningTokens + msgTokens <= ContextConfig.UsableContextTokens)
                {
                    prunedMessages.Insert(1, msg); // Insert after system message
                    runningTokens += msgTokens;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-PRUNE] Stopped at message index {i}, running tokens: {runningTokens}, would exceed with +{msgTokens}");
                    break;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[b24-COMPILE-CHAT-PRUNE] Pruned from {messages.Count} to {prunedMessages.Count} messages (from {totalTokens} to {runningTokens} tokens)");

            return (prunedMessages, true);
        }
    }
}
