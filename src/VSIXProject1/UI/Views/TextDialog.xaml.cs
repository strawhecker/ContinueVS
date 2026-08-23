#nullable enable

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ContinueVS.UI.Views
{
    /// <summary>
    /// TextDialog is a reusable modal content control for capturing user input (text or yes/no confirmation).
    /// Supports two modes: Text input and Confirmation.
    /// </summary>
    public partial class TextDialog : UserControl
    {
        public enum DialogType
        {
            Text,
            Confirmation,
            Progress
        }

        private string? _result;
        private TaskCompletionSource<string?>? _resultTcs;
        private System.Windows.Controls.StackPanel? _progressPanel;
        private System.Windows.Controls.ProgressBar? _progressBarControl;
        private System.Windows.Controls.TextBlock? _progressPercentLabel;

        public static readonly DependencyProperty PromptProperty =
            DependencyProperty.Register(
                nameof(Prompt),
                typeof(string),
                typeof(TextDialog),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty InputProperty =
            DependencyProperty.Register(
                nameof(Input),
                typeof(string),
                typeof(TextDialog),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(
                nameof(Type),
                typeof(DialogType),
                typeof(TextDialog),
                new PropertyMetadata(DialogType.Text, OnTypeChanged));

        public string Prompt
        {
            get => (string)GetValue(PromptProperty) ?? string.Empty;
            set => SetValue(PromptProperty, value ?? string.Empty);
        }

        public string Input
        {
            get => (string)GetValue(InputProperty) ?? string.Empty;
            set => SetValue(InputProperty, value ?? string.Empty);
        }

        public DialogType Type
        {
            get => (DialogType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public string? Result => _result;

        public TextDialog()
        {
            InitializeComponent();
            _progressPanel = (System.Windows.Controls.StackPanel?)FindName("ProgressPanel");
            _progressBarControl = (System.Windows.Controls.ProgressBar?)FindName("ProgressBarControl");
            _progressPercentLabel = (System.Windows.Controls.TextBlock?)FindName("ProgressPercentLabel");
        }

        public void Initialize(DialogType dialogType, string prompt, string defaultValue = "")
        {
            if (string.IsNullOrEmpty(prompt))
                prompt = string.Empty;
            if (defaultValue == null)
                defaultValue = string.Empty;

            Type = dialogType;
            Prompt = prompt;
            Input = defaultValue;
            _result = null;

            UpdateModeVisibility();
        }

        private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextDialog dialog)
            {
                dialog.UpdateModeVisibility();
            }
        }

        private void UpdateModeVisibility()
        {
            if (Type == DialogType.Text)
            {
                InputTextBox.Visibility = Visibility.Visible;
                ProgressPanel.Visibility = Visibility.Collapsed;
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }
            else if (Type == DialogType.Confirmation)
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Collapsed;
                OkButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
            }
            else // Progress
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                ProgressPanel.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Reports progress value (0-100) for Progress mode.
        /// </summary>
        public void ReportProgress(int value)
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            if (_progressBarControl != null) _progressBarControl.Value = value;
            if (_progressPercentLabel != null) _progressPercentLabel.Text = $"{value}%";
        }

        /// <summary>
        /// Signals that the progress work is complete and closes the overlay.
        /// </summary>
        public void CompleteProgress()
        {
            CompleteDialog("done");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteDialog(Input);
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteDialog("yes");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteDialog(null);
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteDialog("no");
        }

        /// <summary>
        /// Completes the dialog with the specified result.
        /// </summary>
        private void CompleteDialog(string? result)
        {
            _result = result;
            _resultTcs?.TrySetResult(result);
        }

        /// <summary>
        /// Returns a task that completes when the user interacts with the dialog (clicks a button).
        /// </summary>
#pragma warning disable VSTHRD003
        public Task<string?> GetResultAsync()
        {
            _resultTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _resultTcs.Task;
        }
#pragma warning restore VSTHRD003
    }
}
