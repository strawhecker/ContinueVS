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
                    this.DataContext = new ChatPageViewModel(llm, context, tool, session, notif, config, systemPrompt, uiState, instructionExecutor, changeStackService, markdownService, llmQuestionService, null, workflow, ideService, null, planOutput);
                    LoggerService.Current.WriteDebug("[sv-chatpage-dc] ✓ ChatPageViewModel constructed and DataContext assigned");
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
                }

                if (_messagesScrollViewer != null)
                {
                    // Auto-scroll to bottom when new messages are added
                    _messagesScrollViewer.ScrollToEnd();
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

            // Find the answer TextBox in the visual tree
            var grid = btn.Parent as Panel;
            var border = grid?.Parent as Border;
            var stackPanel = border?.Child as StackPanel;

            TextBox? answerTextBox = null;
            if (stackPanel != null)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is TextBox tb && tb.Name == "AnswerInput")
                    {
                        answerTextBox = tb;
                        break;
                    }
                }
            }

            if (answerTextBox == null || string.IsNullOrWhiteSpace(answerTextBox.Text))
            {
                LoggerService.Current.WriteDebug("[gap54-question] No answer provided");
                return;
            }

            var answer = answerTextBox.Text;
            LoggerService.Current.WriteDebug($"[gap54-question] Answer provided: {answer}");

            // Fire the OnAnswerAsync callback
            _ = question.OnAnswerAsync?.Invoke(answer);
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
