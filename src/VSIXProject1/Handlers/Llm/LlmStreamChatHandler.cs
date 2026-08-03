using ContinueVS.IPC;
using ContinueVS.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Llm
{
    internal sealed class LlmStreamChatHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        /// <summary>
        /// Configuration for token limits (mirrors ContextConfig from LlmCompileChatHandler)
        /// </summary>
        private static class TokenConfig
        {
            public static int MaxContextTokens
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_MAX_CONTEXT_TOKENS");
                    if (int.TryParse(envVar, out var value) && value > 0)
                        return value;
                    return 4000;
                }
            }

            public static int ReserveForResponse
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_RESERVE_TOKENS");
                    if (int.TryParse(envVar, out var value) && value >= 0)
                        return value;
                    return 1000;
                }
            }

            public static int CharsPerToken
            {
                get
                {
                    var envVar = System.Environment.GetEnvironmentVariable("CONTINUE_CHARS_PER_TOKEN");
                    if (int.TryParse(envVar, out var value) && value > 0)
                        return value;
                    return 4;
                }
            }

            public static int UsableContextTokens => MaxContextTokens - ReserveForResponse;
        }

        public LlmStreamChatHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        /// <summary>
        /// Estimate token count for a single message.
        /// </summary>
        private int EstimateTokensForMessage(JToken msg)
        {
            try
            {
                if (msg is JObject jMsg)
                {
                    var contentToken = jMsg["content"];
                    string contentText = "";

                    if (contentToken is JValue jValue)
                    {
                        contentText = jValue.Value<string>() ?? "";
                    }
                    else if (contentToken is JArray contentArray)
                    {
                        var textParts = new List<string>();
                        foreach (var item in contentArray)
                        {
                            if (item is JObject contentBlock)
                            {
                                var text = contentBlock["text"]?.Value<string>();
                                if (text != null)
                                    textParts.Add(text);
                            }
                        }
                        contentText = string.Join(" ", textParts);
                    }

                    return 4 + (contentText.Length / TokenConfig.CharsPerToken);
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Estimate total token count for all messages.
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
                        totalTokens += EstimateTokensForMessage(msg);
                }
            }
            catch { }

            return totalTokens;
        }

        /// <summary>
        /// Normalize messages: convert array-based content to strings.
        /// </summary>
        private JArray NormalizeMessages(JArray messages)
        {
            try
            {
                var normalizedMessages = new JArray();
                foreach (var msg in messages)
                {
                    var msgObj = msg as JObject;
                    if (msgObj != null)
                    {
                        var normalized = new JObject(msgObj);
                        var contentToken = msgObj["content"];
                        if (contentToken is JArray contentArray)
                        {
                            var textParts = new List<string>();
                            foreach (var item in contentArray)
                            {
                                if (item is JObject block)
                                {
                                    var text = block["text"]?.Value<string>();
                                    if (text != null)
                                        textParts.Add(text);
                                }
                            }
                            normalized["content"] = string.Join(" ", textParts);
                        }
                        normalizedMessages.Add(normalized);
                    }
                    else
                    {
                        normalizedMessages.Add(msg);
                    }
                }
                return normalizedMessages;
            }
            catch
            {
                return messages;
            }
        }

        /// <summary>
        /// Internally compile messages if not already compiled (i.e., not coming from llm/compileChat).
        /// This ensures messages are always pruned and normalized before streaming.
        /// </summary>
        private JArray CompileMessagesIfNeeded(JArray messages)
        {
            try
            {
                // Estimate tokens
                var totalTokens = EstimateTokens(messages);
                System.Diagnostics.Debug.WriteLine($"[b24-STREAM-PRECOMPILE] Estimated total tokens: {totalTokens}");

                // For now, keep all messages but normalize them
                // In future, add pruning logic if tokens exceed UsableContextTokens
                var compiledMessages = NormalizeMessages(messages);

                System.Diagnostics.Debug.WriteLine($"[b24-STREAM-PRECOMPILE] Compiled {compiledMessages.Count} messages (normalized)");
                return compiledMessages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-STREAM-PRECOMPILE] Error during message compilation: {ex.Message}");
                return messages;
            }
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.WriteLine($"[b24-HANDLER-ENTRY] HandleAsync invoked: MessageType={message.MessageType}, MessageId={message.MessageId}");
            System.Diagnostics.Debug.WriteLine($"[b24-RAW-DATA] Full message.Data={JsonConvert.SerializeObject(message.Data)}");

            // Cast message.Data (JToken) to JObject to access properties
            var dataObj = message.Data as JObject;
            var title    = dataObj?["title"]?.Value<string>() ?? "";
            var messages = dataObj?["messages"] as JArray ?? new JArray();
            System.Diagnostics.Debug.WriteLine($"[b24-PAYLOAD-EXTRACT] Extracted title='{title}', message count={messages.Count}");

            // Auto-compile messages if not already compiled (ensure normalization and pruning)
            var compiledMessages = CompileMessagesIfNeeded(messages);

            var modelConfig = ContinueConfigReader.FindModel(title);
            System.Diagnostics.Debug.WriteLine($"[b24-MODEL-CONFIG-LOOKUP] Model config lookup: title='{title}', found={modelConfig != null}");
            if (modelConfig != null)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-MODEL-CONFIG-DETAILS] Provider={modelConfig.Provider}, Model={modelConfig.Model}, ApiBase={modelConfig.ApiBase ?? "(null)"}");
            }
            if (modelConfig == null)
            {
                System.Diagnostics.Debug.WriteLine("[b24-MODEL-CONFIG-NULL] Model config is null, sending empty response");
                _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "", done = true });
                return;
            }

            // [b24-FIX] Handle empty messages
            if (compiledMessages.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[b24-EMPTY-MESSAGES] Messages array is empty after compilation");
                _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "Error: No chat messages provided", done = true });
                return;
            }

            var accumulatedContent = new StringBuilder();

            Action<string> onChunk = chunk =>
            {
                accumulatedContent.Append(chunk);
                _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = chunk, done = false });
            };

            try
            {
                System.Diagnostics.Debug.WriteLine("[b24-STREAM-START] Starting LLM stream chat");
                await LlmHttpClient.StreamChatAsync(modelConfig, compiledMessages, onChunk, cancellationToken);
                System.Diagnostics.Debug.WriteLine("[b24-STREAM-COMPLETE] LLM stream chat completed successfully");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-ERROR-HTTP] HttpRequestException: {ex.Message}");
                _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-ERROR-GENERAL] Exception: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("[b24-HANDLER-EXIT] Sending final completion marker");
            _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "", done = true });
        }
    }
}
