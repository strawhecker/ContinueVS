#nullable enable

using System.Windows;

namespace ContinueVS.UI
{
    /// <summary>
    /// ProgressWindow.xaml code-behind.
    /// Displays a progress bar with cancel capability.
    /// </summary>
    public partial class ProgressWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the ProgressWindow class.
        /// </summary>
        public ProgressWindow()
        {
            InitializeComponent();
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
