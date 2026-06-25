using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigDeleteModelHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ConfigDeleteModelHandler(ContinueToolWindowControl control)
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
