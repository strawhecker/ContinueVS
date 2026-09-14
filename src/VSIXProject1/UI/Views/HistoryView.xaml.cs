using System;
using System.Windows.Controls;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Views
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml (gap76).
    /// Displays list of available sessions sorted by last modified.
    /// Clicking a session loads it into the chat view.
    /// </summary>
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();

            // Wire selection changed event to load session
            this.Loaded += (s, e) =>
            {
                var listBox = this.FindName("SessionsListBox") as ListBox;
                if (listBox != null)
                {
                    listBox.SelectionChanged += SessionsListBox_SelectionChanged;
                }
            };
        }

        /// <summary>
        /// Handles session selection from the list (gap76).
        /// Calls ChatPageViewModel.LoadSessionAsync() with the selected session ID.
        /// </summary>
        private void SessionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null)
                return;

            var selectedSession = listBox.SelectedItem as ContinueVS.Core.Types.SessionMetadata;
            if (selectedSession == null)
                return;

            var viewModel = this.DataContext as ChatPageViewModel;
            if (viewModel == null)
                return;

            // Load the selected session asynchronously
            _ = viewModel.LoadSessionAsync(selectedSession.Id);
        }
    }
}
