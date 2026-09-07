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
                    LoggerService.Current.WriteDebug("[ContinueToolWindowPane] Setting ViewModelLocator.ServiceProvider...");
                    try
                    {
                        ViewModelLocator.ServiceProvider = ContinueVSPackage.ServiceProvider;
                        LoggerService.Current.WriteDebug("[ContinueToolWindowPane] ✓ ViewModelLocator.ServiceProvider set");
                    }
                    catch (ArgumentNullException)
                    {
                        // Already set by another pane instance; ignore
                        LoggerService.Current.WriteDebug("[ContinueToolWindowPane] Note: ViewModelLocator.ServiceProvider already set");
                    }
                }

                // Create and host the WPF UserControl inside this tool window pane
                LoggerService.Current.WriteDebug("[ContinueToolWindowPane] Creating ContinueToolWindowControl...");
                var control = new ContinueToolWindowControl();
                this.Content = control;

                LoggerService.Current.WriteDebug("[ContinueToolWindowPane] ✓ Tool window pane created and control hosted");
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[ContinueToolWindowPane] ✗ Error during initialization: {ex.GetType().Name}: {ex.Message}", ex);
                throw;
            }
        }
    }
}
