using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.UI.Pages;
using ContinueVS.ViewModels;
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
                _boundMessage.PropertyChanged -= OnMessagePropertyChanged;
                _boundMessage = null;
            }

            // Subscribe to the new message's PropertyChanged so code-block state is
            // evaluated the moment Content changes (not deferred until the next Loaded).
            // Note: streaming tokens are rendered by the unified StreamingMarkdownRenderer,
            // which subscribes to the message's TokenAppended itself via its Message DP —
            // ChatMessageControl only needs PropertyChanged for dropdown/minimize state.
            if (DataContext is ChatMessage message)
            {
                _boundMessage = message;
                message.PropertyChanged += OnMessagePropertyChanged;

                // Evaluate immediately at bind time (runs on the UI thread here).
                ReevaluateCodeBlockState();
                ApplyDropdownVisibility();
            }
        }

        /// <summary>
        /// Fired when any property on the bound message changes. We only care about
        /// Content. During streaming this runs on the streaming (likely background)
        /// thread, so the parse/log happen here but the UI update is dispatched.
        /// </summary>
        private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatMessage.Content))
            {
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
            else if (e.PropertyName == nameof(ChatMessage.IsMinimized))
            {
                // gap85: Re-apply the collapse/expand visuals (and minimize icon) when the
                // IsMinimized flag toggles. This keeps both the hover toolbar button and the
                // right-click toggle in sync without waiting for a reload.
                if (Dispatcher.CheckAccess())
                {
                    ApplyMinimizedState();
                }
                else
                {
#pragma warning disable VSTHRD001 // Await JoinableTaskFactory.SwitchToMainThreadAsync
                    Dispatcher.Invoke(
                        new Action(ApplyMinimizedState));
#pragma warning restore VSTHRD001
                }
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

            // gap89: reset the per-card view to pretty when the card leaves the visual tree,
            // so raw never persists as a resting state (non-sticky by design).
            if (DataContext is ChatMessage leaving && leaving.ViewMode != MessageViewMode.Pretty)
            {
                leaving.ViewMode = MessageViewMode.Pretty;
            }

            var rawToggle = FindName("RawToggleButton") as Button;
            if (rawToggle != null)
            {
                rawToggle.Click -= RawToggleButton_Click;
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

            // gap89: wire the raw/processed view toggle to the bound message.
            var rawToggle = FindName("RawToggleButton") as Button;
            if (rawToggle != null)
            {
                rawToggle.Click += RawToggleButton_Click;
            }
            ApplyRawToggleState();

            // gap85: Apply delete/undelete + minimize/maximize icons and toggle collapse state
            ApplyDeleteButtonIcon();
            ApplyMinimizedState();
            ApplyToolCallHeader();

            // No parsing/logging here anymore (gap53): code-block state is evaluated in
            // DataContextChanged / OnMessagePropertyChanged at the moment Content changes.
            // Loaded only re-applies the already-computed visibility, in case the control
            // was bound before its template finished materializing.
            ApplyDropdownVisibility();
        }

        /// <summary>
        /// gap85: Routes the delete/undelete toggle. If the bound message is already soft-deleted
        /// (tombstone set), clicking undeletes it (↺) instead of hard-deleting; otherwise it
        /// soft-deletes (✕). Reuses the existing SoftDeleteMessageCommand / UndeleteMessageCommand.
        /// </summary>
        private void DeleteUndeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatMessage message || string.IsNullOrEmpty(message.Id))
                return;

            var vm = ResolveViewModel();
            if (vm == null)
                return;

            if (message.IsDeleted)
            {
                vm.UndeleteMessageCommand.Execute(message.Id);
            }
            else
            {
                vm.SoftDeleteMessageCommand.Execute(message.Id);
            }
        }

        /// <summary>
        /// gap85: Fires the minimize/maximize toggle command for the bound message.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMinimize();
        }

        /// <summary>
        /// gap85: Right-click on a message toggles its minimize/maximize state.
        /// Reuses the same ToggleMinimizeMessageCommand as the hover toolbar button so
        /// users can quickly collapse a message without moving the mouse to the toolbar.
        /// </summary>
        private void MessageGrid_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleMinimize();
        }

        /// <summary>
        /// gap85: Toggles the bound message's minimize/maximize state via the parent ViewModel.
        /// No-op when the bound message has no Id or no parent ViewModel can be resolved.
        /// </summary>
        private void ToggleMinimize()
        {
            if (DataContext is ChatMessage message && !string.IsNullOrEmpty(message.Id))
            {
                ResolveViewModel()?.ToggleMinimizeMessageCommand.Execute(message.Id);
            }
        }

        /// <summary>
        /// gap85: Resolves the parent ChatPageViewModel from the visual tree.
        /// </summary>
        private ChatPageViewModel? ResolveViewModel()
        {
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is System.Windows.FrameworkElement fe && fe.DataContext is ChatPageViewModel vm)
                    return vm;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// gap85: Sets the delete button icon based on tombstone state — "✕" when not deleted,
        /// "↺" (undelete) when soft-deleted.
        /// </summary>
        private void ApplyDeleteButtonIcon()
        {
            var message = DataContext as ChatMessage;
            var icon = FindName("DeleteButtonIcon") as TextBlock;
            if (icon == null)
                return;

            if (message?.IsDeleted == true)
            {
                icon.Text = "↺";
                var btn = FindName("DeleteButton") as Button;
                if (btn != null) btn.ToolTip = "Undelete message";
            }
            else
            {
                icon.Text = "✕";
                var btn = FindName("DeleteButton") as Button;
                if (btn != null) btn.ToolTip = "Delete message";
            }
        }

        /// <summary>
        /// gap85: Applies the minimize/maximize icon AND collapses/expands the bubble body.
        /// When IsMinimized, shows "▢/⤢" (maximize) and collapses the renderers; otherwise "_" (minimize).
        /// The collapse is UI-only and driven by IsMinimized; it never touches the tombstone.
        /// </summary>
        private void ApplyMinimizedState()
        {
            var message = DataContext as ChatMessage;
            var icon = FindName("MinimizeButtonIcon") as TextBlock;
            if (icon != null)
            {
                icon.Text = message?.IsMinimized == true ? "▢/⤢" : "_";
                var btn = FindName("MinimizeButton") as Button;
                if (btn != null) btn.ToolTip = message?.IsMinimized == true ? "Maximize message" : "Minimize message";
            }

            bool minimized = message?.IsMinimized == true;
            var renderer = FindName("StreamingMarkdownRenderer") as FrameworkElement;
            var placeholder = FindName("MinimizedPlaceholder") as TextBlock;
            var copyAll = FindName("CopyAllButton") as Button;
            var dropdown = FindName("CodeActionDropdown") as ComboBox;

            if (minimized)
            {
                if (renderer != null) renderer.Visibility = Visibility.Collapsed;
                if (placeholder != null) placeholder.Visibility = Visibility.Visible;
                if (copyAll != null) copyAll.Visibility = Visibility.Collapsed;
                if (dropdown != null) dropdown.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (placeholder != null) placeholder.Visibility = Visibility.Collapsed;
                ApplyDropdownVisibility();
                // The unified renderer's visibility is bound to the message role via the
                // converters, so it is reapplied automatically on restore.
            }
        }

        /// <summary>
        /// gap85: Updates the minimized placeholder text to the tool-call label for Tool-role
        /// messages (tool name + optional file used). For non-tool messages it shows a generic cue.
        /// </summary>
        private void ApplyToolCallHeader()
        {
            var placeholder = FindName("MinimizedPlaceholder") as TextBlock;
            if (placeholder == null || DataContext is not ChatMessage message)
                return;

            if (message.Role == ChatMessageRole.Tool)
            {
                var file = message.ToolFileName;
                placeholder.Text = string.IsNullOrWhiteSpace(file)
                    ? $"🧰 {message.ToolCallLabel}"
                    : $"🧰 {message.ToolCallLabel} — {file}";
            }
            else
            {
                placeholder.Text = "Message minimized — click ▢/⤢ to expand";
            }
        }

        private void MessageGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Visible;
            MinimizeButton.Visibility = System.Windows.Visibility.Visible;

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
            MinimizeButton.Visibility = System.Windows.Visibility.Hidden;

            var copyAllButton = FindName("CopyAllButton") as Button;
            if (copyAllButton != null && copyAllButton.Visibility != System.Windows.Visibility.Collapsed)
                copyAllButton.Visibility = System.Windows.Visibility.Hidden;

            var comboBox = FindName("CodeActionDropdown") as ComboBox;
            if (comboBox != null && comboBox.Visibility != System.Windows.Visibility.Collapsed)
                comboBox.Visibility = System.Windows.Visibility.Hidden;

            // gap89: non-sticky raw view — reverting to Pretty when the pointer leaves the card.
            if (DataContext is ChatMessage left && left.ViewMode != MessageViewMode.Pretty)
            {
                left.ViewMode = MessageViewMode.Pretty;
            }
        }

        /// <summary>
        /// Copies the entire response message, honoring the per-card view mode (gap89):
        /// Pretty → RTF + plain on one DataObject; Raw → plain only, byte-faithful.
        /// After a copy the view reverts to Pretty (non-sticky by design).
        /// </summary>
        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatMessage message)
                return;

            if (string.IsNullOrWhiteSpace(message.Content))
                return;

            bool ok = ClipboardWriter.Copy(message);
            if (ok)
                LoggerService.Current.WriteDebug($"[gap89-copy-all] Copied ({message.ViewMode}) to clipboard; {(message.ViewMode == MessageViewMode.Pretty ? "RTF+UnicodeText" : "UnicodeText")}");
            else
                LoggerService.Current.WriteError("[gap89-copy-all-error] Failed to copy via ClipboardWriter");

            // Non-sticky raw view: return to Pretty after the copy ships the bytes.
            if (message.ViewMode != MessageViewMode.Pretty)
            {
                message.ViewMode = MessageViewMode.Pretty;
                ApplyRawToggleState();
            }
        }

        /// <summary>
        /// gap89: Toggles the bound message's per-card view between Pretty and Raw.
        /// </summary>
        private void RawToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatMessage message)
                return;

            // Only markdown (reasoning/response) cards carry a meaningful raw/pretty split;
            // user/tool cards are verbatim by kind and the toggle reflects that (no-op flip).
            message.ViewMode = message.ViewMode == MessageViewMode.Pretty
                ? MessageViewMode.Raw
                : MessageViewMode.Pretty;

            ApplyRawToggleState();
        }

        /// <summary>
        /// gap89: Reflects the current per-card view on the toggle icon/tip.
        /// The glyph is always visible: normal WindowText in Pretty (== the XAML default,
        /// which we re-apply here defensively) and orange+bold in Raw so the active,
        /// destination-facing raw view is unmistakable at a glance.
        /// </summary>
        private void ApplyRawToggleState()
        {
            var msg = DataContext as ChatMessage;
            var icon = FindName("RawToggleIcon") as TextBlock;
            var btn = FindName("RawToggleButton") as Button;

            bool raw = msg?.ViewMode == MessageViewMode.Raw;
            if (icon != null)
            {
                icon.FontWeight = raw ? FontWeights.Bold : FontWeights.Normal;
                icon.Foreground = raw
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0))
                    : TryGetWindowTextBrush();
            }
            if (btn != null)
                btn.ToolTip = raw ? "Viewing raw source — switch to processed" : "View raw source (destination-facing)";
        }

        /// <summary>
        /// Resolves the theme-aware WindowText brush (same key the XAML uses), falling back
        /// to a neutral BrushText if the resource isn't currently available (e.g. pre-load).
        /// </summary>
        private static System.Windows.Media.Brush? TryGetWindowTextBrush()
        {
            try
            {
                if (Application.Current != null &&
                    Application.Current.TryFindResource("VsBrush.WindowText") is System.Windows.Media.Brush brush)
                {
                    return brush;
                }
            }
            catch
            {
                // ignore resource lookup failures; fall through to the neutral fallback
            }
            return System.Windows.Media.Brushes.Black;
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

            var message = DataContext as ChatMessage;
            var content = message?.Content ?? string.Empty;

            // Check which item was selected by content
            if (selectedItem.Content.ToString().Contains("Copy"))
            {
                // gap89: route through the view-state-aware writer (Pretty → RTF+plain, Raw → plain only).
                if (string.IsNullOrWhiteSpace(content))
                    return;

                bool ok = ClipboardWriter.Copy(message);
                if (ok)
                    LoggerService.Current.WriteDebug($"[gap89-dropdown-copy] Copied ({message?.ViewMode}) to clipboard");
                else
                    LoggerService.Current.WriteError("[gap89-dropdown-copy-error] Failed to copy via ClipboardWriter");

                // Non-sticky raw view: return to Pretty after the copy.
                if (message != null && message.ViewMode != MessageViewMode.Pretty)
                {
                    message.ViewMode = MessageViewMode.Pretty;
                    ApplyRawToggleState();
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
