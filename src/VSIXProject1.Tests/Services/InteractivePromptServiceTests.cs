#nullable enable

using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for InteractivePromptService.
    /// Tests prompt display, user choice handling, and mode-dependent behavior.
    /// </summary>
    public class InteractivePromptServiceTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly InteractivePromptService _service;

        public InteractivePromptServiceTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<IBridgeLogger>();
            _service = new InteractivePromptService(_mockNotificationService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task PromptOnPhaseFailureAsync_InteractiveMode_DisplaysPrompt()
        {
            // Arrange
            var phaseName = "Analysis";
            var errorMessage = "Timeout while parsing stack trace";
            _mockNotificationService.Setup(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PromptOnPhaseFailureAsync(phaseName, errorMessage, isInteractiveMode: true);

            // Assert
            Assert.Equal(UserPromptChoice.Retry, result);
            _mockNotificationService.Verify(
                ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task PromptOnPhaseFailureAsync_AutonomousMode_SkipsPrompt()
        {
            // Arrange
            var phaseName = "Instrumentation";
            var errorMessage = "Failed to apply changes";

            // Act
            var result = await _service.PromptOnPhaseFailureAsync(phaseName, errorMessage, isInteractiveMode: false);

            // Assert
            Assert.Equal(UserPromptChoice.Retry, result);
            _mockNotificationService.Verify(
                ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task PromptOnPhaseFailureAsync_UserSkips_ReturnsSkip()
        {
            // Arrange
            _mockNotificationService.Setup(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.PromptOnPhaseFailureAsync("TestPhase", "Test error", isInteractiveMode: true);

            // Assert
            Assert.Equal(UserPromptChoice.Skip, result);
        }

        [Fact]
        public async Task PromptOnRetryThresholdAsync_InteractiveMode_DisplaysPrompt()
        {
            // Arrange
            _mockNotificationService.Setup(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PromptOnRetryThresholdAsync("Fix bug in SendMessage", attemptCount: 3, maxRetries: 3, isInteractiveMode: true);

            // Assert
            Assert.Equal(UserPromptChoice.Retry, result);
            _mockNotificationService.Verify(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PromptOnRetryThresholdAsync_AutonomousMode_CancelsWithoutPrompt()
        {
            // Arrange
            // No setup needed; autonomous mode should not call ShowConfirmationAsync

            // Act
            var result = await _service.PromptOnRetryThresholdAsync("Fix bug", attemptCount: 3, maxRetries: 3, isInteractiveMode: false);

            // Assert
            Assert.Equal(UserPromptChoice.Cancel, result);
            _mockNotificationService.Verify(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PromptOnRiskyChangeAsync_InteractiveMode_DisplaysPrompt()
        {
            // Arrange
            _mockNotificationService.Setup(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.PromptOnRiskyChangeAsync(
                filePath: "src/Program.cs",
                riskReason: "Deletes critical initialization code",
                changePreview: "- main()",
                isInteractiveMode: true);

            // Assert
            Assert.Equal(UserPromptChoice.Retry, result);
            _mockNotificationService.Verify(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task PromptOnRiskyChangeAsync_AutonomousMode_AutoApprovesWithoutPrompt()
        {
            // Arrange
            // No setup needed

            // Act
            var result = await _service.PromptOnRiskyChangeAsync(
                filePath: "src/Program.cs",
                riskReason: "Deletes code",
                isInteractiveMode: false);

            // Assert
            Assert.Equal(UserPromptChoice.Retry, result); // Retry means "approve" for risky changes
            _mockNotificationService.Verify(ns => ns.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task PromptOnPhaseFailureAsync_InvalidParams_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnPhaseFailureAsync("", "error", true));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnPhaseFailureAsync("phase", "", true));
        }

        [Fact]
        public async Task PromptOnRetryThresholdAsync_InvalidParams_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnRetryThresholdAsync("", 2, 3, true));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnRetryThresholdAsync("change", 0, 3, true));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnRetryThresholdAsync("change", 2, 0, true));
        }

        [Fact]
        public async Task PromptOnRiskyChangeAsync_InvalidParams_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnRiskyChangeAsync("", "risk", null, true));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.PromptOnRiskyChangeAsync("file.cs", "", null, true));
        }
    }
}
