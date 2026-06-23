using Microsoft.VisualStudio.Shell;
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
            Caption = "Continue";
        }

        protected override void Initialize()
        {
            base.Initialize();
            _control = new ContinueToolWindowControl();
            Content  = _control;
        }

        /// <summary>Forwards a pre-populated message to the React GUI.</summary>
        internal void SendToGui(string messageType, object data)
        {
            _control?.SendToGui(messageType, data);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _control?.Dispose();

            base.Dispose(disposing);
        }
    }
}
