using ContinueVS.IPC;
using ContinueVS.Settings;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class IsTelemetryEnabledHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public IsTelemetryEnabledHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var options = ContinueVSPackage.Instance?.GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
            bool enabled = !(options?.DisableTelemetry ?? false);
            _control.SendReplyToGui(message.MessageType, message.MessageId, enabled);
        }
    }
}
