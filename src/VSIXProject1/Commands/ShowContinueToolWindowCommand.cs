using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ContinueVS.UI;

namespace ContinueVS.Commands
{
    /// <summary>
    /// Command handler for showing the Continue tool window (Ctrl+Shift+J).
    /// Responds to the ShowContinuePanel command (0x0100) defined in Menus.vsct.
    /// </summary>
    public sealed class ShowContinueToolWindowCommand
    {
        /// <summary>
        /// Initializes the command (no-op if using .vsct keybindings directly).
        /// </summary>
        public static void Initialize(IAsyncServiceProvider serviceProvider)
        {
            // Command routing is handled by .vsct file + direct keybinding
            // This method is kept for compatibility but does nothing
            System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] Initialized (keybindings via Menus.vsct)");
        }

        /// <summary>
        /// Static method called when Ctrl+Shift+J is pressed.
        /// This must be called from a global command handler since AsyncPackage doesn't support Exec/QueryStatus overrides.
        /// </summary>
        public static void Execute()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] *** EXECUTE CALLED *** - Showing Continue tool window");

                // Get the package instance
                var package = ContinueVSPackage.Instance;
                if (package == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] ✗ ContinueVSPackage.Instance is null");
                    return;
                }

                // Call FindToolWindow to create/find the tool window pane
                // The second parameter (ID) must match what was registered in [ProvideToolWindow]
                var windowPane = package.FindToolWindow(typeof(ContinueToolWindowPane), 0, true);
                if (windowPane?.Frame is not IVsWindowFrame windowFrame)
                {
                    System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] ✗ FindToolWindow returned null or no frame");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] ✓ Tool window frame found/created");

                // Show the tool window
                int hr = windowFrame.Show();
                if (hr == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[ShowContinueToolWindowCommand] ✓ Tool window shown successfully!");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ShowContinueToolWindowCommand] ✗ Show failed with HRESULT 0x{hr:X}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowContinueToolWindowCommand] ✗ Execute error: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShowContinueToolWindowCommand] Stack: {ex.StackTrace}");
            }
        }
    }
}
