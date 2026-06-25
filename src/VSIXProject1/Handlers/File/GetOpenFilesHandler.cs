using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class GetOpenFilesHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetOpenFilesHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            var paths = new List<string>();

            if (dte != null)
            {
                foreach (Document doc in dte.Documents)
                {
                    if (!string.IsNullOrWhiteSpace(doc.FullName))
                        paths.Add(doc.FullName);
                }
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, paths.ToArray());
        }
    }
}
