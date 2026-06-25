using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.File
{
    internal sealed class ApplyToFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public ApplyToFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var filepath = message.Data?.Value<string>("filepath");
            var text     = message.Data?.Value<string>("text");

            if (string.IsNullOrWhiteSpace(filepath))
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                System.IO.File.WriteAllText(filepath, text ?? "", Encoding.UTF8);
            }
            catch (Exception)
            {
                // swallow silently
            }

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                foreach (Document doc in dte.Documents)
                {
                    if (string.Equals(doc.FullName, filepath, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Close(vsSaveChanges.vsSaveChangesNo);
                        dte.ItemOperations.OpenFile(filepath);
                        break;
                    }
                }
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
        }
    }
}
