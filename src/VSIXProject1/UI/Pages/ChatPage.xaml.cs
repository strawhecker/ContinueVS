using System;
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
        public ChatPage()
        {
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

            InitializeComponent();
        }
    }
}
