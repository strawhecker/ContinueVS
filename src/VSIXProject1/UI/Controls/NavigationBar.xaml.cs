using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;

#nullable enable

namespace ContinueVS.UI.Controls
{
    public partial class NavigationBar : UserControl
    {
        private readonly IConfigService? _configService;
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
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[g7-nav-b1] NavigationBar() parameterless constructor called");
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
            RefreshToolCount();
        }

        private void RefreshToolCount()
        {
            try
            {
                var count = _configService?.GetEnabledTools()?.Count() ?? 0;
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
