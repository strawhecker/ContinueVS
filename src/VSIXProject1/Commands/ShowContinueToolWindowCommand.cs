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
            LoggerService.Current.WriteDebug("[ShowContinueToolWindowCommand] Initialized (keybindings via Menus.vsct)");
        }

        /// <summary>
        /// Static method called when Ctrl+Shift+J is pressed.
        /// This must be called from a global command handler since AsyncPackage doesn't support Exec/QueryStatus overrides.
        /// </summary>
        public static void Execute()
        {
            var debugFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".continueVS", "logs", "DEBUG_Ctrl_Shift_J.txt");

            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ShowContinueToolWindowCommand.Execute() called\n");
                LoggerService.Current.WriteDebug("[ShowContinueToolWindowCommand] *** EXECUTE CALLED *** - Showing Continue tool window");

                // Get the package instance
                var package = ContinueVSPackage.Instance;
                if (package == null)
                {
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ERROR: ContinueVSPackage.Instance is null\n");
                    LoggerService.Current.WriteError("[ShowContinueToolWindowCommand] ✗ ContinueVSPackage.Instance is null - package not initialized yet", null);
                    return;
                }

                System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] Package acquired, calling FindToolWindow...\n");
                LoggerService.Current.WriteDebug("[ShowContinueToolWindowCommand] Package instance acquired, attempting FindToolWindow...");

                // Call FindToolWindow to create/find the tool window pane
                // The second parameter (ID) must match what was registered in [ProvideToolWindow]
                // Wrap in timeout to prevent infinite loops
                ToolWindowPane? windowPane = null;
                try
                {
                    windowPane = package.FindToolWindow(typeof(ContinueToolWindowPane), 0, true);
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] FindToolWindow completed, pane={windowPane != null}\n");
                }
                catch (Exception findEx)
                {
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] FindToolWindow EXCEPTION: {findEx.GetType().Name}: {findEx.Message}\n");
                    LoggerService.Current.WriteError($"[ShowContinueToolWindowCommand] ✗ FindToolWindow threw exception: {findEx.GetType().Name}: {findEx.Message}", findEx);
                    return;
                }

                if (windowPane == null)
                {
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ERROR: windowPane is null\n");
                    LoggerService.Current.WriteError("[ShowContinueToolWindowCommand] ✗ FindToolWindow returned null", null);
                    return;
                }

                var windowFrame = windowPane.Frame as IVsWindowFrame;
                if (windowFrame == null)
                {
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ERROR: windowFrame is null\n");
                    LoggerService.Current.WriteError("[ShowContinueToolWindowCommand] ✗ Tool window pane has no frame (IVsWindowFrame)", null);
                    return;
                }

                System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] Window frame acquired, calling Show()...\n");
                LoggerService.Current.WriteDebug("[ShowContinueToolWindowCommand] ✓ Tool window frame found/created");

                // Show the tool window
                try
                {
                    int hr = windowFrame.Show();
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] Show() returned HRESULT=0x{hr:X}\n");
                    if (hr == 0)
                    {
                        System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ✓✓✓ SUCCESS - Tool window shown!\n");
                        LoggerService.Current.WriteDebug("[ShowContinueToolWindowCommand] ✓ Tool window shown successfully!");
                    }
                    else
                    {
                        System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] ERROR: Show() failed with HRESULT 0x{hr:X}\n");
                        LoggerService.Current.WriteError($"[ShowContinueToolWindowCommand] ✗ Show failed with HRESULT 0x{hr:X}", null);
                    }
                }
                catch (Exception showEx)
                {
                    System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] EXCEPTION during Show(): {showEx.GetType().Name}: {showEx.Message}\n");
                    LoggerService.Current.WriteError($"[ShowContinueToolWindowCommand] ✗ Show threw exception: {showEx.GetType().Name}: {showEx.Message}", showEx);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(debugFile, $"[{System.DateTime.Now:HH:mm:ss.fff}] OUTER EXCEPTION: {ex.GetType().Name}: {ex.Message}\n");
                LoggerService.Current.WriteError($"[ShowContinueToolWindowCommand] ✗ Execute error: {ex.GetType().Name}: {ex.Message}", ex);
            }
        }
    }
}
