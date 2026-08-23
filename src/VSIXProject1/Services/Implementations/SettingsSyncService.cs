using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using ContinueVS.Services.Interfaces;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service that monitors configuration file changes and synchronizes settings across windows.
    /// Uses FileSystemWatcher to detect config.json changes and raises PropertyChanged events
    /// to notify subscribers (e.g., SettingsViewModel) of font size and other setting updates.
    /// 
    /// This enables cross-window synchronization: when one window changes font size on disk,
    /// other windows pick up the change via file watcher notifications.
    /// </summary>
    public class SettingsSyncService : INotifyPropertyChanged, IDisposable
    {
        private readonly IConfigService _configService;
        private FileSystemWatcher? _watcher;
        private int _cachedFontSize;
        private bool _disposed = false;
        private readonly object _lock = new object();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the current cached font size.
        /// </summary>
        public int FontSize
        {
            get => _cachedFontSize;
            private set
            {
                if (_cachedFontSize != value)
                {
                    _cachedFontSize = value;
                    OnPropertyChanged(nameof(FontSize));
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of SettingsSyncService.
        /// </summary>
        /// <param name="configService">The configuration service to read settings from.</param>
        public SettingsSyncService(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _cachedFontSize = GetCurrentFontSize();
            InitializeWatcher();
        }

        /// <summary>
        /// Initializes the FileSystemWatcher to monitor config.json changes.
        /// </summary>
        private void InitializeWatcher()
        {
            try
            {
                var configDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".continueVS");

                if (!Directory.Exists(configDir))
                {
                    Debug.WriteLine("[SettingsSyncService] Config directory does not exist yet");
                    return;
                }

                _watcher = new FileSystemWatcher(configDir, "continueVS.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnConfigFileChanged;

                Debug.WriteLine("[SettingsSyncService] FileSystemWatcher initialized for config.json");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSyncService] Error initializing FileSystemWatcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current font size from ConfigService.
        /// </summary>
        private int GetCurrentFontSize()
        {
            try
            {
                var config = _configService.GetCurrentConfig();
                if (config?.CustomSettings != null &&
                    config.CustomSettings.TryGetValue(CoreTypes.UserSettings.Appearance_FontSize, out var value))
                {
                    if (value is int intValue)
                        return intValue;

                    if (int.TryParse(value?.ToString(), out int parsed))
                        return parsed;
                }

                var defaults = CoreTypes.UserSettings.GetDefaults();
                if (defaults.TryGetValue(CoreTypes.UserSettings.Appearance_FontSize, out var defaultValue) &&
                    defaultValue is int defaultInt)
                    return defaultInt;

                return 14; // Fallback default
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSyncService] Error getting current font size: {ex.Message}");
                return 14;
            }
        }

        /// <summary>
        /// Handles FileSystemWatcher events when config.json changes on disk.
        /// </summary>
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                lock (_lock)
                {
                    if (_disposed)
                        return;

                    // Small delay to ensure file is fully written
                    System.Threading.Thread.Sleep(50);

                    var newFontSize = GetCurrentFontSize();
                    FontSize = newFontSize;

                    Debug.WriteLine($"[SettingsSyncService] Config changed, FontSize updated to {newFontSize}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsSyncService] Error handling config file change: {ex.Message}");
            }
        }

        /// <summary>
        /// Raises PropertyChanged event for the specified property.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Disposes the FileSystemWatcher and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_lock)
                {
                    _watcher?.Dispose();
                    _watcher = null;
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Destructor ensures FileSystemWatcher is cleaned up.
        /// </summary>
        ~SettingsSyncService()
        {
            Dispose(false);
        }
    }
}
