using ContinueVS.IPC;
using Microsoft.Web.WebView2.Core;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ContinueVS.UI
{
    /// <summary>
    /// WPF UserControl hosting a WebView2 that renders the Continue React GUI.
    ///
    /// While the binary is initialising, a loading spinner is shown.  Once the binary
    /// fires its <c>Ready</c> event the control navigates WebView2 to
    /// <c>http://localhost:{port}/</c> and hides the spinner.
    ///
    /// Messages from the React app that are addressed to the VS IDE are forwarded
    /// to <see cref="ContinueClient"/> and vice-versa.
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
            if (pkg?.BinaryManager == null)
            {
                SetStatus("Continue is not available.");
                return;
            }

            if (pkg.BinaryManager.Port > 0)
            {
                // Binary already running — navigate immediately.
                _ = ThreadHelper.JoinableTaskFactory.RunAsync(
                    () => NavigateAsync(pkg.BinaryManager.Port));
            }
            else
            {
                pkg.BinaryManager.Ready += OnBinaryReady;
            }
        }

        private void OnBinaryReady(object sender, int port)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => NavigateAsync(port));
        }

        private async System.Threading.Tasks.Task NavigateAsync(int port)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!_webViewInitialized)
            {
                await WebView.EnsureCoreWebView2Async();
                WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webViewInitialized = true;

                // Wire incoming IPC messages from the binary → the WebView.
                var pkg = ContinueVSPackage.Instance;
                if (pkg?.Client != null)
                    pkg.Client.MessageReceived += OnClientMessageReceived;
            }

            WebView.Source = new Uri($"http://localhost:{port}/");
            LoadingPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility      = Visibility.Visible;
        }

        // -----------------------------------------------------------------
        // WebView2 → Continue binary bridge
        // -----------------------------------------------------------------

        /// <summary>
        /// Messages posted by the React GUI (window.chrome.webview.postMessage) are
        /// forwarded to the Continue binary via the IPC client.
        /// </summary>
        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(json)) return;

            var pkg = ContinueVSPackage.Instance;
            if (pkg?.Client == null || !pkg.Client.IsConnected) return;

            try
            {
                var msg = JsonConvert.DeserializeObject<Message>(json);
                if (msg != null)
                    await pkg.Client.SendRawMessageAsync(msg, CancellationToken.None);
            }
            catch { /* malformed message — ignore */ }
        }

        /// <summary>
        /// Messages arriving from the binary that are intended for the GUI are forwarded
        /// to the React app via <c>window.continueVS.onMessage(json)</c>.
        /// </summary>
        private void OnClientMessageReceived(object sender, Message msg)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!_webViewInitialized || WebView.CoreWebView2 == null) return;

                var json = JsonConvert.SerializeObject(msg);
                var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            });
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

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await WebView.CoreWebView2.ExecuteScriptAsync(
                    $"window.continueVS && window.continueVS.onMessage('{escaped}');");
            });
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
            if (pkg?.BinaryManager != null)
                pkg.BinaryManager.Ready -= OnBinaryReady;
            if (pkg?.Client != null)
                pkg.Client.MessageReceived -= OnClientMessageReceived;

            WebView.Dispose();
        }
    }
}
