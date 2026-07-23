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
            System.Diagnostics.Debug.WriteLine("[CV-t7] WorkspaceConfigWatcher.ctor ENTRY - pusher parameter received");
            _pusher = pusher;
            System.Diagnostics.Debug.WriteLine("[CV-t7] WorkspaceConfigWatcher.ctor EXIT - _pusher field assigned");
        }

        /// <summary>Locates the solution root and starts watching; must be called on UI thread.</summary>
        internal void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            System.Diagnostics.Debug.WriteLine("[CV-t7] Start() ENTRY - UI thread verified");

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            string? solutionDir = null;
            try
            {
                var sln = dte?.Solution;
                if (sln != null && !string.IsNullOrEmpty(sln.FullName))
                {
                    solutionDir = Path.GetDirectoryName(sln.FullName);
                    System.Diagnostics.Debug.WriteLine($"[CV-t7] Solution directory resolved: {solutionDir}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t7] DTE access failed: {ex.Message}");
            }

            if (string.IsNullOrEmpty(solutionDir))
            {
                System.Diagnostics.Debug.WriteLine("[CV-t7] Start() EXIT - solution directory not found, watcher not started");
                return;
            }

            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".continue");

            if (!Directory.Exists(configDir))
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t7] Start() EXIT - config directory not found: {configDir}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[CV-t7] Creating FileSystemWatcher for {configDir}");
            _watcher = new FileSystemWatcher(configDir, "config.json")
            {
                NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += OnConfigChanged;
            System.Diagnostics.Debug.WriteLine("[CV-t7] Start() EXIT - FileSystemWatcher created and subscribed");
        }

        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[CV-t7] OnConfigChanged FIRED - {e.FullPath}");
            // Debounce: the OS often fires two events in quick succession.
            System.Threading.Thread.Sleep(200);
            System.Diagnostics.Debug.WriteLine("[CV-t7] Debounce complete, calling PushConfigUpdate()");

            string content = "";
            try { content = File.ReadAllText(e.FullPath); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t7] File read failed: {ex.Message}");
            }

            _pusher.PushConfigUpdate();
            System.Diagnostics.Debug.WriteLine("[CV-t7] PushConfigUpdate() completed");
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("[CV-t7] Dispose() ENTRY");
            if (_disposed)
            {
                System.Diagnostics.Debug.WriteLine("[CV-t7] Dispose() EXIT - already disposed");
                return;
            }
            _disposed = true;

            if (_watcher != null)
            {
                _watcher.Changed -= OnConfigChanged;
                System.Diagnostics.Debug.WriteLine("[CV-t7] FileSystemWatcher event handler unsubscribed");
                _watcher.Dispose();
                System.Diagnostics.Debug.WriteLine("[CV-t7] FileSystemWatcher disposed");
            }
            System.Diagnostics.Debug.WriteLine("[CV-t7] Dispose() EXIT");
        }
    }
}
