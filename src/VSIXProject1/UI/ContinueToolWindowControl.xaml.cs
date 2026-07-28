using ContinueVS.Binary;
using ContinueVS.Editor;
using System.Collections.Concurrent;
using ContinueVS.Handlers;
using ContinueVS.Handlers.Bridge;
using ContinueVS.Handlers.Config;
using ContinueVS.Handlers.Context;
using ContinueVS.Handlers.File;
using ContinueVS.Handlers.Ide;
using ContinueVS.Handlers.Llm;
using ContinueVS.Handlers.Push;
using ContinueVS.IPC;
using ContinueVS.Settings;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace ContinueVS.UI
{
    /// <summary>
    /// Interface for sending replies back to the GUI (testable abstraction)
    /// </summary>
    public interface IGuiReplyProvider
    {
        void SendReplyToGui(string messageType, string messageId, object data);
    }
    /// <summary>
    /// WPF UserControl hosting a WebView2 that renders the Continue React GUI.
    ///
    /// The GUI HTML is extracted alongside the binary from the Continue VSIX package.
    /// It communicates with the continue-binary via the stdio IPC client.
    /// </summary>
    public partial class ContinueToolWindowControl : UserControl, IDisposable, IGuiReplyProvider
    {
        private bool _webViewInitialized;
        private bool _disposed;
        private readonly MessageDispatcher _dispatcher = new MessageDispatcher();
        private readonly ConcurrentDictionary<string, System.Threading.Tasks.TaskCompletionSource<JToken?>> _pendingReplies = new ConcurrentDictionary<string, System.Threading.Tasks.TaskCompletionSource<JToken?>>();
        private readonly WebviewPusher _pusher;
        private WorkspaceConfigWatcher? _configWatcher;
        private EditorContextProvider? _editorContextProvider;

        public ContinueToolWindowControl()
        {
            // BREAKPOINT: t4 - Set breakpoint here to inspect ContinueToolWindowControl constructor entry
            System.Diagnostics.Debug.WriteLine("[CV] Step 13: ContinueToolWindowControl ctor START");
            System.Diagnostics.Debug.WriteLine("[CV-t4] Constructor entry");

            var tracer = ContinueVSPackage.ExecutionTracer;
            IDisposable? scope = tracer?.BeginScope("t4", "ContinueToolWindowControl.ctor");

            try
            {
                // t4.1 - InitializeComponent (WPF setup)
                System.Diagnostics.Debug.WriteLine("[CV-t4.1] Invoking InitializeComponent()...");
                IDisposable? scope41 = tracer?.BeginScope("t4.1", "ContinueToolWindowControl.InitializeComponent");
                try
                {
                    InitializeComponent();
                    System.Diagnostics.Debug.WriteLine("[CV-t4.1] ✓ InitializeComponent() complete");
                }
                finally
                {
                    scope41?.Dispose();
                }

                // t4.2 - MessageDispatcher setup
                System.Diagnostics.Debug.WriteLine("[CV-t4.2] MessageDispatcher initialization (already created as field)");
                IDisposable? scope42 = tracer?.BeginScope("t4.2", "ContinueToolWindowControl.MessageDispatcher");
                try
                {
                    // _dispatcher is already initialized as a field
                    System.Diagnostics.Debug.WriteLine("[CV-t4.2] ✓ MessageDispatcher ready");
                }
                finally
                {
                    scope42?.Dispose();
                }

                // t4.3 - Core UI services (WebviewPusher, ConfigWatcher, EditorContextProvider)
                System.Diagnostics.Debug.WriteLine("[CV-t4.3] Creating core UI services...");
                IDisposable? scope43 = tracer?.BeginScope("t4.3", "ContinueToolWindowControl.UIServices");
                try
                {
                    _pusher = new WebviewPusher(this);
                    System.Diagnostics.Debug.WriteLine("[CV-t4.3] ✓ WebviewPusher created");

                    _configWatcher = new WorkspaceConfigWatcher(_pusher);
                    System.Diagnostics.Debug.WriteLine("[CV-t4.3] ✓ WorkspaceConfigWatcher created");

                    _editorContextProvider = new EditorContextProvider(this);
                    System.Diagnostics.Debug.WriteLine("[CV-t4.3] ✓ EditorContextProvider created");
                }
                finally
                {
                    scope43?.Dispose();
                }

                // t4.4 - Handler registration (t5 entry point)
                System.Diagnostics.Debug.WriteLine("[CV-t4.4] Registering message handlers (t5 begins)...");
                IDisposable? scope44 = tracer?.BeginScope("t4.4", "ContinueToolWindowControl.HandlerRegistration");
                try
                {
                    // Bridge bootstrap handler - React sends this first!
                    _dispatcher.Register("bridge:bootstrap", new BootstrapHandler(_dispatcher));

                    _dispatcher.Register("getWorkspaceDirs", new GetWorkspaceDirsHandler(this));
                    _dispatcher.Register("getIdeInfo", new GetIdeInfoHandler(this));
                    _dispatcher.Register("getIdeSettings", new GetIdeSettingsHandler(this));
                    _dispatcher.Register("getUniqueId", new GetUniqueIdHandler(this));
                    _dispatcher.Register("isTelemetryEnabled", new IsTelemetryEnabledHandler(this));
                    _dispatcher.Register("isWorkspaceRemote", new IsWorkspaceRemoteHandler(this));
                    _dispatcher.Register("readFile", new ReadFileHandler(this));
                    _dispatcher.Register("fileExists", new FileExistsHandler(this));
                    _dispatcher.Register("getOpenFiles", new GetOpenFilesHandler(this));
                    _dispatcher.Register("writeFile", new WriteFileHandler(this));
                    _dispatcher.Register("saveFile", new SaveFileHandler(this));
                    _dispatcher.Register("openFile", new OpenFileHandler(this));
                    _dispatcher.Register("openUrl", new OpenUrlHandler(this));
                    _dispatcher.Register("getBranch", new GetBranchHandler(this));
                    _dispatcher.Register("context/getContextItems", new ContextGetContextItemsHandler(this));
                    _dispatcher.Register("context/getSymbolsForFiles", new ContextGetSymbolsForFilesHandler(this));
                    _dispatcher.Register("context/loadSubmenuItems", new ContextLoadSubmenuItemsHandler(this));
                    _dispatcher.Register("context/addDocs", new ContextAddDocsHandler(this));
                    _dispatcher.Register("context/removeDocs", new ContextRemoveDocsHandler(this));
                    _dispatcher.Register("context/indexDocs", new ContextIndexDocsHandler(this));
                    _dispatcher.Register("config/addOpenAiKey", new ConfigAddOpenAiKeyHandler(this));
                    _dispatcher.Register("config/ideSettingsUpdate", new ConfigIdeSettingsUpdateHandler(this));
                    _dispatcher.Register("config/deleteModel", new ConfigDeleteModelHandler(this));
                    _dispatcher.Register("config/getSerializedProfileInfo", new ConfigGetSerializedProfileInfoHandler(this));
                    _dispatcher.Register("config/addModel", new ConfigAddModelHandler(this));
                    _dispatcher.Register("config/addLocalWorkspaceBlock", new ConfigAddLocalWorkspaceBlockHandler(this));
                    _dispatcher.Register("config/addGlobalRule", new ConfigAddGlobalRuleHandler(this));
                    _dispatcher.Register("config/deleteRule", new ConfigDeleteRuleHandler(this));
                    _dispatcher.Register("config/newPromptFile", new ConfigNewPromptFileHandler(this));
                    _dispatcher.Register("config/newAssistantFile", new ConfigNewAssistantFileHandler(this));
                    _dispatcher.Register("config/refreshProfiles", new ConfigRefreshProfilesHandler(this));
                    _dispatcher.Register("config/openProfile", new ConfigOpenProfileHandler(this));
                    _dispatcher.Register("config/updateSharedConfig", new ConfigUpdateSharedConfigHandler(this));
                    _dispatcher.Register("config/updateSelectedModel", new ConfigUpdateSelectedModelHandler(this));
                    _dispatcher.Register("llm/complete", new LlmCompleteHandler(this));
                    _dispatcher.Register("llm/streamChat", new LlmStreamChatHandler(this));
                    _dispatcher.Register("llm/listModels", new LlmListModelsHandler(this));
                    _dispatcher.Register("llm/compileChat", new LlmCompileChatHandler(this));
                    _dispatcher.Register("bridge:getModelInfo", new GetModelInfoHandler(this));
                    _dispatcher.Register("getCurrentFile", new GetCurrentFileHandler(this));
                    _dispatcher.Register("applyToFile", new ApplyToFileHandler(this));
                    _dispatcher.Register("acceptDiff", new AcceptDiffHandler(this));
                    _dispatcher.Register("rejectDiff", new RejectDiffHandler(this));
                    _dispatcher.Register("autocomplete/complete", new AutocompleteCompleteHandler(this));
                    _dispatcher.Register("autocomplete/accept", new AutocompleteAcceptHandler(this));
                    _dispatcher.Register("autocomplete/cancel", new AutocompleteCancelHandler(this));

                    // History handler - GUI requests session history on init
                    _dispatcher.Register("history/load", new GenericReplyHandler(this, new { history = new object[0], title = "New Session", sessionId = "", workspaceDirectory = "" }));
                    _dispatcher.Register("history/list", new GenericReplyHandler(this, new object[0]));
                    _dispatcher.Register("history/save", new GenericReplyHandler(this, new { success = true }));
                    _dispatcher.Register("history/delete", new GenericReplyHandler(this, new { success = true }));
                    _dispatcher.Register("docs/initStatuses", new GenericReplyHandler(this, new object[] { }));
                    _dispatcher.Register("models/fetch", new GenericReplyHandler(this, new object[] { }));

                    System.Diagnostics.Debug.WriteLine("[CV-t4.4] ✓ All 41 handlers registered");
                }
                finally
                {
                    scope44?.Dispose();
                }

                // [b20] Handler registration verification
                System.Diagnostics.Debug.WriteLine($"[b20-HANDLER-COUNT] Registered handlers: {_dispatcher.GetHandlerCount()}");
                var handlerMessageTypes = new[]
                {
                    "getWorkspaceDirs", "getIdeInfo", "getIdeSettings", "getUniqueId", "isTelemetryEnabled", "isWorkspaceRemote",
                    "readFile", "fileExists", "getOpenFiles", "writeFile", "saveFile", "openFile", "openUrl", "getBranch",
                    "context/getContextItems", "context/getSymbolsForFiles", "context/loadSubmenuItems", "context/addDocs", "context/removeDocs", "context/indexDocs",
                    "config/addOpenAiKey", "config/ideSettingsUpdate", "config/deleteModel", "config/getSerializedProfileInfo", "config/addModel", "config/addLocalWorkspaceBlock", "config/addGlobalRule", "config/deleteRule", "config/newPromptFile", "config/newAssistantFile", "config/refreshProfiles", "config/openProfile", "config/updateSharedConfig", "config/updateSelectedModel",
                    "llm/complete", "llm/streamChat", "llm/listModels", "llm/compileChat", "bridge:getModelInfo", "getCurrentFile", "applyToFile", "acceptDiff", "rejectDiff",
                    "autocomplete/complete", "autocomplete/accept", "autocomplete/cancel"
                };
                System.Diagnostics.Debug.WriteLine($"[b20-HANDLER-LIST] {string.Join(", ", handlerMessageTypes)}");
                System.Diagnostics.Debug.WriteLine("[b20-REGISTRATION-COMPLETE]");

                // t4.5 - Loaded event wiring (prelude to t22+)
                System.Diagnostics.Debug.WriteLine("[CV-t4.5] Wiring Loaded event...");
                IDisposable? scope45 = tracer?.BeginScope("t4.5", "ContinueToolWindowControl.LoadedEvent");
                try
                {
                    Loaded += OnLoaded;
                    System.Diagnostics.Debug.WriteLine("[CV-t4.5] ✓ Loaded event wired");

                    // Wire Unloaded event for bridge teardown (b15)
                    Unloaded += OnUnloaded;
                    System.Diagnostics.Debug.WriteLine("[CV-t4.5] ✓ Unloaded event wired");
                }
                finally
                {
                    scope45?.Dispose();
                }

                System.Diagnostics.Debug.WriteLine("[CV-t4] ✓ Constructor END - SUCCESS");
                System.Diagnostics.Debug.WriteLine("[CV] Step 13 complete");

                // Schedule NavigateAsync to run on the next dispatcher cycle to ensure UI is ready
                System.Diagnostics.Debug.WriteLine("[CV-t4] Scheduling NavigateAsync via dispatcher");
#pragma warning disable VSSDK007
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    // Yield to let the visual tree settle before navigating
                    await System.Threading.Tasks.Task.Yield();
                    System.Diagnostics.Debug.WriteLine("[CV-Dispatcher] NavigateAsync scheduled task starting");
                    try
                    {
                        await NavigateAsync();
                        System.Diagnostics.Debug.WriteLine("[CV-Dispatcher] NavigateAsync completed successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CV-Dispatcher] NavigateAsync FAILED: {ex}");
                    }
                }).FileAndForget("vs/continuevs/navigate");
#pragma warning restore VSSDK007
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t4] ✗ Constructor FAILED: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[CV-t4] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CV-t4] Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                scope?.Dispose();
            }
        }

        // -----------------------------------------------------------------
        // Startup
        // -----------------------------------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.OnLoaded] Event fired (now handled in constructor via dispatcher)");
            // NavigateAsync is now called directly from the constructor via dispatcher
            // This OnLoaded event handler is kept for backward compatibility but is no longer needed
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[b15-UNLOADED-EVENT] Control unloaded event fired");
            try
            {
                // Fire-and-forget the dispose without blocking
                // The actual dispose will happen asynchronously
#pragma warning disable VSSDK007
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    try
                    {
                        // Give the teardown script a short time to execute
                        using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2)))
                        {
                            if (WebView?.CoreWebView2 != null)
                            {
                                System.Diagnostics.Debug.WriteLine("[b15-SCRIPT-INJECT] Invoking InjectTeardownScriptAsync from OnUnloaded");
                                var teardownResult = await WebviewInjectorTeardownExtensions.InjectTeardownScriptAsync(WebView.CoreWebView2, cts.Token);
                                System.Diagnostics.Debug.WriteLine($"[b15-SCRIPT-RESULT] Teardown result: {teardownResult}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b15-TEARDOWN-ERROR] Teardown failed: {ex.Message}");
                    }
                    finally
                    {
                        System.Diagnostics.Debug.WriteLine("[b15-COMPLETION] OnUnloaded cleanup finished");
                    }
                }).FileAndForget("vs/continuevs/unloaded");
#pragma warning restore VSSDK007
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b15-UNLOADED-ERROR] Failed in OnUnloaded: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task NavigateAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl.NavigateAsync] START - _webViewInitialized={_webViewInitialized}");

            // Ensure GUI assets are extracted before WebView2 initialization
            System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.NavigateAsync] Calling EnsureExtractedAsync...");
            try
            {
                await GuiExtractor.EnsureExtractedAsync();
                System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.NavigateAsync] EnsureExtractedAsync completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl.NavigateAsync] EnsureExtractedAsync FAILED: {ex.Message}");
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl.NavigateAsync] After SwitchToMainThreadAsync - _webViewInitialized={_webViewInitialized}");

            if (!_webViewInitialized)
            {
                System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.NavigateAsync] ENTERING WebView2 initialization block");

                // STEP b1.7: Log pre-call state
                System.Diagnostics.Debug.WriteLine($"[b1-PRE-STATE] _webViewInitialized={_webViewInitialized}, WebView control={WebView != null}");

                // STEP b1.2: Log user data folder path
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ContinueVS", "WebView2");
                System.Diagnostics.Debug.WriteLine($"[b1-FOLDER-PATH] userDataFolder={userDataFolder}");

                // STEP b1.1 & b1.4: Add exception boundary logging with strategic instrumentation
                try
                {
                    System.Diagnostics.Debug.WriteLine("[b1-ENV-CREATE-START] About to call CoreWebView2Environment.CreateAsync()");
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    var env = await CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: userDataFolder);

                    stopwatch.Stop();
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-CREATE-SUCCESS] CreateAsync completed in {stopwatch.ElapsedMilliseconds}ms");

                    // STEP b1.3: Instrument environment object state
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-OBJECT] env != null: {env != null}");
                    if (env != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b1-ENV-OBJECT] BrowserVersionString={env.BrowserVersionString}");
                        System.Diagnostics.Debug.WriteLine($"[b1-ENV-OBJECT] UserDataFolder={env.UserDataFolder}");
                    }

                    System.Diagnostics.Debug.WriteLine("[b1-ENSURE-START] About to call WebView.EnsureCoreWebView2Async(env)");
                    if (env == null)
                    {
                        throw new InvalidOperationException("environment object is null after CreateAsync");
                    }
                    if (WebView == null)
                    {
                        throw new InvalidOperationException("WebView control is null; cannot initialize CoreWebView2");
                    }
                    await WebView.EnsureCoreWebView2Async(env);
                    System.Diagnostics.Debug.WriteLine("[b1-ENSURE-SUCCESS] EnsureCoreWebView2Async completed successfully");

                    // STEP b1.11: Configure WebView2 settings for bridge communication
                    System.Diagnostics.Debug.WriteLine("[b1-SECURITY-CONFIG] Configuring WebView2Settings for bridge communication");
                    try
                    {
                        if (WebView.CoreWebView2 != null)
                        {
                            var settings = WebView.CoreWebView2.Settings;

                            // Enable dev tools for console debugging (available in WebView2 1.0.x)
                            settings.AreDevToolsEnabled = true;
                            System.Diagnostics.Debug.WriteLine("[b1-SECURITY] AreDevToolsEnabled = true");

                            System.Diagnostics.Debug.WriteLine("[b1-SECURITY-CONFIG] WebView2 settings configured successfully");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[b1-SECURITY-CONFIG-ERROR] CoreWebView2 is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b1-SECURITY-CONFIG-ERROR] {ex.GetType().Name}: {ex.Message}");
                    }

                    // STEP b2.1: Pre-state logging before controller binding verification
                    System.Diagnostics.Debug.WriteLine("[b2-PRE-STATE] WebView element reference obtained, preparing for controller HWND binding");

                    // STEP b2.2: CoreWebView2 access verification
                    var stopwatchB2 = System.Diagnostics.Stopwatch.StartNew();
                    if (WebView.CoreWebView2 != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b2-CONTROLLER-ACCESS] CoreWebView2 initialized successfully, BrowserProcessId={WebView.CoreWebView2.BrowserProcessId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[b2-CONTROLLER-ACCESS] ERROR: CoreWebView2 is null after EnsureCoreWebView2Async");
                    }

                    // STEP b2.3: Controller properties inspection
                    if (WebView.CoreWebView2 != null)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[b2-CONTROLLER-PROPS] IsDefaultDownloadDialogOpen={WebView.CoreWebView2.IsDefaultDownloadDialogOpen}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[b2-CONTROLLER-PROPS] Error reading properties: {ex.Message}");
                        }
                    }

                    // STEP b2.4: Parent window HWND capture and parent-child validation
                    try
                    {
                        var presentationSource = System.Windows.PresentationSource.FromVisual(WebView);
                        if (presentationSource != null && presentationSource.RootVisual is System.Windows.Window parentWindow)
                        {
                            var parentHwnd = new System.Windows.Interop.WindowInteropHelper(parentWindow).Handle;
                            System.Diagnostics.Debug.WriteLine($"[b2-PARENT-HWND] Parent ToolWindow HWND: 0x{parentHwnd:X8}, WebView child relationship established");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[b2-PARENT-HWND] WARNING: Could not resolve parent window HWND");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b2-PARENT-HWND] Exception resolving parent HWND: {ex.GetType().Name} - {ex.Message}");
                    }

                    // STEP b2.5: Visual tree timing and controller ready state
                    System.Diagnostics.Debug.WriteLine("[b2-VISUAL-TREE] Controller bound to visual tree, DOM receptive for message dispatch");

                    // STEP b2.6: Bounds capture for layout integration verification
                    System.Diagnostics.Debug.WriteLine($"[b2-BOUNDS-CAPTURE] WebView layout: ActualWidth={WebView.ActualWidth}, ActualHeight={WebView.ActualHeight}");

                    // STEP b2.7: Event readiness verification (confirm WebMessageReceived subscription possible)
                    try
                    {
                        // Verify the event subscription mechanism is ready (we'll actually subscribe later in the flow)
                        System.Diagnostics.Debug.WriteLine("[b2-EVENT-READY] CoreWebView2 ready for event subscription (WebMessageReceived, NavigationCompleted, etc.)");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[b2-EVENT-READY] ERROR: Event subscription not ready: {ex.Message}");
                    }

                    stopwatchB2.Stop();
                    System.Diagnostics.Debug.WriteLine($"[b2-TIMING] Controller initialization completed in {stopwatchB2.ElapsedMilliseconds}ms");

                    // STEP b1.6: Verify async factory completion
                    System.Diagnostics.Debug.WriteLine("[b1-ASYNC-COMPLETION] Async factory pattern verified - environment ready for downstream operations");
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[b2-EXCEPTION-INVALID-OP] InvalidOperationException (controller uninitialized or invalid state): {ex.Message}");
                    throw;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-EXCEPTION-COM] COMException during environment creation: 0x{ex.HResult:X8} - {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[b2-EXCEPTION-COM] COMException during controller binding (HWND binding failure): 0x{ex.HResult:X8} - {ex.Message}");
                    throw;
                }
                catch (ArgumentException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-EXCEPTION-ARGUMENT] ArgumentException (possibly invalid folder path): {ex.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-EXCEPTION-GENERAL] Exception during environment creation: {ex.GetType().Name} - {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[b1-ENV-EXCEPTION-STACKTRACE] {ex.StackTrace}");
                    throw;
                }

                // STEP b1.5: Document folder creation side effects
                if (System.IO.Directory.Exists(userDataFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[b1-FOLDER-STATE] User data folder already existed: {userDataFolder}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[b1-FOLDER-STATE] User data folder was newly created by WebView2 runtime: {userDataFolder}");
                }

                // STEP b1.10: Verify integration boundary - check CoreWebView2 readiness
                if (WebView.CoreWebView2 != null)
                {
                    System.Diagnostics.Debug.WriteLine("[b1-INTEGRATION] CoreWebView2 is ready for virtual host mapping and bridge injection");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[b1-INTEGRATION] WARNING: CoreWebView2 is null after EnsureCoreWebView2Async - unexpected state");
                }

                // Map https://continue.local/ → %APPDATA%\ContinueVS\gui\
                // This lets the React bundle resolve absolute paths like /assets/index.js
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        hostName: "continue.local",
                        folderPath: GuiExtractor.GuiRoot,
                        accessKind: CoreWebView2HostResourceAccessKind.Allow);
                }
                else
                {
                    throw new InvalidOperationException("CoreWebView2 is null after EnsureCoreWebView2Async");
                }

                // Inject the continueVS bridge (will be done in NavigationCompleted instead)
                // The bridge must be injected AFTER navigation completes, not before
                // Otherwise the page load will reset the JavaScript context
                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Capture JS console output for diagnostics
                WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                WebView.CoreWebView2.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled").DevToolsProtocolEventReceived += (s2, ev) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[JS-CONSOLE] {ev.ParameterObjectAsJson}");
                };
#pragma warning disable VSTHRD110, CS4014
                WebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
#pragma warning restore VSTHRD110, CS4014

                // STEP b3.1: Add NavigationCompleted event handler
                System.Diagnostics.Debug.WriteLine("[b3-NAV-ENTRY] Navigation handler registration starting");
                var navStopwatch = System.Diagnostics.Stopwatch.StartNew();
#pragma warning disable VSTHRD101 // All exceptions are caught in try/catch below
                WebView.CoreWebView2.NavigationCompleted += async (sender, args) =>
{
   navStopwatch.Stop();
   System.Diagnostics.Debug.WriteLine($"[b3-NAV-COMPLETED] NavigationCompleted event fired, IsSuccess={args.IsSuccess}, WebErrorStatus={args.WebErrorStatus}, elapsed={navStopwatch.ElapsedMilliseconds}ms");
   var b21InitStopwatch = System.Diagnostics.Stopwatch.StartNew();

   try
   {
        // STEP b3.2: DOM verification script execution
       System.Diagnostics.Debug.WriteLine("[b3-DOM-READY] Executing DOM readiness verification script");
       var domVerifyScript = @"
(function() {
  try {
    return JSON.stringify({
      readyState: document.readyState,
      bodyExists: document.body !== null && document.body !== undefined
    });
  } catch (e) {
    return JSON.stringify({
      readyState: 'error',
      bodyExists: false,
      error: e.message
    });
  }
})();
";
       var domStopwatch = System.Diagnostics.Stopwatch.StartNew();
       string domResult = await WebView.CoreWebView2.ExecuteScriptAsync(domVerifyScript);
       domStopwatch.Stop();
       System.Diagnostics.Debug.WriteLine($"[b3-DOM-READY] DOM verification completed in {domStopwatch.ElapsedMilliseconds}ms, result={domResult}");

        // Parse DOM result
       try
       {
            // ExecuteScriptAsync returns JSON string (result includes quotes)
            // Remove outer quotes if present
           string cleanedDomResult = domResult;
           if (cleanedDomResult.StartsWith("\"") && cleanedDomResult.EndsWith("\""))
           {
               var parsedJson = System.Text.Json.JsonDocument.Parse(domResult);
               cleanedDomResult = parsedJson.RootElement.GetString() ?? domResult;
           }

           var domState = Newtonsoft.Json.Linq.JObject.Parse(cleanedDomResult ?? "");
           string readyState = domState["readyState"]?.ToString() ?? "unknown";
           bool bodyExists = domState["bodyExists"]?.Value<bool>() ?? false;
           System.Diagnostics.Debug.WriteLine($"[b3-DOM-BODY] document.readyState={readyState}, document.body exists={bodyExists}");
           System.Diagnostics.Debug.WriteLine($"[b21-DOM-READY] document.readyState={readyState}, bodyExists={bodyExists}, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms");
       }
       catch (Exception ex)
       {
           System.Diagnostics.Debug.WriteLine($"[b3-DOM-READY] WARNING: Failed to parse DOM result: {ex.Message}");
           System.Diagnostics.Debug.WriteLine($"[b21-DOM-READY] WARN: DOM parse failed, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms, error={ex.Message}");
       }

        // STEP b21: React mount probe
       System.Diagnostics.Debug.WriteLine("[b21-REACT-MOUNT] Executing React mount probe script");
       var reactMountScript = @"
(function() {
  try {
    var rootEl = document.getElementById('root');
    var reactRootEl = document.querySelector('[data-reactroot]');
    var rootFound = rootEl !== null && rootEl !== undefined;
    var childCount = rootFound ? rootEl.childElementCount : 0;
    var reactMounted = rootFound && childCount > 0;
    console.log('[VS-React-Check] rootFound=' + rootFound + ', childCount=' + childCount + ', reactMounted=' + reactMounted);
    return JSON.stringify({
      reactMounted: reactMounted,
      rootFound: rootFound,
      childCount: childCount,
      hasDataReactRoot: reactRootEl !== null
    });
  } catch (e) {
    console.error('[VS-React-Check] Exception: ' + e.message);
    return JSON.stringify({ reactMounted: false, rootFound: false, childCount: 0, error: e.message });
  }
})();
";
       try
       {
           string reactResult = await WebView.CoreWebView2.ExecuteScriptAsync(reactMountScript);
           string cleanedReactResult = reactResult;
           if (cleanedReactResult.StartsWith("\"") && cleanedReactResult.EndsWith("\""))
           {
               var parsedJson = System.Text.Json.JsonDocument.Parse(reactResult);
               cleanedReactResult = parsedJson.RootElement.GetString() ?? reactResult;
           }
           var reactState = Newtonsoft.Json.Linq.JObject.Parse(cleanedReactResult ?? "");
           bool reactMounted = reactState["reactMounted"]?.Value<bool>() ?? false;
           bool rootFound = reactState["rootFound"]?.Value<bool>() ?? false;
           int childCount = reactState["childCount"]?.Value<int>() ?? 0;
           bool hasDataReactRoot = reactState["hasDataReactRoot"]?.Value<bool>() ?? false;
           System.Diagnostics.Debug.WriteLine($"[b21-REACT-MOUNT] reactMounted={reactMounted}, rootFound={rootFound}, childCount={childCount}, hasDataReactRoot={hasDataReactRoot}, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms");
       }
       catch (Exception ex)
       {
           System.Diagnostics.Debug.WriteLine($"[b21-REACT-MOUNT] WARN: React mount probe failed, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms, error={ex.Message}");
       }

        // STEP b3.2b: Bridge is now loaded via HTML (bridge-wrapper.js in index.html)
        // The new bridge wrapper loads automatically before React initializes
       System.Diagnostics.Debug.WriteLine("[b3-BRIDGE-INJECT] Bridge wrapper loaded via index.html (no C# injection needed)");

        // STEP b3.3: Bridge ready verification script
        // Check for the new bridge-wrapper.js bridge layer
       System.Diagnostics.Debug.WriteLine("[b3-BRIDGE-READY] Executing bridge readiness verification script");
       var bridgeVerifyScript = @"
                                                 (function() {
                                                   try {
                                                     console.log('[VS-Bridge-Check] Starting bridge verification');

                                                     // Check for new bridge wrapper
                                                     var wrapperReady = 
                                                       typeof window.continueVSBridge !== 'undefined' &&
                                                       typeof window.continueVSBridge.sendToExtension === 'function' &&
                                                       typeof window.continueVSBridge.onMessageFromExtension === 'function';

                                                     console.log('[VS-Bridge-Check] wrapperReady=' + wrapperReady);
                                                     console.log('[VS-Bridge-Check] window.continueVSBridge=' + (typeof window.continueVSBridge));

                                                     // Legacy bridge check (for backward compatibility)
                                                     var legacyReady = 
                                                       typeof window.continueVS !== 'undefined' &&
                                                       typeof window.continueVS.sendMessage === 'function' &&
                                                       typeof window.continueVS.onMessage === 'function';

                                                     console.log('[VS-Bridge-Check] legacyReady=' + legacyReady);

                                                     var result = {
                                                       bridgeReady: wrapperReady || legacyReady,
                                                       wrapperReady: wrapperReady,
                                                       legacyReady: legacyReady,
                                                       hasWrapper: typeof window.continueVSBridge !== 'undefined',
                                                       hasSendToExtension: typeof window.continueVSBridge?.sendToExtension === 'function',
                                                       hasOnMessageFromExtension: typeof window.continueVSBridge?.onMessageFromExtension === 'function'
                                                     };

                                                     console.log('[VS-Bridge-Check] Result:' + JSON.stringify(result));
                                                     return JSON.stringify(result);
                                                   } catch (e) {
                                                     console.error('[VS-Bridge-Check] Exception: ' + e.message);
                                                     return JSON.stringify({
                                                       bridgeReady: false,
                                                       error: e.message
                                                     });
                                                   }
                                                 })();
                                                 ";
       var bridgeStopwatch = System.Diagnostics.Stopwatch.StartNew();
       string bridgeResult = await WebView.CoreWebView2.ExecuteScriptAsync(bridgeVerifyScript);
       bridgeStopwatch.Stop();
       System.Diagnostics.Debug.WriteLine($"[b3-BRIDGE-READY] Bridge verification completed in {bridgeStopwatch.ElapsedMilliseconds}ms, result={bridgeResult}");

        // Parse bridge result
       try
       {
            // ExecuteScriptAsync returns JSON string (result includes quotes)
            // Remove outer quotes if present
           string cleanedBridgeResult = bridgeResult;
           if (cleanedBridgeResult.StartsWith("\"") && cleanedBridgeResult.EndsWith("\""))
           {
               var parsedJson = System.Text.Json.JsonDocument.Parse(bridgeResult);
               cleanedBridgeResult = parsedJson.RootElement.GetString() ?? bridgeResult;
           }

           var bridgeState = Newtonsoft.Json.Linq.JObject.Parse(cleanedBridgeResult ?? "");
           bool bridgeReady = bridgeState["bridgeReady"]?.Value<bool>() ?? false;
           System.Diagnostics.Debug.WriteLine($"[b3-INTEGRATION] Bridge operational: {bridgeReady}, wrapper={bridgeState["wrapperReady"]}, legacy={bridgeState["legacyReady"]}, sendToExtension={bridgeState["hasSendToExtension"]}");
           System.Diagnostics.Debug.WriteLine($"[b21-BRIDGE-READY] bridgeReady={bridgeReady}, wrapperReady={bridgeState["wrapperReady"]}, legacyReady={bridgeState["legacyReady"]}, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms");
           b21InitStopwatch.Stop();
           System.Diagnostics.Debug.WriteLine($"[b21-INIT-TIME-MS] WebView2 init complete, totalMs={b21InitStopwatch.ElapsedMilliseconds}, bridgeReady={bridgeReady}");
       }
       catch (Exception ex)
       {
           System.Diagnostics.Debug.WriteLine($"[b3-BRIDGE-READY] WARNING: Failed to parse bridge result: {ex.Message}");
           System.Diagnostics.Debug.WriteLine($"[b21-BRIDGE-READY] WARN: Bridge parse failed, elapsed={b21InitStopwatch.ElapsedMilliseconds}ms, error={ex.Message}");
       }

        // KICKSTART: Disabled — the GUI initiates communication on its own
        // via vscode.postMessage once it loads. Sending bridge:init caused
        // the GUI to re-initialize and loop.
       System.Diagnostics.Debug.WriteLine("[KICKSTART] Skipped — GUI self-initiates via vscode shim");

        // DIAGNOSTIC: Inject a message listener to log all incoming messages
        // This will help us understand if the GUI's request() method matches responses
       await WebView.CoreWebView2.ExecuteScriptAsync(@"
                                (function() {
                                    window.addEventListener('message', function(e) {
                                        if (e && e.data && e.data.messageType) {
                                            console.log('[DIAG-MSG-IN] type=' + e.data.messageType + ' id=' + e.data.messageId + ' hasData=' + !!e.data.data);
                                        }
                                    });
                                    console.log('[DIAG] Message listener installed for diagnostics');
                                })()
                            ");
   }
   catch (System.Runtime.InteropServices.COMException comEx)
   {
       System.Diagnostics.Debug.WriteLine($"[b3-EXCEPTION-NAV] COMException during navigation completion handler: HResult=0x{comEx.HResult:X8}, Message={comEx.Message}");
   }
   catch (System.OperationCanceledException opEx)
   {
       System.Diagnostics.Debug.WriteLine($"[b3-EXCEPTION-EXEC] OperationCanceledException during DOM/bridge verification: {opEx.Message}");
   }
   catch (Exception ex)
   {
       System.Diagnostics.Debug.WriteLine($"[b3-EXCEPTION-EXEC] Unexpected exception in NavigationCompleted handler: {ex.GetType().Name} - {ex.Message}");
   }
};
#pragma warning restore VSTHRD101
                System.Diagnostics.Debug.WriteLine("[b3-NAV-ENTRY] NavigationCompleted handler registered successfully");

                _webViewInitialized = true;
                _pusher.Subscribe();
                await _editorContextProvider?.RegisterAsync()!;
                _configWatcher?.Start();
            }

            // STEP b3: Navigation to HTML content
            System.Diagnostics.Debug.WriteLine("[b3-VHOST-STATE] Virtual host mapping pre-check: https://continue.local/ -> GUI assets");
            var navigationUri = new Uri("https://continue.local/index.html");
            System.Diagnostics.Debug.WriteLine($"[b3-NAV-ENTRY] Navigation starting: {navigationUri.AbsoluteUri}");

            WebView.Source = navigationUri;

            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine("[b3-TIMING] Navigation initiated, awaiting NavigationCompleted event and DOM/bridge verification");
            // NOTE: PushConfigUpdate() and PushIndexProgress() are now called from NavigationCompleted handler
            // after the bridge is successfully injected, not before!
        }

        // -----------------------------------------------------------------
        // WebView2 ↔ Continue binary bridge
        // -----------------------------------------------------------------

        /// <summary>
        /// Messages posted by the React GUI (window.chrome.webview.postMessage) are
        /// forwarded to the Continue binary via the IPC client.
        /// </summary>
        // VSTHRD100: replaced async void with sync wrapper + OnWebMessageReceivedAsync
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // [b14-ENTRY] Entry point - capture current thread ID
            var entryThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            System.Diagnostics.Debug.WriteLine($"[b14-ENTRY] OnWebMessageReceived entry on thread: {entryThreadId}");

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(() => OnWebMessageReceivedAsync(sender, e))
                           .FileAndForget("vs/continuevs/webmessage");  // VSSDK007
#pragma warning restore VSSDK007
        }

        private System.Threading.Tasks.Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            // [b16-REQUEST-RECEIVED] Start timing measurement for b16 verification
            var b16RequestTimestamp = System.Diagnostics.Stopwatch.StartNew();

            // [b14-ENTRY] Async entry - capture thread ID after JoinableTask switch
            var asyncThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            System.Diagnostics.Debug.WriteLine($"[b14-ENTRY] OnWebMessageReceivedAsync executing on thread: {asyncThreadId}");

            // [b12-RECEIVED] Capture raw JSON from WebView
            // Use WebMessageAsJson property which is always available (doesn't throw like TryGetWebMessageAsString)
            var json = e.WebMessageAsJson;
            System.Diagnostics.Debug.WriteLine($"[b12-RECEIVED] Raw JSON received: {json}");

            // [b12-DESERIALIZED] Deserialize JSON to Message object
            var message = JsonConvert.DeserializeObject<Message>(json);
            if (message == null)
            {
                System.Diagnostics.Debug.WriteLine($"[b12-DESERIALIZED] Failed: message is null after deserialization");
                return System.Threading.Tasks.Task.CompletedTask;
            }
            System.Diagnostics.Debug.WriteLine($"[b12-DESERIALIZED] Message: Type={message.MessageType}, ID={message.MessageId}");

            // [b16-REQUEST-RECEIVED] Log for loadSettings handler
            if (message.MessageType == "bridge:loadSettings")
            {
                System.Diagnostics.Debug.WriteLine($"[b16-REQUEST-RECEIVED] loadSettings request: MessageId={message.MessageId}, timestamp={b16RequestTimestamp.ElapsedMilliseconds}ms");
            }

            // [t9] Log message reception at dispatcher entry
            System.Diagnostics.Debug.WriteLine($"[t9-DISPATCH] Message received from bridge: Type={message.MessageType}, ID={message.MessageId}");

            if (_pendingReplies.TryRemove(message.MessageId, out var pendingTcs))
            {
                System.Diagnostics.Debug.WriteLine($"[t9-DISPATCH] Message {message.MessageId} matched pending reply");
                pendingTcs.TrySetResult(message.Data ?? JToken.FromObject(""));
                return System.Threading.Tasks.Task.CompletedTask;
            }

            // [b12-DISPATCH-START] Routing to dispatcher
            System.Diagnostics.Debug.WriteLine($"[b12-DISPATCH-START] Routing message to dispatcher: {message.MessageType}");
            return _dispatcher.DispatchAsync(message, System.Threading.CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DISPATCH-ERROR] {message.MessageType}: {t.Exception?.InnerException?.Message}");
                        // Send error response so GUI doesn't hang waiting
                        // Note: SendReplyToGui wraps in {status:"success",content:...} so we pass the error object directly
                        // and the GUI will get {status:"success",content:{error:"..."}}
                        // The GUI's request() resolves, but the caller should handle missing expected fields gracefully
                        try { SendErrorReplyToGui(message.MessageType, message.MessageId, t.Exception?.InnerException?.Message ?? "Unknown error"); }
                        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"[DISPATCH-ERROR-REPLY] Failed: {ex.Message}"); }
                    }
                }, System.Threading.Tasks.TaskScheduler.Default);
        }

        // -----------------------------------------------------------------
        // Public helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Sends a pre-formed chat message to the React GUI (e.g., from a code-action
        /// command that wants to pre-populate the input box with selected code).
        /// </summary>
        public void SendToGui(string messageType, object data)
        {
            if (!_webViewInitialized || WebView.CoreWebView2 == null) return;

            var msg = new Message
            {
                MessageType = messageType,
                MessageId = Guid.NewGuid().ToString(),
                Data = JToken.FromObject(data),
            };
            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[SendToGui-DEBUG] messageType={messageType}, json={json}");
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
            // Call via window.continueVS.onMessage (the documented C#→JS bridge API)
            var script = $"window.continueVS && window.continueVS.onMessage('{escaped}');";
            System.Diagnostics.Debug.WriteLine($"[SendToGui-SCRIPT] {script.Substring(0, Math.Min(200, script.Length))}...");

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    await WebView.CoreWebView2.ExecuteScriptAsync(script);
                    System.Diagnostics.Debug.WriteLine($"[SendToGui-OK] Message sent: {messageType}");

                    // Also log the raw message for debugging
                    System.Diagnostics.Debug.WriteLine($"[SendToGui-MSG] {messageType}: {json.Substring(0, Math.Min(150, json.Length))}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SendToGui-ERROR] Failed to send {messageType}: {ex.Message}");
                }
            }).FileAndForget("vs/continuevs/sendtogui");                // VSSDK007
#pragma warning restore VSSDK007
        }

        /// <summary>
        /// Sends a message to the GUI and waits asynchronously for a reply with the same messageId.
        /// </summary>
        internal System.Threading.Tasks.Task<JToken?> SendToGuiAndAwaitReplyAsync(
            string messageType, object data, System.Threading.CancellationToken cancellationToken)
        {
            if (!_webViewInitialized || WebView.CoreWebView2 == null)
                return System.Threading.Tasks.Task.FromResult<JToken?>(null);

            var messageId = Guid.NewGuid().ToString();
            var tcs = new System.Threading.Tasks.TaskCompletionSource<JToken?>();
            _pendingReplies[messageId] = tcs;
            cancellationToken.Register(() =>
            {
                _pendingReplies.TryRemove(messageId, out _);
                tcs.TrySetCanceled();
            });

            var msg = new Message
            {
                MessageType = messageType,
                MessageId = messageId,
                Data = JToken.FromObject(data),
            };
            var json = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            }).FileAndForget("vs/continuevs/sendtogui");
#pragma warning restore VSSDK007

            return tcs.Task;
        }

        /// <summary>Send an error reply that the GUI's request() will see as {status:"error", error:"..."}.</summary>
        public void SendErrorReplyToGui(string messageType, string messageId, string errorMessage)
        {
            SendReplyToGuiInternal(messageType, messageId, new { status = "error", error = errorMessage });
        }

        public void SendReplyToGui(string messageType, string messageId, object data)
        {
            // [b16-RESPONSE-SERIALIZED] Start timing measurement for b16 verification
            var b16ResponseStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // [b12-RESPONSE] Log outbound response initialization
            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] SendReplyToGui called: Type={messageType}, ID={messageId}");

            if (!_webViewInitialized)
            {
                System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] WebView not initialized, reply not sent");
                return;
            }

            // [b13-RESPONSE-OBJECT] Inspect payload object structure before JToken conversion
            System.Diagnostics.Debug.WriteLine($"[b13-RESPONSE-OBJECT] Handler response object: Type={data?.GetType().Name ?? "null"}, Content={JsonConvert.SerializeObject(data)}");

            // [b12-RESPONSE] Response serialization
            // Wrap in WebviewSingleMessage envelope: { status: "success", done: true, content: <payload> }
            var wrapped = new { status = "success", done = true, content = data ?? new object() };
            SendReplyToGuiInternal(messageType, messageId, wrapped);
        }

        private void SendReplyToGuiInternal(string messageType, string messageId, object wrappedData)
        {
            var b16ResponseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var msg = new Message
            {
                MessageType = messageType,
                MessageId = messageId,
                Data = JToken.FromObject(wrappedData),
            };

            // [b13-JTOKEN-SERIALIZE] Log JToken creation
            System.Diagnostics.Debug.WriteLine($"[b13-JTOKEN-SERIALIZE] JToken created from payload");

            var json = JsonConvert.SerializeObject(msg);
            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] Message serialized: {json}");

            // [b13-JSON-VALID] Validate JSON structure
            var isValidJson = IsValidJson(json);
            System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON validation: IsValid={isValidJson}, Length={json?.Length ?? 0}");

            // [b16-RESPONSE-SERIALIZED] Log for loadSettings handler
            if (messageType == "bridge:loadSettings")
            {
                System.Diagnostics.Debug.WriteLine($"[b16-RESPONSE-SERIALIZED] Response prepared: DataType={wrappedData?.GetType().Name}, JsonLength={json?.Length}");
            }

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                // [b14-THREAD-BEFORE] Capture thread ID before SwitchToMainThreadAsync
                var threadBefore = System.Threading.Thread.CurrentThread.ManagedThreadId;
                System.Diagnostics.Debug.WriteLine($"[b14-THREAD-BEFORE] Thread ID before switch: {threadBefore}");

                // [b14-SWITCH] About to switch to UI thread
                System.Diagnostics.Debug.WriteLine($"[b14-SWITCH] Switching to main thread");
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // [b14-THREAD-AFTER] Capture thread ID after SwitchToMainThreadAsync
                var threadAfter = System.Threading.Thread.CurrentThread.ManagedThreadId;
                System.Diagnostics.Debug.WriteLine($"[b14-THREAD-AFTER] Thread ID after switch: {threadAfter}");

                // [b14-ASSERTION] Verify we are on the UI thread
                // After SwitchToMainThreadAsync(), we should be on UI thread
                // Log confirmation without throwing (VerifyAccess would violate VSTHRD109)
                System.Diagnostics.Debug.WriteLine($"[b14-ASSERTION] After SwitchToMainThreadAsync - proceeding to ExecuteScriptAsync on UI thread");

                // [b12-SCRIPT-EXEC] Post message to WebView2 via PostWebMessageAsJson
                // PostWebMessageAsJson automatically handles JSON → fires chrome.webview 'message'
                // bridge-wrapper relays it to window 'message' listeners via _dispatchToMessageListeners
                System.Diagnostics.Debug.WriteLine($"[b12-SCRIPT-EXEC] Delivering via PostWebMessageAsJson, jsonLen={json?.Length}");
                try
                {
                    WebView.CoreWebView2.PostWebMessageAsJson(json);
                    System.Diagnostics.Debug.WriteLine($"[b13-SCRIPT-RESULT] PostWebMessageAsJson succeeded");
                }
                catch (Exception postEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[b12-ERROR] PostWebMessageAsJson failed: {postEx.Message}");
                }

                // [b16-SCRIPT-INJECTED] Log for loadSettings handler
                if (messageType == "bridge:loadSettings")
                {
                    b16ResponseStopwatch.Stop();
                    System.Diagnostics.Debug.WriteLine($"[b16-SCRIPT-INJECTED] JavaScript executed, totalElapsedMs={b16ResponseStopwatch.ElapsedMilliseconds}");
                }
            }).FileAndForget("vs/continuevs/sendtogui");                // VSSDK007
#pragma warning restore VSSDK007
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        /// <summary>
        /// Validates that a string is well-formed JSON without parsing side effects.
        /// </summary>
        private static bool IsValidJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;

            try
            {
                JsonConvert.DeserializeObject(json!);
                return true;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[b13-JSON-VALID] JSON parse error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // [b15-TEARDOWN-START] Bridge teardown on control disposal
                System.Diagnostics.Debug.WriteLine("[b15-TEARDOWN-START] ContinueToolWindowControl.Dispose() initiating bridge teardown");

                try
                {
                    // Trigger bridge teardown if WebView is initialized
                    if (WebView?.CoreWebView2 != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[b15-SCRIPT-INJECT] Invoking InjectTeardownScriptAsync");
                        var teardownResult = await WebviewInjectorTeardownExtensions.InjectTeardownScriptAsync(WebView.CoreWebView2);
                        System.Diagnostics.Debug.WriteLine($"[b15-SCRIPT-RESULT] Teardown result: {teardownResult}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[b15-TEARDOWN-ERROR] Teardown failed: {ex.Message}");
                }

                _pusher.Dispose();
            });

            _editorContextProvider?.Dispose();
            _configWatcher?.Dispose();
            WebView.Dispose();
            System.Diagnostics.Debug.WriteLine("[b15-COMPLETION] ContinueToolWindowControl.Dispose() completed");
        }
    }
}
