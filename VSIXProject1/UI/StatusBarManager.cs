using ContinueVS.IPC;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using System;

namespace ContinueVS.UI
{
    /// <summary>
    /// Listens for model-change messages from the Continue binary and updates
    /// the VS status bar with the active model name.
    /// </summary>
    internal sealed class StatusBarManager : IDisposable
    {
        private readonly ContinueClient   _client;
        private readonly IServiceProvider _services;
        private bool _disposed;

        public StatusBarManager(ContinueClient client, IServiceProvider services)
        {
            _client   = client;
            _services = services;
        }

        public void Register()   => _client.MessageReceived += OnMessage;
        public void Unregister() => _client.MessageReceived -= OnMessage;

        private void OnMessage(object sender, Message msg)
        {
            string? modelText = null;

            if (msg.MessageType == "setTitle")
                modelText = msg.Data?["text"]?.Value<string>();
            else if (msg.MessageType == "configUpdate")
                modelText = TryExtractModel(msg.Data);

            if (string.IsNullOrWhiteSpace(modelText)) return;

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_services.GetService(typeof(SVsStatusbar)) is IVsStatusbar bar)
                    bar.SetText($"Continue: {modelText}");
            });
        }

        private static string? TryExtractModel(JToken? data)
        {
            try
            {
                return data?["defaultModelTitle"]?.Value<string>()
                    ?? data?["models"]?[0]?["title"]?.Value<string>();
            }
            catch { return null; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unregister();
        }
    }
}
