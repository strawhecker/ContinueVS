using ContinueVS.IPC;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Threading;

namespace ContinueVS.Settings
{
    /// <summary>
    /// Watches <c>.continue/config.json</c> in the solution root and pushes a
    /// <c>configUpdate</c> message to the Continue binary whenever the file changes.
    ///
    /// This mirrors how the VS Code extension reloads config without a full restart.
    /// </summary>
    internal sealed class WorkspaceConfigWatcher : IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ContinueClient   _client;
        private FileSystemWatcher?        _watcher;
        private bool _disposed;

        public WorkspaceConfigWatcher(IServiceProvider services, ContinueClient client)
        {
            _services = services;
            _client   = client;
        }

        /// <summary>Locates the solution root and starts watching; must be called on UI thread.</summary>
        internal void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = ((IServiceProvider)_services).GetService(typeof(DTE)) as DTE2;
            string? solutionDir = null;
            try
            {
                var sln = dte?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                    solutionDir = Path.GetDirectoryName(sln.FullName);
            }
            catch { }

            if (string.IsNullOrEmpty(solutionDir)) return;

            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continue");

            if (!Directory.Exists(configDir)) return;

            _watcher = new FileSystemWatcher(configDir, "config.json")
            {
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnConfigChanged;
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            if (!_client.IsConnected) return;

            // Debounce: the OS often fires two events in quick succession.
            System.Threading.Thread.Sleep(200);

            string content = "";
            try { content = File.ReadAllText(e.FullPath); } catch { }

            _ = _client.SendAsync("configUpdate",
                new { config = content },
                CancellationToken.None);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _watcher?.Dispose();
        }
    }
}
