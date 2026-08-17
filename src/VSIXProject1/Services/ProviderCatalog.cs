#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;

namespace ContinueVS.Services
{
    /// <summary>
    /// Static catalog of supported LLM providers with metadata and default model lists.
    /// </summary>
    public static class ProviderCatalog
    {
        private static readonly Dictionary<ModelProvider, ProviderMetadata> _providers = new Dictionary<ModelProvider, ProviderMetadata>
        {
            {
                ModelProvider.Anthropic,
                new ProviderMetadata
                {
                    Name = "Anthropic",
                    Provider = ModelProvider.Anthropic,
                    DownloadUrl = "https://www.anthropic.com",
                    SupportsAutodetect = false,
                    DefaultModels = new List<string>
                    {
                        "Claude Opus 4.6",
                        "Claude Opus 4.5",
                        "Claude Opus 4.1",
                        "Claude Sonnet 4.6",
                        "Claude Sonnet 4.5",
                        "Claude Sonnet 4",
                        "Claude Haiku 4.5"
                    }
                }
            },
            {
                ModelProvider.Azure,
                new ProviderMetadata
                {
                    Name = "Azure OpenAI",
                    Provider = ModelProvider.Azure,
                    DownloadUrl = "https://azure.microsoft.com",
                    SupportsAutodetect = false,
                    DefaultModels = new List<string>
                    {
                        "GPT-4o",
                        "GPT-4 Turbo",
                        "GPT-4",
                        "GPT-3.5-Turbo"
                    }
                }
            },
            {
                ModelProvider.Gemini,
                new ProviderMetadata
                {
                    Name = "Google Gemini",
                    Provider = ModelProvider.Gemini,
                    DownloadUrl = "https://ai.google.dev",
                    SupportsAutodetect = false,
                    DefaultModels = new List<string>
                    {
                        "Gemini 3.1 Pro",
                        "Gemini 3 Flash",
                        "Gemini 3.1 Flash Lite",
                        "Gemini 2.5 Pro",
                        "Gemini 2.5 Flash",
                        "Gemini 2.5 Flash Lite"
                    }
                }
            },
            {
                ModelProvider.Mistral,
                new ProviderMetadata
                {
                    Name = "Mistral",
                    Provider = ModelProvider.Mistral,
                    DownloadUrl = "https://console.mistral.ai",
                    SupportsAutodetect = false,
                    DefaultModels = new List<string>
                    {
                        "Devstral Medium",
                        "Devstral Small",
                        "Magistral Medium",
                        "Devstral 8B",
                        "Codestral",
                        "Codestral Mamba",
                        "Mistral Large",
                        "Mistral Small",
                        "Mistral 8x22B"
                    }
                }
            },
            {
                ModelProvider.Ollama,
                new ProviderMetadata
                {
                    Name = "Ollama",
                    Provider = ModelProvider.Ollama,
                    DownloadUrl = "https://ollama.ai/download",
                    SupportsAutodetect = true,
                    DefaultModels = new List<string>
                    {
                        "Llama 3.1 Chat",
                        "Llama 3.2 Chat",
                        "DeepSeek Coder",
                        "Mistral",
                        "CodeLlama Instruct",
                        "Llama 3.2",
                        "Llama 3 Chat",
                        "Granite Code",
                        "WizardCoder",
                        "Phind CodeLlama (34b)",
                        "Gemma 4"
                    }
                }
            },
            {
                ModelProvider.OpenAI,
                new ProviderMetadata
                {
                    Name = "OpenAI",
                    Provider = ModelProvider.OpenAI,
                    DownloadUrl = "https://openai.com",
                    SupportsAutodetect = false,
                    DefaultModels = new List<string>
                    {
                        "GPT-4o",
                        "GPT-4o Mini",
                        "GPT-4 Turbo",
                        "GPT-4",
                        "GPT-3.5-Turbo",
                        "o3",
                        "o1"
                    }
                }
            },
            {
                ModelProvider.OpenRouter,
                new ProviderMetadata
                {
                    Name = "OpenRouter",
                    Provider = ModelProvider.OpenRouter,
                    DownloadUrl = "https://openrouter.ai",
                    SupportsAutodetect = true,
                    DefaultModels = new List<string>
                    {
                        "(Dynamic discovery via API)"
                    }
                }
            }
        };

        /// <summary>
        /// Gets metadata for a specific provider.
        /// </summary>
        public static ProviderMetadata? GetProviderMetadata(ModelProvider provider)
        {
            return _providers.ContainsKey(provider) ? _providers[provider] : null;
        }

        /// <summary>
        /// Gets all supported providers.
        /// </summary>
        public static List<ProviderMetadata> GetAllProviders()
        {
            return _providers.Values.ToList();
        }

        /// <summary>
        /// Gets default models for a specific provider.
        /// </summary>
        public static List<string> GetDefaultModels(ModelProvider provider)
        {
            var metadata = GetProviderMetadata(provider);
            return metadata?.DefaultModels ?? new List<string>();
        }
    }
}
