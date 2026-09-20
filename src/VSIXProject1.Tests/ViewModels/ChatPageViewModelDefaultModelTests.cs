#nullable enable
using System.Collections.Generic;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Moq;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for default model selection behavior (model load persistence).
    ///
    /// Root cause: LoadModelsAsync() blindly selected AvailableModels[0] (the first/default
    /// model) on every startup whenever _selectedModel was null, discarding the user's
    /// persisted selection (SelectedModelId). This fix restores the configured model via
    /// GetSelectedModel() and only falls back to the first model when none is set.
    ///
    /// ResolveInitialSelectedModel is made internal (InternalsVisibleTo) so the pure
    /// decision logic is testable without spinning up a WPF Application / Dispatcher.
    /// </summary>
    public class ChatPageViewModelDefaultModelTests
    {
        private Mock<IConfigService> CreateMockConfigService(ModelInfo? configuredSelected)
        {
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetSelectedModel()).Returns(configuredSelected);
            return mock;
        }

        [Fact(DisplayName = "Configured selected model is restored, not the first/default model")]
        public void ResolveInitialSelectedModel_RestoresConfiguredModel()
        {
            // Arrange: two models, user has explicitly configured the SECOND one.
            var firstModel = new ModelInfo { Id = "model-1", Name = "default-model", Provider = "openai" };
            var userModel = new ModelInfo { Id = "model-2", Name = "user-model", Provider = "openai" };
            var configMock = CreateMockConfigService(configuredSelected: userModel);

            var chatVm = new ChatPageViewModel(
                new Mock<ILlmService>().Object,
                new Mock<IContextService>().Object,
                new Mock<IToolService>().Object,
                new Mock<ISessionService>().Object,
                new Mock<INotificationService>().Object,
                configMock.Object,
                new Mock<ISystemPromptService>().Object,
                new Mock<IUIStateService>().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);

            var available = new System.Collections.ObjectModel.ObservableCollection<ModelInfo>
            {
                firstModel, userModel
            };

            // Act
            var result = chatVm.ResolveInitialSelectedModel(available);

            // Assert: the user's configured model is selected, NOT the first/default one.
            Assert.NotNull(result);
            Assert.Equal("model-2", result.Id);
        }

        [Fact(DisplayName = "Default model is only selected when no model is configured")]
        public void ResolveInitialSelectedModel_SelectsFirstWhenNoModelConfigured()
        {
            // Arrange: only the default model exists, GetSelectedModel returns null (nothing set).
            var firstModel = new ModelInfo { Id = "model-1", Name = "default-model", Provider = "openai" };
            var configMock = CreateMockConfigService(configuredSelected: null);

            var chatVm = new ChatPageViewModel(
                new Mock<ILlmService>().Object,
                new Mock<IContextService>().Object,
                new Mock<IToolService>().Object,
                new Mock<ISessionService>().Object,
                new Mock<INotificationService>().Object,
                configMock.Object,
                new Mock<ISystemPromptService>().Object,
                new Mock<IUIStateService>().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);

            var available = new System.Collections.ObjectModel.ObservableCollection<ModelInfo>
            {
                firstModel
            };

            // Act
            var result = chatVm.ResolveInitialSelectedModel(available);

            // Assert: fall back to the first/default model since none is configured.
            Assert.NotNull(result);
            Assert.Equal("model-1", result.Id);
        }

        [Fact(DisplayName = "Configured model that no longer exists falls back to first/default")]
        public void ResolveInitialSelectedModel_FallsBackWhenConfiguredModelMissing()
        {
            // Arrange: configured model id refers to a model not present in the available list.
            var staleModel = new ModelInfo { Id = "stale", Name = "removed-model", Provider = "openai" };
            var actualModel = new ModelInfo { Id = "model-1", Name = "actual-model", Provider = "openai" };
            var configMock = CreateMockConfigService(configuredSelected: staleModel);

            var chatVm = new ChatPageViewModel(
                new Mock<ILlmService>().Object,
                new Mock<IContextService>().Object,
                new Mock<IToolService>().Object,
                new Mock<ISessionService>().Object,
                new Mock<INotificationService>().Object,
                configMock.Object,
                new Mock<ISystemPromptService>().Object,
                new Mock<IUIStateService>().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);

            var available = new System.Collections.ObjectModel.ObservableCollection<ModelInfo>
            {
                actualModel
            };

            // Act
            var result = chatVm.ResolveInitialSelectedModel(available);

            // Assert: falls back to the first available model rather than returning a stale one.
            Assert.NotNull(result);
            Assert.Equal("model-1", result.Id);
        }
    }
}
