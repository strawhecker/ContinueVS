#nullable enable

using System.Windows;
using System.Windows.Controls;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Views
{
    /// <summary>
    /// AddModelDialog.xaml code-behind.
    /// Non-modal UserControl for adding models via provider catalog and discovery.
    /// </summary>
    public partial class AddModelDialog : UserControl
    {
        public AddModelDialog()
        {
            InitializeComponent();
        }

        public void Initialize(AddModelViewModel viewModel)
        {
            DataContext = viewModel;
            Visibility = Visibility.Visible;
        }

        public void Close()
        {
            Visibility = Visibility.Collapsed;
            DataContext = null;
        }
    }
}
