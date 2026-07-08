using ContinueVS.IPC;
using ContinueVS.UI;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ContinueVS.Handlers.Push
{
    internal sealed class WebviewPusher : IVsRunningDocTableEvents3
    {
        private readonly ContinueToolWindowControl _control;
        private IVsRunningDocumentTable? _rdt;
        private uint _rdtCookie;
        private WindowEvents? _windowEvents;

        internal WebviewPusher(ContinueToolWindowControl control)
        {
            _control = control;
        }

        // -----------------------------------------------------------------
        // Push methods
        // -----------------------------------------------------------------

        internal void PushConfigUpdate()
        {
            _control.SendToGui("configUpdate", new
            {
                result    = new IdeSettings(),
                profileId = (string?)null,
                profiles  = new object[0],
            });
        }

        internal void PushIndexProgress()
        {
            _control.SendToGui("indexProgress", new
            {
                progress                  = 1.0,
                desc                      = "Indexing complete",
                status                    = "done",
                shouldClearIndexingStatus = false,
            });
        }

        internal void PushDidChangeActiveTextEditor(string filepath)
        {
            _control.SendToGui("didChangeActiveTextEditor", new DidChangeActiveTextEditor { Filepath = filepath });
        }

        // -----------------------------------------------------------------
        // Subscribe / Dispose
        // -----------------------------------------------------------------

        internal void Subscribe()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _rdt = Package.GetGlobalService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            _rdt?.AdviseRunningDocTableEvents(this, out _rdtCookie);

            var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                _windowEvents = dte.Events.WindowEvents;
                if (_windowEvents != null)
                {
                    _windowEvents.WindowActivated += OnWindowActivated;
                }
            }
        }

        internal void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_windowEvents != null)
            {
                _windowEvents.WindowActivated -= OnWindowActivated;
                _windowEvents = null;
            }
            if (_rdt != null && _rdtCookie != 0)
            {
                _rdt.UnadviseRunningDocTableEvents(_rdtCookie);
                _rdtCookie = 0;
                _rdt       = null;
            }
        }

        private void OnWindowActivated(Window gotFocus, Window lostFocus)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var filepath = gotFocus?.Document?.FullName ?? "";
            if (!string.IsNullOrEmpty(filepath))
                PushDidChangeActiveTextEditor(filepath);
        }

        // -----------------------------------------------------------------
        // IVsRunningDocTableEvents  (base — all stubs)
        // -----------------------------------------------------------------

        int IVsRunningDocTableEvents.OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents.OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents.OnAfterSave(uint docCookie)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        // -----------------------------------------------------------------
        // IVsRunningDocTableEvents2  (all stubs)
        // -----------------------------------------------------------------

        int IVsRunningDocTableEvents2.OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnAfterSave(uint docCookie)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents2.OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        // -----------------------------------------------------------------
        // IVsRunningDocTableEvents3  (all stubs + OnBeforeSave)
        // -----------------------------------------------------------------

        int IVsRunningDocTableEvents3.OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnAfterSave(uint docCookie)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnAfterAttributeChange(uint docCookie, uint grfAttribs)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
            => Microsoft.VisualStudio.VSConstants.S_OK;

        int IVsRunningDocTableEvents3.OnBeforeSave(uint docCookie)
            => Microsoft.VisualStudio.VSConstants.S_OK;
    }
}
