using System;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.ViewModels;
using System.Diagnostics;

namespace ContinueVS.UI.Pages
{
    /// <summary>
    /// SettingsControl.xaml - UserControl for managing user preferences (Chat, Appearance, Autocomplete, Experimental).
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        private SettingsViewModel? _viewModel;

        public SettingsControl()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsControl-ctor] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the ViewModel for this control. Called by host (ConfigPageViewModel).
        /// </summary>
        public void SetViewModel(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            this.DataContext = viewModel;
        }

        /// <summary>
        /// Gets the current ViewModel.
        /// </summary>
        public SettingsViewModel? GetViewModel()
        {
            return _viewModel;
        }
    }
}
