using System;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using ContinueVS.Services;
using ContinueVS.ViewModels;

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
                _ = LoggerService.Current.WriteErrorAsync($"[SettingsControl-ctor] Error: {ex.Message}", ex);
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
