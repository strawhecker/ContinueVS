using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.InteropServices;

namespace ContinueVS.UI
{
    /// <summary>
    /// VS Tool Window that hosts <see cref="ContinueToolWindowControl"/>.
    /// Registered via <c>[ProvideToolWindow]</c> on <see cref="ContinueVSPackage"/>.
    /// </summary>
    [Guid("E3A7F1C2-8B4D-4E5A-9F2C-1D6B3A8E0F7D")]
    public sealed class ContinueToolWindowPane : ToolWindowPane
    {
        private ContinueToolWindowControl? _control;

        public ContinueToolWindowPane() : base(null)
        {
            System.Diagnostics.Debug.WriteLine("[CV-t3] ContinueToolWindowPane ctor: entry");
            Caption = "Continue";
            System.Diagnostics.Debug.WriteLine("[CV-t3] ContinueToolWindowPane ctor: caption set to 'Continue'");
        }

        protected override void Initialize()
        {
            System.Diagnostics.Debug.WriteLine("[CV-t3] Initialize() called");
            try
            {
                // t3.1 - Base initialization
                var tracer = ContinueVSPackage.ExecutionTracer;
                IDisposable? scope31 = tracer?.BeginScope("t3.1", "ContinueToolWindowPane.Initialize");
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CV-t3.1] Calling base.Initialize()...");
                    base.Initialize();
                    System.Diagnostics.Debug.WriteLine("[CV-t3.1] base.Initialize() complete");
                }
                finally
                {
                    scope31?.Dispose();
                }

                // t3.2 - Control instantiation
                IDisposable? scope32 = tracer?.BeginScope("t3.2", "ContinueToolWindowPane.Initialize");
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CV-t3.2] Creating ContinueToolWindowControl...");
                    _control = new ContinueToolWindowControl();
                    System.Diagnostics.Debug.WriteLine("[CV-t3.2] ContinueToolWindowControl created");
                }
                finally
                {
                    scope32?.Dispose();
                }

                // t3.3 - Content assignment
                IDisposable? scope33 = tracer?.BeginScope("t3.3", "ContinueToolWindowPane.Initialize");
                try
                {
                    System.Diagnostics.Debug.WriteLine("[CV-t3.3] Assigning control to Content...");
                    Content = _control;
                    System.Diagnostics.Debug.WriteLine("[CV-t3.3] Content assigned");
                    System.Diagnostics.Debug.WriteLine("[CV-t3] Initialize() END - SUCCESS");
                }
                finally
                {
                    scope33?.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t3] Initialize() FAILED: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[CV-t3] Exception message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CV-t3] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>Forwards a pre-populated message to the React GUI.</summary>
        internal void SendToGui(string messageType, object data)
        {
            _control?.SendToGui(messageType, data);
        }

        /// <summary>Sends a message to the GUI and awaits a reply with the matching messageId.</summary>
        internal System.Threading.Tasks.Task<JToken?> SendToGuiAndAwaitReplyAsync(
            string messageType, object data, System.Threading.CancellationToken cancellationToken)
        {
            return _control != null
                ? _control.SendToGuiAndAwaitReplyAsync(messageType, data, cancellationToken)
                : System.Threading.Tasks.Task.FromResult<JToken?>(null);
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
                _control?.Dispose();

            base.Dispose(disposing);
        }
    }
}
