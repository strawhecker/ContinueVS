#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;

namespace ContinueVS.Services
{
    /// <summary>
    /// Static model catalog providing curated metadata for 50-75 popular LLM models across all 7 providers.
    /// Used by AddModelViewModel and ModelDiscoveryService to hydrate ModelInfo with context windows, tool support, and provider-specific IDs.
    /// Phase 1 (MVP) for gap18: Model Catalog Parity.
    /// </summary>
    public static class ModelCatalog
    {
        private static readonly Dictionary<(ModelProvider, string), ModelInfo> _catalog = new Dictionary<(ModelProvider, string), ModelInfo>
        {
            // --- OLLAMA (11 models from ProviderCatalog) ---
            {
                (ModelProvider.Ollama, "Llama 3.1 Chat"),
                new ModelInfo { Name = "Llama 3.1 Chat", Provider = "ollama", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" }, OllamaModelId = "llama2" }
            },
            {
                (ModelProvider.Ollama, "Llama 3.2 Chat"),
                new ModelInfo { Name = "Llama 3.2 Chat", Provider = "ollama", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" }, OllamaModelId = "llama2" }
            },
            {
                (ModelProvider.Ollama, "DeepSeek Coder"),
                new ModelInfo { Name = "DeepSeek Coder", Provider = "ollama", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "deepseek-coder" }
            },
            {
                (ModelProvider.Ollama, "Mistral"),
                new ModelInfo { Name = "Mistral", Provider = "ollama", ContextWindow = 32768, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "mistral" }
            },
            {
                (ModelProvider.Ollama, "CodeLlama Instruct"),
                new ModelInfo { Name = "CodeLlama Instruct", Provider = "ollama", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "codellama" }
            },
            {
                (ModelProvider.Ollama, "Llama 3.2"),
                new ModelInfo { Name = "Llama 3.2", Provider = "ollama", ContextWindow = 8192, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "llama2" }
            },
            {
                (ModelProvider.Ollama, "Llama 3 Chat"),
                new ModelInfo { Name = "Llama 3 Chat", Provider = "ollama", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" }, OllamaModelId = "llama2" }
            },
            {
                (ModelProvider.Ollama, "Granite Code"),
                new ModelInfo { Name = "Granite Code", Provider = "ollama", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "granite-code" }
            },
            {
                (ModelProvider.Ollama, "WizardCoder"),
                new ModelInfo { Name = "WizardCoder", Provider = "ollama", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "wizardcoder" }
            },
            {
                (ModelProvider.Ollama, "Phind CodeLlama (34b)"),
                new ModelInfo { Name = "Phind CodeLlama (34b)", Provider = "ollama", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "phind-codellama" }
            },
            {
                (ModelProvider.Ollama, "Gemma 4"),
                new ModelInfo { Name = "Gemma 4", Provider = "ollama", ContextWindow = 8192, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>(), OllamaModelId = "gemma" }
            },

            // --- OPENAI (17 models from ProviderCatalog) ---
            {
                (ModelProvider.OpenAI, "GPT-5.4 Pro"),
                new ModelInfo { Name = "GPT-5.4 Pro", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5.4"),
                new ModelInfo { Name = "GPT-5.4", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5.4 Mini"),
                new ModelInfo { Name = "GPT-5.4 Mini", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5.2"),
                new ModelInfo { Name = "GPT-5.2", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5.1"),
                new ModelInfo { Name = "GPT-5.1", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5"),
                new ModelInfo { Name = "GPT-5", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5 Mini"),
                new ModelInfo { Name = "GPT-5 Mini", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-5 Codex"),
                new ModelInfo { Name = "GPT-5 Codex", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-4.1"),
                new ModelInfo { Name = "GPT-4.1", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-4.1 Mini"),
                new ModelInfo { Name = "GPT-4.1 Mini", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "Codex Mini"),
                new ModelInfo { Name = "Codex Mini", Provider = "openai", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenAI, "o3"),
                new ModelInfo { Name = "o3", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenAI, "o4"),
                new ModelInfo { Name = "o4", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenAI, "GPT-4o"),
                new ModelInfo { Name = "GPT-4o", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-4o Mini"),
                new ModelInfo { Name = "GPT-4o Mini", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-4 Turbo"),
                new ModelInfo { Name = "GPT-4 Turbo", Provider = "openai", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenAI, "GPT-3.5-Turbo"),
                new ModelInfo { Name = "GPT-3.5-Turbo", Provider = "openai", ContextWindow = 16384, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },

            // --- ANTHROPIC (7 models from ProviderCatalog) ---
            {
                (ModelProvider.Anthropic, "Claude 3.5 Sonnet"),
                new ModelInfo { Name = "Claude 3.5 Sonnet", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Opus 4.6"),
                new ModelInfo { Name = "Claude Opus 4.6", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Opus 4.5"),
                new ModelInfo { Name = "Claude Opus 4.5", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Opus 4.1"),
                new ModelInfo { Name = "Claude Opus 4.1", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Sonnet 4.6"),
                new ModelInfo { Name = "Claude Sonnet 4.6", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Sonnet 4.5"),
                new ModelInfo { Name = "Claude Sonnet 4.5", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Sonnet 4"),
                new ModelInfo { Name = "Claude Sonnet 4", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.Anthropic, "Claude Haiku 4.5"),
                new ModelInfo { Name = "Claude Haiku 4.5", Provider = "anthropic", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },

            // --- AZURE (4 models from ProviderCatalog) ---
            {
                (ModelProvider.Azure, "GPT-4o"),
                new ModelInfo { Name = "GPT-4o", Provider = "azure", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Azure, "GPT-4 Turbo"),
                new ModelInfo { Name = "GPT-4 Turbo", Provider = "azure", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Azure, "GPT-4"),
                new ModelInfo { Name = "GPT-4", Provider = "azure", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Azure, "GPT-3.5-Turbo"),
                new ModelInfo { Name = "GPT-3.5-Turbo", Provider = "azure", ContextWindow = 16384, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },

            // --- GEMINI (6 models from ProviderCatalog) ---
            {
                (ModelProvider.Gemini, "Gemini 3.1 Pro"),
                new ModelInfo { Name = "Gemini 3.1 Pro", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Gemini, "Gemini 3 Flash"),
                new ModelInfo { Name = "Gemini 3 Flash", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Gemini, "Gemini 3.1 Flash Lite"),
                new ModelInfo { Name = "Gemini 3.1 Flash Lite", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Gemini, "Gemini 2.5 Pro"),
                new ModelInfo { Name = "Gemini 2.5 Pro", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Gemini, "Gemini 2.5 Flash"),
                new ModelInfo { Name = "Gemini 2.5 Flash", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Gemini, "Gemini 2.5 Flash Lite"),
                new ModelInfo { Name = "Gemini 2.5 Flash Lite", Provider = "gemini", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },

            // --- MISTRAL (9 models from ProviderCatalog) ---
            {
                (ModelProvider.Mistral, "Devstral Medium"),
                new ModelInfo { Name = "Devstral Medium", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Devstral Small"),
                new ModelInfo { Name = "Devstral Small", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Magistral Medium"),
                new ModelInfo { Name = "Magistral Medium", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Devstral 8B"),
                new ModelInfo { Name = "Devstral 8B", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Codestral"),
                new ModelInfo { Name = "Codestral", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Codestral Mamba"),
                new ModelInfo { Name = "Codestral Mamba", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.Mistral, "Mistral Large"),
                new ModelInfo { Name = "Mistral Large", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Mistral Small"),
                new ModelInfo { Name = "Mistral Small", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.Mistral, "Mistral 8x22B"),
                new ModelInfo { Name = "Mistral 8x22B", Provider = "mistral", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },

            // --- OPENROUTER (25+ comprehensive models - all major providers via OpenRouter) ---
            {
                (ModelProvider.OpenRouter, "Claude 3.5 Sonnet"),
                new ModelInfo { Name = "Claude 3.5 Sonnet", Provider = "openrouter", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.OpenRouter, "Claude 3.5 Haiku"),
                new ModelInfo { Name = "Claude 3.5 Haiku", Provider = "openrouter", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.OpenRouter, "Claude Opus 4.1"),
                new ModelInfo { Name = "Claude Opus 4.1", Provider = "openrouter", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "anthropic" } }
            },
            {
                (ModelProvider.OpenRouter, "GPT-4o"),
                new ModelInfo { Name = "GPT-4o", Provider = "openrouter", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "GPT-4 Turbo"),
                new ModelInfo { Name = "GPT-4 Turbo", Provider = "openrouter", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "GPT-4o Mini"),
                new ModelInfo { Name = "GPT-4o Mini", Provider = "openrouter", ContextWindow = 128000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "GPT-3.5 Turbo"),
                new ModelInfo { Name = "GPT-3.5 Turbo", Provider = "openrouter", ContextWindow = 16384, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Mistral Large"),
                new ModelInfo { Name = "Mistral Large", Provider = "openrouter", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Mistral Small"),
                new ModelInfo { Name = "Mistral Small", Provider = "openrouter", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Mistral 7B Instruct"),
                new ModelInfo { Name = "Mistral 7B Instruct", Provider = "openrouter", ContextWindow = 32768, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenRouter, "Llama 3.1 405B"),
                new ModelInfo { Name = "Llama 3.1 405B", Provider = "openrouter", ContextWindow = 131072, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Llama 3 70B"),
                new ModelInfo { Name = "Llama 3 70B", Provider = "openrouter", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Llama 2 70B"),
                new ModelInfo { Name = "Llama 2 70B", Provider = "openrouter", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenRouter, "DeepSeek Coder 33B"),
                new ModelInfo { Name = "DeepSeek Coder 33B", Provider = "openrouter", ContextWindow = 4096, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenRouter, "Qwen 2.5 72B"),
                new ModelInfo { Name = "Qwen 2.5 72B", Provider = "openrouter", ContextWindow = 131072, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Qwen 1.5 110B"),
                new ModelInfo { Name = "Qwen 1.5 110B", Provider = "openrouter", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Phi 3.5 Mini"),
                new ModelInfo { Name = "Phi 3.5 Mini", Provider = "openrouter", ContextWindow = 128000, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            },
            {
                (ModelProvider.OpenRouter, "Gemini 2.0 Flash"),
                new ModelInfo { Name = "Gemini 2.0 Flash", Provider = "openrouter", ContextWindow = 1000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Gemini 1.5 Pro"),
                new ModelInfo { Name = "Gemini 1.5 Pro", Provider = "openrouter", ContextWindow = 2000000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Command R+"),
                new ModelInfo { Name = "Command R+", Provider = "openrouter", ContextWindow = 4096, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Groq Llama 3 70B"),
                new ModelInfo { Name = "Groq Llama 3 70B", Provider = "openrouter", ContextWindow = 8192, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Groq Mixtral 8x7B"),
                new ModelInfo { Name = "Groq Mixtral 8x7B", Provider = "openrouter", ContextWindow = 32768, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Jamba 1.5 Large"),
                new ModelInfo { Name = "Jamba 1.5 Large", Provider = "openrouter", ContextWindow = 99000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "YI Large"),
                new ModelInfo { Name = "YI Large", Provider = "openrouter", ContextWindow = 200000, SupportsFunctionCalling = true, SupportedToolFormats = new List<string> { "openai" } }
            },
            {
                (ModelProvider.OpenRouter, "Perplexity Sonar 8B"),
                new ModelInfo { Name = "Perplexity Sonar 8B", Provider = "openrouter", ContextWindow = 12000, SupportsFunctionCalling = false, SupportedToolFormats = new List<string>() }
            }
        };

        /// <summary>
        /// Attempts to retrieve a model from the catalog by provider and model name.
        /// </summary>
        /// <param name="provider">The model provider.</param>
        /// <param name="modelName">The model name (display name or partial match).</param>
        /// <param name="model">Populated with the ModelInfo if found; otherwise null.</param>
        /// <returns>True if model found; false otherwise.</returns>
        public static bool TryGetModel(ModelProvider provider, string modelName, out ModelInfo? model)
        {
            model = null;

            if (string.IsNullOrWhiteSpace(modelName))
                return false;

            var key = (provider, modelName.Trim());
            if (_catalog.TryGetValue(key, out var catalogModel))
            {
                model = new ModelInfo
                {
                    Name = catalogModel.Name,
                    Provider = catalogModel.Provider,
                    ContextWindow = catalogModel.ContextWindow,
                    SupportsFunctionCalling = catalogModel.SupportsFunctionCalling,
                    SupportedToolFormats = new List<string>(catalogModel.SupportedToolFormats ?? new List<string>()),
                    OllamaModelId = catalogModel.OllamaModelId
                };
                return true;
            }

            // Fallback: partial case-insensitive match for robustness
            var lowerName = modelName.ToLower();
            var match = _catalog.FirstOrDefault(kvp => kvp.Key.Item1 == provider && kvp.Key.Item2.ToLower().Contains(lowerName));
            if (match.Key != (default(ModelProvider), null))
            {
                model = new ModelInfo
                {
                    Name = match.Value.Name,
                    Provider = match.Value.Provider,
                    ContextWindow = match.Value.ContextWindow,
                    SupportsFunctionCalling = match.Value.SupportsFunctionCalling,
                    SupportedToolFormats = new List<string>(match.Value.SupportedToolFormats ?? new List<string>()),
                    OllamaModelId = match.Value.OllamaModelId
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the default context window for a provider (used as fallback when model not in catalog).
        /// </summary>
        public static int GetDefaultContextWindow(ModelProvider provider)
        {
            return provider switch
            {
                ModelProvider.Ollama => 8192,
                ModelProvider.OpenAI => 128000,
                ModelProvider.Anthropic => 200000,
                ModelProvider.Azure => 128000,
                ModelProvider.Gemini => 1000000,
                ModelProvider.Mistral => 32768,
                ModelProvider.OpenRouter => 32768,
                _ => 4096
            };
        }

        /// <summary>
        /// Gets whether a provider's models typically support tool/function calling (used as fallback).
        /// </summary>
        public static bool GetDefaultToolSupport(ModelProvider provider)
        {
            return provider switch
            {
                ModelProvider.Ollama => false,
                ModelProvider.OpenAI => true,
                ModelProvider.Anthropic => true,
                ModelProvider.Azure => true,
                ModelProvider.Gemini => true,
                ModelProvider.Mistral => true,
                ModelProvider.OpenRouter => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets default supported tool formats for a provider (used as fallback).
        /// </summary>
        public static List<string> GetDefaultToolFormats(ModelProvider provider)
        {
            return provider switch
            {
                ModelProvider.Ollama => new List<string>(),
                ModelProvider.OpenAI => new List<string> { "openai" },
                ModelProvider.Anthropic => new List<string> { "anthropic" },
                ModelProvider.Azure => new List<string> { "openai" },
                ModelProvider.Gemini => new List<string> { "openai" },
                ModelProvider.Mistral => new List<string> { "openai" },
                ModelProvider.OpenRouter => new List<string> { "openai" },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Gets all models for a specific provider from the catalog.
        /// </summary>
        public static List<ModelInfo> GetModelsForProvider(ModelProvider provider)
        {
            return _catalog
                .Where(kvp => kvp.Key.Item1 == provider)
                .Select(kvp => new ModelInfo
                {
                    Name = kvp.Value.Name,
                    Provider = kvp.Value.Provider,
                    ContextWindow = kvp.Value.ContextWindow,
                    SupportsFunctionCalling = kvp.Value.SupportsFunctionCalling,
                    SupportedToolFormats = new List<string>(kvp.Value.SupportedToolFormats ?? new List<string>()),
                    OllamaModelId = kvp.Value.OllamaModelId
                })
                .ToList();
        }

        /// <summary>
        /// Gets the total number of models in the catalog.
        /// </summary>
        public static int GetCatalogSize()
        {
            return _catalog.Count;
        }
    }
}
