using System;
using ContinueVS.ViewModels;
using Microsoft.VisualStudio.Shell;

namespace ContinueVS.UI
{
    /// <summary>
    /// VS Tool Window Pane wrapper for the Continue WPF UserControl.
    /// Bridges the VSIX framework (ToolWindowPane) with the WPF UI layer (ContinueToolWindowControl).
    /// </summary>
    public class ContinueToolWindowPane : ToolWindowPane
    {
        public ContinueToolWindowPane() : base(null)
        {
            // Set the window title
            this.Caption = "Continue";

            try
            {
                // Ensure ViewModelLocator.ServiceProvider is set before creating the control
                // This handles both early startup (deferred) and on-demand scenarios
                if (ContinueVSPackage.ServiceProvider != null && ViewModelLocator.ServiceProvider == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ContinueToolWindowPane] Setting ViewModelLocator.ServiceProvider...");
                    try
                    {
                        ViewModelLocator.ServiceProvider = ContinueVSPackage.ServiceProvider;
                        System.Diagnostics.Debug.WriteLine("[ContinueToolWindowPane] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (ArgumentNullException)
                    {
                        // Already set by another pane instance; ignore
                        System.Diagnostics.Debug.WriteLine("[ContinueToolWindowPane] Note: ViewModelLocator.ServiceProvider already set");
                    }
                }

                // Create and host the WPF UserControl inside this tool window pane
                System.Diagnostics.Debug.WriteLine("[ContinueToolWindowPane] Creating ContinueToolWindowControl...");
                var control = new ContinueToolWindowControl();
                this.Content = control;

                System.Diagnostics.Debug.WriteLine("[ContinueToolWindowPane] ✓ Tool window pane created and control hosted");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowPane] ✗ Error during initialization: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowPane] Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowPane] Stack: {ex.StackTrace}");
                throw;
            }
        }
    }
}
