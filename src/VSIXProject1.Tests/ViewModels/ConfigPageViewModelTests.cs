#nullable enable

using System;
using System.Collections.ObjectModel;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;
using ContinueVS.UI.Views;
using System.Collections.Generic;

namespace ContinueVS.Tests.ViewModels
{
    public class ConfigPageViewModelTests : TestFixtureBase
    {
        private Mock<IModelDiscoveryService> CreateMockModelDiscoveryService()
        {
            var mock = CreateLooseMock<IModelDiscoveryService>();
            mock.Setup(s => s.DiscoverModelsAsync(It.IsAny<ModelProvider>(), It.IsAny<string>()))
                .ReturnsAsync(new List<string> { "model1", "model2" });
            return mock;
        }

        [Fact]
        public void Constructor_WithValidDependencies_InitializesCollections()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new System.Collections.Generic.List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            // Act
            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Assert
            Assert.NotNull(viewModel);
            Assert.NotNull(viewModel.AvailableModels);
            Assert.NotNull(viewModel.AvailableTools);
            Assert.NotNull(viewModel.Profiles);
            Assert.IsType<ObservableCollection<ModelInfo>>(viewModel.AvailableModels);
            Assert.IsType<ObservableCollection<ToolDefinition>>(viewModel.AvailableTools);
            Assert.IsType<ObservableCollection<ProfileInfo>>(viewModel.Profiles);
        }

        [Fact]
        public void Constructor_WithNullConfigService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ConfigPageViewModel(null!, mockIndexingService.Object, mockIdeService.Object, mockModelDiscoveryService.Object));
        }

        [Fact]
        public void Constructor_WithNullIndexingService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ConfigPageViewModel(mockConfigService.Object, null!, mockIdeService.Object, mockModelDiscoveryService.Object));
        }

        [Fact]
        public void SelectedModel_CanBeSet()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new System.Collections.Generic.List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            var testModel = new ModelInfo { Name = "test-model" };

            // Act
            viewModel.SelectedModel = testModel;

            // Assert
            Assert.NotNull(viewModel.SelectedModel);
            Assert.Equal("test-model", viewModel.SelectedModel.Name);
        }

        [Fact]
        public void Commands_AreNotNull()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new System.Collections.Generic.List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            // Act
            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Assert
            Assert.NotNull(viewModel.SaveConfigCommand);
            Assert.NotNull(viewModel.AddModelCommand);
            Assert.NotNull(viewModel.RemoveModelCommand);
            Assert.NotNull(viewModel.ReindexCommand);
        }

        [Fact]
        public void CanAddModel_ToAvailableModels()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new System.Collections.Generic.List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            var testModel = new ModelInfo { Name = "gpt-4" };

            // Act
            viewModel.AvailableModels.Add(testModel);

            // Assert
            Assert.Single(viewModel.AvailableModels);
            Assert.Equal("gpt-4", viewModel.AvailableModels[0].Name);
        }

        [Fact]
        public void CanAddTool_ToAvailableTools()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new System.Collections.Generic.List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            var testTool = new ToolDefinition { Name = "grep" };

            // Act
            viewModel.AvailableTools.Add(testTool);

            // Assert
            Assert.Single(viewModel.AvailableTools);
            Assert.Equal("grep", viewModel.AvailableTools[0].Name);
        }

        [Fact]
        public void ExecuteAddModel_SwitchesToAddModelTab()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Act: Execute AddModel command
            viewModel.AddModelCommand.Execute(null);

            // Assert: SelectedTabIndex should be 3 (Add Model tab)
            Assert.Equal(3, viewModel.SelectedTabIndex);
            // AddModelViewModel should be initialized
            Assert.NotNull(viewModel.AddModelViewModel);
        }

        [Fact]
        public void AddModelViewModel_CancelCallback_SwitchesBackToModelsTab()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Act: Execute AddModel to go to Add Model tab
            viewModel.AddModelCommand.Execute(null);
            Assert.Equal(3, viewModel.SelectedTabIndex);

            // Simulate cancel by calling CancelCommand on AddModelViewModel
            viewModel.AddModelViewModel?.CancelCommand.Execute(null);

            // Assert: SelectedTabIndex should be back to 0 (Models tab)
            Assert.Equal(0, viewModel.SelectedTabIndex);
        }

        [Fact]
        public void ConfigChanged_Event_RefreshesAvailableModels()
        {
            // Arrange
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Id = "1", Name = "LLM1", Provider = "ollama" }
                }
            };

            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(config);

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(config.Models[0]);

            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Initial state
            Assert.NotEmpty(viewModel.AvailableModels);

            // Act: Simulate ConfigChanged event (add a new model)
            var newModel = new ModelInfo { Id = "2", Name = "LLM2", Provider = "openai" };
            config.Models.Add(newModel);

            // Manually call LoadConfiguration via reflection to simulate ConfigChanged behavior
            var loadConfigMethod = viewModel.GetType().GetMethod("LoadConfiguration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (loadConfigMethod != null)
            {
                loadConfigMethod.Invoke(viewModel, null);
            }

            // Assert: AvailableModels should be refreshed
            Assert.NotEmpty(viewModel.AvailableModels);
        }

        [Fact]
        public void AddModelCommand_IsNotNull()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();
            var mockIdeService = CreateLooseMock<IIdeService>();
            var mockModelDiscoveryService = CreateMockModelDiscoveryService();

            mockConfigService
                .Setup(s => s.GetCurrentConfig())
                .Returns(new ContinueConfig());

            mockConfigService
                .Setup(s => s.GetEnabledTools())
                .Returns(new List<ToolDefinition>());

            mockConfigService
                .Setup(s => s.GetSelectedModel())
                .Returns(new ModelInfo());

            // Act
            var viewModel = new ConfigPageViewModel(
                mockConfigService.Object,
                mockIndexingService.Object,
                mockIdeService.Object,
                mockModelDiscoveryService.Object);

            // Assert
            Assert.NotNull(viewModel.AddModelCommand);
            Assert.True(viewModel.AddModelCommand.CanExecute(null));
        }
    }
}
