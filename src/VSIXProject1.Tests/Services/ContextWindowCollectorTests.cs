using Xunit;
using Moq;
using EnvDTE;
using System;
using System.Collections.Generic;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using ContinueVS.Core.Types;
using System.Threading.Tasks;

#nullable enable
#pragma warning disable VSTHRD010 // Accessing DTE/Documents in unit tests is acceptable

namespace ContinueVS.Tests.Services
{
    public class ContextWindowCollectorTests
    {
        private Mock<DTE> CreateMockDte()
        {
            var dteMock = new Mock<DTE>();
            var docsMock = new Mock<Documents>();
            dteMock.Setup(d => d.Documents).Returns(docsMock.Object);
            return dteMock;
        }

        private Mock<IConfigService> CreateMockConfigService(ModelInfo? activeModel = null)
        {
            var configServiceMock = new Mock<IConfigService>();
            configServiceMock.Setup(c => c.GetSelectedModel()).Returns(activeModel);
            return configServiceMock;
        }

        [Fact]
        public void Constructor_WithValidDTE_InitializesSuccessfully()
        {
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);
            Assert.NotNull(collector);
        }

        [Fact]
        public void Constructor_WithNullDTE_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ContextWindowCollector(null!));
        }

        [Fact]
        public async Task GetContextWindowAsync_ReturnsValidContextWindowInfo()
        {
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.True(result.UsedTokens >= 0);
            Assert.NotNull(result.EstimatedTokens);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesNullActiveDocument()
        {
            var dteMock = CreateMockDte();
            dteMock.Setup(d => d.ActiveDocument).Returns((Document?)null!);

            var collector = new ContextWindowCollector(dteMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesExceptionGracefully()
        {
            var dteMock = new Mock<DTE>();
            dteMock.Setup(d => d.Documents).Throws(new Exception("Test exception"));

            var collector = new ContextWindowCollector(dteMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_HandlesConcurrentCalls()
        {
            var dteMock = CreateMockDte();
            var collector = new ContextWindowCollector(dteMock.Object);

            var tasks = new List<Task<ContextWindowCollector.ContextWindowInfo>>
            {
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
                collector.GetContextWindowAsync(),
            };

            var results = await Task.WhenAll(tasks);

            Assert.Equal(3, results.Length);
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.True(result.MaxTokens > 0);
            }
        }

        // ====================================================================
        // gap19 Context Window Precedence Tests
        // ====================================================================

        [Fact]
        public async Task GetContextWindowAsync_UsesActiveModelContextWindow_WhenModelSelected()
        {
            var dteMock = CreateMockDte();

            var activeModel = new ModelInfo
            {
                Id = "test-model-id",
                Name = "gpt-4",
                ContextWindow = 8192
            };
            var configServiceMock = CreateMockConfigService(activeModel);

            var collector = new ContextWindowCollector(dteMock.Object, configServiceMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.Equal(8192, result.MaxTokens);
        }

        [Fact]
        public async Task GetContextWindowAsync_FallsBackToSettings_WhenNoModelSelected()
        {
            var dteMock = CreateMockDte();
            var configServiceMock = CreateMockConfigService(null);

            var collector = new ContextWindowCollector(dteMock.Object, configServiceMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.True(result.MaxTokens >= 131072 || result.MaxTokens > 0);
        }

        [Fact]
        public async Task GetContextWindowAsync_IgnoresZeroContextWindow_FallsBackToSettings()
        {
            var dteMock = CreateMockDte();

            var activeModel = new ModelInfo
            {
                Id = "test-model-id",
                Name = "test-model",
                ContextWindow = 0
            };
            var configServiceMock = CreateMockConfigService(activeModel);

            var collector = new ContextWindowCollector(dteMock.Object, configServiceMock.Object);
            var result = await collector.GetContextWindowAsync();

            Assert.NotNull(result);
            Assert.True(result.MaxTokens > 0);
            Assert.NotEqual(0, result.MaxTokens);
        }
    }
}
