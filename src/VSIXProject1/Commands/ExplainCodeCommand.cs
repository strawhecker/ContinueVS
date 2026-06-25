using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace ContinueVS.Commands
{
    internal sealed class ExplainCodeCommand : CodeActionCommandBase
    {
        private ExplainCodeCommand(AsyncPackage package, IMenuCommandService mcs, int cmdId)
            : base(package, mcs, cmdId) { }

        protected override JToken? BuildPayload(string selectedText, string filePath)
            => JToken.FromObject(new
            {
                input   = $"/explain\n```\n{selectedText}\n```",
                context = new[] { new { name = filePath, description = "Current file", content = selectedText } },
            });

        internal static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var mcs = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            if (mcs == null) return;
            RegisterBoth(
                (p, m, id) => new ExplainCodeCommand(p, m, id),
                package, mcs,
                ContinueCommandIds.ExplainCode,
                ContinueCommandIds.ContextExplainCode);
        }
    }
}
