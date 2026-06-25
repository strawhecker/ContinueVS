using ContinueVS.IPC;
using ContinueVS.UI;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class OpenUrlHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public OpenUrlHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var url = message.Data?.ToObject<string>();

            if (!string.IsNullOrWhiteSpace(url))
            {
                Process.Start(url);
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
            return Task.CompletedTask;
        }
    }
}
