using ContinueVS.Diagnostics;
using ContinueVS.Services;
using ContinueVS.UI;
using ContinueVS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
    [ProvideToolWindow(typeof(ContinueToolWindowControl),
        Style = VsDockStyle.Tabbed,
        Window = EnvDTE.Constants.vsWindowKindSolutionExplorer)]
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
        public static IExecutionTracer? ExecutionTracer { get; internal set; }

        /// <summary>Support for dependency injection (optional, for service registration).</summary>
        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // BREAKPOINT: t1 - Set breakpoint here to inspect InitializeAsync entry
            System.Diagnostics.Debug.WriteLine(">>> [CV-ENTRY] InitializeAsync called - EXTENSION IS LOADED <<<");
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
                }

                // Options page has been removed; skip configuration dialog setup
                System.Diagnostics.Debug.WriteLine("[CV] Step 8: Skipping options page access (removed)");

                // Tool window creation is deferred
                System.Diagnostics.Debug.WriteLine("[CV] Step 11: Tool window creation deferred (will initialize on-demand)");

                // DI Container Initialization
                System.Diagnostics.Debug.WriteLine("[CV] Step 10: Initializing DI container via ServiceBootstrapper...");
                using (tracer.BeginScope("t1.4.4", "ContinueVSPackage"))
                {
                    try
                    {
                        ServiceProvider = ServiceBootstrapper.ConfigureServices();
                        System.Diagnostics.Debug.WriteLine("[CV] ✓ DI container initialized; ServiceProvider ready");
                    }
                    catch (Exception diEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CV] ✗ DI initialization failed: {diEx.Message}");
                        throw;
                    }
                }

                // Service initialization (Step 98 - critical for config service)
                System.Diagnostics.Debug.WriteLine("[CV] Step 11: Initializing services via ServiceInitializer...");
                using (tracer.BeginScope("t1.4.5", "ContinueVSPackage"))
                {
                    try
                    {
                        await ServiceInitializer.InitializeAsync(ServiceProvider);
                        System.Diagnostics.Debug.WriteLine("[CV] ✓ Services initialized successfully");
                    }
                    catch (Exception siEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CV] ✗ Service initialization failed: {siEx.Message}");
                        throw;
                    }
                }

                // Setup ViewModelLocator for XAML binding (Step 98)
                System.Diagnostics.Debug.WriteLine("[CV] Step 12: Setting up ViewModelLocator...");
                using (tracer.BeginScope("t1.4.6", "ContinueVSPackage"))
                {
                    try
                    {
                        ViewModelLocator.ServiceProvider = ServiceProvider;
                        System.Diagnostics.Debug.WriteLine("[CV] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (Exception vmEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CV] ✗ ViewModelLocator setup failed: {vmEx.Message}");
                        throw;
                    }
                }

                // Create and display WPF tool window (Step 98)
                System.Diagnostics.Debug.WriteLine("[CV] Step 13: Creating WPF tool window...");
                using (tracer.BeginScope("t1.4.7", "ContinueVSPackage"))
                {
                    try
                    {
                        await CreateToolWindowPaneAsync(cancellationToken);
                        System.Diagnostics.Debug.WriteLine("[CV] ✓ WPF tool window created and displayed");
                    }
                    catch (Exception twEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CV] ✗ Tool window creation failed: {twEx.Message}");
                        throw;
                    }
                }

                // Command initialization skipped (no remaining command classes)

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

        /// <summary>
        /// Creates and initializes the Continue Tool Window Pane during package initialization (Step t3).
        /// Wraps ShowToolWindowAsync with execution tracing for debugging.
        /// </summary>
        private async Task CreateToolWindowPaneAsync(CancellationToken cancellationToken)
        {
            // BREAKPOINT: t3 - Set breakpoint here to inspect tool window pane creation
            System.Diagnostics.Debug.WriteLine("[CV] Step 13: Creating tool window pane...");

            var tracer = ExecutionTracer;
            IDisposable? scope = tracer?.BeginScope("t3", "ContinueVSPackage.CreateToolWindowPaneAsync");
            try
            {
                // Create and instantiate the WPF UserControl
                System.Diagnostics.Debug.WriteLine("[CV-t3] Instantiating ContinueToolWindowControl (WPF)...");
                var toolControl = new ContinueToolWindowControl();
                System.Diagnostics.Debug.WriteLine("[CV-t3] ✓ ContinueToolWindowControl instantiated");

                // Get the tool window pane (VS framework integration)
                System.Diagnostics.Debug.WriteLine("[CV-t3] Finding tool window pane...");
                var windowPane = FindToolWindow(typeof(ContinueToolWindowControl), 0, create: true) as ToolWindowPane;
                if (windowPane != null)
                {
                    System.Diagnostics.Debug.WriteLine("[CV-t3] ✓ Tool window pane found/created");

                    // Set the WPF control as the content of the tool window pane
                    windowPane.Content = toolControl;
                    System.Diagnostics.Debug.WriteLine("[CV-t3] ✓ WPF control set as tool window content");

                    // Show the tool window
                    await this.ShowToolWindowAsync(typeof(ContinueToolWindowControl), 0, create: true, cancellationToken: cancellationToken);
                    System.Diagnostics.Debug.WriteLine("[CV-t3] ✓ Tool window shown");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[CV-t3] ✗ Tool window pane not found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t3] ✗ Exception during tool window creation: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[CV-t3] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CV-t3] Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                scope?.Dispose();
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

