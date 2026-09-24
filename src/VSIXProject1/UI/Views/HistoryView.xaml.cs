using System;
using System.Windows.Controls;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Views
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml (gap76, gap91).
    /// Displays list of available sessions sorted by last modified.
    /// Clicking a session loads it into the chat view. Hovering a row reveals a
    /// delete (✕) button; right-clicking opens a menu with Delete and Export (gap91).
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

        /// <summary>
        /// Handles right-click "Delete session" menu item (gap91).
        /// The session ID is carried on the MenuItem's Tag via PlacementTarget binding.
        /// </summary>
        private void OnDeleteSessionClick(object sender, System.Windows.RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var sessionId = menuItem?.Tag as string;
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            var viewModel = this.DataContext as ChatPageViewModel;
            viewModel?.DeleteSessionCommand?.Execute(sessionId);
        }

        /// <summary>
        /// Handles right-click "Export session" menu item (gap91).
        /// The session ID is carried on the MenuItem's Tag via PlacementTarget binding.
        /// </summary>
        private void OnExportSessionClick(object sender, System.Windows.RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            var sessionId = menuItem?.Tag as string;
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            var viewModel = this.DataContext as ChatPageViewModel;
            viewModel?.ExportSessionCommand?.Execute(sessionId);
        }
    }
}
