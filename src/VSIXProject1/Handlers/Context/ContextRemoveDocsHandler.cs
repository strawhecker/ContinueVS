using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Context
{
    internal sealed class ContextRemoveDocsHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ContextRemoveDocsHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
            return Task.CompletedTask;
        }
    }
}
