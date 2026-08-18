using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    public class ConfigPageViewModel : ViewModelBase
    {
        private readonly IConfigService _configService;
        private readonly IIndexingService _indexingService;
        private readonly IIdeService _ideService;
        private readonly IModelDiscoveryService _modelDiscoveryService;

        private ModelInfo? _selectedModel;
        private SettingsViewModel? _settingsViewModel;
        private string? _searchText;
        private int? _editingContextWindow;
        private ObservableCollection<ModelInfo> _filteredModels;
        private AddModelViewModel? _addModelViewModel;

        private const int DefaultContextWindow = 131072; // 2^17

        public ObservableCollection<ModelInfo> AvailableModels { get; }
        public ObservableCollection<ToolDefinition> AvailableTools { get; }
        public ObservableCollection<ProfileInfo> Profiles { get; }
        public ObservableCollection<ModelInfo> FilteredModels => _filteredModels;

        public ModelInfo? SelectedModel
        {
            get => _selectedModel;
            set => Set(ref _selectedModel, value);
        }

        public SettingsViewModel? SettingsViewModel
        {
            get => _settingsViewModel;
            set => Set(ref _settingsViewModel, value);
        }

        public AddModelViewModel? AddModelViewModel
        {
            get => _addModelViewModel;
            set => Set(ref _addModelViewModel, value);
        }

        public string? SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    UpdateFilteredModels();
                }
            }
        }

        public int? EditingContextWindow
        {
            get => _editingContextWindow;
            set => Set(ref _editingContextWindow, value);
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => Set(ref _selectedTabIndex, value);
        }

        public Visibility FetchFromProviderButtonVisibility => Visibility.Collapsed;

        public RelayCommand AddModelCommand { get; }
        public RelayCommand RemoveModelCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand EditConfigCommand { get; }
        public RelayCommand ReindexCommand { get; }
        public RelayCommand<ToolDefinition> ToggleToolCommand { get; }
        public RelayCommand UpdateContextWindowCommand { get; }

        public ConfigPageViewModel(
            IConfigService configService,
            IIndexingService indexingService,
            IIdeService ideService,
            IModelDiscoveryService modelDiscoveryService)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (indexingService == null) throw new ArgumentNullException(nameof(indexingService));
            if (ideService == null) throw new ArgumentNullException(nameof(ideService));
            if (modelDiscoveryService == null) throw new ArgumentNullException(nameof(modelDiscoveryService));

            Debug.WriteLine("[gap8_1-configvm-ctor-start] ConfigPageViewModel CONSTRUCTOR CALLED");

            _configService = configService;
            _indexingService = indexingService;
            _ideService = ideService;
            _modelDiscoveryService = modelDiscoveryService;

            AvailableModels = new ObservableCollection<ModelInfo>();
            AvailableTools = new ObservableCollection<ToolDefinition>();
            Profiles = new ObservableCollection<ProfileInfo>();
            _filteredModels = new ObservableCollection<ModelInfo>();

            Debug.WriteLine("[gap8_1-configvm-ctor-cmds] Initializing commands");
            AddModelCommand = new RelayCommand(ExecuteAddModel);
            RemoveModelCommand = new RelayCommand(ExecuteRemoveModel);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            EditConfigCommand = new RelayCommand(ExecuteEditConfig);
            ReindexCommand = new RelayCommand(ExecuteReindex);
            ToggleToolCommand = new RelayCommand<ToolDefinition>(ExecuteToggleTool);
            UpdateContextWindowCommand = new RelayCommand(ExecuteUpdateContextWindow);

            Debug.WriteLine("[gap8_1-configvm-ctor-load] Calling LoadConfiguration()");
            LoadConfiguration();

            Debug.WriteLine("[gap8_1-configvm-ctor-settings] Creating SettingsViewModel");
            _settingsViewModel = new SettingsViewModel(_configService);
            _settingsViewModel.LoadSettings();
            RaisePropertyChanged(nameof(SettingsViewModel));

            // Subscribe to config changes to refresh filtered models
            _configService.ConfigChanged += (s, e) =>
            {
                Debug.WriteLine("[gap12_1-configvm] ConfigChanged event received, refreshing filtered models");
                LoadConfiguration();
                UpdateFilteredModels();
            };

            Debug.WriteLine("[gap8_1-configvm-ctor-end] ConfigPageViewModel CONSTRUCTOR COMPLETE");
        }

        private void UpdateFilteredModels()
        {
            Debug.WriteLine($"[gap12_1-configvm-filter] UpdateFilteredModels called with SearchText='{SearchText}'");
            _filteredModels.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // No filter: show all models
                foreach (var model in AvailableModels)
                {
                    _filteredModels.Add(model);
                }
                Debug.WriteLine($"[gap12_1-configvm-filter-all] Showing all {_filteredModels.Count} models");
            }
            else
            {
                // Filter: case-insensitive substring match on Name or Provider
                var searchLower = SearchText?.ToLower() ?? string.Empty;
                foreach (var model in AvailableModels)
                {
                    if ((model.Name?.ToLower().Contains(searchLower) ?? false) ||
                        (model.Provider?.ToLower().Contains(searchLower) ?? false))
                    {
                        _filteredModels.Add(model);
                    }
                }
                Debug.WriteLine($"[gap12_1-configvm-filter-results] Found {_filteredModels.Count} models matching '{SearchText}'");
            }

            RaisePropertyChanged(nameof(FilteredModels));
        }

        private void LoadConfiguration()
        {
            try
            {
                Debug.WriteLine("[gap8_1-configvm-load-start] LoadConfiguration called");
                var config = _configService.GetCurrentConfig();
                Debug.WriteLine($"[gap8_1-configvm-load-config] GetCurrentConfig returned: {(config == null ? "NULL" : "OK")}");

                AvailableModels.Clear();
                if (config?.Models != null)
                {
                    Debug.WriteLine($"[gap8_1-configvm-load-models-detail] Models count from config: {config.Models.Count}");
                    foreach (var model in config.Models)
                    {
                        AvailableModels.Add(model);
                    }
                }
                Debug.WriteLine($"[gap8_1-configvm-models] Loaded {AvailableModels.Count} models into ObservableCollection");

                AvailableTools.Clear();
                Debug.WriteLine("[gap8_1-configvm-load-tools-start] About to call GetEnabledTools");
                var enabledTools = _configService.GetEnabledTools();
                Debug.WriteLine($"[gap8_1-configvm-load-tools-result] GetEnabledTools returned: {(enabledTools == null ? "NULL" : $"COUNT={enabledTools.Count()}")}");

                if (enabledTools != null)
                {
                    foreach (var tool in enabledTools)
                    {
                        Debug.WriteLine($"[gap8_1-configvm-adding-tool] Adding tool: {tool.Name} (enabled={tool.IsEnabled})");
                        AvailableTools.Add(tool);
                    }
                }
                Debug.WriteLine($"[gap8_1-configvm-tools] Loaded {AvailableTools.Count} enabled tools into ObservableCollection");

                var selectedModel = _configService.GetSelectedModel();
                if (selectedModel != null)
                {
                    SelectedModel = selectedModel;
                    Debug.WriteLine($"[gap8_1-configvm-selected-model] Selected model set: {selectedModel.Name}");
                }

                Debug.WriteLine("[gap8_1-configvm-load-end] LoadConfiguration complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_1-configvm-error] LoadConfiguration error: {ex.Message}");
                Debug.WriteLine($"[gap8_1-configvm-error-stack] {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Refreshes the available tools collection. Should be called when the Config page becomes visible.
        /// This allows tools to load asynchronously and still display in the UI.
        /// </summary>
        public void RefreshAvailableTools()
        {
            try
            {
                Debug.WriteLine("[gap8_1-configvm-refresh-start] RefreshAvailableTools called");

                AvailableTools.Clear();
                var enabledTools = _configService.GetEnabledTools();
                if (enabledTools != null)
                {
                    foreach (var tool in enabledTools)
                    {
                        AvailableTools.Add(tool);
                    }
                }

                Debug.WriteLine($"[gap8_1-configvm-refresh-end] Refreshed {AvailableTools.Count} tools");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_1-configvm-refresh-error] RefreshAvailableTools error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles a tool enable/disable toggle. User unchecks/checks a tool.
        /// Immediately saves the configuration and refreshes the UI collection.
        /// This ensures the enabled tool count stays in sync across all layers.
        /// </summary>
        /// <param name="tool">The tool that was toggled.</param>
        private void ExecuteToggleTool(ToolDefinition? tool)
        {
            if (tool == null) return;

            try
            {
                Debug.WriteLine($"[gap8_1-configvm-toggle-start] Tool '{tool.Name}' toggled to IsEnabled={tool.IsEnabled}");

                // Fire-and-forget: persist the change; exceptions are caught inside the lambda
                _ = _configService.SaveConfigAsync().ContinueWith(t =>
                {
                    if (t.Exception != null)
                        Debug.WriteLine($"[gap8_1-configvm-toggle-save-error] SaveConfigAsync failed: {t.Exception.GetBaseException().Message}");
                    else
                        Debug.WriteLine("[gap8_1-configvm-toggle-saved] Config saved with tool state change");
                }, TaskScheduler.Default);

                // Refresh the collection to reflect the new enabled count
                RefreshAvailableTools();

                Debug.WriteLine($"[gap8_1-configvm-toggle-complete] Enabled tool count now: {AvailableTools.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_1-configvm-toggle-error] Error toggling tool '{tool.Name}': {ex.Message}");
            }
        }

private void ExecuteAddModel()
{
    try
    {
        Debug.WriteLine("[gap12_2-configvm-addmodel-start] ExecuteAddModel called");

        // Initialize viewmodel if not already done
        if (_addModelViewModel == null)
        {
            // Callbacks to switch back to Models tab after save or cancel
            _addModelViewModel = new AddModelViewModel(
                _modelDiscoveryService,
                _configService,
                onSaveCompleted: () => SelectedTabIndex = 0,
                onCanceled: () => SelectedTabIndex = 0
            );
            AddModelViewModel = _addModelViewModel;
            Debug.WriteLine("[gap12_2-configvm-addmodel-vm-created] AddModelViewModel instantiated");
        }

        // Reset the viewmodel state for a fresh form
        _addModelViewModel.ResetForm();

        // Switch to the Add Model tab (tab index 3)
        SelectedTabIndex = 3;
        Debug.WriteLine("[gap12_2-configvm-addmodel-complete] Switched to Add Model tab");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[gap12_2-configvm-addmodel-error] Error in ExecuteAddModel: {ex.Message}");
    }
}

#pragma warning disable VSTHRD100
        private async void ExecuteRemoveModel()
#pragma warning restore VSTHRD100
        {
            try
            {
                // Stub: Remove selected model
            }
            catch (Exception)
            {
                // Handle error
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteSaveConfig()
#pragma warning restore VSTHRD100
        {
            try
            {
                // Save settings first
                if (_settingsViewModel != null)
                {
                    Debug.WriteLine("[gap8_1-configvm-save-settings] Saving settings via SettingsViewModel");
                    await _settingsViewModel.SaveSettingsAsync();
                }

                // Save config (includes tools and models)
                await _configService.SaveConfigAsync();
                Debug.WriteLine("[gap8_1-configvm-save-complete] Configuration and settings saved");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_1-configvm-save-error] Error saving config: {ex.Message}");
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteEditConfig()
#pragma warning restore VSTHRD100
        {
            try
            {
                Debug.WriteLine("[gap8_3-configvm-editconfig-start] EditConfig command executed");

                var config = _configService.GetCurrentConfig();
                if (config?.ConfigFilePath == null)
                {
                    Debug.WriteLine("[gap8_3-configvm-editconfig-nopath] Config file path is null");
                    return;
                }

                Debug.WriteLine($"[gap8_3-configvm-editconfig-path] Opening config file: {config.ConfigFilePath}");
                Debug.WriteLine("[gap8_3-configvm-editconfig-calling-ideservice] Calling IIdeService.OpenFileInEditorAsync");

                await _ideService.OpenFileInEditorAsync(config.ConfigFilePath);

                Debug.WriteLine("[gap8_3-configvm-editconfig-complete] Config file opened in editor");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_3-configvm-editconfig-error] Error opening config editor: {ex.Message}");
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteReindex()
#pragma warning restore VSTHRD100
        {
            try
            {
                await _indexingService.RebuildIndexAsync();
            }
            catch (Exception)
            {
                // Handle error
            }
        }

        private void ExecuteUpdateContextWindow()
        {
            try
            {
                if (SelectedModel == null)
                {
                    Debug.WriteLine("[gap12_1-configvm-context-window-nomodel] No model selected");
                    return;
                }

                if (EditingContextWindow == null || EditingContextWindow <= 0)
                {
                    // Use default if invalid
                    EditingContextWindow = DefaultContextWindow;
                    Debug.WriteLine($"[gap12_1-configvm-context-window-default] Setting default context window: {DefaultContextWindow}");
                }

                SelectedModel.ContextWindow = EditingContextWindow.Value;
                Debug.WriteLine($"[gap12_1-configvm-context-window-updated] Model '{SelectedModel.Name}' context window updated to {EditingContextWindow}");

                // Save immediately to config.json (fire-and-forget)
                _ = _configService.SaveConfigAsync().ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Debug.WriteLine($"[gap12_1-configvm-context-window-save-error] SaveConfigAsync failed: {t.Exception.GetBaseException().Message}");
                    }
                    else
                    {
                        Debug.WriteLine("[gap12_1-configvm-context-window-saved] Context window change saved to config.json");
                    }
                }, TaskScheduler.Default);

                // Clear editing state and refresh UI
                EditingContextWindow = null;
                RaisePropertyChanged(nameof(SelectedModel));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap12_1-configvm-context-window-error] Error updating context window: {ex.Message}");
            }
        }
    }
}
