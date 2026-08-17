#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace ContinueVS.ViewModels
{
    /// <summary>
    /// ViewModel for the Add Model dialog. Handles provider selection, model discovery, and connection validation.
    /// </summary>
    public class AddModelViewModel : ViewModelBase
    {
        private readonly IModelDiscoveryService _discoveryService;
        private readonly IConfigService _configService;

        private ModelProvider? _selectedProvider;
        private string? _selectedModel;
        private string? _apiKey;
        private string? _baseUrl;
        private bool _isValidating;
        private string? _validationError;
        private int _currentStep;

        public ObservableCollection<ModelProvider> Providers { get; }
        public ObservableCollection<string> AvailableModels { get; }

        public ModelProvider? SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (Set(ref _selectedProvider, value))
                {
                    LoadModelsForProvider();
                    CurrentStep = 2;
                }
            }
        }

        public string? SelectedModel
        {
            get => _selectedModel;
            set => Set(ref _selectedModel, value);
        }

        public string? ApiKey
        {
            get => _apiKey;
            set => Set(ref _apiKey, value);
        }

        public string? BaseUrl
        {
            get => _baseUrl;
            set => Set(ref _baseUrl, value);
        }

        public bool IsValidating
        {
            get => _isValidating;
            set => Set(ref _isValidating, value);
        }

        public string? ValidationError
        {
            get => _validationError;
            set => Set(ref _validationError, value);
        }

        public int CurrentStep
        {
            get => _currentStep;
            set => Set(ref _currentStep, value);
        }

        public RelayCommand AutodetectCommand { get; }
        public RelayCommand ConnectCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public AddModelViewModel(IModelDiscoveryService discoveryService, IConfigService configService)
        {
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            Providers = new ObservableCollection<ModelProvider>();
            AvailableModels = new ObservableCollection<string>();

            AutodetectCommand = new RelayCommand(ExecuteAutodetect);
            ConnectCommand = new RelayCommand(ExecuteConnect);
            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(() => CurrentStep = 0);

            _currentStep = 1;
            InitializeProviders();
        }

        private void InitializeProviders()
        {
            try
            {
                Providers.Clear();
                foreach (var provider in Enum.GetValues(typeof(ModelProvider)).Cast<ModelProvider>())
                {
                    Providers.Add(provider);
                }
                Debug.WriteLine($"[gap8_4-addmodelvm-providers-init] Initialized {Providers.Count} providers");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_4-addmodelvm-providers-error] Error initializing providers: {ex.Message}");
            }
        }

        private void LoadModelsForProvider()
        {
            if (SelectedProvider == null)
                return;

            var provider = SelectedProvider.Value;
            _ = Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine($"[gap8_4-addmodelvm-load-models] Loading models for provider: {provider}");
                    var models = await _discoveryService.DiscoverModelsAsync(provider, ApiKey);

                    // Update UI on main thread using Invoke (synchronous dispatch from background thread)
                    // VSTHRD001 suppressed: Invoke is correct here because we're already on a background thread
                    // from Task.Run and need to synchronously marshal to UI thread for collection update
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
#pragma warning disable VSTHRD001
                        dispatcher.Invoke(() =>
                        {
                            AvailableModels.Clear();
                            foreach (var model in models)
                            {
                                AvailableModels.Add(model);
                            }
                            Debug.WriteLine($"[gap8_4-addmodelvm-loaded] Loaded {AvailableModels.Count} models");
                        });
#pragma warning restore VSTHRD001
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[gap8_4-addmodelvm-load-error] Error loading models: {ex.Message}");
                }
            });
        }

        private void ExecuteAutodetect()
        {
            LoadModelsForProvider();
        }

        private void ExecuteConnect()
        {
            if (string.IsNullOrEmpty(SelectedModel))
            {
                ValidationError = "Please select a model.";
                return;
            }

            IsValidating = true;
            ValidationError = null;

#pragma warning disable VSTHRD100
#pragma warning disable VSTHRD200
            ValidateConnectionAsync();
#pragma warning restore VSTHRD200
#pragma warning restore VSTHRD100
        }

        #pragma warning disable VSTHRD100 // Fire-and-forget from RelayCommand; exception handled internally
        #pragma warning disable VSTHRD200 // Async void required for synchronous RelayCommand.Execute pattern
                private async void ValidateConnectionAsync()
                {
                    try
                    {
                        Debug.WriteLine($"[gap8_4-addmodelvm-validate-start] Validating connection for model: {SelectedModel}");

                        var model = new ModelInfo
                        {
                            Name = SelectedModel,
                            Provider = SelectedProvider?.ToString() ?? string.Empty,
                            ApiKey = ApiKey,
                            BaseUrl = BaseUrl ?? string.Empty,
                            ContextWindow = 4096
                        };

                        var isValid = await _discoveryService.ValidateConnectionAsync(model);

                        if (isValid)
                        {
                            ValidationError = null;
                            CurrentStep = 4;
                            Debug.WriteLine($"[gap8_4-addmodelvm-validate-success] Connection validated");
                        }
                        else
                        {
                            ValidationError = "Connection validation failed. Please check your API key and settings.";
                            Debug.WriteLine($"[gap8_4-addmodelvm-validate-failed] Connection failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        ValidationError = $"Error validating connection: {ex.Message}";
                        Debug.WriteLine($"[gap8_4-addmodelvm-validate-error] {ex.Message}");
                    }
                    finally
                    {
                        IsValidating = false;
                    }
                }
        #pragma warning restore VSTHRD200
        #pragma warning restore VSTHRD100

        private void ExecuteSave()
        {
            try
            {
                Debug.WriteLine($"[gap8_4-addmodelvm-save-start] Saving model: {SelectedModel}");

                var model = new ModelInfo
                {
                    Name = SelectedModel,
                    Provider = SelectedProvider?.ToString() ?? string.Empty,
                    ApiKey = ApiKey,
                    BaseUrl = BaseUrl ?? string.Empty,
                    ContextWindow = 4096,
                    SupportsFunctionCalling = false
                };

                var config = _configService.GetCurrentConfig();
                if (config != null)
                {
                    config.Models.Add(model);
                    _ = SaveModelAsync(config);
                }
            }
            catch (Exception ex)
            {
                ValidationError = $"Error saving model: {ex.Message}";
                Debug.WriteLine($"[gap8_4-addmodelvm-save-error] {ex.Message}");
            }
        }

        private async Task SaveModelAsync(ContinueConfig config)
        {
            try
            {
                await _configService.SaveConfigAsync();
                Debug.WriteLine("[gap8_4-addmodelvm-save-success] Model saved successfully");
                CurrentStep = 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_4-addmodelvm-save-failed] Failed to save: {ex.Message}");
                ValidationError = $"Failed to save model: {ex.Message}";
            }
        }
    }
}
