#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    /// <summary>
    /// Service for discovering models and validating connections across multiple LLM providers.
    /// Implements provider-specific discovery logic with fallback to hardcoded catalogs.
    /// </summary>
    public class ModelDiscoveryService : IModelDiscoveryService
    {
        private readonly HttpClient _httpClient;
        private const int DiscoveryTimeoutMs = 5000;

        public ModelDiscoveryService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Discovers available models for a provider. For Ollama, queries /api/tags.
        /// For other providers, returns hardcoded default list.
        /// </summary>
        public async Task<List<string>> DiscoverModelsAsync(ModelProvider provider, string? apiKey = null)
        {
            try
            {
                Debug.WriteLine($"[gap8_4-discovery-start] Discovering models for provider: {provider}");

                List<string> models = provider switch
                {
                    ModelProvider.Ollama => await DiscoverOllamaModelsAsync(),
                    ModelProvider.OpenRouter => await DiscoverOpenRouterModelsAsync(apiKey),
                    _ => ProviderCatalog.GetDefaultModels(provider)
                };

                Debug.WriteLine($"[gap8_4-discovery-complete] Discovered {models.Count} models for {provider}");
                return models;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_4-discovery-error] Error discovering models for {provider}: {ex.Message}");
                // Fallback to default models
                return ProviderCatalog.GetDefaultModels(provider);
            }
        }

        /// <summary>
        /// Validates connection to a model provider by testing API key and model availability.
        /// </summary>
        public async Task<bool> ValidateConnectionAsync(ModelInfo model)
        {
            if (model == null) return false;

            try
            {
                Debug.WriteLine($"[gap8_4-validation-start] Validating connection for model: {model.Name}, provider: {model.Provider}");

                // Parse provider from string
                if (!Enum.TryParse<ModelProvider>(model.Provider, ignoreCase: true, out var provider))
                {
                    Debug.WriteLine($"[gap8_4-validation-error] Invalid provider: {model.Provider}");
                    return false;
                }

                bool result = provider switch
                {
                    ModelProvider.Ollama => await ValidateOllamaAsync(model),
                    ModelProvider.OpenAI => await ValidateOpenAIAsync(model),
                    ModelProvider.Anthropic => await ValidateAnthropicAsync(model),
                    ModelProvider.Azure => await ValidateAzureAsync(model),
                    ModelProvider.Gemini => await ValidateGeminiAsync(model),
                    ModelProvider.Mistral => await ValidateMistralAsync(model),
                    ModelProvider.OpenRouter => await ValidateOpenRouterAsync(model),
                    _ => false
                };

                Debug.WriteLine($"[gap8_4-validation-complete] Connection validation result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[gap8_4-validation-error] Unexpected error validating connection: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets provider metadata (download URL, supported models, etc).
        /// </summary>
        public ProviderMetadata? GetProviderMetadata(ModelProvider provider)
        {
            return ProviderCatalog.GetProviderMetadata(provider);
        }

        private async Task<List<string>> DiscoverOllamaModelsAsync()
        {
            try
            {
                var baseUrl = "http://localhost:11434";
                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);
                    if (!response.IsSuccessStatusCode)
                        return ProviderCatalog.GetDefaultModels(ModelProvider.Ollama);

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(json);
                    var models = data?["models"]?.Select(m => m["name"]?.ToString() ?? string.Empty)
                        .Where(m => !string.IsNullOrEmpty(m))
                        .ToList() ?? new List<string>();

                    return models.Count > 0 ? models : ProviderCatalog.GetDefaultModels(ModelProvider.Ollama);
                }
            }
            catch
            {
                return ProviderCatalog.GetDefaultModels(ModelProvider.Ollama);
            }
        }

        private async Task<List<string>> DiscoverOpenRouterModelsAsync(string? apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return ProviderCatalog.GetDefaultModels(ModelProvider.OpenRouter);

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    if (!response.IsSuccessStatusCode)
                        return ProviderCatalog.GetDefaultModels(ModelProvider.OpenRouter);

                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonConvert.DeserializeObject<JObject>(json);
                    var models = data?["data"]?.Select(m => m["id"]?.ToString() ?? string.Empty)
                        .Where(m => !string.IsNullOrEmpty(m))
                        .ToList() ?? new List<string>();

                    return models.Count > 0 ? models : ProviderCatalog.GetDefaultModels(ModelProvider.OpenRouter);
                }
            }
            catch
            {
                return ProviderCatalog.GetDefaultModels(ModelProvider.OpenRouter);
            }
        }

        private async Task<bool> ValidateOllamaAsync(ModelInfo model)
        {
            try
            {
                var baseUrl = model.BaseUrl ?? "http://localhost:11434";
                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var response = await _httpClient.GetAsync($"{baseUrl}/api/tags", cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateOpenAIAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                    request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateAnthropicAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/messages");
                    request.Headers.Add("x-api-key", model.ApiKey);

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateAzureAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey) || string.IsNullOrEmpty(model.BaseUrl))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var baseUrl = model.BaseUrl ?? string.Empty;
                    var response = await _httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/deployments?api-version=2024-06-01", cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateGeminiAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var response = await _httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={model.ApiKey}", cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateMistralAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.mistral.ai/v1/models");
                    request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> ValidateOpenRouterAsync(ModelInfo model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ApiKey))
                    return false;

                using (var cts = new CancellationTokenSource(DiscoveryTimeoutMs))
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/auth/key");
                    request.Headers.Add("Authorization", $"Bearer {model.ApiKey}");

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
