#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services;

namespace ContinueVS.Tests.Services
{
    public class ProviderCatalogTests
    {
        [Fact]
        public void GetProviderMetadata_WithValidProvider_ReturnsMetadata()
        {
            // Arrange
            var provider = ModelProvider.Ollama;

            // Act
            var metadata = ProviderCatalog.GetProviderMetadata(provider);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal("Ollama", metadata.Name);
            Assert.Equal(ModelProvider.Ollama, metadata.Provider);
            Assert.True(metadata.SupportsAutodetect);
        }

        [Fact]
        public void GetAllProviders_ReturnsAllSupportedProviders()
        {
            // Act
            var providers = ProviderCatalog.GetAllProviders();

            // Assert
            Assert.NotNull(providers);
            Assert.Equal(7, providers.Count);
        }

        [Fact]
        public void GetAllProviders_ContainsOllama()
        {
            // Act
            var providers = ProviderCatalog.GetAllProviders();

            // Assert
            var ollama = providers.Find(p => p.Provider == ModelProvider.Ollama);
            Assert.NotNull(ollama);
        }

        [Fact]
        public void GetAllProviders_ContainsOpenAI()
        {
            // Act
            var providers = ProviderCatalog.GetAllProviders();

            // Assert
            var openai = providers.Find(p => p.Provider == ModelProvider.OpenAI);
            Assert.NotNull(openai);
        }

        [Fact]
        public void GetDefaultModels_WithValidProvider_ReturnsModels()
        {
            // Arrange
            var provider = ModelProvider.OpenAI;

            // Act
            var models = ProviderCatalog.GetDefaultModels(provider);

            // Assert
            Assert.NotNull(models);
            Assert.NotEmpty(models);
        }

        [Fact]
        public void GetDefaultModels_OllamaSupportsAutodetect()
        {
            // Arrange
            var provider = ModelProvider.Ollama;

            // Act
            var metadata = ProviderCatalog.GetProviderMetadata(provider);

            // Assert
            Assert.NotNull(metadata);
            Assert.True(metadata.SupportsAutodetect);
        }

        [Fact]
        public void GetProviderMetadata_AllProvidersHaveDownloadUrl()
        {
            // Act
            var providers = ProviderCatalog.GetAllProviders();

            // Assert
            foreach (var provider in providers)
            {
                Assert.NotNull(provider.DownloadUrl);
                Assert.NotEmpty(provider.DownloadUrl);
            }
        }

        [Fact]
        public void GetProviderMetadata_AllProvidersHaveDefaultModels()
        {
            // Act
            var providers = ProviderCatalog.GetAllProviders();

            // Assert
            foreach (var provider in providers)
            {
                Assert.NotNull(provider.DefaultModels);
                Assert.NotEmpty(provider.DefaultModels);
            }
        }

        [Theory]
        [InlineData(ModelProvider.Anthropic)]
        [InlineData(ModelProvider.Azure)]
        [InlineData(ModelProvider.Gemini)]
        [InlineData(ModelProvider.Mistral)]
        [InlineData(ModelProvider.Ollama)]
        [InlineData(ModelProvider.OpenAI)]
        [InlineData(ModelProvider.OpenRouter)]
        public void GetProviderMetadata_AllEnumValuesSupported(ModelProvider provider)
        {
            // Act
            var metadata = ProviderCatalog.GetProviderMetadata(provider);

            // Assert
            Assert.NotNull(metadata);
            Assert.Equal(provider, metadata.Provider);
        }
    }
}
