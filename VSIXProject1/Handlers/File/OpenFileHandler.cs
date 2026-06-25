using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class OpenFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public OpenFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var path = message.Data?.Value<string>("path");

            if (!string.IsNullOrWhiteSpace(path))
            {
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                dte?.ItemOperations?.OpenFile(path);
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
        }
    }
}
