using System;
using System.Windows.Controls;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Pages
{
    public partial class ChatPage : UserControl
    {
        public ChatPage()
        {
            try
            {
                if (ViewModelLocator.ServiceProvider != null)
                {
                    this.DataContext = new ViewModelLocator().ChatPageViewModel;
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
