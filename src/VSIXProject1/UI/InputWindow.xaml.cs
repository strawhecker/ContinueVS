#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ContinueVS.UI
{
    /// <summary>
    /// InputWindow.xaml code-behind.
    /// Displays a dialog for user text input.
    /// </summary>
    public partial class InputWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// Gets or sets the prompt text displayed to the user.
        /// </summary>
        public string Prompt
        {
            get => PromptLabel.Text;
            set => PromptLabel.Text = value;
        }

        /// <summary>
        /// Gets or sets the input text.
        /// </summary>
        public string Input
        {
            get => InputTextBox.Text;
            set => InputTextBox.Text = value;
        }

        /// <summary>
        /// Initializes a new instance of the InputWindow class.
        /// </summary>
        public InputWindow()
        {
            InitializeComponent();
            InputTextBox.Focus();
            this.Loaded += InputWindow_Loaded;
        }

        private void InputWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Make visible only after loaded (was Hidden in XAML)
            this.Visibility = Visibility.Visible;

            // Send window to back to prevent it from stealing focus
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// Handles the OK button click event.
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
