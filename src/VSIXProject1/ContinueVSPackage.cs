using ContinueVS.Commands;
using ContinueVS.Diagnostics;
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

        /// <summary>Execution tracer for t1 step instrumentation. Populated during InitializeAsync for debugging.</summary>
        public static IExecutionTracer? ExecutionTracer { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // BREAKPOINT: t1 - Set breakpoint here to inspect InitializeAsync entry
            var tracer = new ExecutionTracer();
            ExecutionTracer = tracer;
            System.Diagnostics.Debug.WriteLine("╔════════════════════════════════════════════════╗");
            System.Diagnostics.Debug.WriteLine("║  [ContinueVS] InitializeAsync START            ║");
            System.Diagnostics.Debug.WriteLine("╚════════════════════════════════════════════════╝");

            try
            {
                // BREAKPOINT: t1.1 - Thread switch verification
                System.Diagnostics.Debug.WriteLine("[CV] Step 1: Switching to main thread...");
                using (tracer.BeginScope("t1.1", "ContinueVSPackage"))
                {
                    await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                }
                System.Diagnostics.Debug.WriteLine("[CV] ✓ Main thread switch complete");

                // BREAKPOINT: t1.2 - Instance setup
                System.Diagnostics.Debug.WriteLine("[CV] Step 2: Setting Instance...");
                using (tracer.BeginScope("t1.2", "ContinueVSPackage"))
                {
                    Instance = this;
                }
                System.Diagnostics.Debug.WriteLine("[CV] ✓ Instance set");

                // BREAKPOINT: t1.3 - Service creation phase
                System.Diagnostics.Debug.WriteLine("[CV] Step 3: Creating VersionSelectorService...");
                using (tracer.BeginScope("t1.3.1", "ContinueVSPackage"))
                {
                    var versionSelector = new VersionSelectorService();
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ VersionSelectorService created");

                    System.Diagnostics.Debug.WriteLine("[CV] Step 4: Creating VersionManager...");
                    using (tracer.BeginScope("t1.3.2", "ContinueVSPackage"))
                    {
                        VersionManager = new VersionManager(versionSelector);
                    }
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ VersionManager created");

                    System.Diagnostics.Debug.WriteLine("[CV] Step 5: Creating DowngradeWarningService...");
                    using (tracer.BeginScope("t1.3.3", "ContinueVSPackage"))
                    {
                        DowngradeWarningService = new Services.DowngradeWarningService();
                    }
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ DowngradeWarningService created");

                    System.Diagnostics.Debug.WriteLine("[CV] Step 6: Creating BridgeLogger...");
                    using (tracer.BeginScope("t1.3.4", "ContinueVSPackage"))
                    {
                        Logger = new BridgeLogger(this);
                    }
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ BridgeLogger created");
                    System.Diagnostics.Debug.WriteLine($"[CV-t2] Logger instance created: {(Logger != null ? "✓ SUCCESS" : "✗ NULL")}");
                    System.Diagnostics.Debug.WriteLine($"[CV-t2] Logger type: {Logger?.GetType().Name ?? "null"}");

                    System.Diagnostics.Debug.WriteLine("[CV] Step 7: Creating BridgeTelemetryCollector...");
                    using (tracer.BeginScope("t1.3.5", "ContinueVSPackage"))
                    {
                        var telemetryCollector = new BridgeTelemetryCollector();
                        TelemetryCollector = telemetryCollector;
                    }
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ BridgeTelemetryCollector created");
                }

                // BREAKPOINT: t1.4 - Options page and configuration
                System.Diagnostics.Debug.WriteLine("[CV] Step 8: Getting options page...");
                using (tracer.BeginScope("t1.4", "ContinueVSPackage"))
                {
                    var optionsPage = GetDialogPage(typeof(ContinueOptionsPage)) as ContinueOptionsPage;
                    System.Diagnostics.Debug.WriteLine($"[CV] ✓ Options page retrieved: {(optionsPage != null ? "EXISTS" : "NULL")}");

                    if (optionsPage != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CV] Step 9a: Setting telemetry enabled...");
                        using (tracer.BeginScope("t1.4.1", "ContinueVSPackage"))
                        {
                            bool telemetryEnabled = !optionsPage.DisableTelemetry;
                            // Telemetry will be configured through the collector once it's retrieved
                            System.Diagnostics.Debug.WriteLine($"[CV] ✓ Telemetry enabled: {telemetryEnabled}");
                        }

                        System.Diagnostics.Debug.WriteLine("[CV] Step 9b: Setting active bridge version...");
                        using (tracer.BeginScope("t1.4.2", "ContinueVSPackage"))
                        {
                            optionsPage.ActiveBridgeVersion = VersionManager!.GetActiveVersion();
                            System.Diagnostics.Debug.WriteLine($"[CV] ✓ Active version: {optionsPage.ActiveBridgeVersion}");
                        }

                        System.Diagnostics.Debug.WriteLine("[CV] Step 9c: Caching EnableBridgeMode...");
                        using (tracer.BeginScope("t1.4.3", "ContinueVSPackage"))
                        {
                            EnableBridgeMode = optionsPage.EnableBridgeMode;
                        }
                        System.Diagnostics.Debug.WriteLine($"[CV] ✓ EnableBridgeMode: {EnableBridgeMode}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CV] ⚠ Options page is NULL, skipping telemetry/version setup");
                    }
                }

                // BREAKPOINT: t1.5 - Command initialization phase
                System.Diagnostics.Debug.WriteLine("[CV] Step 10: Initializing commands...");
                using (tracer.BeginScope("t1.5", "ContinueVSPackage"))
                {
                    await ShowContinuePanelCommand.InitializeAsync(this);
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ ShowContinuePanelCommand initialized");

                    await AskContinueCommand.InitializeAsync(this);
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ AskContinueCommand initialized");

                    await ExplainCodeCommand.InitializeAsync(this);
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ ExplainCodeCommand initialized");

                    await FixCodeCommand.InitializeAsync(this);
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ FixCodeCommand initialized");

                    await AddCommentCommand.InitializeAsync(this);
                    System.Diagnostics.Debug.WriteLine("[CV] ✓ AddCommentCommand initialized");
                }

                System.Diagnostics.Debug.WriteLine("╔════════════════════════════════════════════════╗");
                System.Diagnostics.Debug.WriteLine("║  [ContinueVS] InitializeAsync END - SUCCESS ✓  ║");
                System.Diagnostics.Debug.WriteLine("╚════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("╔════════════════════════════════════════════════╗");
                System.Diagnostics.Debug.WriteLine("║  [ContinueVS] InitializeAsync FAILED ✗         ║");
                System.Diagnostics.Debug.WriteLine("╚════════════════════════════════════════════════╝");
                System.Diagnostics.Debug.WriteLine($"[CV] Exception Type: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"[CV] Exception Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CV] Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[CV] Inner Exception Type: {ex.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"[CV] Inner Exception Message: {ex.InnerException.Message}");
                }

                throw;
            }
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

