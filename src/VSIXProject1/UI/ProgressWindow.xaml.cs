#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ContinueVS.UI
{
    /// <summary>
    /// ProgressWindow.xaml code-behind.
    /// Displays a progress bar with cancel capability.
    /// </summary>
    public partial class ProgressWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// Initializes a new instance of the ProgressWindow class.
        /// </summary>
        public ProgressWindow()
        {
            InitializeComponent();
            this.Loaded += ProgressWindow_Loaded;
        }

        private void ProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Send window to back to prevent it from stealing focus
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// Reports progress to the progress bar.
        /// </summary>
        /// <param name="value">The progress value (0-100).</param>
        public void ReportProgress(int value)
        {
            if (value < 0)
                value = 0;
            if (value > 100)
                value = 100;

            ProgressBar.Value = value;
            PercentLabel.Text = $"{value}%";
        }
    }
}
