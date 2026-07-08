using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigGetSerializedProfileInfoHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ConfigGetSerializedProfileInfoHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            _control.SendReplyToGui(message.MessageType, message.MessageId,
                new { result = (object?)null, profileId = (string?)null, profiles = new object[0] });
            return Task.CompletedTask;
        }
    }
}
