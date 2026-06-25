using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class SaveFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public SaveFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var filepath = message.Data?.Value<string>("filepath");

            if (!string.IsNullOrWhiteSpace(filepath))
            {
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte != null)
                {
                    foreach (Document doc in dte.Documents)
                    {
                        if (string.Equals(doc.FullName, filepath, StringComparison.OrdinalIgnoreCase))
                        {
                            doc.Save();
                            break;
                        }
                    }
                }
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
        }
    }
}
