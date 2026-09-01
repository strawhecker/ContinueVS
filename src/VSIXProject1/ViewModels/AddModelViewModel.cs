#nullable enable

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
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
        private Action? _onSaveCompleted;
        private Action? _onCanceled;

        private ProviderMetadata? _selectedProvider;
        private string? _selectedModel;
        private string? _apiKey;
        private string? _baseUrl;
        private string? _contextWindow;
        private bool _isValidating;
        private string? _validationError;
        private int _currentStep;

        public ObservableCollection<ProviderMetadata> Providers { get; }
        public ObservableCollection<string> AvailableModels { get; }

        public ProviderMetadata? SelectedProvider
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
            set
            {
                if (Set(ref _selectedModel, value))
                {
                    // When model is selected, auto-populate context window from catalog
                    if (!string.IsNullOrEmpty(value) && SelectedProvider != null)
                    {
                        if (ModelCatalog.TryGetModel(SelectedProvider.Provider, value ?? string.Empty, out var catalogEntry))
                        {
                            ContextWindow = catalogEntry!.ContextWindow.ToString();
                            _ = LoggerService.Current.WriteDebugAsync($"[gap19-addmodelvm-selected-model-catalog] Auto-populated ContextWindow={ContextWindow} from catalog for {value}");
                        }
                        else
                        {
                            int defaultContextWindow = ModelCatalog.GetDefaultContextWindow(SelectedProvider.Provider);
                            ContextWindow = defaultContextWindow.ToString();
                            _ = LoggerService.Current.WriteDebugAsync($"[gap19-addmodelvm-selected-model-default] Auto-populated ContextWindow={ContextWindow} from provider defaults for {value}");
                        }
                    }
                }
            }
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

        public string? ContextWindow
        {
            get => _contextWindow;
            set => Set(ref _contextWindow, value);
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

        public AddModelViewModel(IModelDiscoveryService discoveryService, IConfigService configService, Action? onSaveCompleted = null, Action? onCanceled = null)
        {
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _onSaveCompleted = onSaveCompleted;
            _onCanceled = onCanceled;

            Providers = new ObservableCollection<ProviderMetadata>();
            AvailableModels = new ObservableCollection<string>();

            AutodetectCommand = new RelayCommand(ExecuteAutodetect);
            ConnectCommand = new RelayCommand(ExecuteConnect);
            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(() =>
            {
                CurrentStep = 0;
                _onCanceled?.Invoke();
            });

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
                    var metadata = global::ContinueVS.Services.ProviderCatalog.GetProviderMetadata(provider);
                    if (metadata != null)
                    {
                        Providers.Add(metadata);
                    }
                }
                _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-providers-init] Initialized {Providers.Count} providers");
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[gap12_3-addmodelvm-providers-error] Error initializing providers: {ex.Message}", ex);
            }
        }

        private void LoadModelsForProvider()
        {
            if (SelectedProvider == null)
                return;

            var provider = SelectedProvider.Provider;
            _ = Task.Run(async () =>
            {
                try
                {
                    _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-load-models] Loading models for provider: {provider}");

                    // First, load default models from the catalog
                    var metadata = SelectedProvider;
                    var models = new List<string>();

                    if (metadata?.DefaultModels != null)
                    {
                        models.AddRange(metadata.DefaultModels);
                        _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-catalog] Loaded {models.Count} default models from catalog");
                    }

                    // If provider supports autodetect and API key is provided, try discovery
                    if (metadata?.SupportsAutodetect == true && !string.IsNullOrEmpty(ApiKey))
                    {
                        try
                        {
                            var discoveredModels = await _discoveryService.DiscoverModelsAsync(provider, ApiKey);
                            if (discoveredModels != null && discoveredModels.Any())
                            {
                                models = discoveredModels.ToList();
                                _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-discovery] Discovered {models.Count} models via API");
                            }
                        }
                        catch (Exception ex)
                        {
                            _ = LoggerService.Current.WriteErrorAsync($"[gap12_3-addmodelvm-discovery-error] Error during discovery: {ex.Message}, using defaults", ex);
                        }
                    }

                    // Update UI on main thread
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
                            _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-loaded] Loaded {AvailableModels.Count} models total");
                        });
#pragma warning restore VSTHRD001
                    }
                    else
                    {
                        // No dispatcher available (e.g., in tests) - update directly
                        AvailableModels.Clear();
                        foreach (var model in models)
                        {
                            AvailableModels.Add(model);
                        }
                        _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-loaded] Loaded {AvailableModels.Count} models total (no dispatcher)");
                    }
                }
                catch (Exception ex)
                {
                    _ = LoggerService.Current.WriteErrorAsync($"[gap12_3-addmodelvm-load-error] Error loading models: {ex.Message}", ex);
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
                        _ = LoggerService.Current.WriteDebugAsync($"[gap8_4-addmodelvm-validate-start] Validating connection for model: {SelectedModel}");

                        var model = new ModelInfo
                        {
                            Name = SelectedModel,
                            Provider = SelectedProvider?.ToString() ?? string.Empty,
                            ApiKey = ApiKey,
                            BaseUrl = BaseUrl ?? string.Empty
                        };

                        // Hydrate model metadata from ModelCatalog; fallback to provider defaults if not found
                        if (SelectedProvider != null && ModelCatalog.TryGetModel(SelectedProvider.Provider, SelectedModel ?? string.Empty, out var catalogEntry))
                        {
                            model.ContextWindow = catalogEntry!.ContextWindow;
                            model.SupportsFunctionCalling = catalogEntry.SupportsFunctionCalling;
                            model.SupportedToolFormats = catalogEntry.SupportedToolFormats ?? new List<string>();
                            _ = LoggerService.Current.WriteDebugAsync($"[gap18-addmodelvm-validate-catalog] Loaded from catalog: ContextWindow={model.ContextWindow}");
                        }
                        else if (SelectedProvider != null)
                        {
                            model.ContextWindow = ModelCatalog.GetDefaultContextWindow(SelectedProvider.Provider);
                            model.SupportsFunctionCalling = ModelCatalog.GetDefaultToolSupport(SelectedProvider.Provider);
                            model.SupportedToolFormats = ModelCatalog.GetDefaultToolFormats(SelectedProvider.Provider);
                            _ = LoggerService.Current.WriteDebugAsync($"[gap18-addmodelvm-validate-fallback] Using defaults: ContextWindow={model.ContextWindow}");
                        }
                        else
                        {
                            model.ContextWindow = 4096;
                            model.SupportsFunctionCalling = false;
                        }

                        var isValid = await _discoveryService.ValidateConnectionAsync(model);

                        if (isValid)
                        {
                            ValidationError = null;
                            CurrentStep = 4;
                            _ = LoggerService.Current.WriteDebugAsync($"[gap8_4-addmodelvm-validate-success] Connection validated");
                        }
                        else
                        {
                            ValidationError = "Connection validation failed. Please check your API key and settings.";
                            _ = LoggerService.Current.WriteDebugAsync($"[gap8_4-addmodelvm-validate-failed] Connection failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        ValidationError = $"Error validating connection: {ex.Message}";
                        _ = LoggerService.Current.WriteErrorAsync($"[gap8_4-addmodelvm-validate-error] {ex.Message}", ex);
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
                _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-save-start] Saving model: {SelectedModel}");

                var model = new ModelInfo
                {
                    Name = SelectedModel,
                    Provider = SelectedProvider?.Provider.ToString() ?? string.Empty,
                    ApiKey = ApiKey,
                    BaseUrl = BaseUrl ?? string.Empty
                };

                // Validate and use user-provided context window if provided
                var (isValidContextWindow, userContextWindow) = ValidateContextWindow(ContextWindow);
                if (!isValidContextWindow)
                {
                    ValidationError = "Context Window must be empty or a positive integer (e.g., 8192, 128000).";
                    _ = LoggerService.Current.WriteDebugAsync($"[gap19-addmodelvm-save-validation-error] Invalid context window: {ContextWindow}");
                    return;
                }

                // User-provided context window takes precedence; otherwise fall back to catalog/defaults
                if (userContextWindow.HasValue)
                {
                    model.ContextWindow = userContextWindow.Value;
                    _ = LoggerService.Current.WriteDebugAsync($"[gap19-addmodelvm-save-user-input] Using user-provided ContextWindow={model.ContextWindow}");
                }
                else if (SelectedProvider != null && ModelCatalog.TryGetModel(SelectedProvider.Provider, SelectedModel ?? string.Empty, out var catalogEntry))
                {
                    model.ContextWindow = catalogEntry!.ContextWindow;
                    model.SupportsFunctionCalling = catalogEntry.SupportsFunctionCalling;
                    model.SupportedToolFormats = catalogEntry.SupportedToolFormats ?? new List<string>();
                    model.OllamaModelId = catalogEntry.OllamaModelId;
                    _ = LoggerService.Current.WriteDebugAsync($"[gap18-addmodelvm-save-catalog-found] Loaded model metadata from catalog: ContextWindow={model.ContextWindow}");
                }
                else if (SelectedProvider != null)
                {
                    model.ContextWindow = ModelCatalog.GetDefaultContextWindow(SelectedProvider.Provider);
                    model.SupportsFunctionCalling = ModelCatalog.GetDefaultToolSupport(SelectedProvider.Provider);
                    model.SupportedToolFormats = ModelCatalog.GetDefaultToolFormats(SelectedProvider.Provider);
                    _ = LoggerService.Current.WriteDebugAsync($"[gap18-addmodelvm-save-catalog-fallback] Using provider defaults: ContextWindow={model.ContextWindow}");
                }
                else
                {
                    model.ContextWindow = 4096;
                    model.SupportsFunctionCalling = false;
                    model.SupportedToolFormats = new List<string>();
                    _ = LoggerService.Current.WriteDebugAsync($"[gap18-addmodelvm-save-no-provider] No provider selected; using hardcoded defaults");
                }

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
                _ = LoggerService.Current.WriteErrorAsync($"[gap12_3-addmodelvm-save-error] {ex.Message}", ex);
            }
        }

        private async Task SaveModelAsync(global::ContinueVS.Core.Types.ContinueConfig config)
        {
            try
            {
                await _configService.SaveConfigAsync();
                _ = LoggerService.Current.WriteDebugAsync("[gap8_4-addmodelvm-save-success] Model saved successfully");
                CurrentStep = 0;
                _onSaveCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[gap8_4-addmodelvm-save-failed] Failed to save: {ex.Message}", ex);
                ValidationError = $"Failed to save model: {ex.Message}";
            }
        }

        /// <summary>
        /// Resets the form to its initial state for a fresh Add Model workflow.
        /// </summary>
        public void ResetForm()
        {
            // Clear form data
            _selectedModel = null;
            _apiKey = null;
            _baseUrl = null;
            _contextWindow = null;
            _validationError = null;
            _isValidating = false;
            AvailableModels.Clear();

            // Reset to step 1 (provider selection)
            _currentStep = 1;
            RaisePropertyChanged(nameof(CurrentStep));

            // Auto-select first provider (which will trigger LoadModelsForProvider via property setter)
            if (Providers.Count > 0)
            {
                SelectedProvider = Providers[0];
                _ = LoggerService.Current.WriteDebugAsync($"[gap12_3-addmodelvm-reset] Form reset; auto-selected first provider: {Providers[0].Name}");
            }
            else
            {
                SelectedProvider = null;
                _ = LoggerService.Current.WriteDebugAsync("[gap12_3-addmodelvm-reset] Form reset; no providers available");
            }

            // Raise property changed for form fields
            RaisePropertyChanged(nameof(SelectedModel));
            RaisePropertyChanged(nameof(ApiKey));
            RaisePropertyChanged(nameof(BaseUrl));
            RaisePropertyChanged(nameof(ContextWindow));
            RaisePropertyChanged(nameof(ValidationError));
            RaisePropertyChanged(nameof(IsValidating));
        }

        /// <summary>
        /// Validates a context window input string (e.g., user-entered text).
        /// </summary>
        /// <param name="input">The input string to validate (can be empty or numeric).</param>
        /// <returns>A tuple of (isValid, parsedValue). If input is empty, returns (true, null). If input is a positive integer, returns (true, value). Otherwise returns (false, null).</returns>
        private (bool isValid, int? value) ValidateContextWindow(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (true, null);
            }

            if (int.TryParse(input, out var value) && value > 0)
            {
                return (true, value);
            }

            return (false, null);
        }
    }
}
