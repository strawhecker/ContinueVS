using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace ContinueVS.Commands
{
    /// <summary>
    /// "View → Continue Chat" and Ctrl+Shift+J command.
    /// Shows (or activates) the <see cref="ContinueToolWindowPane"/>.
    /// </summary>
    internal sealed class ShowContinuePanelCommand
    {
        private readonly AsyncPackage _package;

        private ShowContinuePanelCommand(AsyncPackage package, IMenuCommandService mcs)
        {
            _package = package;

            var cmdId = new CommandID(ContinueGuids.CmdSetGuid, ContinueCommandIds.ShowContinuePanel);
            var cmd   = new MenuCommand(Execute, cmdId);
            mcs.AddCommand(cmd);
        }

        /// <summary>Registers the command on the package's menu command service.</summary>
        internal static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (mcs != null)
                new ShowContinuePanelCommand(package, mcs);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await _package.ShowToolWindowAsync(
                    typeof(ContinueToolWindowPane),
                    id:             0,
                    create:         true,
                    cancellationToken: System.Threading.CancellationToken.None);

                if (window?.Frame is IVsWindowFrame frame)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    frame.Show();
                }
            });
        }
    }
}
