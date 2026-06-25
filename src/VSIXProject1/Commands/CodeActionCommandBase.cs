using ContinueVS.IPC;
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
    /// Shared base for all code-action commands (Explain, Fix, Add Comment, Ask).
    /// Reads the active editor selection, opens the Continue chat panel, and sends
    /// a pre-populated message to the React GUI.
    /// </summary>
    internal abstract class CodeActionCommandBase
    {
        protected readonly AsyncPackage Package;

        protected CodeActionCommandBase(AsyncPackage package, IMenuCommandService mcs, int cmdId)
        {
            Package = package;
            var id  = new CommandID(ContinueGuids.CmdSetGuid, cmdId);
            var cmd = new OleMenuCommand(Execute, id);
            cmd.BeforeQueryStatus += OnBeforeQueryStatus;
            mcs.AddCommand(cmd);
        }

        // Disable when no text is selected (except for ShowPanel which always shows).
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (sender is OleMenuCommand cmd)
                cmd.Enabled = HasActiveSelection();
        }

        private bool HasActiveSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = ((IServiceProvider)Package).GetService(typeof(DTE)) as DTE2;
            var sel = dte?.ActiveDocument?.Selection as TextSelection;
            return sel != null && !string.IsNullOrEmpty(sel.Text);
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = Package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte        = ((IServiceProvider)Package).GetService(typeof(DTE)) as DTE2;
                var doc        = dte?.ActiveDocument;
                var sel        = doc?.Selection as TextSelection;
                var selectedText = sel?.Text ?? "";
                var filePath   = doc?.FullName ?? "";

                // Open the chat panel.
                var window = await Package.ShowToolWindowAsync(
                    typeof(ContinueToolWindowPane), 0, true,
                    System.Threading.CancellationToken.None);

                if (window?.Frame is IVsWindowFrame frame)
                    frame.Show();

                // Build and send the context message to the GUI.
                var payload = BuildPayload(selectedText, filePath);
                if (payload != null && window is ContinueToolWindowPane pane)
                    pane.SendToGui("userInput", payload);
            });
        }

        /// <summary>
        /// Override to return the JSON object that will be posted to the React GUI
        /// as a <c>userInput</c> message.  Return null to open the panel without
        /// pre-populating anything.
        /// </summary>
        protected abstract JToken? BuildPayload(string selectedText, string filePath);

        // -----------------------------------------------------------------
        // Static factory — registers both the main-menu and context-menu variants.
        // -----------------------------------------------------------------
        protected static void RegisterBoth(
            Func<AsyncPackage, IMenuCommandService, int, CodeActionCommandBase> factory,
            AsyncPackage package,
            IMenuCommandService mcs,
            int mainCmdId,
            int ctxCmdId)
        {
            factory(package, mcs, mainCmdId);
            factory(package, mcs, ctxCmdId);
        }
    }
}
