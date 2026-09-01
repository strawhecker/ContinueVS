using System;
using ContinueVS.Services;
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
            this.Caption = "ContinueVS";

            try
            {
                // Ensure ViewModelLocator.ServiceProvider is set before creating the control
                // This handles both early startup (deferred) and on-demand scenarios
                if (ContinueVSPackage.ServiceProvider != null && ViewModelLocator.ServiceProvider == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[ContinueToolWindowPane] Setting ViewModelLocator.ServiceProvider...");
                    try
                    {
                        ViewModelLocator.ServiceProvider = ContinueVSPackage.ServiceProvider;
                        _ = LoggerService.Current.WriteDebugAsync("[ContinueToolWindowPane] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (ArgumentNullException)
                    {
                        // Already set by another pane instance; ignore
                        _ = LoggerService.Current.WriteDebugAsync("[ContinueToolWindowPane] Note: ViewModelLocator.ServiceProvider already set");
                    }
                }

                // Create and host the WPF UserControl inside this tool window pane
                _ = LoggerService.Current.WriteDebugAsync("[ContinueToolWindowPane] Creating ContinueToolWindowControl...");
                var control = new ContinueToolWindowControl();
                this.Content = control;

                _ = LoggerService.Current.WriteDebugAsync("[ContinueToolWindowPane] ✓ Tool window pane created and control hosted");
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[ContinueToolWindowPane] ✗ Error during initialization: {ex.GetType().Name}: {ex.Message}", ex);
                throw;
            }
        }
    }
}
