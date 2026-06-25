using ContinueVS.IPC;
using ContinueVS.UI;
using Newtonsoft.Json.Linq;
using System;
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

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var title    = message.Data?["title"]?.Value<string>() ?? "";
            var messages = message.Data?["messages"] as JArray ?? new JArray();

            var modelConfig = ContinueConfigReader.FindModel(title);
            if (modelConfig == null)
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "", done = true });
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
                await LlmHttpClient.StreamChatAsync(modelConfig, messages, onChunk, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" });
            }
            catch (Exception) { }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new { role = "assistant", content = "", done = true });
        }
    }
}
