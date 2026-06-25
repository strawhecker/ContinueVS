using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetWorkspaceDirsHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetWorkspaceDirsHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            string[] dirs;

            if (dte != null && !string.IsNullOrEmpty(dte.Solution?.FullName))
            {
                dirs = new[] { Path.GetDirectoryName(dte.Solution.FullName) };
            }
            else
            {
                dirs = new string[0];
            }

            _control.SendReplyToGui(message.MessageType, message.MessageId, dirs);
        }
    }
}
