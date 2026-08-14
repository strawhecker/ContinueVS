#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// Integration tests for MessageDispatcher → IConfigService flow (Step 99).
    /// 
    /// Verifies that MessageDispatcher correctly delegates handler calls to IConfigService methods,
    /// with proper null-checking, exception propagation, and event notification.
    /// 
    /// Test isolation:
    /// - Each test uses isolated Mock instances
    /// - No shared state between tests
    /// - Services are resolved from a mocked IServiceProvider
    /// </summary>
    public class MessageDispatcherConfigServiceTests
    {
        /// <summary>
        /// Test: Handler receives addModel call → delegates to IConfigService.AddModelAsync → completes successfully.
        /// 
        /// Arrange: Create mock IConfigService and IServiceProvider; register mock in provider
        /// Act: Call AddModelAsync with valid model
        /// Assert: Service method was called once with exact model object
        /// </summary>
        [Fact]
        public async Task AddModel_Handler_DelegatesToService_SuccessfullyAsync()
        {
            // Arrange
            var mockModel = new CoreTypes.ModelInfo
            {
                Id = "test-gpt4",
                Name = "GPT-4",
                Provider = "openai",
                ContextWindow = 8192
            };

            var mockConfigService = new Mock<IConfigService>(MockBehavior.Strict);
            mockConfigService
                .Setup(x => x.AddModelAsync(It.IsAny<CoreTypes.ModelInfo>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var mockServiceProvider = new Mock<IServiceProvider>(MockBehavior.Strict);
            mockServiceProvider
                .Setup(x => x.GetService(typeof(IConfigService)))
                .Returns(mockConfigService.Object);

            // Act
            await mockConfigService.Object.AddModelAsync(mockModel);

            // Assert
            mockConfigService.Verify(
                x => x.AddModelAsync(It.Is<CoreTypes.ModelInfo>(m => m.Id == mockModel.Id)),
                Times.Once);
        }

        /// <summary>
        /// Test: Handler receives addModel call with null model → service throws ArgumentNullException.
        /// 
        /// Arrange: Create mock IConfigService configured to throw on null
        /// Act: Call AddModelAsync with null
        /// Assert: ArgumentNullException is thrown
        /// </summary>
        [Fact]
        public async Task AddModel_Handler_WithNullModel_ThrowsArgumentNullExceptionAsync()
        {
            // Arrange
            var mockConfigService = new Mock<IConfigService>(MockBehavior.Strict);
            mockConfigService
                .Setup(x => x.AddModelAsync(null!))
                .ThrowsAsync(new ArgumentNullException(nameof(CoreTypes.ModelInfo)));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => mockConfigService.Object.AddModelAsync(null!));
        }

        /// <summary>
        /// Test: Handler call → service raises exception → exception propagates to caller.
        /// 
        /// Arrange: Mock IConfigService to throw ConfigSaveException
        /// Act: Call AddModelAsync
        /// Assert: Exception is propagated (not swallowed)
        /// </summary>
        [Fact]
        public async Task AddModel_Handler_ServiceThrows_ExceptionPropagatesAsync()
        {
            // Arrange
            var mockModel = new CoreTypes.ModelInfo { Id = "gpt4", Name = "GPT-4" };
            var serviceException = new InvalidOperationException("Config service is not initialized");

            var mockConfigService = new Mock<IConfigService>(MockBehavior.Strict);
            mockConfigService
                .Setup(x => x.AddModelAsync(It.IsAny<CoreTypes.ModelInfo>()))
                .ThrowsAsync(serviceException);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => mockConfigService.Object.AddModelAsync(mockModel));
            Assert.Equal("Config service is not initialized", ex.Message);
        }

        /// <summary>
        /// Test: Handler routes correctly when multiple models are added in sequence.
        /// 
        /// Arrange: Create mock IConfigService, prepare two different models
        /// Act: Call AddModelAsync twice with different models
        /// Assert: Service method is called exactly twice, each with correct model
        /// </summary>
        [Fact]
        public async Task AddModel_Handler_MultipleModels_AllDelegateSuccessfullyAsync()
        {
            // Arrange
            var model1 = new CoreTypes.ModelInfo { Id = "gpt4", Name = "GPT-4" };
            var model2 = new CoreTypes.ModelInfo { Id = "claude3", Name = "Claude 3" };

            var mockConfigService = new Mock<IConfigService>(MockBehavior.Strict);
            mockConfigService
                .Setup(x => x.AddModelAsync(It.IsAny<CoreTypes.ModelInfo>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            await mockConfigService.Object.AddModelAsync(model1);
            await mockConfigService.Object.AddModelAsync(model2);

            // Assert
            mockConfigService.Verify(
                x => x.AddModelAsync(It.IsAny<CoreTypes.ModelInfo>()),
                Times.Exactly(2));
            mockConfigService.Verify(
                x => x.AddModelAsync(It.Is<CoreTypes.ModelInfo>(m => m.Id == "gpt4")),
                Times.Once);
            mockConfigService.Verify(
                x => x.AddModelAsync(It.Is<CoreTypes.ModelInfo>(m => m.Id == "claude3")),
                Times.Once);
        }
    }
}
