using ContinueVS.UI;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace ContinueVS.Commands
{
    /// <summary>
    /// "Ask Continue…" — opens the chat panel and, when text is selected, pre-fills
    /// the input with a code block so the user can type their own question.
    /// Unlike the other code-action commands this one is always enabled.
    /// </summary>
    internal sealed class AskContinueCommand
    {
        private readonly AsyncPackage _package;

        private AskContinueCommand(AsyncPackage package, IMenuCommandService mcs, int cmdId)
        {
            _package = package;
            var id  = new CommandID(ContinueGuids.CmdSetGuid, cmdId);
            var cmd = new MenuCommand(Execute, id);
            mcs.AddCommand(cmd);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte      = ((IServiceProvider)_package).GetService(typeof(DTE)) as DTE2;
                var sel      = dte?.ActiveDocument?.Selection as TextSelection;
                var selected = sel?.Text ?? "";
                var path     = dte?.ActiveDocument?.FullName ?? "";

                var window = await _package.ShowToolWindowAsync(
                    typeof(ContinueToolWindowPane), 0, true,
                    System.Threading.CancellationToken.None);

                if (window?.Frame is IVsWindowFrame frame)
                    frame.Show();

                // If there is a selection, send it as a code context block
                // so the user can append their question without retyping.
                if (!string.IsNullOrWhiteSpace(selected) && window is ContinueToolWindowPane pane)
                {
                    pane.SendToGui("userInput", JToken.FromObject(new
                    {
                        input   = $"```\n{selected}\n```\n",
                        context = new[] { new { name = path, description = "Current file", content = selected } },
                    }));
                }
            });
        }

        internal static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (mcs == null) return;
            new AskContinueCommand(package, mcs, ContinueCommandIds.AskContinue);
            new AskContinueCommand(package, mcs, ContinueCommandIds.ContextAskContinue);
        }
    }
}
