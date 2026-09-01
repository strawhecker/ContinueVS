using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
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
                _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b1] ContinueToolWindowControl constructor");

                // Ensure ViewModelLocator.ServiceProvider is set before XAML initializes
                if (ContinueVSPackage.ServiceProvider != null && ViewModelLocator.ServiceProvider == null)
                {
                    try
                    {
                        ViewModelLocator.ServiceProvider = ContinueVSPackage.ServiceProvider;
                        _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b2] ServiceProvider set in ViewModelLocator");
                    }
                    catch (ArgumentNullException)
                    {
                        // ServiceProvider already set; ignore
                    }
                }

                _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b3] Calling InitializeComponent");
                InitializeComponent();
                _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b3b] InitializeComponent completed");

                Loaded += OnLoaded;
                _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b3c] Loaded event subscribed");
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[g7-ctrl-b-err] Constructor exception: {ex.Message}", ex);
                MessageBox.Show($"Error initializing Continue tool window: {ex.Message}", "Initialization Error");
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b4] OnLoaded handler invoked");

            try
            {
                // Initialize theme on load
                _ = InitializeThemeAsync();

                // Use the service provider that was set during package initialization
                var sp = ViewModelLocator.ServiceProvider;
                if (sp == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b5] ViewModelLocator.ServiceProvider is null");
                    return;
                }

                _mainViewModel = sp.GetService(typeof(MainViewModel)) as MainViewModel;
                _pageNavigator = sp.GetService(typeof(IPageNavigator)) as IPageNavigator;
                _ = LoggerService.Current.WriteDebugAsync($"[g7-ctrl-b6] MainViewModel: {_mainViewModel != null}, PageNavigator: {_pageNavigator != null}");

                if (_mainViewModel != null)
                {
                    this.DataContext = _mainViewModel;

                    if (this.FindName("NavigationBarControl") is NavigationBar navBar)
                        navBar.DataContext = _mainViewModel;

                    _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;

                    var route = _mainViewModel.CurrentRoute ?? "chat";
                    _ = LoggerService.Current.WriteDebugAsync($"[g7-ctrl-b9] Navigating to: {route}");
                    NavigateToRoute(route);
                }
                else
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-ctrl-b11] MainViewModel is null — not registered in ServiceBootstrapper");
                }
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[g7-ctrl-b12] OnLoaded error: {ex.Message}", ex);
            }
        }

        private async Task InitializeThemeAsync()
        {
            try
            {
                var sp = ViewModelLocator.ServiceProvider;
                if (sp == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-theme] ServiceProvider is null");
                    return;
                }

                var themeService = sp.GetService(typeof(IThemeService)) as IThemeService;
                if (themeService == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[g7-theme] ThemeService not available");
                    return;
                }

                await themeService.LoadThemeAsync("dark");
                themeService.SetCurrentTheme("dark");

                _ = LoggerService.Current.WriteDebugAsync("[g7-theme] Dark theme applied");

                themeService.ThemeChanged += (s, e) => 
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[g7-theme] Theme changed from {e.PreviousThemeName} to {e.NewThemeName}");
                };
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[g7-theme] Theme initialization error: {ex.Message}", ex);
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentRoute) && _mainViewModel != null)
            {
                _ = LoggerService.Current.WriteDebugAsync($"[g7-ctrl-b13] CurrentRoute changed to: {_mainViewModel.CurrentRoute}");
                NavigateToRoute(_mainViewModel.CurrentRoute);
            }
        }

        private void NavigateToRoute(string? route)
        {
            if (_pageNavigator != null && !string.IsNullOrEmpty(route))
            {
                _ = LoggerService.Current.WriteDebugAsync($"[g7-ctrl-b14] NavigatingToRoute: {route}");
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



