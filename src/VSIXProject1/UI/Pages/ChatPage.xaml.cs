using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
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

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage msg)
            {
                return msg.Role switch
                {
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
                        _ = LoggerService.Current.WriteDebugAsync($"[ChatPage] Theme loaded into Application.Current.Resources from: {themeDictPath}");
                    }
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[ChatPage] Theme file not found at: {themeDictPath}");
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[ChatPage] Failed to load theme: {ex.Message}", ex);
            }

            InitializeComponent();

            try
            {
                _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage] ChatPage constructor: resolving services from DI");
                var sp = ViewModelLocator.ServiceProvider;
                if (sp != null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage] ServiceProvider is available");

                    var llm         = sp.GetRequiredService<ILlmService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-1] ✓ ILlmService resolved");

                    var context     = sp.GetRequiredService<IContextService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-2] ✓ IContextService resolved");

                    var tool        = sp.GetRequiredService<IToolService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-3] ✓ IToolService resolved");

                    var session     = sp.GetRequiredService<ISessionService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-4] ✓ ISessionService resolved");

                    var notif       = sp.GetRequiredService<INotificationService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-5] ✓ INotificationService resolved");

                    var config      = sp.GetRequiredService<IConfigService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-6] ✓ IConfigService resolved");

                    var systemPrompt = sp.GetRequiredService<ISystemPromptService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-7] ✓ ISystemPromptService resolved");

                    var uiState     = sp.GetRequiredService<IUIStateService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-8] ✓ IUIStateService resolved");

                    var instructionExecutor = sp.GetRequiredService<IInstructionExecutorService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-9] ✓ IInstructionExecutorService resolved");

                    var changeStackService = sp.GetRequiredService<IChangeStackService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-9b] ✓ IChangeStackService resolved");

                    var markdownService = sp.GetRequiredService<IMarkdownService>();
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-9c] ✓ IMarkdownService resolved");

                    var workflow    = sp.GetService<IWorkflowService>();
                    _ = LoggerService.Current.WriteDebugAsync($"[sv-chatpage-10] IWorkflowService resolved={workflow != null} (optional)");

                    var ideService  = sp.GetService<IIdeService>();
                    _ = LoggerService.Current.WriteDebugAsync($"[sv-chatpage-11] IIdeService resolved={ideService != null} (optional)");

                    var planOutput  = sp.GetService<IPlanOutputService>();
                    _ = LoggerService.Current.WriteDebugAsync($"[sv-chatpage-12] IPlanOutputService resolved={planOutput != null} (optional)");

                    // BP:sv-chatpage-dc — breakpoint here confirms all services resolved and DataContext is being assigned
                    this.DataContext = new ChatPageViewModel(llm, context, tool, session, notif, config, systemPrompt, uiState, instructionExecutor, changeStackService, markdownService, null, workflow, ideService, null, planOutput);
                    _ = LoggerService.Current.WriteDebugAsync("[sv-chatpage-dc] ✓ ChatPageViewModel constructed and DataContext assigned");
                }
                else
                {
                    _ = LoggerService.Current.WriteErrorAsync("[sv-chatpage-FAIL] ServiceProvider is NULL — ViewModelLocator.ServiceProvider not set. InitializeAsync may not have completed.", null);
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[sv-chatpage-FAIL] ✗ Exception type: {ex.GetType().FullName}", ex);
                _ = LoggerService.Current.WriteErrorAsync($"[sv-chatpage-FAIL] ✗ Message: {ex.Message}", ex);
                _ = LoggerService.Current.WriteErrorAsync($"[sv-chatpage-FAIL] ✗ StackTrace: {ex.StackTrace}", ex);
                if (ex.InnerException != null)
                {
                    _ = LoggerService.Current.WriteErrorAsync($"[sv-chatpage-FAIL] ✗ InnerException type: {ex.InnerException.GetType().FullName}", ex);
                    _ = LoggerService.Current.WriteErrorAsync($"[sv-chatpage-FAIL] ✗ InnerException message: {ex.InnerException.Message}", ex);
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
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[ChatPage] Loaded event error: {ex.Message}", ex);
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
                _ = LoggerService.Current.WriteErrorAsync($"[ChatPage] Unloaded event error: {ex.Message}", ex);
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
                _ = LoggerService.Current.WriteErrorAsync($"[ChatPage] Messages_CollectionChanged error: {ex.Message}", ex);
            }
        }

        private void Message_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // When a message's Content property changes (during streaming), scroll to bottom to show the new text
            if (e.PropertyName == nameof(ChatMessage.Content) && _messagesScrollViewer != null)
            {
                try
                {
                    _messagesScrollViewer.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    _ = LoggerService.Current.WriteErrorAsync($"[ChatPage] Message_PropertyChanged scroll error: {ex.Message}", ex);
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
            _ = LoggerService.Current.WriteDebugAsync($"[gap42-paste] multiline content pasted: {lines} lines, {len} characters");
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
                    _ = LoggerService.Current.WriteDebugAsync("[gap35] Enter key intercepted — firing SendMessageCommand");
                    if (DataContext is ChatPageViewModel vm && vm.SendMessageCommand.CanExecute(null))
                        vm.SendMessageCommand.Execute(null);
                }
            }
        }
    }
}
