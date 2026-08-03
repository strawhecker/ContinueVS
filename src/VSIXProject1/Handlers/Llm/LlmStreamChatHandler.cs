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

        public LlmStreamChatHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        /// <summary>
        /// Extract content text from either string or array format.
        /// Handles both: "content": "text" and "content": [{"text": "...", "type": "text"}]
        /// </summary>
        private string ExtractContentPreview(JToken? contentToken)
        {
            if (contentToken == null)
                return "(null)";

            if (contentToken is JValue jValue)
            {
                return jValue.Value<string>() ?? "(null)";
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
                        {
                            textParts.Add(text);
                        }
                    }
                }
                return textParts.Count > 0 ? string.Join(" ", textParts) : "(empty array)";
            }

            return "(unsupported format)";
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

            // Log message array structure
            if (messages.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[b24-PAYLOAD-MESSAGES] Messages content={JsonConvert.SerializeObject(messages)}");
                var firstMsg = messages[0] as JObject;
                var lastMsg = messages[messages.Count - 1] as JObject;

                var firstContent = ExtractContentPreview(firstMsg?["content"]);
                var lastContent = ExtractContentPreview(lastMsg?["content"]);

                System.Diagnostics.Debug.WriteLine($"[b24-PAYLOAD-SAMPLE] First message: role={firstMsg?["role"]?.Value<string>()}, content_len={firstContent?.Length ?? 0}, content_preview={firstContent?.Substring(0, Math.Min(50, firstContent?.Length ?? 0))}");
                System.Diagnostics.Debug.WriteLine($"[b24-PAYLOAD-SAMPLE] Last message: role={lastMsg?["role"]?.Value<string>()}, content_len={lastContent?.Length ?? 0}, content_preview={lastContent?.Substring(0, Math.Min(50, lastContent?.Length ?? 0))}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[b24-PAYLOAD-EMPTY] Messages array is empty");
            }

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

            // [b24-FIX] Handle empty messages - this should no longer happen now that llm/compileChat returns properly
            if (messages.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[b24-EMPTY-MESSAGES] Messages array is empty - ensure llm/compileChat is returning compiled messages");
                _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "Error: No chat messages provided by frontend", done = true });
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
                await LlmHttpClient.StreamChatAsync(modelConfig, messages, onChunk, cancellationToken);
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
