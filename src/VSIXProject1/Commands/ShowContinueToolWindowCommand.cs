using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using ContinueVS.Services;
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
            _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] Initialized (keybindings via Menus.vsct)");
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
                _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] *** EXECUTE CALLED *** - Showing Continue tool window");

                // Get the package instance
                var package = ContinueVSPackage.Instance;
                if (package == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] ✗ ContinueVSPackage.Instance is null");
                    return;
                }

                // Call FindToolWindow to create/find the tool window pane
                // The second parameter (ID) must match what was registered in [ProvideToolWindow]
                var windowPane = package.FindToolWindow(typeof(ContinueToolWindowPane), 0, true);
                if (windowPane?.Frame is not IVsWindowFrame windowFrame)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] ✗ FindToolWindow returned null or no frame");
                    return;
                }

                _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] ✓ Tool window frame found/created");

                // Show the tool window
                int hr = windowFrame.Show();
                if (hr == 0)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[ShowContinueToolWindowCommand] ✓ Tool window shown successfully!");
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[ShowContinueToolWindowCommand] ✗ Show failed with HRESULT 0x{hr:X}");
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[ShowContinueToolWindowCommand] ✗ Execute error: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }
    }
}
