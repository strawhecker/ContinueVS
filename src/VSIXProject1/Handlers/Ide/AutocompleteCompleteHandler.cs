using ContinueVS.Handlers.Llm;
using ContinueVS.IPC;
using ContinueVS.UI;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class AutocompleteCompleteHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public AutocompleteCompleteHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var input = message.Data?.ToObject<AutocompleteInput>() ?? new AutocompleteInput();
            var prompt = input.Filepath + ":" + input.Pos.Line + ":" + input.Pos.Character;

            var modelConfig = ContinueConfigReader.FindModel("");
            if (modelConfig == null)
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, new string[0]);
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
                _control.SendReplyToGui(message.MessageType, message.MessageId, new string[0]);
                return;
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new[] { completion });
        }
    }
}
