using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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

        private ModelInfo? _selectedModel;
        private SettingsViewModel? _settingsViewModel;

        public ObservableCollection<ModelInfo> AvailableModels { get; }
        public ObservableCollection<ToolDefinition> AvailableTools { get; }
        public ObservableCollection<ProfileInfo> Profiles { get; }

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

        public RelayCommand AddModelCommand { get; }
        public RelayCommand RemoveModelCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand EditConfigCommand { get; }
        public RelayCommand ReindexCommand { get; }
        public RelayCommand<ToolDefinition> ToggleToolCommand { get; }

        public ConfigPageViewModel(
            IConfigService configService,
            IIndexingService indexingService,
            IIdeService ideService)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (indexingService == null) throw new ArgumentNullException(nameof(indexingService));
            if (ideService == null) throw new ArgumentNullException(nameof(ideService));

            Debug.WriteLine("[gap8_1-configvm-ctor-start] ConfigPageViewModel CONSTRUCTOR CALLED");

            _configService = configService;
            _indexingService = indexingService;
            _ideService = ideService;

            AvailableModels = new ObservableCollection<ModelInfo>();
            AvailableTools = new ObservableCollection<ToolDefinition>();
            Profiles = new ObservableCollection<ProfileInfo>();

            Debug.WriteLine("[gap8_1-configvm-ctor-cmds] Initializing commands");
            AddModelCommand = new RelayCommand(ExecuteAddModel);
            RemoveModelCommand = new RelayCommand(ExecuteRemoveModel);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            EditConfigCommand = new RelayCommand(ExecuteEditConfig);
            ReindexCommand = new RelayCommand(ExecuteReindex);
            ToggleToolCommand = new RelayCommand<ToolDefinition>(ExecuteToggleTool);

            Debug.WriteLine("[gap8_1-configvm-ctor-load] Calling LoadConfiguration()");
            LoadConfiguration();

            Debug.WriteLine("[gap8_1-configvm-ctor-settings] Creating SettingsViewModel");
            _settingsViewModel = new SettingsViewModel(_configService);
            _settingsViewModel.LoadSettings();
            RaisePropertyChanged(nameof(SettingsViewModel));

            Debug.WriteLine("[gap8_1-configvm-ctor-end] ConfigPageViewModel CONSTRUCTOR COMPLETE");
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

#pragma warning disable VSTHRD100
        private async void ExecuteAddModel()
#pragma warning restore VSTHRD100
        {
            try
            {
                Debug.WriteLine("[gap8_4-configvm-addmodel-start] ExecuteAddModel called");

                // This would normally show a dialog, but in this MVP we delegate to service
                // In a full UI, this would instantiate AddModelDialog and show modally
                // For now, just log that the command was invoked
                Debug.WriteLine("[gap8_4-configvm-addmodel-complete] AddModel command would show dialog");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_4-configvm-addmodel-error] Error in ExecuteAddModel: {ex.Message}");
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
    }
}
