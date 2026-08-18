#nullable enable

using System.Collections.Specialized;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using System.Collections.Generic;

namespace ContinueVS.Tests.UI
{
    public class ConfigPageBindingTests : DataBindingTestBase
    {
        private Mock<IModelDiscoveryService> CreateMockModelDiscoveryService()
        {
            var mock = CreateLooseMock<IModelDiscoveryService>();
            mock.Setup(s => s.DiscoverModelsAsync(It.IsAny<ModelProvider>(), It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "model1", "model2" });
            return mock;
        }

        [Fact]
        public void SelectedModel_PropertyChanged_FiresNotification()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            using var tracker = new PropertyChangedTracker(viewModel);

            var modelInfo = new ModelInfo { Name = "gpt-4", Provider = "openai" };

            // Act
            viewModel.SelectedModel = modelInfo;

            // Assert
            AssertPropertyChanged(tracker, nameof(ConfigPageViewModel.SelectedModel));
            Assert.Equal(modelInfo, viewModel.SelectedModel);
        }

        [Fact]
        public void AvailableModels_CollectionChanged_FiresNotificationOnAdd()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            using var collectionTracker = new CollectionChangeTracker(viewModel.AvailableModels);

            var modelInfo = new ModelInfo { Name = "gpt-4", Provider = "openai" };

            // Act
            viewModel.AvailableModels.Add(modelInfo);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.AvailableModels);
            Assert.Contains(modelInfo, viewModel.AvailableModels);
        }

        [Fact]
        public void AvailableModels_CollectionChanged_FiresNotificationOnRemove()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            var modelInfo = new ModelInfo { Name = "gpt-4", Provider = "openai" };
            viewModel.AvailableModels.Add(modelInfo);

            using var collectionTracker = new CollectionChangeTracker(viewModel.AvailableModels);

            // Act
            viewModel.AvailableModels.Remove(modelInfo);

            // Assert
            AssertCollectionRemoved(collectionTracker, count: 1);
            Assert.Empty(viewModel.AvailableModels);
        }

        [Fact]
        public void AvailableTools_CollectionChanged_FiresNotificationOnAdd()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            using var collectionTracker = new CollectionChangeTracker(viewModel.AvailableTools);

            var toolDef = new ToolDefinition { Name = "bash", Description = "Execute bash commands" };

            // Act
            viewModel.AvailableTools.Add(toolDef);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.AvailableTools);
            Assert.Contains(toolDef, viewModel.AvailableTools);
        }

        [Fact]
        public void AddModelCommand_CanBeExecuted()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Act & Assert
            Assert.NotNull(viewModel.AddModelCommand);
            Assert.True(viewModel.AddModelCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveModelCommand_CanBeExecutedWithSelectedModel()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            var modelInfo = new ModelInfo { Name = "gpt-4", Provider = "openai" };
            viewModel.SelectedModel = modelInfo;

            // Act & Assert
            Assert.NotNull(viewModel.RemoveModelCommand);
            Assert.True(viewModel.RemoveModelCommand.CanExecute(null));
        }

        [Fact]
        public void SaveConfigCommand_CanBeExecuted()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Act & Assert
            Assert.NotNull(viewModel.SaveConfigCommand);
            Assert.True(viewModel.SaveConfigCommand.CanExecute(null));
        }

        [Fact]
        public void ReindexCommand_CanBeExecuted()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Act & Assert
            Assert.NotNull(viewModel.ReindexCommand);
            Assert.True(viewModel.ReindexCommand.CanExecute(null));
        }

        [Fact]
        public void MultipleCollectionChanges_AllFireNotifications()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            using var modelTracker = new CollectionChangeTracker(viewModel.AvailableModels);
            using var toolTracker = new CollectionChangeTracker(viewModel.AvailableTools);

            var model1 = new ModelInfo { Name = "gpt-4", Provider = "openai" };
            var model2 = new ModelInfo { Name = "claude-3", Provider = "anthropic" };
            var tool1 = new ToolDefinition { Name = "bash", Description = "Execute bash" };

            // Act
            viewModel.AvailableModels.Add(model1);
            viewModel.AvailableModels.Add(model2);
            viewModel.AvailableTools.Add(tool1);

            // Assert
            AssertCollectionChanged(modelTracker, NotifyCollectionChangedAction.Add);
            AssertCollectionChanged(toolTracker, NotifyCollectionChangedAction.Add);
            Assert.Equal(2, viewModel.AvailableModels.Count);
            Assert.Single(viewModel.AvailableTools);
        }

        [Fact]
        public void Profiles_CollectionChanged_FiresNotificationOnAdd()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            using var collectionTracker = new CollectionChangeTracker(viewModel.Profiles);

            var profileInfo = new ProfileInfo { Name = "default", Description = "Default profile" };

            // Act
            viewModel.Profiles.Add(profileInfo);

            // Assert
            AssertCollectionAdded(collectionTracker, count: 1);
            Assert.Single(viewModel.Profiles);
            Assert.Contains(profileInfo, viewModel.Profiles);
        }
    }
}
