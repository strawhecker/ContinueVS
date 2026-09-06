#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Types;
using Newtonsoft.Json;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for case-insensitive provider and model name matching (gap67).
    /// Verifies that provider names work correctly regardless of casing.
    /// </summary>
    public class CaseInsensitiveProviderTests
    {
        [Theory]
        [InlineData("ollama")]
        [InlineData("OLLAMA")]
        [InlineData("Ollama")]
        [InlineData("OlLaMa")]
        public void ModelInfo_NormalizesProviderToLowercase_OnDeserialization(string providerInput)
        {
            // Arrange
            var json = $@"{{
                ""id"": ""test-1"",
                ""name"": ""Test Model"",
                ""provider"": ""{providerInput}"",
                ""contextWindow"": 4096,
                ""supportsFunctionCalling"": false
            }}";

            // Act
            var model = JsonConvert.DeserializeObject<ModelInfo>(json);

            // Assert
            Assert.NotNull(model);
            Assert.Equal("ollama", model!.Provider);
        }

        [Theory]
        [InlineData("openai")]
        [InlineData("OPENAI")]
        [InlineData("OpenAI")]
        public void ModelInfo_NormalizesOpenaiProviderToLowercase(string providerInput)
        {
            // Arrange
            var json = $@"{{
                ""id"": ""test-2"",
                ""name"": ""GPT-4"",
                ""provider"": ""{providerInput}"",
                ""apiKey"": ""sk-test"",
                ""contextWindow"": 8192,
                ""supportsFunctionCalling"": true
            }}";

            // Act
            var model = JsonConvert.DeserializeObject<ModelInfo>(json);

            // Assert
            Assert.NotNull(model);
            Assert.Equal("openai", model!.Provider);
        }

        [Fact]
        public void ModelInfo_ProviderPropertySetter_NormalizesToLowercase()
        {
            // Arrange
            var model = new ModelInfo();

            // Act
            model.Provider = "OLLAMA";

            // Assert
            Assert.Equal("ollama", model.Provider);
        }

        [Fact]
        public void ModelInfo_ProviderPropertySetter_HandlesNullProvider()
        {
            // Arrange
            var model = new ModelInfo();

            // Act
            model.Provider = null;

            // Assert
            Assert.Null(model.Provider);
        }

        [Theory]
        [InlineData("anthropic")]
        [InlineData("ANTHROPIC")]
        [InlineData("Anthropic")]
        public void ModelInfo_NormalizesMultipleProviderNames(string providerInput)
        {
            // Arrange
            var json = $@"{{
                ""id"": ""test-3"",
                ""name"": ""Claude 3"",
                ""provider"": ""{providerInput}"",
                ""contextWindow"": 200000,
                ""supportsFunctionCalling"": true
            }}";

            // Act
            var model = JsonConvert.DeserializeObject<ModelInfo>(json);

            // Assert
            Assert.NotNull(model);
            Assert.Equal("anthropic", model!.Provider);
        }

        [Fact]
        public void ModelInfoList_AllProvidersNormalizedAfterDeserialization()
        {
            // Arrange
            var json = @"[
                {
                    ""id"": ""m1"",
                    ""name"": ""Model 1"",
                    ""provider"": ""OLLAMA"",
                    ""contextWindow"": 4096,
                    ""supportsFunctionCalling"": false
                },
                {
                    ""id"": ""m2"",
                    ""name"": ""Model 2"",
                    ""provider"": ""OpenAI"",
                    ""contextWindow"": 8192,
                    ""supportsFunctionCalling"": true
                },
                {
                    ""id"": ""m3"",
                    ""name"": ""Model 3"",
                    ""provider"": ""ANTHROPIC"",
                    ""contextWindow"": 200000,
                    ""supportsFunctionCalling"": true
                }
            ]";

            // Act
            var models = JsonConvert.DeserializeObject<List<ModelInfo>>(json);

            // Assert
            Assert.NotNull(models);
            Assert.Equal(3, models!.Count);
            Assert.Equal("ollama", models[0].Provider);
            Assert.Equal("openai", models[1].Provider);
            Assert.Equal("anthropic", models[2].Provider);
        }

        [Fact]
        public void ModelInfo_ProviderComparisonWorks_CaseInsensitively()
        {
            // Arrange
            var model1 = new ModelInfo { Provider = "OLLAMA" };
            var model2 = new ModelInfo { Provider = "ollama" };
            var model3 = new ModelInfo { Provider = "Ollama" };

            // Act & Assert
            // After setting, all should be normalized to lowercase
            Assert.Equal(model1.Provider, model2.Provider);
            Assert.Equal(model2.Provider, model3.Provider);
            Assert.Equal("ollama", model1.Provider);
            Assert.Equal("ollama", model2.Provider);
            Assert.Equal("ollama", model3.Provider);
        }

        [Theory]
        [InlineData("ollama")]
        [InlineData("OLLAMA")]
        [InlineData("Ollama")]
        public void ModelInfo_SerializedJson_PreservesNormalizedCase(string inputProvider)
        {
            // Arrange
            var model = new ModelInfo
            {
                Id = "test-id",
                Name = "Test Model",
                Provider = inputProvider,
                ContextWindow = 4096,
                SupportsFunctionCalling = false
            };

            // Act
            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<ModelInfo>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("ollama", deserialized!.Provider);
        }

        [Fact]
        public void ModelInfo_WithEmptyProvider_DoesNotThrow()
        {
            // Arrange
            var model = new ModelInfo { Provider = "" };

            // Act & Assert
            Assert.Equal("", model.Provider);
        }

        [Fact]
        public void ModelInfo_WithWhitespaceProvider_NormalizesToLowerWhitespace()
        {
            // Arrange
            var model = new ModelInfo { Provider = "  " };

            // Act & Assert
            Assert.Equal("  ", model.Provider);
        }
    }
}
