using System.Windows.Controls;

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
        }

        private void MessageGrid_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Visible;
        }

        private void MessageGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = System.Windows.Visibility.Hidden;
        }
    }
}
