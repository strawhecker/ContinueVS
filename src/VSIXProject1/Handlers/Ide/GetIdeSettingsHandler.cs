using ContinueVS.IPC;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetIdeSettingsHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetIdeSettingsHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var settings = new IdeSettings();
            _control.SendReplyToGui(message.MessageType, message.MessageId, settings);
        }
    }
}
