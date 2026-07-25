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
            // [t9] Handler entry point
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] getWorkspaceDirs handler ENTRY");
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] Message ID: {message.MessageId}, Type: {message.MessageType}");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            string[] dirs;

            if (dte?.Solution?.FullName != null && !string.IsNullOrEmpty(dte.Solution.FullName))
            {
                dirs = new[] { Path.GetDirectoryName(dte.Solution.FullName)! };
                System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] Workspace directory found: {dirs[0]}");
            }
            else
            {
                dirs = new string[0];
                System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] No workspace directory available (no solution open)");
            }

            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] SendReplyToGui being called with {dirs.Length} directories");
            _control.SendReplyToGui(message.MessageType, message.MessageId, dirs);
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] getWorkspaceDirs handler COMPLETE");
        }
    }
}
