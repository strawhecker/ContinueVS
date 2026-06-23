using ContinueVS.IPC;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

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

        public ContinueToolWindowControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // -----------------------------------------------------------------
        // Startup
        // -----------------------------------------------------------------

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var pkg = ContinueVSPackage.Instance;
        }

        private async System.Threading.Tasks.Task NavigateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!_webViewInitialized)
            {
                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewInitialized = true;

                // Wire incoming IPC messages from the binary → the WebView.
                var pkg = ContinueVSPackage.Instance;
            }

            // Navigate to the bundled GUI HTML extracted from the Continue VSIX,
            // or fall back to the marketplace page if it isn't extracted yet.
            var guiHtml = Path.Combine(
                Path.GetDirectoryName(typeof(ContinueToolWindowControl).Assembly.Location)!,
                "gui", "index.html");

            WebView.Source = File.Exists(guiHtml)
                ? new Uri(guiHtml)
                : new Uri("https://marketplace.visualstudio.com/items?itemName=Continue.continue");

            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility      = Visibility.Visible;
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

        private async System.Threading.Tasks.Task OnWebMessageReceivedAsync(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            var pkg = ContinueVSPackage.Instance;

            try
            {
                var msg = JsonConvert.DeserializeObject<Message>(json);
            }
            catch { /* malformed message — ignore */ }
        }

        /// <summary>
        /// Messages arriving from the binary that are intended for the GUI are forwarded
        /// to the React app via <c>window.continueVS.onMessage(json)</c>.
        /// </summary>
        private void OnClientMessageReceived(object sender, Message msg)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!_webViewInitialized || WebView.CoreWebView2 == null) return;

                var json    = JsonConvert.SerializeObject(msg);
                var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            }).FileAndForget("vs/continuevs/clientmessage");            // VSSDK007
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

        private void SetStatus(string text)
        {
            StatusText.Text = text;
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var pkg = ContinueVSPackage.Instance;

            WebView.Dispose();
        }
    }
}
