using ContinueVS.Handlers.Push;
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
        private readonly WebviewPusher    _pusher;
        private FileSystemWatcher?        _watcher;
        private bool _disposed;

        public WorkspaceConfigWatcher(WebviewPusher pusher)
        {
            _pusher = pusher;
        }

        /// <summary>Locates the solution root and starts watching; must be called on UI thread.</summary>
        internal void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
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
            // Debounce: the OS often fires two events in quick succession.
            System.Threading.Thread.Sleep(200);

            string content = "";
            try { content = File.ReadAllText(e.FullPath); } catch { }

            _pusher.PushConfigUpdate();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _watcher?.Dispose();
        }
    }
}
