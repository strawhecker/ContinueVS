using ContinueVS.Binary;
using ContinueVS.Handlers;
using ContinueVS.Handlers.File;
using ContinueVS.Handlers.Ide;
using ContinueVS.Handlers.Push;
using ContinueVS.IPC;
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
        private readonly WebviewPusher _pusher;

        public ContinueToolWindowControl()
        {
            InitializeComponent();
            _pusher = new WebviewPusher(this);
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
            Loaded += OnLoaded;
        }

        // -----------------------------------------------------------------
        // Startup
        // -----------------------------------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await GuiExtractor.EnsureExtractedAsync();
                await NavigateAsync();
            }).FileAndForget("vs/continuevs/loaded");
        }

        private async System.Threading.Tasks.Task NavigateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!_webViewInitialized)
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ContinueVS", "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder:          userDataFolder);

                await WebView.EnsureCoreWebView2Async(env);

                // Map https://continue.local/ → %APPDATA%\ContinueVS\gui\
                // This lets the React bundle resolve absolute paths like /assets/index.js
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    hostName:          "continue.local",
                    folderPath:        GuiExtractor.GuiRoot,
                    accessKind:        CoreWebView2HostResourceAccessKind.Allow);

                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewInitialized = true;
                _pusher.Subscribe();
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
            => ThreadHelper.JoinableTaskFactory.RunAsync(() => OnWebMessageReceivedAsync(sender, e))
                           .FileAndForget("vs/continuevs/webmessage");  // VSSDK007

        private System.Threading.Tasks.Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            var message = JsonConvert.DeserializeObject<Message>(json);
            if (message == null)
                return System.Threading.Tasks.Task.CompletedTask;

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

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            }).FileAndForget("vs/continuevs/sendtogui");                // VSSDK007
        }

        internal void SendReplyToGui(string messageType, string messageId, object data)
        {
            if (!_webViewInitialized || WebView.CoreWebView2 == null) return;

            var msg = new Message
            {
                MessageType = messageType,
                MessageId   = messageId,
                Data        = JToken.FromObject(data),
            };
            var json    = JsonConvert.SerializeObject(msg);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            }).FileAndForget("vs/continuevs/sendtogui");                // VSSDK007
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

            WebView.Dispose();
        }
    }
}
