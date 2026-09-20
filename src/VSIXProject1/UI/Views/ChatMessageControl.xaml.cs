using System;
using System.ComponentModel;
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

        // gap53: cache last analyzed content + count so we only re-parse/re-log when
        // the content (and thus code-block count) actually changes. Kept on the control
        // so it survives visual-tree reloads (e.g. tab switches) without re-logging.
        private string? _lastAnalyzedContent;
        private int _lastCodeBlockCount;

        public ChatMessageControl()
        {
            InitializeComponent();
            this.Loaded += ChatMessageControl_Loaded;
            this.Unloaded += ChatMessageControl_Unloaded;
            this.DataContextChanged += ChatMessageControl_DataContextChanged;
        }

        /// <summary>
        /// Gets the message Border element so parent can constrain its width for text wrapping.
        /// </summary>
        public Border? GetMessageBorder() => FindName("MessageBorder") as Border;

        private void ChatMessageControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from the previous message to avoid leaks.
            if (_boundMessage != null)
            {
                _boundMessage.TokenAppended -= OnTokenAppended;
                _boundMessage.PropertyChanged -= OnMessagePropertyChanged;
                _boundMessage = null;
            }

            // Subscribe to the new message's token stream so the streaming
            // reasoning renderer receives each incremental segment directly,
            // and to its PropertyChanged so code-block state is evaluated the
            // moment Content changes (not deferred until the next Loaded).
            if (DataContext is ChatMessage message)
            {
                _boundMessage = message;
                message.TokenAppended += OnTokenAppended;
                message.PropertyChanged += OnMessagePropertyChanged;

                // Evaluate immediately at bind time (runs on the UI thread here).
                ReevaluateCodeBlockState();
                ApplyDropdownVisibility();
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

        /// <summary>
        /// Fired when any property on the bound message changes. We only care about
        /// Content. During streaming this runs on the streaming (likely background)
        /// thread, so the parse/log happen here but the UI update is dispatched.
        /// </summary>
        private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ChatMessage.Content))
                return;

            ReevaluateCodeBlockState();

            if (Dispatcher.CheckAccess())
            {
                ApplyDropdownVisibility();
            }
            else
            {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                Dispatcher.Invoke(
                    new Action(ApplyDropdownVisibility));
#pragma warning restore VSTHRD001
            }
        }

        /// <summary>
        /// Re-parses code-block state only when Content actually changed, and logs
        /// only when the code-block count changed. Thread-agnostic: only touches the
        /// message content and our cached fields (no UI).
        /// </summary>
        private void ReevaluateCodeBlockState()
        {
            var message = DataContext as ChatMessage;
            if (message == null || string.IsNullOrEmpty(message.Content))
                return;

            if (_lastAnalyzedContent == message.Content)
                return; // nothing changed (covers tab-switch reloads)

            _lastAnalyzedContent = message.Content;
            int newCount = CountCodeBlocks(message.Content);

            if (newCount != _lastCodeBlockCount)
            {
                _lastCodeBlockCount = newCount;
                LoggerService.Current.WriteDebug($"[gap53-dropdown-visibility] Message now has {newCount} code block(s); message-level dropdown {(newCount > 0 ? "hidden" : "shown")}");
            }
        }

        /// <summary>
        /// Applies dropdown visibility from the current (cached) code-block count.
        /// Must be called on the UI thread.
        /// </summary>
        private void ApplyDropdownVisibility()
        {
            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null && _lastCodeBlockCount > 0)
                comboBox.Visibility = Visibility.Collapsed;
        }

        private void ChatMessageControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe so a live message doesn't keep references after the control
            // is unloaded (session switch / list virtualization).
            if (_boundMessage != null)
            {
                _boundMessage.TokenAppended -= OnTokenAppended;
                _boundMessage.PropertyChanged -= OnMessagePropertyChanged;
                _boundMessage = null;
            }

            MessageGrid.MouseEnter -= MessageGrid_MouseEnter;
            MessageGrid.MouseLeave -= MessageGrid_MouseLeave;

            var copyAllButton = FindName("CopyAllButton") as Button;
            if (copyAllButton != null)
            {
                copyAllButton.Click -= CopyAllButton_Click;
            }

            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null)
            {
                comboBox.SelectionChanged -= CodeActionDropdown_SelectionChanged;
            }
        }

        private void ChatMessageControl_Loaded(object sender, RoutedEventArgs e)
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
            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null)
            {
                comboBox.SelectionChanged += CodeActionDropdown_SelectionChanged;
            }

            // No parsing/logging here anymore (gap53): code-block state is evaluated in
            // DataContextChanged / OnMessagePropertyChanged at the moment Content changes.
            // Loaded only re-applies the already-computed visibility, in case the control
            // was bound before its template finished materializing.
            ApplyDropdownVisibility();
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
