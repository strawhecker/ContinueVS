#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services;

namespace ContinueVS.Tests.Services
{
    public class ModelCatalogTests
    {
        [Fact]
        public void TryGetModel_Ollama_Llama31_ReturnsCorrectContextWindow()
        {
            // Arrange
            var provider = ModelProvider.Ollama;
            var modelName = "Llama 3.1 Chat";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal("Llama 3.1 Chat", model.Name);
            Assert.Equal(8192, model.ContextWindow);
            Assert.Equal("ollama", model.Provider);
        }

        [Fact]
        public void TryGetModel_OpenAI_GPT4o_HasToolSupport()
        {
            // Arrange
            var provider = ModelProvider.OpenAI;
            var modelName = "GPT-4o";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal("GPT-4o", model.Name);
            Assert.True(model.SupportsFunctionCalling);
            Assert.Contains("openai", model.SupportedToolFormats);
            Assert.Equal(128000, model.ContextWindow);
        }

        [Fact]
        public void TryGetModel_Anthropic_Claude_HasCorrectContextWindow()
        {
            // Arrange
            var provider = ModelProvider.Anthropic;
            var modelName = "Claude 3.5 Sonnet";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal("Claude 3.5 Sonnet", model.Name);
            Assert.Equal(200000, model.ContextWindow);
            Assert.True(model.SupportsFunctionCalling);
        }

        [Fact]
        public void TryGetModel_AllProviders_HaveAtLeastOneEntry()
        {
            // Arrange & Act
            var providers = new[] { ModelProvider.Ollama, ModelProvider.OpenAI, ModelProvider.Anthropic, ModelProvider.Azure, ModelProvider.Gemini, ModelProvider.Mistral, ModelProvider.OpenRouter };

            // Assert
            foreach (var provider in providers)
            {
                var models = ModelCatalog.GetModelsForProvider(provider);
                Assert.NotEmpty(models);
            }
        }

        [Fact]
        public void TryGetModel_AllOllamaModels_HaveOllamaModelId()
        {
            // Arrange & Act
            var ollamaModels = ModelCatalog.GetModelsForProvider(ModelProvider.Ollama);

            // Assert
            foreach (var model in ollamaModels)
            {
                Assert.NotNull(model.OllamaModelId);
                Assert.NotEmpty(model.OllamaModelId);
            }
        }

        [Fact]
        public void TryGetModel_UnknownModel_ReturnsFalse()
        {
            // Arrange
            var provider = ModelProvider.OpenAI;
            var modelName = "NonExistentModel-XYZ";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.False(result);
            Assert.Null(model);
        }

        [Fact]
        public void TryGetModel_EmptyModelName_ReturnsFalse()
        {
            // Arrange
            var provider = ModelProvider.OpenAI;

            // Act
            var result = ModelCatalog.TryGetModel(provider, string.Empty, out var model);

            // Assert
            Assert.False(result);
            Assert.Null(model);
        }

        [Fact]
        public void GetDefaultContextWindow_AllProviders_ReturnPositiveValue()
        {
            // Arrange
            var providers = new[] { ModelProvider.Ollama, ModelProvider.OpenAI, ModelProvider.Anthropic, ModelProvider.Azure, ModelProvider.Gemini, ModelProvider.Mistral, ModelProvider.OpenRouter };

            // Act & Assert
            foreach (var provider in providers)
            {
                var contextWindow = ModelCatalog.GetDefaultContextWindow(provider);
                Assert.True(contextWindow > 0);
            }
        }

        [Fact]
        public void TryGetModel_AllModels_HavePositiveContextWindow()
        {
            // Arrange
            var providers = new[] { ModelProvider.Ollama, ModelProvider.OpenAI, ModelProvider.Anthropic, ModelProvider.Azure, ModelProvider.Gemini, ModelProvider.Mistral, ModelProvider.OpenRouter };

            // Act & Assert
            foreach (var provider in providers)
            {
                var models = ModelCatalog.GetModelsForProvider(provider);
                foreach (var model in models)
                {
                    Assert.True(model.ContextWindow > 0, $"Model {model.Name} ({provider}) has invalid ContextWindow: {model.ContextWindow}");
                }
            }
        }

        [Fact]
        public void GetCatalogSize_ReturnsExpectedCount()
        {
            // Act
            var size = ModelCatalog.GetCatalogSize();

            // Assert
            Assert.True(size >= 50, $"Expected at least 50 models in catalog; got {size}");
        }

        [Fact]
        public void TryGetModel_Gemini_2_5_Pro_HasLargeContextWindow()
        {
            // Arrange
            var provider = ModelProvider.Gemini;
            var modelName = "Gemini 2.5 Pro";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal(1000000, model.ContextWindow);
        }

        [Fact]
        public void TryGetModel_Mistral_Codestral_HasToolSupport()
        {
            // Arrange
            var provider = ModelProvider.Mistral;
            var modelName = "Codestral";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal("Codestral", model.Name);
            Assert.True(model.SupportsFunctionCalling);
            Assert.Equal(32768, model.ContextWindow);
        }

        [Fact]
        public void GetDefaultToolSupport_OpenAI_ReturnsTrue()
        {
            // Act
            var supports = ModelCatalog.GetDefaultToolSupport(ModelProvider.OpenAI);

            // Assert
            Assert.True(supports);
        }

        [Fact]
        public void GetDefaultToolSupport_Ollama_ReturnsFalse()
        {
            // Act
            var supports = ModelCatalog.GetDefaultToolSupport(ModelProvider.Ollama);

            // Assert
            Assert.False(supports);
        }

        [Fact]
        public void GetDefaultToolFormats_OpenAI_ReturnsOpenAI()
        {
            // Act
            var formats = ModelCatalog.GetDefaultToolFormats(ModelProvider.OpenAI);

            // Assert
            Assert.NotNull(formats);
            Assert.Contains("openai", formats);
        }

        [Fact]
        public void GetModelsForProvider_Ollama_ReturnsOllamaModels()
        {
            // Act
            var models = ModelCatalog.GetModelsForProvider(ModelProvider.Ollama);

            // Assert
            Assert.NotEmpty(models);
            foreach (var model in models)
            {
                Assert.Equal("ollama", model.Provider);
            }
        }

        [Fact]
        public void TryGetModel_Azure_GPT4o_ReturnsLargeContextWindow()
        {
            // Arrange
            var provider = ModelProvider.Azure;
            var modelName = "GPT-4o";

            // Act
            var result = ModelCatalog.TryGetModel(provider, modelName, out var model);

            // Assert
            Assert.True(result);
            Assert.NotNull(model);
            Assert.Equal(128000, model.ContextWindow);
        }
    }
}
