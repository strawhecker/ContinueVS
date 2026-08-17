using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Pages
{
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
