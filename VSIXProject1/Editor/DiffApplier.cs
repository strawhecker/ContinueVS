using ContinueVS.IPC;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Editor
{
    /// <summary>
    /// Handles <c>applyToFile</c> messages from the Continue binary.
    ///
    /// When the binary asks the IDE to apply a code change:
    ///   1. Opens the target file in the editor.
    ///   2. Displays a VS diff view between the current content and the proposed content.
    ///   3. After the user reviews it, they can Accept (Ctrl+S on the modified side) or
    ///      Reject (close the diff window without saving) — standard VS diff workflow.
    ///
    /// For headless apply (no confirmation, used by quick-actions) call
    /// <see cref="ApplyDirectlyAsync"/> with the proposed text.
    /// </summary>
    internal sealed class DiffApplier : IDisposable
    {
        private readonly IServiceProvider _services;
        private bool _disposed;

        public DiffApplier(IServiceProvider services)
        {
            _services = services;
        }

        public void Register()
        {
        }

        public void Unregister()
        {
        }

        // -----------------------------------------------------------------
        // Message dispatch
        // -----------------------------------------------------------------

        private void OnMessage(object sender, Message msg)
        {
            if (msg.MessageType != "applyToFile") return;

            var filepath = msg.Data?["filepath"]?.Value<string>() ?? "";
            var text     = msg.Data?["text"]?.Value<string>() ?? "";

            if (string.IsNullOrEmpty(filepath) || string.IsNullOrEmpty(text)) return;

            _ = Task.Run(() => ShowDiffAsync(filepath, text, msg));
        }

        // -----------------------------------------------------------------
        // Diff view
        // -----------------------------------------------------------------

        private async Task ShowDiffAsync(string filepath, string proposedText, Message originalMsg)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Write the proposed content to a temp file.
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                $"continue_proposed_{Path.GetFileName(filepath)}");

            try { File.WriteAllText(tempPath, proposedText); }
            catch { return; }

            // Open the diff service.
            var diffService = _services.GetService(typeof(SVsDifferenceService)) as IVsDifferenceService;
            if (diffService == null)
            {
                // Fallback: just overwrite the file directly and open it.
                await ApplyDirectlyAsync(filepath, proposedText);
                return;
            }

            var leftLabel  = $"{Path.GetFileName(filepath)} (current)";
            var rightLabel = $"{Path.GetFileName(filepath)} (proposed)";

            diffService.OpenComparisonWindow2(
                filepath, tempPath,
                $"Continue — {Path.GetFileName(filepath)}",
                "", leftLabel, rightLabel, "", "",
                (uint)(__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary |
                       __VSDIFFSERVICEOPTIONS.VSDIFFOPT_RightFileIsTemporary));
        }

        // -----------------------------------------------------------------
        // Direct (headless) apply
        // -----------------------------------------------------------------

        /// <summary>
        /// Overwrites the file and reloads it in the editor without showing a diff.
        /// Called by the diff view's Accept path or from test code.
        /// </summary>
        internal static async Task ApplyDirectlyAsync(string filepath, string proposedText)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                File.WriteAllText(filepath, proposedText);
            }
            catch { return; }

            // If the file is open in VS, refresh the document.
            var rdt = ServiceProvider.GlobalProvider.GetService(typeof(SVsRunningDocumentTable))
                          as IVsRunningDocumentTable;
            if (rdt != null)
            {
                uint cookie;
                IVsHierarchy hierarchy;
                uint itemId;
                IntPtr docData;

                int hr = rdt.FindAndLockDocument(
                    (uint)_VSRDTFLAGS.RDT_NoLock, filepath,
                    out hierarchy, out itemId, out docData, out cookie);

                if (hr == VSConstants.S_OK && docData != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.Release(docData);
                    rdt.NotifyDocumentChanged(cookie, (uint)__VSRDTATTRIB.RDTA_DocDataReloaded);
                }
            }
        }

        // -----------------------------------------------------------------
        // IDisposable
        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unregister();
        }
    }
}
