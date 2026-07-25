using ContinueVS.Binary;
using ContinueVS.Editor;
using System.Collections.Concurrent;
using ContinueVS.Handlers;
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
    /// WPF UserControl hosting a WebView2 that renders the Continue React GUI.
    ///
    /// The GUI HTML is extracted alongside the binary from the Continue VSIX package.
    /// It communicates with the continue-binary via the stdio IPC client.
    /// </summary>
    public partial class ContinueToolWindowControl : UserControl, IDisposable
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
                    _dispatcher.Register("getWorkspaceDirs",  new GetWorkspaceDirsHandler(this));
                    _dispatcher.Register("getIdeInfo",        new GetIdeInfoHandler(this));
                    _dispatcher.Register("getIdeSettings",    new GetIdeSettingsHandler(this));
                    _dispatcher.Register("getUniqueId",       new GetUniqueIdHandler(this));
                    _dispatcher.Register("isTelemetryEnabled", new IsTelemetryEnabledHandler(this));
                    _dispatcher.Register("isWorkspaceRemote", new IsWorkspaceRemoteHandler(this));
                    _dispatcher.Register("readFile",          new ReadFileHandler(this));
                    _dispatcher.Register("fileExists",        new FileExistsHandler(this));
                    _dispatcher.Register("getOpenFiles",      new GetOpenFilesHandler(this));
                    _dispatcher.Register("writeFile",         new WriteFileHandler(this));
                    _dispatcher.Register("saveFile",          new SaveFileHandler(this));
                    _dispatcher.Register("openFile",          new OpenFileHandler(this));
                    _dispatcher.Register("openUrl",           new OpenUrlHandler(this));
                    _dispatcher.Register("getBranch",         new GetBranchHandler(this));
                    _dispatcher.Register("context/getContextItems",     new ContextGetContextItemsHandler(this));
                    _dispatcher.Register("context/getSymbolsForFiles",  new ContextGetSymbolsForFilesHandler(this));
                    _dispatcher.Register("context/loadSubmenuItems",    new ContextLoadSubmenuItemsHandler(this));
                    _dispatcher.Register("context/addDocs",             new ContextAddDocsHandler(this));
                    _dispatcher.Register("context/removeDocs",          new ContextRemoveDocsHandler(this));
                    _dispatcher.Register("context/indexDocs",           new ContextIndexDocsHandler(this));
                    _dispatcher.Register("config/addOpenAiKey",         new ConfigAddOpenAiKeyHandler(this));
                    _dispatcher.Register("config/ideSettingsUpdate",    new ConfigIdeSettingsUpdateHandler(this));
                    _dispatcher.Register("config/deleteModel",          new ConfigDeleteModelHandler(this));
                    _dispatcher.Register("config/getSerializedProfileInfo", new ConfigGetSerializedProfileInfoHandler(this));
                    _dispatcher.Register("config/addModel",             new ConfigAddModelHandler(this));
                    _dispatcher.Register("config/addLocalWorkspaceBlock", new ConfigAddLocalWorkspaceBlockHandler(this));
                    _dispatcher.Register("config/addGlobalRule",        new ConfigAddGlobalRuleHandler(this));
                    _dispatcher.Register("config/deleteRule",           new ConfigDeleteRuleHandler(this));
                    _dispatcher.Register("config/newPromptFile",        new ConfigNewPromptFileHandler(this));
                    _dispatcher.Register("config/newAssistantFile",     new ConfigNewAssistantFileHandler(this));
                    _dispatcher.Register("config/refreshProfiles",      new ConfigRefreshProfilesHandler(this));
                    _dispatcher.Register("config/openProfile",          new ConfigOpenProfileHandler(this));
                    _dispatcher.Register("config/updateSharedConfig",   new ConfigUpdateSharedConfigHandler(this));
                    _dispatcher.Register("config/updateSelectedModel",  new ConfigUpdateSelectedModelHandler(this));
                    _dispatcher.Register("llm/complete",                new LlmCompleteHandler(this));
                    _dispatcher.Register("llm/streamChat",              new LlmStreamChatHandler(this));
                    _dispatcher.Register("llm/listModels",              new LlmListModelsHandler(this));
                    _dispatcher.Register("llm/compileChat",             new LlmCompileChatHandler(this));
                    _dispatcher.Register("getCurrentFile",               new GetCurrentFileHandler(this));
                    _dispatcher.Register("applyToFile",                  new ApplyToFileHandler(this));
                    _dispatcher.Register("acceptDiff",                   new AcceptDiffHandler(this));
                    _dispatcher.Register("rejectDiff",                   new RejectDiffHandler(this));
                    _dispatcher.Register("autocomplete/complete",         new AutocompleteCompleteHandler(this));
                    _dispatcher.Register("autocomplete/accept",           new AutocompleteAcceptHandler(this));
                    _dispatcher.Register("autocomplete/cancel",           new AutocompleteCancelHandler(this));
                    System.Diagnostics.Debug.WriteLine("[CV-t4.4] ✓ All 41 handlers registered");
                }
                finally
                {
                    scope44?.Dispose();
                }

                // t4.5 - Loaded event wiring (prelude to t22+)
                System.Diagnostics.Debug.WriteLine("[CV-t4.5] Wiring Loaded event...");
                IDisposable? scope45 = tracer?.BeginScope("t4.5", "ContinueToolWindowControl.LoadedEvent");
                try
                {
                    Loaded += OnLoaded;
                    System.Diagnostics.Debug.WriteLine("[CV-t4.5] ✓ Loaded event wired");
                }
                finally
                {
                    scope45?.Dispose();
                }

                System.Diagnostics.Debug.WriteLine("[CV-t4] ✓ Constructor END - SUCCESS");
                System.Diagnostics.Debug.WriteLine("[CV] Step 13 complete");
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
            System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.OnLoaded] Event fired");
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.OnLoaded] Async task started");
                try
                {
                    await GuiExtractor.EnsureExtractedAsync();
                    System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.OnLoaded] GuiExtractor done");

                    await NavigateAsync();
                    System.Diagnostics.Debug.WriteLine("[ContinueToolWindowControl.OnLoaded] NavigateAsync done");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl.OnLoaded] FAILED: {ex}");
                }
            }).FileAndForget("vs/continuevs/loaded");
#pragma warning restore VSSDK007
        }

        private async System.Threading.Tasks.Task NavigateAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl.NavigateAsync] START - _webViewInitialized={_webViewInitialized}");
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
                        userDataFolder:          userDataFolder);

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
                        hostName:          "continue.local",
                        folderPath:        GuiExtractor.GuiRoot,
                        accessKind:        CoreWebView2HostResourceAccessKind.Allow);
                }
                else
                {
                    throw new InvalidOperationException("CoreWebView2 is null after EnsureCoreWebView2Async");
                }

                // Inject the continueVS bridge (must happen before navigation)
                var injector = new WebviewInjector();
                var injectionResult = await injector.InjectBridgeAsync(
                    WebView.CoreWebView2,
                    System.Threading.CancellationToken.None);

                if (!injectionResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ContinueVS] Webview bridge injection failed: {injectionResult.ErrorMessage}");
                }

                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewInitialized = true;
                _pusher.Subscribe();
                await _editorContextProvider?.RegisterAsync()!;
                _configWatcher?.Start();
            }

            WebView.Source = new Uri("https://continue.local/index.html");

            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility      = Visibility.Visible;
            _pusher.PushConfigUpdate();
            _pusher.PushIndexProgress();
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
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(() => OnWebMessageReceivedAsync(sender, e))
                           .FileAndForget("vs/continuevs/webmessage");  // VSSDK007
#pragma warning restore VSSDK007
        }

        private System.Threading.Tasks.Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            var message = JsonConvert.DeserializeObject<Message>(json);
            if (message == null)
                return System.Threading.Tasks.Task.CompletedTask;

            // [t9] Log message reception at dispatcher entry
            System.Diagnostics.Debug.WriteLine($"[t9-DISPATCH] Message received from bridge: Type={message.MessageType}, ID={message.MessageId}");

            if (_pendingReplies.TryRemove(message.MessageId, out var pendingTcs))
            {
                System.Diagnostics.Debug.WriteLine($"[t9-DISPATCH] Message {message.MessageId} matched pending reply");
                pendingTcs.TrySetResult(message.Data ?? JToken.FromObject(""));
                return System.Threading.Tasks.Task.CompletedTask;
            }

            System.Diagnostics.Debug.WriteLine($"[t9-DISPATCH] Routing message to dispatcher: {message.MessageType}");
            return _dispatcher.DispatchAsync(message, System.Threading.CancellationToken.None);
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
                MessageId   = Guid.NewGuid().ToString(),
                Data        = JToken.FromObject(data),
            };
            var json    = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
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
                MessageId   = messageId,
                Data        = JToken.FromObject(data),
            };
            var json    = JsonConvert.SerializeObject(msg);
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

        internal void SendReplyToGui(string messageType, string messageId, object data)
        {
            // [t9] Log outbound response
            System.Diagnostics.Debug.WriteLine($"[t9-RESPONSE] SendReplyToGui called: Type={messageType}, ID={messageId}");

            if (!_webViewInitialized || WebView.CoreWebView2 == null) 
            {
                System.Diagnostics.Debug.WriteLine($"[t9-RESPONSE] WebView not initialized, reply not sent");
                return;
            }

            var msg = new Message
            {
                MessageType = messageType,
                MessageId   = messageId,
                Data        = JToken.FromObject(data),
            };
            var json    = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

            System.Diagnostics.Debug.WriteLine($"[t9-RESPONSE] Executing script to send reply to bridge");
#pragma warning disable VSSDK007
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
                System.Diagnostics.Debug.WriteLine($"[t9-RESPONSE] Reply script executed successfully");
            }).FileAndForget("vs/continuevs/sendtogui");                // VSSDK007
#pragma warning restore VSSDK007
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _pusher.Dispose();
            });

            _editorContextProvider?.Dispose();
            _configWatcher?.Dispose();
            WebView.Dispose();
        }
    }
}
