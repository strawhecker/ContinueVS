#nullable enable

using System;
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
            Confirmation
        }

        private string? _result;

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
                // Text mode: show TextBox and OK/Cancel buttons
                InputTextBox.Visibility = Visibility.Visible;
                OkButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                YesButton.Visibility = Visibility.Collapsed;
                NoButton.Visibility = Visibility.Collapsed;
            }
            else // DialogType.Confirmation
            {
                // Confirmation mode: hide TextBox, show Yes/No buttons
                InputTextBox.Visibility = Visibility.Collapsed;
                OkButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
            }

            if (Type == DialogType.Text)
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _result = Input;
            // Dialog result will be read via Result property
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            _result = "yes";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _result = null;
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            _result = "no";
        }
    }
}
