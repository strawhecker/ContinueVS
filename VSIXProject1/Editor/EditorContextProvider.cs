using ContinueVS.IPC;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Editor
{
    /// <summary>
    /// Tracks the active editor document and streams <c>currentFile</c> context
    /// updates to the Continue binary whenever the active document or cursor changes.
    ///
    /// Implements <see cref="IVsRunningDocTableEvents3"/> to receive document lifecycle
    /// events without polling.
    ///
    /// Call <see cref="RegisterAsync"/> once after the IPC client is connected.
    /// Call <see cref="Dispose"/> on package shutdown.
    /// </summary>
    internal sealed class EditorContextProvider : IVsRunningDocTableEvents3, IDisposable
    {
        // Debounce: skip sending updates faster than 300 ms.
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(300);

        private readonly IServiceProvider _services;
        private readonly ContinueClient   _client;

        private IVsRunningDocumentTable? _rdt;
        private uint                      _rdtCookie;
        private DTE2?                     _dte;
        private Events2?                  _events;
        private SelectionEvents?          _selectionEvents;

        private CancellationTokenSource? _debounceCts;
        private bool _disposed;

        public EditorContextProvider(IServiceProvider services, ContinueClient client)
        {
            _services = services;
            _client   = client;
        }

        /// <summary>
        /// Subscribes to VS document/selection events.  Must be called on the UI thread.
        /// </summary>
        internal async Task RegisterAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _rdt = _services.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            _rdt?.AdviseRunningDocTableEvents(this, out _rdtCookie);

            _dte = _services.GetService(typeof(DTE)) as DTE2;
            if (_dte != null)
            {
                _events          = _dte.Events as Events2;
                _selectionEvents = _events?.SelectionEvents;
                if (_selectionEvents != null)
                    _selectionEvents.OnChange += OnSelectionChange;
            }
        }

        // -----------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------

        private void OnSelectionChange()
        {
            ScheduleContextUpdate();
        }

        // IVsRunningDocTableEvents3 — only care about window-frame activations.
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld,
            uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            // RDTA_DocDataReloaded = 0x00000020 — content changed.
            if ((grfAttribs & 0x20) != 0)
                ScheduleContextUpdate();
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint cookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            if (fFirstShow != 0)
            {
                ScheduleContextUpdate();
                _ = PushActiveEditorChangedAsync();
            }
            return VSConstants.S_OK;
        }

        // -----------------------------------------------------------------
        // Debounced push
        // -----------------------------------------------------------------

        private void ScheduleContextUpdate()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Delay(DebounceInterval, token).ContinueWith(
                _ => PushCurrentFileContextAsync(),
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private async Task PushCurrentFileContextAsync()
        {
            if (!_client.IsConnected) return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var doc = _dte?.ActiveDocument;
            if (doc == null) return;

            var path    = doc.FullName;
            string contents = "";
            try { contents = File.ReadAllText(path); } catch { return; }

            // Resolve cursor position from the text selection.
            int line = 0, col = 0;
            if (doc.Selection is TextSelection sel)
            {
                line = sel.ActivePoint.Line - 1;       // 0-based
                col  = sel.ActivePoint.LineCharOffset - 1;
            }

            var data = new
            {
                filepath       = path,
                contents,
                cursorPosition = new { line, character = col },
            };

            await _client.SendAsync("currentFileUpdate", data, CancellationToken.None);
        }

        // -----------------------------------------------------------------
        // Unused IVsRunningDocTableEvents3 members (must be implemented)
        // -----------------------------------------------------------------

        public int OnAfterFirstDocumentLock(uint cookie, uint lockType, uint readLocks, uint editLocks) => VSConstants.S_OK;
        public int OnBeforeLastDocumentUnlock(uint cookie, uint lockType, uint readLocks, uint editLocks) => VSConstants.S_OK;
        public int OnAfterSave(uint cookie) => VSConstants.S_OK;
        public int OnBeforeSave(uint cookie) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint cookie, uint grfAttribs) => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint cookie, IVsWindowFrame pFrame) => VSConstants.S_OK;

        private async System.Threading.Tasks.Task PushActiveEditorChangedAsync()
        {
            if (!_client.IsConnected) return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var path = _dte?.ActiveDocument?.FullName ?? "";
            if (string.IsNullOrEmpty(path)) return;

            await _client.SendAsync(
                "didChangeActiveTextEditor",
                new DidChangeActiveTextEditor { Filepath = path },
                CancellationToken.None);
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

                if (_selectionEvents != null)
                    _selectionEvents.OnChange -= OnSelectionChange;

                if (_rdt != null && _rdtCookie != 0)
                    _rdt.UnadviseRunningDocTableEvents(_rdtCookie);
            });

            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
        }
    }
}
