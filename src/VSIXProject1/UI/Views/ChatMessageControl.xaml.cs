using System.Windows.Controls;
using System.Windows;
using ContinueVS.Core.Types;

namespace ContinueVS.UI.Views
{
    public partial class ChatMessageControl : UserControl
    {
        public ChatMessageControl()
        {
            InitializeComponent();
            this.Loaded += ChatMessageControl_Loaded;
        }

        private void ChatMessageControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            MessageGrid.MouseEnter += MessageGrid_MouseEnter;
            MessageGrid.MouseLeave += MessageGrid_MouseLeave;
            CopyAllButton.Click += CopyAllButton_Click;
        }

        private void MessageGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Visible;
            if (CopyAllButton.Visibility != System.Windows.Visibility.Collapsed)
                CopyAllButton.Visibility = System.Windows.Visibility.Visible;
        }

        private void MessageGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Hidden;
            if (CopyAllButton.Visibility != System.Windows.Visibility.Collapsed)
                CopyAllButton.Visibility = System.Windows.Visibility.Hidden;
        }

        private void CopyAllButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                var content = (DataContext as ChatMessage)?.Content ?? string.Empty;
                Clipboard.SetText(content);
            }
            catch { }
        }
    }
}
