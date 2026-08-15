using System;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.UI.Controls;
using ContinueVS.UI.Navigation;
using ContinueVS.ViewModels;

namespace ContinueVS.UI
{
    /// <summary>
    /// WPF UserControl hosting the Continue tool window UI with WPF pages.
    /// Serves as the main container for ChatPage, ConfigPage, and other UI pages.
    /// </summary>
    public partial class ContinueToolWindowControl : UserControl, IDisposable
    {
        private bool _disposed;
        private MainViewModel? _mainViewModel;
        private IPageNavigator? _pageNavigator;

        public ContinueToolWindowControl()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[g7-ctrl-b1] ContinueToolWindowControl constructor");

                // Ensure ViewModelLocator.ServiceProvider is set before XAML initializes
                // (XAML bindings in pages may depend on MainViewModel resolution via ViewModelLocator)
                if (ContinueVSPackage.ServiceProvider != null && ViewModelLocator.ServiceProvider == null)
                {
                    try
                    {
                        ViewModelLocator.ServiceProvider = ContinueVSPackage.ServiceProvider;
                        System.Diagnostics.Debug.WriteLine("[g7-ctrl-b2] ServiceProvider set in ViewModelLocator");
                    }
                    catch (ArgumentNullException)
                    {
                        // ServiceProvider already set by InitializeAsync; ignore duplicate assignment
                    }
                }

                InitializeComponent();
                Loaded += OnLoaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b3] Initialization error: {ex.Message}");
                MessageBox.Show($"Error initializing Continue tool window: {ex.Message}", "Initialization Error");
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[g7-ctrl-b4] OnLoaded handler invoked");

            try
            {
                var serviceProvider = ViewModelLocator.ServiceProvider;
                if (serviceProvider == null)
                {
                    System.Diagnostics.Debug.WriteLine("[g7-ctrl-b5] ServiceProvider is null, cannot resolve services");
                    return;
                }

                _mainViewModel = serviceProvider.GetService(typeof(MainViewModel)) as MainViewModel;
                _pageNavigator = serviceProvider.GetService(typeof(IPageNavigator)) as IPageNavigator;

                System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b6] MainViewModel resolved: {_mainViewModel != null}, PageNavigator resolved: {_pageNavigator != null}");

                if (_mainViewModel != null)
                {
                    this.DataContext = _mainViewModel;
                    System.Diagnostics.Debug.WriteLine("[g7-ctrl-b7] DataContext set for control");

                    // Also explicitly set NavigationBar's DataContext
                    if (this.FindName("NavigationBarControl") is NavigationBar navBar)
                    {
                        navBar.DataContext = _mainViewModel;
                        System.Diagnostics.Debug.WriteLine("[g7-ctrl-b7b] NavigationBar DataContext set");
                    }

                    _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
                    System.Diagnostics.Debug.WriteLine("[g7-ctrl-b8] PropertyChanged handler subscribed");

                    if (!string.IsNullOrEmpty(_mainViewModel.CurrentRoute))
                    {
                        System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b9] InitialRoute: {_mainViewModel.CurrentRoute}");
                        NavigateToRoute(_mainViewModel.CurrentRoute);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[g7-ctrl-b10] CurrentRoute null, navigating to 'chat'");
                        NavigateToRoute("chat");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[g7-ctrl-b11] MainViewModel resolution failed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b12] OnLoaded error: {ex}");
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentRoute) && _mainViewModel != null)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b13] CurrentRoute changed to: {_mainViewModel.CurrentRoute}");
                NavigateToRoute(_mainViewModel.CurrentRoute);
            }
        }

        private void NavigateToRoute(string? route)
        {
            if (_pageNavigator != null && !string.IsNullOrEmpty(route))
            {
                System.Diagnostics.Debug.WriteLine($"[g7-ctrl-b14] NavigatingToRoute: {route}");
                _ = _pageNavigator.NavigateAsync(route, MainContentFrame);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_mainViewModel != null)
                {
                    _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
                }
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }

        ~ContinueToolWindowControl()
        {
            Dispose();
        }
    }
}



