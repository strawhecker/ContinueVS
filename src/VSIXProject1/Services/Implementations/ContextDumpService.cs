using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Dumps context information for debugging LLM requests.
    /// Provides visibility into what's being sent to the LLM before tokenization.
    /// </summary>
    public class ContextDumpService : IContextDumpService
    {
        private readonly IConfigService _configService;

        /// <summary>
        /// Initializes a new instance of the ContextDumpService.
        /// </summary>
        public ContextDumpService(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Estimates the number of tokens in a text string using a simple heuristic.
        /// Uses ~1.3 tokens per word for English text (approximate).
        /// </summary>
        private static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Split on whitespace and estimate tokens
            var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            // For most English text, tokens ≈ 1.3 * words (rough estimate)
            return (int)Math.Ceiling(wordCount * 1.3);
        }

        /// <summary>
        /// Dumps the complete context (system message, context items, user message) 
        /// that will be sent to the LLM to Debug Output.
        /// </summary>
        public async Task DumpContextBeforeSendAsync(List<ChatMessage> messages, List<ContextItem>? selectedContext = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    var config = _configService.GetCurrentConfig();
                    // Check both config.Debug.DumpContextBeforeSend and UserSettings toggle
                    var dumpEnabled = config.Debug.DumpContextBeforeSend;
                    if (!dumpEnabled && config.CustomSettings.TryGetValue("experimental.dumpContextBeforeSend", out var settingValue))
                    {
                        dumpEnabled = settingValue is bool boolValue && boolValue;
                    }

                    if (!dumpEnabled)
                        return;

                    Debug.WriteLine("================================================================================");
                    Debug.WriteLine("[CONTEXT_DUMP] === LLM REQUEST CONTEXT BEFORE SEND ===");
                    Debug.WriteLine("================================================================================");

                    // Dump each message separately
                    var totalTokens = 0;
                    for (int i = 0; i < messages.Count; i++)
                    {
                        var msg = messages[i];
                        var tokens = EstimateTokenCount(msg.Content ?? "");
                        totalTokens += tokens;

                        Debug.WriteLine($"\n[MESSAGE {i}] Role: {msg.Role}");
                        Debug.WriteLine($"  Token Estimate: {tokens} tokens");
                        Debug.WriteLine($"  Content Length: {(msg.Content?.Length ?? 0)} characters");
                        Debug.WriteLine($"  Content:\n{msg.Content ?? "[empty]"}");
                        Debug.WriteLine("--- END MESSAGE ---");
                    }

                    // Dump selected context if provided
                    if (selectedContext?.Count > 0)
                    {
                        Debug.WriteLine($"\n[CONTEXT_ITEMS] Count: {selectedContext.Count}");
                        for (int i = 0; i < selectedContext.Count; i++)
                        {
                            var ctx = selectedContext[i];
                            var tokens = EstimateTokenCount(ctx.Content ?? "");
                            totalTokens += tokens;
                            Debug.WriteLine($"\n[CONTEXT_ITEM {i}] {ctx.FilePath ?? "[unnamed]"}");
                            Debug.WriteLine($"  Token Estimate: {tokens} tokens");
                            Debug.WriteLine($"  Content Length: {(ctx.Content?.Length ?? 0)} characters");
                            Debug.WriteLine($"  Content:\n{ctx.Content ?? "[empty]"}");
                            Debug.WriteLine("--- END CONTEXT_ITEM ---");
                        }
                    }

                    // Summary
                    Debug.WriteLine($"\n[SUMMARY]");
                    Debug.WriteLine($"  Total Messages: {messages.Count}");
                    Debug.WriteLine($"  Total Context Items: {selectedContext?.Count ?? 0}");
                    Debug.WriteLine($"  Estimated Total Tokens: {totalTokens}");
                    Debug.WriteLine("================================================================================\n");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] ContextDumpService.DumpContextBeforeSendAsync failed: {ex.Message}");
                    Debug.WriteLine($"  Stack: {ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// Dumps the response received from the LLM to Debug Output.
        /// </summary>
        public async Task DumpResponseAfterReceiveAsync(string responseContent)
        {
            await Task.Run(() =>
            {
                try
                {
                    var config = _configService.GetCurrentConfig();
                    // Check both config.Debug.DumpResponseAfterReceive and UserSettings toggle
                    var dumpEnabled = config.Debug.DumpResponseAfterReceive;
                    if (!dumpEnabled && config.CustomSettings.TryGetValue("experimental.dumpResponseAfterReceive", out var settingValue))
                    {
                        dumpEnabled = settingValue is bool boolValue && boolValue;
                    }

                    if (!dumpEnabled)
                        return;

                    var tokens = EstimateTokenCount(responseContent);

                    Debug.WriteLine("================================================================================");
                    Debug.WriteLine("[CONTEXT_DUMP] === LLM RESPONSE RECEIVED ===");
                    Debug.WriteLine("================================================================================");
                    Debug.WriteLine($"  Response Length: {responseContent?.Length ?? 0} characters");
                    Debug.WriteLine($"  Estimated Tokens: {tokens}");
                    Debug.WriteLine($"  Content:\n{responseContent ?? "[empty]"}");
                    Debug.WriteLine("================================================================================\n");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] ContextDumpService.DumpResponseAfterReceiveAsync failed: {ex.Message}");
                    Debug.WriteLine($"  Stack: {ex.StackTrace}");
                }
            });
        }
    }
}
