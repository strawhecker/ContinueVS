using ContinueVS.Commands;
using ContinueVS.Diagnostics;
using ContinueVS.Services;
using ContinueVS.UI;
using ContinueVS.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
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
            await LoggerService.Current.WriteDebugAsync("[CV-ENTRY] InitializeAsync called - EXTENSION IS LOADED");
            var tracer = new ExecutionTracer();
            ExecutionTracer = tracer;
            await LoggerService.Current.WriteDebugAsync("╔════════════════════════════════════════════════╗");
            await LoggerService.Current.WriteDebugAsync("║  [ContinueVS] InitializeAsync START            ║");
            await LoggerService.Current.WriteDebugAsync("╚════════════════════════════════════════════════╝");

            try
            {
                // BREAKPOINT: t1.1 - Thread switch verification
                await LoggerService.Current.WriteDebugAsync("[CV] Step 1: Switching to main thread...");
                using (tracer.BeginScope("t1.1", "ContinueVSPackage"))
                {
                    await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                }
                await LoggerService.Current.WriteDebugAsync("[CV] ✓ Main thread switch complete");

                // BREAKPOINT: t1.2 - Instance setup
                await LoggerService.Current.WriteDebugAsync("[CV] Step 2: Setting Instance...");
                using (tracer.BeginScope("t1.2", "ContinueVSPackage"))
                {
                    Instance = this;
                }
                await LoggerService.Current.WriteDebugAsync("[CV] ✓ Instance set");

                // BREAKPOINT: t1.3 - Service creation phase
                await LoggerService.Current.WriteDebugAsync("[CV] Step 3: Creating VersionSelectorService...");
                using (tracer.BeginScope("t1.3.1", "ContinueVSPackage"))
                {
                    var versionSelector = new VersionSelectorService();
                    await LoggerService.Current.WriteDebugAsync("[CV] ✓ VersionSelectorService created");

                    await LoggerService.Current.WriteDebugAsync("[CV] Step 4: Creating VersionManager...");
                    using (tracer.BeginScope("t1.3.2", "ContinueVSPackage"))
                    {
                        VersionManager = new VersionManager(versionSelector);
                    }
                    await LoggerService.Current.WriteDebugAsync("[CV] ✓ VersionManager created");

                    await LoggerService.Current.WriteDebugAsync("[CV] Step 5: Creating DowngradeWarningService...");
                    using (tracer.BeginScope("t1.3.3", "ContinueVSPackage"))
                    {
                        DowngradeWarningService = new Services.DowngradeWarningService();
                    }
                    await LoggerService.Current.WriteDebugAsync("[CV] ✓ DowngradeWarningService created");
                }

                // Options page has been removed; skip configuration dialog setup
                await LoggerService.Current.WriteDebugAsync("[CV] Step 8: Skipping options page access (removed)");

                // Tool window creation is deferred
                await LoggerService.Current.WriteDebugAsync("[CV] Step 11: Tool window creation deferred (will initialize on-demand)");

                // DI Container Initialization
                await LoggerService.Current.WriteDebugAsync("[CV] Step 10: Initializing DI container via ServiceBootstrapper...");
                using (tracer.BeginScope("t1.4.4", "ContinueVSPackage"))
                {
                    try
                    {
                        ServiceProvider = ServiceBootstrapper.ConfigureServices();
                        Logger = ServiceProvider.GetService(typeof(IBridgeLogger)) as IBridgeLogger;
                        await LoggerService.Current.WriteDebugAsync("[CV] ✓ DI container initialized; ServiceProvider ready");
                    }
                    catch (Exception diEx)
                    {
                        await LoggerService.Current.WriteErrorAsync($"[CV] ✗ DI initialization failed: {diEx.Message}", diEx);
                        throw;
                    }
                }

                // Service initialization (Step 98 - critical for config service)
                await LoggerService.Current.WriteDebugAsync("[CV] Step 11: Initializing services via ServiceInitializer...");
                using (tracer.BeginScope("t1.4.5", "ContinueVSPackage"))
                {
                    try
                    {
                        await ServiceInitializer.InitializeAsync(ServiceProvider);
                        await LoggerService.Current.WriteDebugAsync("[CV] ✓ Services initialized successfully");
                    }
                    catch (Exception siEx)
                    {
                        await LoggerService.Current.WriteErrorAsync($"[CV] ✗ Service initialization failed: {siEx.Message}", siEx);
                        throw;
                    }
                }

                // Setup ViewModelLocator for XAML binding (Step 98)
                await LoggerService.Current.WriteDebugAsync("[CV] Step 12: Setting up ViewModelLocator...");
                using (tracer.BeginScope("t1.4.6", "ContinueVSPackage"))
                {
                    try
                    {
                        ViewModelLocator.ServiceProvider = ServiceProvider;
                        await LoggerService.Current.WriteDebugAsync("[CV] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (Exception vmEx)
                    {
                        await LoggerService.Current.WriteErrorAsync($"[CV] ✗ ViewModelLocator setup failed: {vmEx.Message}", vmEx);
                        throw;
                    }
                }

                // Register Ctrl+Shift+J command handler with VS OleMenuCommandService
                await LoggerService.Current.WriteDebugAsync("[CV] Step 14: Registering ShowContinuePanel command...");
                using (tracer.BeginScope("t1.4.8", "ContinueVSPackage"))
                {
                    var cmdService = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
                    if (cmdService != null)
                    {
                        var cmdId = new CommandID(ContinueGuids.CmdSetGuid, ContinueCommandIds.ShowContinuePanel);
                        cmdService.AddCommand(new MenuCommand((s, e) => ShowContinueToolWindowCommand.Execute(), cmdId));
                        await LoggerService.Current.WriteDebugAsync("[CV] ✓ ShowContinuePanel command registered");
                    }
                    else
                    {
                        await LoggerService.Current.WriteErrorAsync("[CV] ✗ OleMenuCommandService not available", null);
                    }
                }

                // Tool window is shown on-demand (Ctrl+Shift+J) — do NOT call FindToolWindow here,
                // as VS cannot create a window frame while the package is still loading (COMException 0x80049283).
                await LoggerService.Current.WriteDebugAsync("[CV] Step 13: Tool window deferred to on-demand (Ctrl+Shift+J).");

                await LoggerService.Current.WriteDebugAsync("╔════════════════════════════════════════════════╗");
                await LoggerService.Current.WriteDebugAsync("║  [ContinueVS] InitializeAsync END - SUCCESS ✓  ║");
                await LoggerService.Current.WriteDebugAsync("╚════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("╔════════════════════════════════════════════════╗");
                await LoggerService.Current.WriteErrorAsync("║  [ContinueVS] InitializeAsync FAILED ✗         ║", ex);
                System.Diagnostics.Debug.WriteLine("╚════════════════════════════════════════════════╝");
                await LoggerService.Current.WriteErrorAsync($"[CV] Exception Type: {ex.GetType().FullName}", ex);
                await LoggerService.Current.WriteErrorAsync($"[CV] Exception Message: {ex.Message}", ex);
                await LoggerService.Current.WriteErrorAsync($"[CV] Stack Trace: {ex.StackTrace}", ex);

                if (ex.InnerException != null)
                {
                    await LoggerService.Current.WriteErrorAsync($"[CV] Inner Exception Type: {ex.InnerException.GetType().FullName}", ex);
                    await LoggerService.Current.WriteErrorAsync($"[CV] Inner Exception Message: {ex.InnerException.Message}", ex);
                }

                await LoggerService.Current.WriteErrorAsync($"[CV] InitializeAsync failed: {ex.GetType().FullName}: {ex.Message}", ex);

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
            await LoggerService.Current.WriteDebugAsync("[CV] Step 13: Creating tool window pane...");

            var tracer = ExecutionTracer;
            IDisposable? scope = tracer?.BeginScope("t3", "ContinueVSPackage.CreateToolWindowPaneAsync");
            try
            {
                // Find or create the tool window pane (ContinueToolWindowPane creates its own WPF control)
                await LoggerService.Current.WriteDebugAsync("[CV-t3] Finding/creating ContinueToolWindowPane...");
                var windowPane = FindToolWindow(typeof(ContinueToolWindowPane), 0, create: true) as ToolWindowPane;
                if (windowPane != null)
                {
                    await LoggerService.Current.WriteDebugAsync("[CV-t3] ✓ Tool window pane found/created");

                    // Show the tool window
                    await this.ShowToolWindowAsync(typeof(ContinueToolWindowPane), 0, create: true, cancellationToken: cancellationToken);
                    await LoggerService.Current.WriteDebugAsync("[CV-t3] ✓ Tool window shown");
                }
                else
                {
                    await LoggerService.Current.WriteDebugAsync("[CV-t3] ✗ Tool window pane not found");
                }
            }
            catch (Exception ex)
            {
                await LoggerService.Current.WriteErrorAsync($"[CV-t3] ✗ Exception during tool window creation: {ex.GetType().Name}: {ex.Message}", ex);
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

