using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Context
{
    internal sealed class ContextGetContextItemsHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ContextGetContextItemsHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _control.SendReplyToGui(message.MessageType, message.MessageId, new object[0]);
            return Task.CompletedTask;
        }
    }
}
