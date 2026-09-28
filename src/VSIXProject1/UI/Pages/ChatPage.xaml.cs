using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Pages
{
    /// <summary>
    /// DataTemplateSelector for routing messages to appropriate templates based on role and context.
    /// </summary>
    public class ChatMessageTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// DataTemplate for user messages.
        /// </summary>
        public DataTemplate? UserMessageTemplate { get; set; }

        /// <summary>
        /// DataTemplate for assistant messages.
        /// </summary>
        public DataTemplate? AssistantMessageTemplate { get; set; }

        /// <summary>
        /// DataTemplate for tool invocation messages (Role.Tool).
        /// </summary>
        public DataTemplate? ToolInvocationTemplate { get; set; }

        /// <summary>
        /// DataTemplate for system messages.
        /// </summary>
        public DataTemplate? SystemMessageTemplate { get; set; }

        /// <summary>
        /// DataTemplate for LLM question messages with answer controls.
        /// </summary>
        public DataTemplate? QuestionMessageTemplate { get; set; }

        /// <summary>
        /// DataTemplate for thinking/reasoning messages (Role.Thinking).
        /// </summary>
        public DataTemplate? ThinkingMessageTemplate { get; set; }

        /// <summary>
        /// DataTemplate for execution impact messages (gap69).
        /// </summary>
        public DataTemplate? ExecutionImpactTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            // Check for ExecutionImpactMessage first (gap69)
            if (item is Core.Types.ExecutionImpactMessage)
            {
                return ExecutionImpactTemplate ?? SystemMessageTemplate ?? base.SelectTemplate(item, container);
            }

            // Check for LLMQuestionMessage (derived from ChatMessage)
            if (item is Core.Types.LLMQuestionMessage)
            {
                return QuestionMessageTemplate ?? SystemMessageTemplate ?? base.SelectTemplate(item, container);
            }

            if (item is ChatMessage msg)
            {
                return msg.Role switch
                {
                    ChatMessageRole.Thinking => ThinkingMessageTemplate ?? base.SelectTemplate(item, container),
                    ChatMessageRole.User => UserMessageTemplate ?? base.SelectTemplate(item, container),
                    ChatMessageRole.Assistant => AssistantMessageTemplate ?? base.SelectTemplate(item, container),
                    ChatMessageRole.Tool => ToolInvocationTemplate ?? base.SelectTemplate(item, container),
                    ChatMessageRole.System => SystemMessageTemplate ?? base.SelectTemplate(item, container),
                    _ => base.SelectTemplate(item, container)
                };
            }
            return base.SelectTemplate(item, container);
        }
    }

    public partial class ChatPage : UserControl
    {
        private ScrollViewer? _messagesScrollViewer;

        public ChatPage()
        {
            // Load theme resources before XAML initialization so DynamicResource can resolve them
            try
            {
                var themeDictPath = Path.Combine(
                    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                    "UI", "Styles", "Themes", "ThemeDark.xaml"
                );

                if (File.Exists(themeDictPath))
                {
                    var themeDictionary = new ResourceDictionary
                    {
                        Source = new Uri(themeDictPath, UriKind.Absolute)
                    };
                    // Merge into Application.Current.Resources so all controls can access theme brushes
                    if (Application.Current != null && Application.Current.Resources != null)
                    {
                        Application.Current.Resources.MergedDictionaries.Add(themeDictionary);
                        LoggerService.Current.WriteDebug($"[ChatPage] Theme loaded into Application.Current.Resources from: {themeDictPath}");
                    }
                }
                else
                {
                    LoggerService.Current.WriteDebug($"[ChatPage] Theme file not found at: {themeDictPath}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[ChatPage] Failed to load theme: {ex.Message}", ex);
            }

            InitializeComponent();

            try
            {
                LoggerService.Current.WriteDebug("[sv-chatpage] ChatPage constructor: resolving services from DI");
                var sp = ViewModelLocator.ServiceProvider;
                if (sp != null)
                {
                    LoggerService.Current.WriteDebug("[sv-chatpage] ServiceProvider is available");

                    var llm         = sp.GetRequiredService<ILlmService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-1] ✓ ILlmService resolved");

                    var context     = sp.GetRequiredService<IContextService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-2] ✓ IContextService resolved");

                    var tool        = sp.GetRequiredService<IToolService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-3] ✓ IToolService resolved");

                    var session     = sp.GetRequiredService<ISessionService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-4] ✓ ISessionService resolved");

                    var notif       = sp.GetRequiredService<INotificationService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-5] ✓ INotificationService resolved");

                    var config      = sp.GetRequiredService<IConfigService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-6] ✓ IConfigService resolved");

                    var systemPrompt = sp.GetRequiredService<ISystemPromptService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-7] ✓ ISystemPromptService resolved");

                    var uiState     = sp.GetRequiredService<IUIStateService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-8] ✓ IUIStateService resolved");

                    var instructionExecutor = sp.GetRequiredService<IInstructionExecutorService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-9] ✓ IInstructionExecutorService resolved");

                    var changeStackService = sp.GetRequiredService<IChangeStackService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-9b] ✓ IChangeStackService resolved");

                    var markdownService = sp.GetRequiredService<IMarkdownService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-9c] ✓ IMarkdownService resolved");

                    var llmQuestionService = sp.GetService<ILlmQuestionService>();
                    LoggerService.Current.WriteDebug("[sv-chatpage-9d] ✓ ILlmQuestionService resolved");

                    var workflow    = sp.GetService<IWorkflowService>();
                    LoggerService.Current.WriteDebug($"[sv-chatpage-10] IWorkflowService resolved={workflow != null} (optional)");

                    var ideService  = sp.GetService<IIdeService>();
                    LoggerService.Current.WriteDebug($"[sv-chatpage-11] IIdeService resolved={ideService != null} (optional)");

                    var planOutput  = sp.GetService<IPlanOutputService>();
                    LoggerService.Current.WriteDebug($"[sv-chatpage-12] IPlanOutputService resolved={planOutput != null} (optional)");

                    // BP:sv-chatpage-dc — breakpoint here confirms all services resolved and DataContext is being assigned
                    // Resolve the DI singleton so the ChatPageViewModel bound here is the SAME instance the
                    // inline-question handler (IInteractivePromptService) feeds questions into. Constructing a
                    // second instance here previously meant ask_user questions were added to a VM not bound to
                    // this UI, so the user never saw them.
                    var chatPageVm = sp.GetService<ChatPageViewModel>();
                    if (chatPageVm == null)
                    {
                        throw new InvalidOperationException("ChatPageViewModel is not registered in the service container");
                    }
                    this.DataContext = chatPageVm;
                    LoggerService.Current.WriteDebug("[sv-chatpage-dc] ✓ ChatPageViewModel resolved from DI and DataContext assigned");
                }
                else
                {
                    LoggerService.Current.WriteError("[sv-chatpage-FAIL] ServiceProvider is NULL — ViewModelLocator.ServiceProvider not set. InitializeAsync may not have completed.", null);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[sv-chatpage-FAIL] ✗ Exception type: {ex.GetType().FullName}", ex);
                LoggerService.Current.WriteError($"[sv-chatpage-FAIL] ✗ Message: {ex.Message}", ex);
                LoggerService.Current.WriteError($"[sv-chatpage-FAIL] ✗ StackTrace: {ex.StackTrace}", ex);
                if (ex.InnerException != null)
                {
                    LoggerService.Current.WriteError($"[sv-chatpage-FAIL] ✗ InnerException type: {ex.InnerException.GetType().FullName}", ex);
                    LoggerService.Current.WriteError($"[sv-chatpage-FAIL] ✗ InnerException message: {ex.InnerException.Message}", ex);
                }
            }

            // Wire up scroll-to-bottom on messages collection changed
            this.Loaded += ChatPage_Loaded;
            this.Unloaded += ChatPage_Unloaded;
        }

        private void ChatPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get reference to ScrollViewer
                _messagesScrollViewer = this.FindName("MessagesScrollViewer") as ScrollViewer;

                // Constrain ItemsControl width based on ScrollViewer dimensions
                var messagesItemsControl = this.FindName("MessagesItemsControl") as ItemsControl;
                if (_messagesScrollViewer != null && messagesItemsControl != null)
                {
                    // When ScrollViewer size changes, update all message MaxWidth
                    _messagesScrollViewer.SizeChanged += (s, e) =>
                    {
                        if (e.NewSize.Width > 0 && this.DataContext is ChatPageViewModel vm)
                        {
                            // Account for scrollbar width (~17px) and padding
                            double availableWidth = e.NewSize.Width - 20;

                            // Set ViewModel's AvailableMessageWidth for data binding
                            vm.AvailableMessageWidth = availableWidth;

                            // Apply to ItemsControl for layout
                            messagesItemsControl.MaxWidth = availableWidth;
                            messagesItemsControl.Width = double.NaN; // Auto within max
                        }
                    };
                }

                // Hook into Messages collection changed event
                if (this.DataContext is ChatPageViewModel vm && vm.Messages is ObservableCollection<ChatMessage> messages)
                {
                    messages.CollectionChanged += Messages_CollectionChanged;

                    // Initialize the ViewModel asynchronously without blocking (fire and forget with error handling)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await vm.InitializeAsync();
                            LoggerService.Current.WriteDebug("[ChatPage_Loaded] ViewModel initialization complete");
                        }
                        catch (Exception ex)
                        {
                            LoggerService.Current.WriteError("[ChatPage_Loaded] ViewModel initialization error", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[ChatPage] Loaded event error: {ex.Message}", ex);
            }
        }

        private void ChatPage_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Unhook to prevent memory leaks
                if (this.DataContext is ChatPageViewModel vm && vm.Messages is ObservableCollection<ChatMessage> messages)
                {
                    messages.CollectionChanged -= Messages_CollectionChanged;

                    // Also unhook property changed from all messages
                    foreach (var msg in messages)
                    {
                        if (msg is System.ComponentModel.INotifyPropertyChanged notifiable)
                        {
                            notifiable.PropertyChanged -= Message_PropertyChanged;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[ChatPage] Unloaded event error: {ex.Message}", ex);
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            try
            {
                // When new messages are added, hook into their PropertyChanged events
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is ChatMessage msg && msg is System.ComponentModel.INotifyPropertyChanged notifiable)
                        {
                            notifiable.PropertyChanged += Message_PropertyChanged;
                        }
                    }

                    // Also apply MaxWidth to newly added message containers
                    if (_messagesScrollViewer != null && _messagesScrollViewer.ActualWidth > 0)
                    {
                        var messagesItemsControl = this.FindName("MessagesItemsControl") as ItemsControl;
                        if (messagesItemsControl != null)
                        {
                            double availableWidth = _messagesScrollViewer.ActualWidth - 20;
                            foreach (var item in e.NewItems)
                            {
                                var container = messagesItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                                if (container is FrameworkElement fe)
                                {
                                    fe.MaxWidth = availableWidth;

                                    // Also set the message bubble border width for text wrapping
                                    if (fe is ContentPresenter cp)
                                    {
                                        // The ContentPresenter's content should be the ChatMessageControl
                                        var messageControl = cp.Content as UI.Views.ChatMessageControl;
                                        if (messageControl != null)
                                        {
                                            var border = messageControl.GetMessageBorder();
                                            if (border != null)
                                            {
                                                border.MaxWidth = availableWidth * 0.8; // 80% of available for visual balance
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // When messages are removed (session switch, message deletion), unhook
                // their PropertyChanged handlers so they don't keep this page (and its
                // ScrollViewer) alive after the message is gone (missing-unsubscribe leak).
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        if (item is ChatMessage removedMsg &&
                            removedMsg is System.ComponentModel.INotifyPropertyChanged removedNotifiable)
                        {
                            removedNotifiable.PropertyChanged -= Message_PropertyChanged;
                        }
                    }
                }

                if (_messagesScrollViewer != null)
                {
                    // Only auto-scroll to bottom when new messages are added IF the user is
                    // already at (or near) the bottom. If the user has scrolled up to read or
                    // copy earlier content, do NOT force them back down to the bottom.
                    double scrollableHeight = _messagesScrollViewer.ScrollableHeight;
                    double verticalOffset = _messagesScrollViewer.VerticalOffset;

                    if (scrollableHeight <= 0 || verticalOffset >= scrollableHeight - 5)
                    {
                        _messagesScrollViewer.ScrollToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError($"[ChatPage] Messages_CollectionChanged error: {ex.Message}", ex);
            }
        }

        private void Message_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // When a message's Content property changes (during streaming), only scroll to bottom if already at bottom
            // This allows users to scroll up and read history without being forced back down
            if (e.PropertyName == nameof(ChatMessage.Content) && _messagesScrollViewer != null)
            {
                try
                {
                    // Check if scrollbar is at the bottom (with small tolerance for rounding)
                    double scrollableHeight = _messagesScrollViewer.ScrollableHeight;
                    double verticalOffset = _messagesScrollViewer.VerticalOffset;

                    // If at bottom (within 5 pixels tolerance), auto-scroll to show new content
                    if (scrollableHeight <= 0 || verticalOffset >= scrollableHeight - 5)
                    {
                        _messagesScrollViewer.ScrollToEnd();
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError($"[ChatPage] Message_PropertyChanged scroll error: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Event handler for dismissing the warning banner (gap23_4_4).
        /// </summary>
        private void DismissWarningBanner_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ChatPageViewModel vm)
            {
                vm.DismissWarningBannerCommand();
            }
        }

        /// <summary>
        /// Event handler for dismissing the error banner (gap23_4_4).
        /// </summary>
        private void DismissErrorBanner_Click(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is ChatPageViewModel vm)
            {
                vm.ShowErrorBanner = false;
            }
        }

        /// <summary>
        /// Event handler for History button - toggles session history panel visibility (gap76).
        /// </summary>
        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var historyPanel = this.FindName("HistoryPanel") as Border;
            if (historyPanel != null)
            {
                historyPanel.Visibility = historyPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        /// <summary>
        /// gap42_2: PreviewExecuted handler for ApplicationCommands.Paste.
        /// Reads clipboard text and logs line/character count. e.Handled is NOT set,
        /// so WPF default paste proceeds and newlines are preserved (AcceptsReturn="True").
        /// </summary>
        private void InputTextBox_Paste_PreviewExecuted(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (!System.Windows.Clipboard.ContainsText())
                return;

            string content = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(content))
                return;

            int lines = content.Split('\n').Length;
            int len = content.Length;
            LoggerService.Current.WriteDebug($"[gap42-paste] multiline content pasted: {lines} lines, {len} characters");
        }

        /// <summary>
        /// gap35_1: Intercepts Enter to fire SendMessageCommand; Shift+Enter inserts a newline.
        /// Uses PreviewKeyDown (tunneling) so handler fires before WPF default TextBox processing.
        /// AcceptsReturn="True" on the TextBox preserves newlines on paste (gap42_2); Enter alone
        /// is consumed here so it never inserts a newline.
        /// </summary>
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true; // always consume — we decide what happens
                if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    // Shift+Enter → insert newline at caret
                    if (sender is System.Windows.Controls.TextBox tb)
                    {
                        int caret = tb.CaretIndex;
                        tb.Text = tb.Text.Insert(caret, "\n");
                        tb.CaretIndex = caret + 1;
                    }
                }
                else
                {
                    // Enter alone → send message
                    LoggerService.Current.WriteDebug("[gap35] Enter key intercepted — firing SendMessageCommand");
                    if (DataContext is ChatPageViewModel vm && vm.SendMessageCommand.CanExecute(null))
                        vm.SendMessageCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Handles the Send button click on a question message.
        /// Retrieves the answer from the TextBox and invokes the question's OnAnswerAsync callback.
        /// </summary>
        private void QuestionAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ChatPageViewModel vm || sender is not Button btn)
                return;

            // Get the LLMQuestionMessage from the button's DataContext
            if (btn.DataContext is not Core.Types.LLMQuestionMessage question)
                return;

            // The answer TextBox is bound directly to the question's QuestionAnswer property
            // (UpdateSourceTrigger=PropertyChanged), so the user's typed answer is already
            // available here. This avoids a fragile visual-tree walk that previously failed
            // because the template nests another StackPanel between the Button and the Border.
            var answer = question.QuestionAnswer;

            if (string.IsNullOrWhiteSpace(answer))
            {
                LoggerService.Current.WriteDebug("[gap54-question] No answer provided");
                return;
            }

            LoggerService.Current.WriteDebug($"[gap54-question] Answer provided: {answer}");

            // Fire the OnAnswerAsync callback
            _ = question.OnAnswerAsync?.Invoke(answer!);
        }

        /// <summary>
        /// Handles a click on one of the offered answer-option elements in a question card.
        /// The option element's DataContext is the option string (ItemsControl item), so we climb
        /// the visual tree to find the owning LLMQuestionMessage, then answer immediately.
        /// gap86_1: the option is now a clickable <see cref="Border"/> (not a Button) wrapping a
        /// TextBlock, so long answers wrap within the card's finite-width Grid column.
        /// </summary>
        private void QuestionOptionButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.FrameworkElement el)
                return;

            var option = el.DataContext as string;
            if (string.IsNullOrWhiteSpace(option))
                return;

            // Climb to the first ancestor whose DataContext is the LLMQuestionMessage.
            var question = FindAncestorByDataContext<Core.Types.LLMQuestionMessage>(el);
            if (question == null)
                return;

            LoggerService.Current.WriteDebug($"[gap54-question] Option selected: {option}");

            // Answer immediately with the selected option (option text crossed into history).
            _ = question.OnAnswerAsync?.Invoke(option!);
        }

        /// <summary>
        /// Walks up the visual tree from <paramref name="element"/> to find the first
        /// ancestor whose DataContext is assignable to <typeparamref name="T"/>.
        /// </summary>
        private static T? FindAncestorByDataContext<T>(DependencyObject element) where T : class
        {
            var current = element;
            while (current != null)
            {
                if (current is FrameworkElement fe && fe.DataContext is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// Handles the Cancel button click on a question message.
        /// Invokes the question's OnCancelAsync callback to dismiss the question.
        /// </summary>
        private void QuestionCancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            // Get the LLMQuestionMessage from the button's DataContext
            if (btn.DataContext is not Core.Types.LLMQuestionMessage question)
                return;

            LoggerService.Current.WriteDebug("[gap54-question] Question cancelled");

            // Fire the OnCancelAsync callback
            _ = question.OnCancelAsync?.Invoke();
        }


        /// <summary>
        /// gap86_1: Handles Copy All on a question card — copies the whole card
        /// (question + all answer options) as one UnicodeText clipboard payload.
        /// Because every part of the card is captured, no individual part needs to be
        /// selectable; the question remains selectable for convenience via the renderer.
        /// </summary>
        private void QuestionCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.DataContext is not Core.Types.LLMQuestionMessage question)
                return;

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(question.QuestionText))
                sb.AppendLine(question.QuestionText);

            if (question.Answers != null && question.Answers.Count > 0)
            {
                sb.AppendLine();
                foreach (var ans in question.Answers)
                {
                    if (!string.IsNullOrWhiteSpace(ans))
                        sb.AppendLine("- " + ans);
                }
            }

            var payload = sb.ToString().TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(payload))
                return;

            bool ok = Services.Implementations.ClipboardWriter.Copy(payload, MessageViewMode.Raw);
            if (ok)
                LoggerService.Current.WriteDebug("[gap86_1-question-copy-all] Question card copied to clipboard");
            else
                LoggerService.Current.WriteError("[gap86_1-question-copy-all-error] Failed to copy question card");
        }


        /// <summary>
        /// gap86_1: Handles "Use text below as answer" — copies (never cuts/clears) the main
        /// composer's draft text into the card's own independent answer string, then sends it
        /// through OnAnswerAsync. The composer (InputTextBox) is NEVER mutated, honoring the
        /// "user loses nothing" edict; only the card's own field is cleared once the text
        /// crosses into chat history.
        /// </summary>
        private void QuestionClaimComposerText_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.DataContext is not Core.Types.LLMQuestionMessage question)
                return;

            if (DataContext is not ChatPageViewModel vm)
                return;

            var composerText = vm.InputText;
            if (string.IsNullOrWhiteSpace(composerText))
                return;

            // Copy — do NOT clear the composer. It keeps the user's draft.
            string answer = composerText!;

            LoggerService.Current.WriteDebug($"[gap86_1-question-claim] Claimed composer text as answer ({answer.Length} chars)");

            // The card's own independent answer string crosses into chat history.
            _ = question.OnAnswerAsync?.Invoke(answer);
        }


        /// <summary>
        /// gap90c: Handles Copy All for a tool card — copies the full verbatim tool
        /// output (plain) to the clipboard. Tool output is source-facing, so it is
        /// shipped as UnicodeText only (no RTF), matching ClipboardWriter raw semantics.
        /// </summary>
        private void ToolCardCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            if (btn.DataContext is not ChatMessage message)
                return;

            if (string.IsNullOrWhiteSpace(message.Content))
                return;

            bool ok = Services.Implementations.ClipboardWriter.Copy(message.Content, MessageViewMode.Raw);
            if (ok)
                LoggerService.Current.WriteDebug("[gap90c-tool-copy-all] Tool output copied to clipboard");
            else
                LoggerService.Current.WriteError("[gap90c-tool-copy-all-error] Failed to copy tool output");
        }


        /// <summary>
        /// Handles Copy All button click for thinking messages.
        /// </summary>
        private void ThinkingCopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
                return;

            var content = (btn.DataContext as ChatMessage)?.Content ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    Clipboard.SetText(content);
                    LoggerService.Current.WriteDebug("[thinking-copy-all] Thinking content copied to clipboard");
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError("[thinking-copy-all-error] Failed to copy", ex);
                }
            }
        }

        /// <summary>
        /// Handles Copy/Apply dropdown selection change for thinking messages.
        /// </summary>
        private void ThinkingCodeActionDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.DataContext is not ChatMessage message)
                return;

            var selectedItem = comboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
                return;

            var content = message.Content ?? string.Empty;

            if (selectedItem.Content.ToString()?.Contains("Copy") == true)
            {
                try
                {
                    Clipboard.SetText(content);
                    LoggerService.Current.WriteDebug("[thinking-dropdown-copy] Thinking content copied via dropdown");
                }
                catch (Exception ex)
                {
                    LoggerService.Current.WriteError("[thinking-dropdown-copy-error] Failed to copy", ex);
                }
            }
            else if (selectedItem.Content.ToString()?.Contains("Apply") == true)
            {
                LoggerService.Current.WriteDebug("[thinking-dropdown-apply] Apply selected from thinking dropdown");
            }

            // Reset to Copy
            comboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Handles mouse enter on thinking message Grid to show controls.
        /// </summary>
        private void ThinkingMessageGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            try
            {
                var copyAllButton = grid.FindName("ThinkingCopyAllButton") as Button;
                if (copyAllButton != null && copyAllButton.Visibility != Visibility.Collapsed)
                    copyAllButton.Visibility = Visibility.Visible;

                var dropdown = grid.FindName("ThinkingCodeActionDropdown") as ComboBox;
                if (dropdown != null && dropdown.Visibility != Visibility.Collapsed)
                    dropdown.Visibility = Visibility.Visible;

                var deleteButton = grid.FindName("ThinkingDeleteButton") as Button;
                if (deleteButton != null && deleteButton.Visibility != Visibility.Collapsed)
                    deleteButton.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError("[thinking-controls-hover] Error on mouse enter", ex);
            }
        }

        /// <summary>
        /// Handles mouse leave on thinking message Grid to hide controls.
        /// </summary>
        private void ThinkingMessageGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not Grid grid)
                return;

            try
            {
                var copyAllButton = grid.FindName("ThinkingCopyAllButton") as Button;
                if (copyAllButton != null && copyAllButton.Visibility != Visibility.Collapsed)
                    copyAllButton.Visibility = Visibility.Hidden;

                var dropdown = grid.FindName("ThinkingCodeActionDropdown") as ComboBox;
                if (dropdown != null && dropdown.Visibility != Visibility.Collapsed)
                    dropdown.Visibility = Visibility.Hidden;

                var deleteButton = grid.FindName("ThinkingDeleteButton") as Button;
                if (deleteButton != null && deleteButton.Visibility != Visibility.Collapsed)
                    deleteButton.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                LoggerService.Current.WriteError("[thinking-controls-hover] Error on mouse leave", ex);
            }
        }
    }
}
