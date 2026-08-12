#nullable enable

using System.Windows;

namespace ContinueVS.UI
{
    /// <summary>
    /// InputWindow.xaml code-behind.
    /// Displays a dialog for user text input.
    /// </summary>
    public partial class InputWindow : Window
    {
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
