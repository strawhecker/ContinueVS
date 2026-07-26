using ContinueVS.IPC;
using ContinueVS.UI;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Config
{
    internal sealed class ConfigUpdateSharedConfigHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ConfigUpdateSharedConfigHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            // VSTHRD010: Switch to main thread before calling SendReplyToGui
            await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
        }
    }
}
