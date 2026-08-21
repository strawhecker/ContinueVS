using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Core.Types;
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
                        System.Diagnostics.Debug.WriteLine($"[ChatPage] Theme loaded into Application.Current.Resources from: {themeDictPath}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatPage] Theme file not found at: {themeDictPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPage] Failed to load theme: {ex.Message}");
            }

            InitializeComponent();

            try
            {
                var sp = ViewModelLocator.ServiceProvider;
                if (sp != null)
                {
                    var llm         = sp.GetRequiredService<ILlmService>();
                    var context     = sp.GetRequiredService<IContextService>();
                    var tool        = sp.GetRequiredService<IToolService>();
                    var session     = sp.GetRequiredService<ISessionService>();
                    var notif       = sp.GetRequiredService<INotificationService>();
                    var config      = sp.GetRequiredService<IConfigService>();
                    var systemPrompt = sp.GetRequiredService<ISystemPromptService>();
                    this.DataContext = new ChatPageViewModel(llm, context, tool, session, notif, config, systemPrompt);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatPage] DataContext initialization error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ChatPage] Loaded event error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ChatPage] Unloaded event error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"[ChatPage] Messages_CollectionChanged error: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine($"[ChatPage] Message_PropertyChanged scroll error: {ex.Message}");
                }
            }
        }
    }
}
