using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.Design;
using System.Text;
using System.Threading.Tasks;

namespace ContinueVS.Commands
{
    internal sealed class FixCodeCommand : CodeActionCommandBase
    {
        private FixCodeCommand(AsyncPackage package, IMenuCommandService mcs, int cmdId)
            : base(package, mcs, cmdId) { }

        protected override JToken? BuildPayload(string selectedText, string filePath)
        {
            // Collect error-list items from VS to send as additional context.
            var errors = new StringBuilder();
            try
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
                var dte = ((IServiceProvider)Package).GetService(typeof(EnvDTE.DTE)) as DTE2;
                if (dte != null && dte.ToolWindows?.ErrorList?.ErrorItems != null)
                {
                    for (int i = 1; i <= System.Math.Min(dte.ToolWindows.ErrorList.ErrorItems.Count, 5); i++)
                    {
                        var item = dte.ToolWindows.ErrorList.ErrorItems.Item(i);
                        errors.AppendLine($"- {item.Description} (line {item.Line})");
                    }
                }
            }
            catch { /* best-effort */ }

            var errorSection = errors.Length > 0
                ? $"\n\nErrors:\n{errors}"
                : "";

            return JToken.FromObject(new
            {
                input   = $"/fix{errorSection}\n```\n{selectedText}\n```",
                context = new[] { new { name = filePath, description = "Current file", content = selectedText } },
            });
        }

        internal static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (mcs == null) return;
            RegisterBoth(
                (p, m, id) => new FixCodeCommand(p, m, id),
                package, mcs,
                ContinueCommandIds.FixCode,
                ContinueCommandIds.ContextFixCode);
        }
    }
}
