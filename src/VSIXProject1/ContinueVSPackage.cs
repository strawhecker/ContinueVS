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

        /// <summary>Bridge logger facade instance (Step 25).</summary>
        public static IBridgeLogger? Logger { get; private set; }

        /// <summary>Bridge telemetry collector instance (Step 26).</summary>
        public static IBridgeTelemetryCollector? TelemetryCollector { get; private set; }

        /// <summary>Feature flag for bridge mode (Step 40). Set during InitializeAsync from ContinueOptionsPage.EnableBridgeMode.</summary>
        public static bool EnableBridgeMode { get; private set; } = true;

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

            // Initialize bridge logger (Step 25)
            Logger = new BridgeLogger(this);

            // Initialize bridge telemetry collector (Step 26)
            var telemetryCollector = new BridgeTelemetryCollector();
            TelemetryCollector = telemetryCollector;

            // Update telemetry cache after options page is ready
            var optionsPage = GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
            bool telemetryEnabled = !(optionsPage?.DisableTelemetry ?? false);
            telemetryCollector.SetTelemetryEnabled(telemetryEnabled);

            // Ensure the options page is initialized with the active version
            if (optionsPage != null)
            {
                optionsPage.ActiveBridgeVersion = VersionManager.GetActiveVersion();

                // Cache the EnableBridgeMode feature flag for access by BridgeLifecycleManager (Step 45)
                EnableBridgeMode = optionsPage.EnableBridgeMode;
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
                Logger = null;
                TelemetryCollector = null;
                EnableBridgeMode = true; // Reset to default
            }

            base.Dispose(disposing);
        }
    }
}

