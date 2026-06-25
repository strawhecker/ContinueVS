using ContinueVS.IPC;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetIdeInfoHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        public GetIdeInfoHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            string version = "17.0";
            var shell = Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
            if (shell != null &&
                shell.GetProperty((int)__VSSPROPID5.VSSPROPID_ReleaseVersion, out object versionObj) == 0 &&
                versionObj is string versionStr &&
                !string.IsNullOrEmpty(versionStr))
            {
                version = versionStr;
            }

            var info = new IdeInfo { Version = version };
            _control.SendReplyToGui(message.MessageType, message.MessageId, info);
        }
    }
}
