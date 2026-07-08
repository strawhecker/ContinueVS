using ContinueVS.Commands;
using ContinueVS.Services;
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
    public sealed partial class ContinueVSPackage : AsyncPackage
    {
        /// <summary>Singleton reference set during InitializeAsync, cleared on Dispose.</summary>
        public static ContinueVSPackage? Instance { get; private set; }

        /// <summary>Version manager service instance.</summary>
        public static VersionManager? VersionManager { get; private set; }

        /// <summary>Downgrade warning service instance (Step 10).</summary>
        public static DowngradeWarningService? DowngradeWarningService { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            Instance = this;

            // Initialize version manager and selector services
            var versionSelector = new VersionSelectorService();
            VersionManager = new VersionManager(versionSelector);

            // Initialize downgrade warning service (Step 10)
            DowngradeWarningService = new Services.DowngradeWarningService();

            // Ensure the options page is initialized with the active version
            var optionsPage = GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
            if (optionsPage != null)
            {
                optionsPage.ActiveBridgeVersion = VersionManager.GetActiveVersion();
            }

            await ShowContinuePanelCommand.InitializeAsync(this);
            await AskContinueCommand.InitializeAsync(this);
            await ExplainCodeCommand.InitializeAsync(this);
            await FixCodeCommand.InitializeAsync(this);
            await AddCommentCommand.InitializeAsync(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Instance = null;
                VersionManager = null;
                DowngradeWarningService = null;
            }

            base.Dispose(disposing);
        }
    }
}

