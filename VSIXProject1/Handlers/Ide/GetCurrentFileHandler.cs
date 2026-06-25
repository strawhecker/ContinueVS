using ContinueVS.IPC;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetCurrentFileHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetCurrentFileHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
            var doc = dte?.ActiveDocument;

            if (doc == null)
            {
                _control.SendReplyToGui(message.MessageType, message.MessageId, new object());
                return;
            }

            var path = doc.FullName;

            string contents = "";
            try { contents = global::System.IO.File.ReadAllText(path); } catch { }

            int line = 0, col = 0;
            if (doc.Selection is EnvDTE.TextSelection sel)
            {
                line = sel.ActivePoint.Line - 1;
                col  = sel.ActivePoint.LineCharOffset - 1;
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, new
            {
                filepath       = path,
                contents,
                cursorPosition = new { line, character = col },
            });
        }
    }
}
