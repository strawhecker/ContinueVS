using System;
using System.Collections.ObjectModel;
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

        private ModelInfo? _selectedModel;

        public ObservableCollection<ModelInfo> AvailableModels { get; }
        public ObservableCollection<ToolDefinition> AvailableTools { get; }
        public ObservableCollection<ProfileInfo> Profiles { get; }

        public ModelInfo? SelectedModel
        {
            get => _selectedModel;
            set => Set(ref _selectedModel, value);
        }

        public RelayCommand AddModelCommand { get; }
        public RelayCommand RemoveModelCommand { get; }
        public RelayCommand SaveConfigCommand { get; }
        public RelayCommand ReindexCommand { get; }

        public ConfigPageViewModel(
            IConfigService configService,
            IIndexingService indexingService)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (indexingService == null) throw new ArgumentNullException(nameof(indexingService));

            _configService = configService;
            _indexingService = indexingService;

            AvailableModels = new ObservableCollection<ModelInfo>();
            AvailableTools = new ObservableCollection<ToolDefinition>();
            Profiles = new ObservableCollection<ProfileInfo>();

            AddModelCommand = new RelayCommand(ExecuteAddModel);
            RemoveModelCommand = new RelayCommand(ExecuteRemoveModel);
            SaveConfigCommand = new RelayCommand(ExecuteSaveConfig);
            ReindexCommand = new RelayCommand(ExecuteReindex);

            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                var config = _configService.GetCurrentConfig();

                AvailableModels.Clear();
                if (config?.Models != null)
                {
                    foreach (var model in config.Models)
                    {
                        AvailableModels.Add(model);
                    }
                }

                AvailableTools.Clear();
                var enabledTools = _configService.GetEnabledTools();
                if (enabledTools != null)
                {
                    foreach (var tool in enabledTools)
                    {
                        AvailableTools.Add(tool);
                    }
                }

                var selectedModel = _configService.GetSelectedModel();
                if (selectedModel != null)
                {
                    SelectedModel = selectedModel;
                }
            }
            catch (Exception)
            {
                // Initialization error - collections remain empty
            }
        }

#pragma warning disable VSTHRD100
        private async void ExecuteAddModel()
#pragma warning restore VSTHRD100
        {
            try
            {
                // Stub: Show dialog to get model info, then add via service
            }
            catch (Exception)
            {
                // Handle error
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
                await _configService.SaveConfigAsync();
            }
            catch (Exception)
            {
                // Handle error
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
