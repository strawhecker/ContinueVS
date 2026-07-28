using ContinueVS.IPC;
using ContinueVS.UI;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    /// <summary>
    /// Interface for workspace directory provider (testable abstraction)
    /// </summary>
    internal interface IWorkspacePathProvider
    {
        Task<string[]> GetWorkspaceDirectoriesAsync(CancellationToken cancellationToken);
    }

    internal sealed class GetWorkspaceDirsHandler : IMessageHandler
    {
        private readonly IGuiReplyProvider _guiReply;
        private readonly IWorkspacePathProvider _workspaceProvider;

        public GetWorkspaceDirsHandler(IGuiReplyProvider guiReply, IWorkspacePathProvider? workspaceProvider = null)
        {
            _guiReply = guiReply;
            _workspaceProvider = workspaceProvider ?? new DefaultWorkspacePathProvider();
        }

        public async Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            // [b14-HANDLER-ENTRY-TID] Capture entry thread ID
            var entryTid = System.Threading.Thread.CurrentThread.ManagedThreadId;
            System.Diagnostics.Debug.WriteLine($"[b14-HANDLER-ENTRY-TID] getWorkspaceDirs handler entry on thread: {entryTid}");

            // [t9] Handler entry point
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] getWorkspaceDirs handler ENTRY");
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] Message ID: {message.MessageId}, Type: {message.MessageType}");

            var dirs = await _workspaceProvider.GetWorkspaceDirectoriesAsync(cancellationToken);

            // [b14-HANDLER-EXIT-TID] Capture exit thread ID
            var exitTid = System.Threading.Thread.CurrentThread.ManagedThreadId;
            System.Diagnostics.Debug.WriteLine($"[b14-HANDLER-EXIT-TID] getWorkspaceDirs handler exit on thread: {exitTid}");

            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] SendReplyToGui being called with {dirs.Length} directories");
            _guiReply.SendReplyToGui(message.MessageType, message.MessageId, dirs);
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] getWorkspaceDirs handler COMPLETE");
        }
    }

    /// <summary>
    /// Default implementation that retrieves workspace from Visual Studio DTE
    /// </summary>
    internal sealed class DefaultWorkspacePathProvider : IWorkspacePathProvider
    {
        public async Task<string[]> GetWorkspaceDirectoriesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

                var dte = (EnvDTE.DTE)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE));
                if (dte?.Solution != null)
                {
                    var fullName = dte.Solution.FullName;
                    if (!string.IsNullOrEmpty(fullName))
                    {
                        var dir = Path.GetDirectoryName(fullName);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] Workspace directory found: {dir}");
                            return new[] { dir };
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] GetWorkspaceDirectoriesAsync error: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] No workspace directory available (no solution open)");
            return new string[0];
        }
    }
}
