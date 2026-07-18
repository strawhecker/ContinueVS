#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Handlers.Llm;

namespace ContinueVS.Services
{
    /// <summary>
    /// Collects and exposes LLM model configuration from Continue's config.json.
    ///
    /// Provides queries for:
    /// - Current active model (provider, model name, title, API base)
    /// - List of available configured models
    /// - Model capabilities (context length, streaming support, vision support)
    /// - Token limits per model (max input, max output, total context)
    ///
    /// All operations are synchronous and config-only; no IDE/DTE interop required.
    /// Gracefully handles missing or malformed config by returning empty collections.
    ///
    /// Integration:
    /// - Used by: model-info-handler.mjs (Step 88) via C# bridge adapter
    /// - Accesses: ContinueConfigReader.FindModel() to load LlmConfig
    /// - Throws: ModelInfoCollectorException on critical failures
    /// - Errors: Caught and logged; degraded response returned on failure
    /// </summary>
    internal sealed class ModelInfoCollector
    {
        /// <summary>
        /// Optional logger for diagnostics and debugging.
        /// </summary>
        private readonly IBridgeLogger? _logger;

        /// <summary>
        /// Initializes a new instance of ModelInfoCollector.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostics; gracefully degrades if null.</param>
        public ModelInfoCollector(IBridgeLogger? logger = null)
        {
            _logger = logger;
            if (_logger != null)
            {
                _ = _logger.WriteDebugAsync("ModelInfoCollector initialized");
            }
        }

        /// <summary>
        /// Gets the current active model from the Continue configuration.
        /// </summary>
        /// <returns>
        /// A ModelInfoDto representing the current model, or null if no models are configured.
        /// Returns the first configured model by default.
        /// </returns>
        public async Task<ModelInfoDto?> GetCurrentModelAsync()
        {
            try
            {
                var currentModel = ContinueConfigReader.FindModel("");
                if (currentModel == null)
                {
                    if (_logger != null)
                    {
                        await _logger.WriteDebugAsync("No current model found in Continue configuration");
                    }
                    return null;
                }

                return MapToModelInfoDto(currentModel);
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    await _logger.WriteErrorAsync($"Error getting current model: {ex.Message}");
                }
                throw new ModelInfoCollectorException($"Failed to get current model: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all available LLM models configured in Continue's config.json.
        /// </summary>
        /// <returns>
        /// A list of ModelInfoDto objects representing all configured models.
        /// Returns an empty list if no models are configured or config is unavailable.
        /// </returns>
        public async Task<List<ModelInfoDto>> GetAvailableModelsAsync()
        {
            try
            {
                var result = new List<ModelInfoDto>();
                var allModels = GetAllConfiguredModels();

                foreach (var model in allModels)
                {
                    try
                    {
                        result.Add(MapToModelInfoDto(model));
                    }
                    catch (Exception modelEx)
                    {
                        if (_logger != null)
                        {
                            await _logger.WriteWarningAsync($"Failed to map model {model?.Title}: {modelEx.Message}");
                        }
                        // Skip malformed model and continue with others
                    }
                }

                if (_logger != null)
                {
                    await _logger.WriteDebugAsync($"Retrieved {result.Count} available models");
                }

                return result;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    await _logger.WriteErrorAsync($"Error getting available models: {ex.Message}");
                }
                // Return empty list on failure (graceful degradation)
                return new List<ModelInfoDto>();
            }
        }

        /// <summary>
        /// Gets model capabilities for a specific provider.
        /// </summary>
        /// <param name="provider">The LLM provider (e.g., "openai", "anthropic").</param>
        /// <returns>A ModelCapabilities object with provider-specific limits and features.</returns>
        public async Task<ModelCapabilities> GetModelCapabilitiesAsync(string? provider)
        {
            try
            {
                var capabilities = GetProviderCapabilities(provider ?? "openai");
                return capabilities;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    await _logger.WriteWarningAsync($"Error getting capabilities for provider {provider}: {ex.Message}");
                }
                // Return default capabilities on failure
                return GetDefaultCapabilities();
            }
        }

        /// <summary>
        /// Gets token limits for a specific model.
        /// </summary>
        /// <param name="provider">The LLM provider name.</param>
        /// <param name="model">The model identifier.</param>
        /// <returns>A TokenLimits object with input, output, and total context token limits.</returns>
        public async Task<TokenLimits> GetTokenLimitsAsync(string? provider, string? model)
        {
            try
            {
                var limits = GetTokenLimitsForModel(provider ?? "openai", model ?? "");
                return limits;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    await _logger.WriteWarningAsync($"Error getting token limits for {provider}/{model}: {ex.Message}");
                }
                // Return default limits on failure
                return GetDefaultTokenLimits();
            }
        }

        // ===== Private Helpers =====

        /// <summary>
        /// Maps an LlmModelConfig to a ModelInfoDto transfer object.
        /// </summary>
        private static ModelInfoDto MapToModelInfoDto(LlmModelConfig model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            return new ModelInfoDto
            {
                Provider = model.Provider ?? "unknown",
                Model = model.Model ?? "unknown",
                Title = model.Title ?? "Unknown Model",
                ApiBase = model.ApiBase,
                // Never expose API keys in the response
                ApiKey = string.IsNullOrEmpty(model.ApiKey) ? null : "<redacted>"
            };
        }

        /// <summary>
        /// Retrieves all configured models from Continue config.
        /// Returns an empty list if config is unavailable or malformed.
        /// </summary>
        private static List<LlmModelConfig> GetAllConfiguredModels()
        {
            var result = new List<LlmModelConfig>();
            try
            {
                // Try to load the first model to verify config exists
                var firstModel = ContinueConfigReader.FindModel("");
                if (firstModel != null)
                {
                    result.Add(firstModel);
                    // Note: Current implementation of ContinueConfigReader only retrieves
                    // one model at a time. For full model list support, would need
                    // to extend ContinueConfigReader.ReadConfigAsync().
                }
            }
            catch
            {
                // Config unavailable or malformed; return empty list
            }
            return result;
        }

        /// <summary>
        /// Gets capabilities for a specific provider.
        /// </summary>
        private static ModelCapabilities GetProviderCapabilities(string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => new ModelCapabilities
                {
                    ContextLength = 8192,
                    SupportsStreaming = true,
                    SupportsVision = true,
                    MaxRpm = 3500,
                    MaxTokensPerMinute = 90000
                },
                "anthropic" => new ModelCapabilities
                {
                    ContextLength = 100000,
                    SupportsStreaming = true,
                    SupportsVision = true,
                    MaxRpm = 50,
                    MaxTokensPerMinute = 40000
                },
                "ollama" => new ModelCapabilities
                {
                    ContextLength = 4096,
                    SupportsStreaming = true,
                    SupportsVision = false,
                    MaxRpm = 0,
                    MaxTokensPerMinute = 0
                },
                _ => GetDefaultCapabilities()
            };
        }

        /// <summary>
        /// Gets token limits for a specific model.
        /// </summary>
        private static TokenLimits GetTokenLimitsForModel(string provider, string model)
        {
            var baseCapabilities = GetProviderCapabilities(provider);

            return provider.ToLowerInvariant() switch
            {
                "openai" => GetOpenAiTokenLimits(model),
                "anthropic" => GetAnthropicTokenLimits(model),
                "ollama" => new TokenLimits
                {
                    MaxInputTokens = baseCapabilities.ContextLength - 512,
                    MaxOutputTokens = 512,
                    TotalContextTokens = baseCapabilities.ContextLength
                },
                _ => new TokenLimits
                {
                    MaxInputTokens = baseCapabilities.ContextLength - 1024,
                    MaxOutputTokens = 1024,
                    TotalContextTokens = baseCapabilities.ContextLength
                }
            };
        }

        /// <summary>
        /// Gets OpenAI-specific token limits based on model name.
        /// </summary>
        private static TokenLimits GetOpenAiTokenLimits(string modelName)
        {
            var lowerModel = (modelName ?? "").ToLowerInvariant();

            if (lowerModel.Contains("gpt-4-turbo"))
                return new TokenLimits { MaxInputTokens = 128000, MaxOutputTokens = 4096, TotalContextTokens = 128000 };

            if (lowerModel.Contains("gpt-4"))
                return new TokenLimits { MaxInputTokens = 8192, MaxOutputTokens = 2048, TotalContextTokens = 8192 };

            if (lowerModel.Contains("gpt-3.5"))
                return new TokenLimits { MaxInputTokens = 4096, MaxOutputTokens = 2048, TotalContextTokens = 4096 };

            // Default OpenAI model limits
            return new TokenLimits
            {
                MaxInputTokens = 8000,
                MaxOutputTokens = 2000,
                TotalContextTokens = 8192
            };
        }

        /// <summary>
        /// Gets Anthropic-specific token limits based on model name.
        /// </summary>
        private static TokenLimits GetAnthropicTokenLimits(string modelName)
        {
            var lowerModel = (modelName ?? "").ToLowerInvariant();

            if (lowerModel.Contains("opus"))
                return new TokenLimits { MaxInputTokens = 200000, MaxOutputTokens = 4096, TotalContextTokens = 200000 };

            if (lowerModel.Contains("sonnet"))
                return new TokenLimits { MaxInputTokens = 200000, MaxOutputTokens = 4096, TotalContextTokens = 200000 };

            if (lowerModel.Contains("haiku"))
                return new TokenLimits { MaxInputTokens = 75000, MaxOutputTokens = 4096, TotalContextTokens = 75000 };

            // Default Anthropic limits
            return new TokenLimits
            {
                MaxInputTokens = 100000,
                MaxOutputTokens = 4096,
                TotalContextTokens = 100000
            };
        }

        /// <summary>
        /// Gets default model capabilities (fallback).
        /// </summary>
        private static ModelCapabilities GetDefaultCapabilities()
        {
            return new ModelCapabilities
            {
                ContextLength = 4096,
                SupportsStreaming = true,
                SupportsVision = false,
                MaxRpm = 0,
                MaxTokensPerMinute = 0
            };
        }

        /// <summary>
        /// Gets default token limits (fallback).
        /// </summary>
        private static TokenLimits GetDefaultTokenLimits()
        {
            return new TokenLimits
            {
                MaxInputTokens = 3072,
                MaxOutputTokens = 1024,
                TotalContextTokens = 4096
            };
        }
    }

    // ===== Data Transfer Objects =====

    /// <summary>
    /// Represents a single LLM model with its configuration and metadata.
    /// </summary>
    internal sealed class ModelInfoDto
    {
        /// <summary>
        /// The LLM provider name (e.g., "openai", "anthropic", "ollama").
        /// </summary>
        public string Provider { get; set; } = "";

        /// <summary>
        /// The model identifier as defined in the provider's API (e.g., "gpt-4", "claude-3-opus").
        /// </summary>
        public string Model { get; set; } = "";

        /// <summary>
        /// Human-readable title for the model (e.g., "OpenAI GPT-4").
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Optional API base URL override (e.g., for self-hosted models).
        /// </summary>
        public string? ApiBase { get; set; }

        /// <summary>
        /// API key status: null if not set, or "<redacted>" if configured (never exposed).
        /// </summary>
        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// Represents the capabilities and limits of an LLM provider.
    /// </summary>
    internal sealed class ModelCapabilities
    {
        /// <summary>
        /// Maximum context window size in tokens.
        /// </summary>
        public int ContextLength { get; set; } = 4096;

        /// <summary>
        /// Whether the model supports streaming responses.
        /// </summary>
        public bool SupportsStreaming { get; set; } = true;

        /// <summary>
        /// Whether the model supports vision/image analysis.
        /// </summary>
        public bool SupportsVision { get; set; } = false;

        /// <summary>
        /// Maximum requests per minute (0 if unlimited).
        /// </summary>
        public int MaxRpm { get; set; } = 0;

        /// <summary>
        /// Maximum tokens per minute (0 if unlimited).
        /// </summary>
        public int MaxTokensPerMinute { get; set; } = 0;
    }

    /// <summary>
    /// Represents token limits for input, output, and total context.
    /// </summary>
    internal sealed class TokenLimits
    {
        /// <summary>
        /// Maximum tokens allowed in the input/prompt.
        /// </summary>
        public int MaxInputTokens { get; set; } = 4000;

        /// <summary>
        /// Maximum tokens allowed in the model's response.
        /// </summary>
        public int MaxOutputTokens { get; set; } = 2000;

        /// <summary>
        /// Total context window size (input + output combined).
        /// </summary>
        public int TotalContextTokens { get; set; } = 8000;
    }

    /// <summary>
    /// Exception thrown when model info collection fails.
    /// </summary>
    internal sealed class ModelInfoCollectorException : Exception
    {
        /// <summary>
        /// The original exception that caused this failure, if any.
        /// </summary>
        public Exception? OriginalException { get; }

        /// <summary>
        /// Initializes a new instance of ModelInfoCollectorException.
        /// </summary>
        public ModelInfoCollectorException(string message, Exception? originalException = null)
            : base(message)
        {
            OriginalException = originalException;
        }
    }
}
