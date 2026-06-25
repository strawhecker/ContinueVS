using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigAddGlobalRuleHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ConfigAddGlobalRuleHandler(ContinueToolWindowControl control)
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
