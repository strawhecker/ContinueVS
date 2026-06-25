using ContinueVS.IPC;
using ContinueVS.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Llm
{
    internal sealed class LlmCompleteHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public LlmCompleteHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var prompt = message.Data?["prompt"]?.Value<string>() ?? "";
            var title  = message.Data?["title"]?.Value<string>() ?? "";

            var modelConfig = ContinueConfigReader.FindModel(title);
            if (modelConfig == null)
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, "");
                return;
            }

            string completion;
            try
            {
                completion = await LlmHttpClient.CompleteAsync(modelConfig, prompt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _control.SendToGui("showToast", new { message = "Continue: LLM request failed — " + ex.Message, type = "error" });
                completion = "";
            }
            catch (Exception)
            {
                completion = "";
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, completion);
        }
    }
}
