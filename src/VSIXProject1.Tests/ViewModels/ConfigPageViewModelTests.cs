#nullable enable

using System;
using System.Collections.ObjectModel;
using Moq;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ConfigPageViewModelTests : TestFixtureBase
    {
        [Fact]
        public void Constructor_WithValidDependencies_InitializesCollections()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();

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
                mockIndexingService.Object);

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

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ConfigPageViewModel(null!, mockIndexingService.Object));
        }

        [Fact]
        public void Constructor_WithNullIndexingService_ThrowsArgumentNullException()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new ConfigPageViewModel(mockConfigService.Object, null!));
        }

        [Fact]
        public void SelectedModel_CanBeSet()
        {
            // Arrange
            var mockConfigService = CreateLooseMock<IConfigService>();
            var mockIndexingService = CreateLooseMock<IIndexingService>();

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
                mockIndexingService.Object);

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
                mockIndexingService.Object);

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
                mockIndexingService.Object);

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
                mockIndexingService.Object);

            var testTool = new ToolDefinition { Name = "grep" };

            // Act
            viewModel.AvailableTools.Add(testTool);

            // Assert
            Assert.Single(viewModel.AvailableTools);
            Assert.Equal("grep", viewModel.AvailableTools[0].Name);
        }
    }
}
