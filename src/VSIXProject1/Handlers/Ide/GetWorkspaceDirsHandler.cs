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
            // [t9] Handler entry point
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] getWorkspaceDirs handler ENTRY");
            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] Message ID: {message.MessageId}, Type: {message.MessageType}");

            var dirs = await _workspaceProvider.GetWorkspaceDirectoriesAsync(cancellationToken);

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
                // Use reflection to call ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync
                var threadHelper = typeof(Microsoft.VisualStudio.Shell.ThreadHelper);
                var method = threadHelper.GetMethod("SwitchToMainThreadAsync", 
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (method != null)
                {
                    var task = method.Invoke(null, new object[] { cancellationToken });
                    if (task is Task taskObj)
                    {
                        await taskObj;
                    }
                }

                // Get DTE via Package.GetGlobalService
                var packageType = typeof(Microsoft.VisualStudio.Shell.Package);
                var getGlobalServiceMethod = packageType.GetMethod("GetGlobalService",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                if (getGlobalServiceMethod != null)
                {
                    var dteType = typeof(EnvDTE.DTE);
                    var dte = getGlobalServiceMethod.Invoke(null, new object[] { dteType });

                    if (dte != null)
                    {
                        // Use reflection to access Solution.FullName
                        var solutionProperty = dte.GetType().GetProperty("Solution");
                        var solution = solutionProperty?.GetValue(dte);

                        if (solution != null)
                        {
                            var fullNameProperty = solution.GetType().GetProperty("FullName");
                            var fullName = (string?)fullNameProperty?.GetValue(solution);

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
                }
            }
            catch
            {
                // If anything fails (assembly missing, DTE unavailable), return empty
            }

            System.Diagnostics.Debug.WriteLine($"[t9-HANDLER] No workspace directory available (no solution open)");
            return new string[0];
        }
    }
}
