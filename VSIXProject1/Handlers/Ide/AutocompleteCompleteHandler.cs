using ContinueVS.IPC;
using ContinueVS.UI;
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

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _control.SendReplyToGui(message.MessageType, message.MessageId, new string[0]);
            return Task.CompletedTask;
        }
    }
}
