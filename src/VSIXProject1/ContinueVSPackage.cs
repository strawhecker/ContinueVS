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
using System.Windows;
using System.Diagnostics;

namespace ContinueVS
{
    /// <summary>
    /// Continue for Visual Studio — AsyncPackage entry point.
    /// Loads asynchronously so VS startup is not blocked.
    /// Package must load early via ProvideAutoLoad to register keybindings before user input.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ContinueGuids.PackageGuidString)]
    [ProvideAutoLoad("F1536EF8-92EC-443C-9ED7-FDADF150DA82", PackageAutoLoadFlags.BackgroundLoad)] // UIContextGuidShellInitialized
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(ContinueToolWindowPane),
        Style = VsDockStyle.Tabbed,
        Window = EnvDTE.Constants.vsWindowKindSolutionExplorer)]
    public sealed partial class ContinueVSPackage : AsyncPackage
    {
        /// <summary>Singleton reference set during InitializeAsync, cleared on Dispose.</summary>
        public static ContinueVSPackage? Instance { get; private set; }

        /// <summary>Flag to track when command registration is complete.</summary>
        public static bool CommandsRegistered { get; private set; } = false;

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

        /// <summary>
        /// Package constructor.
        /// </summary>
        public ContinueVSPackage()
        {
            // Set Instance immediately in constructor so it's available for keybinding handlers
            // This MUST happen before InitializeAsync is called
            Instance = this;

            // Write to a file immediately to verify this code runs
            var debugFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".continueVS", "logs", "DEBUG_Ctrl_Shift_J.txt");
            try
            {
                System.IO.File.AppendAllText(debugFile, $"[{DateTime.Now:HH:mm:ss.fff}] CONSTRUCTOR: Package created, Instance set\n");
            }
            catch { }
        }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // BREAKPOINT: t1 - Set breakpoint here to inspect InitializeAsync entry
            LoggerService.Current.WriteDebug("[CV-ENTRY] InitializeAsync called - EXTENSION IS LOADED");

            // Register Ctrl+Shift+J command handler FIRST, before any other initialization
            // This ensures the keybinding works as soon as possible
            LoggerService.Current.WriteDebug("[CV] PRIORITY: Registering ShowContinuePanel command immediately...");

            // Try to register command synchronously first using GetService (not async)
            // This ensures the command is available before any async operations
            try
            {
                // Use synchronous GetService to avoid timing issues
                // VSTHRD103 is suppressed here because keybinding registration MUST be synchronous
                // to function correctly before any user input
#pragma warning disable VSTHRD103
                var cmdService = GetService(typeof(IMenuCommandService)) as IMenuCommandService;
#pragma warning restore VSTHRD103

                LoggerService.Current.WriteDebug($"[CV] Synchronous GetService returned: {(cmdService != null ? "✓ service" : "✗ NULL")}");

                if (cmdService != null)
                {
                    var cmdId = new CommandID(ContinueGuids.CmdSetGuid, ContinueCommandIds.ShowContinuePanel);
                    LoggerService.Current.WriteDebug($"[CV] Looking for existing command: GUID={cmdId.Guid}, ID={cmdId.ID}");
                    var existingCmd = cmdService.FindCommand(cmdId);
                    LoggerService.Current.WriteDebug($"[CV] FindCommand returned: {(existingCmd != null ? "FOUND" : "NOT FOUND")}");

                    if (existingCmd == null)
                    {
                        // Use OleMenuCommand for better command handling
                        var command = new OleMenuCommand(
                            (s, e) =>
                            {
                                var debugFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".continueVS", "logs", "DEBUG_Ctrl_Shift_J.txt");
                                try { System.IO.File.AppendAllText(debugFile, $"[{DateTime.Now:HH:mm:ss.fff}] HANDLER FIRED: ShowContinuePanel command executed\n"); } catch { }

                                LoggerService.Current.WriteDebug("[CV] [HANDLER] ShowContinuePanel command EXECUTED via OleMenuCommand!");
                                try
                                {
                                    ThreadHelper.ThrowIfNotOnUIThread();
                                    ShowContinueToolWindowCommand.Execute();
                                }
                                catch (Exception handlerEx)
                                {
                                    try { System.IO.File.AppendAllText(debugFile, $"[{DateTime.Now:HH:mm:ss.fff}] HANDLER ERROR: {handlerEx.Message}\n"); } catch { }
                                    LoggerService.Current.WriteError($"[CV] [HANDLER] Exception in command handler: {handlerEx.Message}", handlerEx);
                                }
                            },
                            cmdId);

                        // Handle QueryStatus to ensure command appears as enabled
                        command.BeforeQueryStatus += (s, e) =>
                        {
                            LoggerService.Current.WriteDebug("[CV] ShowContinuePanel QueryStatus called");
                            command.Enabled = true;
                            command.Visible = true;
                        };

                        var debugFile2 = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".continueVS", "logs", "DEBUG_Ctrl_Shift_J.txt");
                        try { System.IO.File.AppendAllText(debugFile2, $"[{DateTime.Now:HH:mm:ss.fff}] About to call AddCommand for ShowContinuePanel (sync)...\n"); } catch { }

                        LoggerService.Current.WriteDebug("[CV] About to call AddCommand...");
                        cmdService.AddCommand(command);
                        LoggerService.Current.WriteDebug("[CV] AddCommand completed");

                        try { System.IO.File.AppendAllText(debugFile2, $"[{DateTime.Now:HH:mm:ss.fff}] ✓ Command registered (sync)\n"); } catch { }

                        // Verify it was actually added
                        var verifyCmd = cmdService.FindCommand(cmdId);
                        LoggerService.Current.WriteDebug($"[CV] Verification: Command now {(verifyCmd != null ? "EXISTS" : "MISSING")} after AddCommand!");
                        try { System.IO.File.AppendAllText(debugFile2, $"[{DateTime.Now:HH:mm:ss.fff}] Verification: Command {(verifyCmd != null ? "FOUND" : "NOT FOUND")} in service\n"); } catch { }

                        CommandsRegistered = true;
                        LoggerService.Current.WriteDebug("[CV] ✓ ShowContinuePanel command registered (OleMenuCommand) via SYNCHRONOUS GetService");
                    }
                    else
                    {
                        CommandsRegistered = true;
                        LoggerService.Current.WriteDebug("[CV] ✓ ShowContinuePanel command already registered");
                    }
                }
                else
                {
                    LoggerService.Current.WriteDebug("[CV] ⚠ OleMenuCommandService not available via GetService, will retry async...");

                    // Fallback: try async GetServiceAsync
                    var cmdService2 = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
                    if (cmdService2 != null)
                    {
                        var cmdId = new CommandID(ContinueGuids.CmdSetGuid, ContinueCommandIds.ShowContinuePanel);
                        var existingCmd = cmdService2.FindCommand(cmdId);
                        if (existingCmd == null)
                        {
                            var command = new OleMenuCommand(
                                (s, e) =>
                                {
                                    var debugFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".continueVS", "logs", "DEBUG_Ctrl_Shift_J.txt");
                                    try { System.IO.File.AppendAllText(debugFile, $"[{DateTime.Now:HH:mm:ss.fff}] ASYNC HANDLER FIRED: ShowContinuePanel command executed\n"); } catch { }

                                    LoggerService.Current.WriteDebug("[CV] [HANDLER-ASYNC] ShowContinuePanel command EXECUTED via async registration!");
                                    try
                                    {
                                        ThreadHelper.ThrowIfNotOnUIThread();
                                        ShowContinueToolWindowCommand.Execute();
                                    }
                                    catch (Exception handlerEx)
                                    {
                                        try { System.IO.File.AppendAllText(debugFile, $"[{DateTime.Now:HH:mm:ss.fff}] ASYNC HANDLER ERROR: {handlerEx.Message}\n"); } catch { }
                                        LoggerService.Current.WriteError($"[CV] [HANDLER-ASYNC] Exception in async command handler: {handlerEx.Message}", handlerEx);
                                    }
                                },
                                cmdId);

                            command.BeforeQueryStatus += (s, e) =>
                            {
                                LoggerService.Current.WriteDebug("[CV] [ASYNC] ShowContinuePanel QueryStatus called");
                                command.Enabled = true;
                                command.Visible = true;
                            };

                            cmdService2.AddCommand(command);
                            CommandsRegistered = true;
                            LoggerService.Current.WriteDebug("[CV] ✓ ShowContinuePanel command registered via ASYNC GetServiceAsync (fallback)");
                        }
                    }
                    else
                    {
                        LoggerService.Current.WriteError("[CV] ✗ OleMenuCommandService not available even via async GetServiceAsync", null);
                    }
                }
            }
            catch (Exception cmdEx)
            {
                LoggerService.Current.WriteError($"[CV] ✗ Command registration failed: {cmdEx.Message}", cmdEx);
            }

            var tracer = new ExecutionTracer();
            ExecutionTracer = tracer;
            LoggerService.Current.WriteDebug("╔════════════════════════════════════════════════╗");
            LoggerService.Current.WriteDebug("║  [ContinueVS] InitializeAsync START            ║");
            LoggerService.Current.WriteDebug("╚════════════════════════════════════════════════╝");

            try
            {
                // BREAKPOINT: t1.1 - Thread switch verification
                LoggerService.Current.WriteDebug("[CV] Step 1: Switching to main thread...");
                using (tracer.BeginScope("t1.1", "ContinueVSPackage"))
                {
                    await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                }
                LoggerService.Current.WriteDebug("[CV] ✓ Main thread switch complete");

                // BREAKPOINT: t1.2 - Instance setup
                LoggerService.Current.WriteDebug("[CV] Step 2: Instance already set in constructor");
                // Instance = this; -- moved to constructor to ensure it's available immediately

                // BREAKPOINT: t1.3 - Service creation phase
                LoggerService.Current.WriteDebug("[CV] Step 3: Creating VersionSelectorService...");
                using (tracer.BeginScope("t1.3.1", "ContinueVSPackage"))
                {
                    var versionSelector = new VersionSelectorService();
                    LoggerService.Current.WriteDebug("[CV] ✓ VersionSelectorService created");

                    LoggerService.Current.WriteDebug("[CV] Step 4: Creating VersionManager...");
                    using (tracer.BeginScope("t1.3.2", "ContinueVSPackage"))
                    {
                        VersionManager = new VersionManager(versionSelector);
                    }
                    LoggerService.Current.WriteDebug("[CV] ✓ VersionManager created");

                    LoggerService.Current.WriteDebug("[CV] Step 5: Creating DowngradeWarningService...");
                    using (tracer.BeginScope("t1.3.3", "ContinueVSPackage"))
                    {
                        DowngradeWarningService = new Services.DowngradeWarningService();
                    }
                    LoggerService.Current.WriteDebug("[CV] ✓ DowngradeWarningService created");
                }

                // Options page has been removed; skip configuration dialog setup
                LoggerService.Current.WriteDebug("[CV] Step 8: Skipping options page access (removed)");

                // Tool window creation is deferred
                LoggerService.Current.WriteDebug("[CV] Step 11: Tool window creation deferred (will initialize on-demand)");

                // DI Container Initialization
                LoggerService.Current.WriteDebug("[CV] Step 10: Initializing DI container via ServiceBootstrapper...");
                using (tracer.BeginScope("t1.4.4", "ContinueVSPackage"))
                {
                    try
                    {
                        ServiceProvider = ServiceBootstrapper.ConfigureServices();
                        Logger = ServiceProvider.GetService(typeof(IBridgeLogger)) as IBridgeLogger;
                        LoggerService.Current.WriteDebug("[CV] ✓ DI container initialized; ServiceProvider ready");
                    }
                    catch (Exception diEx)
                    {
                        LoggerService.Current.WriteError($"[CV] ✗ DI initialization failed: {diEx.Message}", diEx);
                        throw;
                    }
                }

                // Service initialization (Step 98 - critical for config service)
                LoggerService.Current.WriteDebug("[CV] Step 11: Initializing services via ServiceInitializer...");
                using (tracer.BeginScope("t1.4.5", "ContinueVSPackage"))
                {
                    try
                    {
                        await ServiceInitializer.InitializeAsync(ServiceProvider);
                        LoggerService.Current.WriteDebug("[CV] ✓ Services initialized successfully");
                    }
                    catch (Exception siEx)
                    {
                        LoggerService.Current.WriteError($"[CV] ✗ Service initialization failed: {siEx.Message}", siEx);
                        throw;
                    }
                }

                // Setup ViewModelLocator for XAML binding (Step 98)
                LoggerService.Current.WriteDebug("[CV] Step 12: Setting up ViewModelLocator...");
                using (tracer.BeginScope("t1.4.6", "ContinueVSPackage"))
                {
                    try
                    {
                        ViewModelLocator.ServiceProvider = ServiceProvider;
                        LoggerService.Current.WriteDebug("[CV] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (Exception vmEx)
                    {
                        LoggerService.Current.WriteError($"[CV] ✗ ViewModelLocator setup failed: {vmEx.Message}", vmEx);
                        throw;
                    }
                }

                // Register Ctrl+Shift+J command handler with VS OleMenuCommandService
                // NOTE: Command is registered at the START of InitializeAsync
                LoggerService.Current.WriteDebug("[CV] Step 14: Command registration already completed at start of InitializeAsync");

                // Tool window is shown on-demand (Ctrl+Shift+J) — do NOT call FindToolWindow here,
                // as VS cannot create a window frame while the package is still loading (COMException 0x80049283).
                LoggerService.Current.WriteDebug("[CV] Step 13: Tool window deferred to on-demand (Ctrl+Shift+J).");

                LoggerService.Current.WriteDebug("╔════════════════════════════════════════════════╗");
                LoggerService.Current.WriteDebug("║  [ContinueVS] InitializeAsync END - SUCCESS ✓  ║");
                LoggerService.Current.WriteDebug("╚════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError("╔════════════════════════════════════════════════╗");
                LoggerService.Current.WriteError("║  [ContinueVS] InitializeAsync FAILED ✗         ║", ex);
                LoggerService.Current.WriteError("╚════════════════════════════════════════════════╝");
                LoggerService.Current.WriteError($"[CV] Exception Type: {ex.GetType().FullName}", ex);
                LoggerService.Current.WriteError($"[CV] Exception Message: {ex.Message}", ex);
                LoggerService.Current.WriteError($"[CV] Stack Trace: {ex.StackTrace}", ex);

                if (ex.InnerException != null)
                {
                    LoggerService.Current.WriteError($"[CV] Inner Exception Type: {ex.InnerException.GetType().FullName}", ex);
                    LoggerService.Current.WriteError($"[CV] Inner Exception Message: {ex.InnerException.Message}", ex);
                }

                LoggerService.Current.WriteError($"[CV] InitializeAsync failed: {ex.GetType().FullName}: {ex.Message}", ex);

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
            LoggerService.Current.WriteDebug("[CV] Step 13: Creating tool window pane...");

            var tracer = ExecutionTracer;
            IDisposable? scope = tracer?.BeginScope("t3", "ContinueVSPackage.CreateToolWindowPaneAsync");
            try
            {
                // Find or create the tool window pane (ContinueToolWindowPane creates its own WPF control)
                LoggerService.Current.WriteDebug("[CV-t3] Finding/creating ContinueToolWindowPane...");
                var windowPane = FindToolWindow(typeof(ContinueToolWindowPane), 0, create: true) as ToolWindowPane;
                if (windowPane != null)
                {
                    LoggerService.Current.WriteDebug("[CV-t3] ✓ Tool window pane found/created");

                    // Show the tool window
                    await this.ShowToolWindowAsync(typeof(ContinueToolWindowPane), 0, create: true, cancellationToken: cancellationToken);
                    LoggerService.Current.WriteDebug("[CV-t3] ✓ Tool window shown");
                }
                else
                {
                    LoggerService.Current.WriteDebug("[CV-t3] ✗ Tool window pane not found");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[CV-t3] ✗ Exception during tool window creation: {ex.GetType().Name}: {ex.Message}", ex);
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
                CommandsRegistered = false;
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

