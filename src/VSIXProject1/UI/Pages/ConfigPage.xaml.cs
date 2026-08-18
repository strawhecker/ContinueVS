using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.UI.Pages
{
    /// <summary>
    /// ConfigPage.xaml - UI for managing tool configuration, models, and settings.
    /// </summary>
    public partial class ConfigPage : UserControl
    {
        private ConfigPageViewModel? _viewModel;

        public ConfigPage()
        {
            Debug.WriteLine("[gap12_1-configpage-ctor] ConfigPage CONSTRUCTOR CALLED");

            try
            {
                InitializeComponent();
                Debug.WriteLine("[gap12_1-configpage-ctor-init] InitializeComponent completed");

                Debug.WriteLine("[gap12_1-configpage-ctor-getsp] Getting ServiceProvider from ViewModelLocator");
                var sp = ViewModelLocator.ServiceProvider;

                if (sp != null)
                {
                    Debug.WriteLine("[gap12_1-configpage-ctor-services] ServiceProvider is not null. Getting services...");
                    var config = sp.GetRequiredService<IConfigService>();
                    Debug.WriteLine("[gap12_1-configpage-ctor-config-ok] ✓ IConfigService obtained");

                    var indexing = sp.GetRequiredService<IIndexingService>();
                    Debug.WriteLine("[gap12_1-configpage-ctor-indexing-ok] ✓ IIndexingService obtained");

                    var ideService = sp.GetRequiredService<IIdeService>();
                    Debug.WriteLine("[gap12_1-configpage-ctor-ideservice-ok] ✓ IIdeService obtained");

                    Debug.WriteLine("[gap12_1-configpage-ctor-creating-vm] Creating ConfigPageViewModel...");
                    _viewModel = new ConfigPageViewModel(config, indexing, ideService);
                    Debug.WriteLine("[gap12_1-configpage-ctor-vm-created] ✓ ConfigPageViewModel created");

                    Debug.WriteLine("[gap12_1-configpage-ctor-setting-dc] Setting DataContext");
                    this.DataContext = _viewModel;
                    Debug.WriteLine("[gap12_1-configpage-ctor-dc-ok] ✓ DataContext initialized");
                }
                else
                {
                    Debug.WriteLine("[gap12_1-configpage-ctor-sp-null] ✗ ServiceProvider is NULL - defer to Loaded event!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap12_1-configpage-ctor-error] ✗ DataContext initialization error: {ex.Message}");
                Debug.WriteLine($"[gap12_1-configpage-ctor-stack] {ex.StackTrace}");
                // Don't crash; allow Loaded event to try again
            }

            Debug.WriteLine("[gap12_1-configpage-ctor-end] ConfigPage CONSTRUCTOR COMPLETE");
        }

        /// <summary>
        /// Called when the page is loaded and visible.
        /// Refresh available tools to ensure UI reflects current state.
        /// </summary>
        private void ConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[gap12_1-configpage-loaded] ConfigPage LOADED event fired");

            try
            {
                // If DataContext wasn't set in constructor (ServiceProvider was null), try now
                if (this.DataContext == null)
                {
                    Debug.WriteLine("[gap12_1-configpage-loaded-dc-null] DataContext is null, attempting deferred initialization");

                    var sp = ViewModelLocator.ServiceProvider;
                    if (sp != null)
                    {
                        Debug.WriteLine("[gap12_1-configpage-loaded-sp-ok] ServiceProvider now available");
                        var config = sp.GetRequiredService<IConfigService>();
                        var indexing = sp.GetRequiredService<IIndexingService>();
                        var ideService = sp.GetRequiredService<IIdeService>();

                        _viewModel = new ConfigPageViewModel(config, indexing, ideService);
                        this.DataContext = _viewModel;
                        Debug.WriteLine("[gap12_1-configpage-loaded-deferred-ok] ✓ DataContext deferred initialization successful");
                    }
                    else
                    {
                        Debug.WriteLine("[gap12_1-configpage-loaded-sp-still-null] ✗ ServiceProvider STILL null in Loaded event!");
                        MessageBox.Show("Critical error: ServiceProvider not initialized. Config page cannot load.", "Fatal Error");
                        return;
                    }
                }

                if (_viewModel != null)
                {
                    Debug.WriteLine($"[gap12_1-configpage-loaded-vm-ok] ViewModel exists. Current AvailableTools count: {_viewModel.AvailableTools.Count}");
                    Debug.WriteLine("[gap12_1-configpage-loaded-refresh] Calling RefreshAvailableTools()");
                    _viewModel.RefreshAvailableTools();
                    Debug.WriteLine($"[gap12_1-configpage-loaded-refresh-end] ✓ RefreshAvailableTools complete. Tool count now: {_viewModel.AvailableTools.Count}");

                    // Wire up SettingsControl with SettingsViewModel
                    if (_viewModel.SettingsViewModel != null)
                    {
                        Debug.WriteLine("[gap12_1-configpage-loaded-settings] Wiring SettingsControl with SettingsViewModel");
                        var settingsControl = this.FindName("SettingsControlHost") as SettingsControl;
                        if (settingsControl != null)
                        {
                            settingsControl.SetViewModel(_viewModel.SettingsViewModel);
                            Debug.WriteLine("[gap12_1-configpage-loaded-settings-ok] ✓ SettingsControl wired successfully");
                        }
                        else
                        {
                            Debug.WriteLine("[gap12_1-configpage-loaded-settings-not-found] ✗ SettingsControl not found in XAML");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[gap12_1-configpage-loaded-settings-vm-null] ✗ SettingsViewModel is null");
                    }
                }
                else
                {
                    Debug.WriteLine("[gap12_1-configpage-loaded-vm-null] ✗ ViewModel is null in Loaded event");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap12_1-configpage-loaded-error] ✗ Loaded event error: {ex.Message}");
                Debug.WriteLine($"[gap12_1-configpage-loaded-stack] {ex.StackTrace}");
            }
        }
    }
}
