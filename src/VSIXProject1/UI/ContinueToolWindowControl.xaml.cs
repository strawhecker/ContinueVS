using System;
using System.Windows;
using System.Windows.Controls;

namespace ContinueVS.UI
{
    /// <summary>
    /// WPF UserControl hosting the Continue tool window UI with WPF pages.
    /// Serves as the main container for ChatPage, ConfigPage, and other UI pages.
    /// </summary>
    public partial class ContinueToolWindowControl : UserControl, IDisposable
    {
        private bool _disposed;

        public ContinueToolWindowControl()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContinueToolWindowControl] Initialization error: {ex.Message}");
                MessageBox.Show($"Error initializing Continue tool window: {ex.Message}", "Initialization Error");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~ContinueToolWindowControl()
        {
            Dispose();
        }
    }
}
