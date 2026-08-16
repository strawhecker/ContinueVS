using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace ContinueVS.UI.Controls
{
    public partial class NavigationBar : UserControl, INotifyPropertyChanged
    {
        private readonly IConfigService? _configService;   // set via overloaded ctor (legacy)
        private IConfigService? _configServiceLive;        // set at runtime via Loaded event
        private int _toolCount;

        public int ToolCount
        {
            get => _toolCount;
            set
            {
                if (_toolCount != value)
                {
                    _toolCount = value;
                    System.Diagnostics.Debug.WriteLine($"[g7-nav-b1] ToolCount changed: {value}");
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolCount)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public NavigationBar()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b1-err] InitializeComponent failed: {ex.Message}");
                // Continue without XAML initialization; this can happen if the XAML file isn't in the project
            }
            System.Diagnostics.Debug.WriteLine("[g7-nav-b1] NavigationBar() parameterless constructor called");

            // Wire up service from DI after XAML init
            Loaded += NavigationBar_Loaded;
        }

        private void NavigationBar_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[g7-nav-b1-loaded] NavigationBar Loaded event fired");
            try
            {
                var sp = ViewModelLocator.ServiceProvider;
                if (sp == null)
                {
                    System.Diagnostics.Debug.WriteLine("[g7-nav-b1-loaded] ViewModelLocator.ServiceProvider is null, ToolCount stays 0");
                    return;
                }

                var configSvc = sp.GetService(typeof(IConfigService)) as IConfigService;
                if (configSvc == null)
                {
                    System.Diagnostics.Debug.WriteLine("[g7-nav-b1-loaded] IConfigService not found in DI, ToolCount stays 0");
                    return;
                }

                // Unsubscribe old handler if already wired (e.g. re-load)
                if (_configService != null)
                    _configService.ConfigChanged -= OnConfigChanged;

                // Update field and wire event - using reflection trick via property isn't needed,
                // just reassign the backing field directly since this is the same class
                _configServiceLive = configSvc;
                configSvc.ConfigChanged += OnConfigChanged;

                RefreshToolCount(configSvc);
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b1-loaded] ToolCount initialized to {ToolCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b1-loaded-err] Error: {ex.Message}");
            }
        }

        public NavigationBar(IConfigService configService) : this()
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            System.Diagnostics.Debug.WriteLine("[g7-nav-b2] NavigationBar(IConfigService) constructor called");
            RefreshToolCount();
            _configService.ConfigChanged += OnConfigChanged;
        }

        private void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[g7-nav-b3] ConfigChanged event received, refreshing tool count");
            RefreshToolCount(_configServiceLive ?? _configService);
        }

        private void RefreshToolCount(IConfigService? svc = null)
        {
            try
            {
                var effective = svc ?? _configServiceLive ?? _configService;
                var count = effective?.GetEnabledTools()?.Count() ?? 0;
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b4] RefreshToolCount: {count} tools enabled");
                ToolCount = count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[g7-nav-b5] RefreshToolCount error: {ex.Message}");
                ToolCount = 0;
            }
        }
    }
}
