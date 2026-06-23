using ContinueVS.Binary;
using ContinueVS.Commands;
using ContinueVS.IPC;
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
    public sealed class ContinueVSPackage : AsyncPackage
    {
        /// <summary>Singleton reference set during InitializeAsync, cleared on Dispose.</summary>
        public static ContinueVSPackage? Instance { get; private set; }

        /// <summary>The binary process manager; available after InitializeAsync completes.</summary>
        internal ContinueBinaryManager? BinaryManager { get; private set; }

        /// <summary>The WebSocket client connected to the binary's IDE endpoint.</summary>
        internal ContinueClient? Client { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Instance = this;

            await ShowContinuePanelCommand.InitializeAsync(this);

            BinaryManager = new ContinueBinaryManager();
            Client        = new ContinueClient();

            // When the binary is ready, connect the WebSocket client.
            BinaryManager.Ready += async (_, port) =>
            {
                try
                {
                    await Client.ConnectAsync(port, cancellationToken);
                    var handler = new IdeCallbackHandler(Client, this);
                    handler.Register();
                }
                catch { /* will retry on next crash/restart cycle */ }
            };

            // Reconnect after crashes.
            BinaryManager.Crashed += async (_, __) =>
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
}

