using ContinueVS.Binary;
using ContinueVS.Commands;
using ContinueVS.Editor;
using ContinueVS.IPC;
using ContinueVS.Settings;
using ContinueVS.UI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS
{
    /// <summary>
    /// Continue for Visual Studio — AsyncPackage entry point.
    /// Loads asynchronously so VS startup is not blocked.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ContinueGuids.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ContinueToolWindowPane),
        Style = VsDockStyle.Tabbed,
        Window = EnvDTE.Constants.vsWindowKindSolutionExplorer)]
    [ProvideOptionPage(typeof(ContinueOptionsPage), "Continue", "General", 0, 0, true)]
    public sealed class ContinueVSPackage : AsyncPackage
    {
        /// <summary>Singleton reference set during InitializeAsync, cleared on Dispose.</summary>
        public static ContinueVSPackage? Instance { get; private set; }

        /// <summary>The binary process manager; available after InitializeAsync completes.</summary>
        internal ContinueBinaryManager? BinaryManager { get; private set; }

        /// <summary>The stdio IPC client connected to the binary process.</summary>
        internal ContinueClient? Client { get; private set; }
        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Instance = this;

            await ShowContinuePanelCommand.InitializeAsync(this);
            await AskContinueCommand.InitializeAsync(this);
            await ExplainCodeCommand.InitializeAsync(this);
            await FixCodeCommand.InitializeAsync(this);
            await AddCommentCommand.InitializeAsync(this);

            BinaryManager = new ContinueBinaryManager();
            Client        = new ContinueClient();

            // When the binary process is running, wire up the stdio IPC client.
            BinaryManager.Ready += (_, process) =>
            {
                try
                {
                    Client.Connect(process, cancellationToken);

                    new IdeCallbackHandler(Client, this).Register();
                    new DiffApplier(this, Client).Register();
                    new StatusBarManager(Client, this).Register();

                    var editorCtx = new EditorContextProvider(this, Client);
                    _ = editorCtx.RegisterAsync();

                    _ = JoinableTaskFactory.RunAsync(async () =>
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync();
                        var configWatcher = new WorkspaceConfigWatcher(this, Client);
                        configWatcher.Start();
                    });
                }
                catch { /* will retry on next crash/restart cycle */ }
            };

            // On crash, tear down the old client and create a fresh one.
            BinaryManager.Crashed += (_, __) =>
            {
                Client.Dispose();
                Client = new ContinueClient();
            };

            _ = Task.Run(() => BinaryManager.StartAsync(cancellationToken), cancellationToken);

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Client?.Dispose();
                Client = null;
                BinaryManager?.Dispose();
                BinaryManager = null;
                Instance = null;
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Thin accessor used by <see cref="ContinueBinaryManager"/> (in the Binary namespace)
    /// to read the options page without creating a circular project reference.
    /// </summary>
    internal static class ContinueVSPackageAccessor
    {
        internal static ContinueVSPackage? Instance => ContinueVSPackage.Instance;
    }
}

