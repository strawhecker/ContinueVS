using System;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.UI.Pages;
using ContinueVS.UI.Renderers;
using Markdig;
using Markdig.Syntax;

namespace ContinueVS.UI.Views
{
    public partial class ChatMessageControl : UserControl
    {
        private static readonly MarkdownPipeline _pipeline =
            new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        private ChatMessage? _boundMessage;

        public ChatMessageControl()
        {
            InitializeComponent();
            this.Loaded += ChatMessageControl_Loaded;
            this.DataContextChanged += ChatMessageControl_DataContextChanged;
        }

        /// <summary>
        /// Gets the message Border element so parent can constrain its width for text wrapping.
        /// </summary>
        public Border? GetMessageBorder() => FindName("MessageBorder") as Border;

        private void ChatMessageControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from the previous message's token stream to avoid leaks.
            if (_boundMessage != null)
            {
                _boundMessage.TokenAppended -= OnTokenAppended;
                _boundMessage = null;
            }

            // Subscribe to the new message's token stream so the streaming
            // reasoning renderer receives each incremental segment directly,
            // without the caller having to assemble a cumulative string.
            if (DataContext is ChatMessage message)
            {
                _boundMessage = message;
                message.TokenAppended += OnTokenAppended;
            }
        }

        private void OnTokenAppended(string token)
        {
            var renderer = FindName("StreamingReasoningRenderer") as StreamingReasoningRenderer;
            if (renderer != null)
            {
                renderer.AppendToken(token);
            }
        }

        private void ChatMessageControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            MessageGrid.MouseEnter += MessageGrid_MouseEnter;
            MessageGrid.MouseLeave += MessageGrid_MouseLeave;

            // Wire up Copy All button if it exists in the visual tree
            var copyAllButton = FindName("CopyAllButton") as Button;
            if (copyAllButton != null)
            {
                copyAllButton.Click += CopyAllButton_Click;
            }

            // Wire up dropdown if it exists in the visual tree
            // Gap53: Conditionally show message-level dropdown only if no code blocks present
            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null)
            {
                comboBox.SelectionChanged += CodeActionDropdown_SelectionChanged;

                // Detect if content has code blocks; if it does, hide message-level dropdown (gap53)
                var message = DataContext as ChatMessage;
                if (message != null && !string.IsNullOrEmpty(message.Content))
                {
                    int codeBlockCount = CountCodeBlocks(message.Content);
                    if (codeBlockCount > 0)
                    {
                        comboBox.Visibility = Visibility.Collapsed;
                        LoggerService.Current.WriteDebug($"[gap53-dropdown-visibility] Message has {codeBlockCount} code block(s); message-level dropdown hidden");
                    }
                }
            }
        }

        private void MessageGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Visible;

            var copyAllButton = FindName("CopyAllButton") as Button;
            if (copyAllButton != null && copyAllButton.Visibility != System.Windows.Visibility.Collapsed)
                copyAllButton.Visibility = System.Windows.Visibility.Visible;

            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null && comboBox.Visibility != System.Windows.Visibility.Collapsed)
                comboBox.Visibility = System.Windows.Visibility.Visible;
        }

        private void MessageGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Hidden;

            var copyAllButton = FindName("CopyAllButton") as Button;
            if (copyAllButton != null && copyAllButton.Visibility != System.Windows.Visibility.Collapsed)
                copyAllButton.Visibility = System.Windows.Visibility.Hidden;

            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null && comboBox.Visibility != System.Windows.Visibility.Collapsed)
                comboBox.Visibility = System.Windows.Visibility.Hidden;
        }

        /// <summary>
        /// Handles Copy All button click to copy the entire response message.
        /// </summary>
        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            var content = (DataContext as ChatMessage)?.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    Clipboard.SetText(content);
                    LoggerService.Current.WriteDebug("[gap49-copy-all] Entire response copied to clipboard");
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError($"[gap49-copy-all-error] Failed to copy: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Handles Copy/Apply dropdown selection change (gap49).
        /// When user selects an action, wire it to the appropriate command.
        /// </summary>
        private void CodeActionDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox)
                return;

            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
                return;

            var content = (DataContext as ChatMessage)?.Content ?? string.Empty;

            // Check which item was selected by content
            if (selectedItem.Content.ToString().Contains("Copy"))
            {
                // Execute copy
                try
                {
                    Clipboard.SetText(content);
                    LoggerService.Current.WriteDebug("[gap49-dropdown-copy] Code copied to clipboard");
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError($"[gap49-dropdown-copy-error] Failed to copy: {ex.Message}", ex);
                }
            }
            else if (selectedItem.Content.ToString().Contains("Apply"))
            {
                // Execute apply via command
                LoggerService.Current.WriteDebug("[gap49-dropdown-apply] Apply selected from dropdown");
                // Command will be wired to ApplyCodeBlockCommand via XAML if needed
            }

            // Reset selection to Copy after handling
            comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Counts the number of code blocks in markdown content (gap53).
        /// </summary>
        private int CountCodeBlocks(string content)
        {
            try
            {
                var doc = Markdown.Parse(content, _pipeline);
                int count = 0;
                foreach (var block in doc)
                {
                    if (block is FencedCodeBlock)
                        count++;
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }
    }
}

