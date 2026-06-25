using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Llm
{
    internal sealed class LlmCompileChatHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public LlmCompileChatHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _control.SendReplyToGui(message.MessageType, message.MessageId, "");
            return Task.CompletedTask;
        }
    }
}
