using ContinueVS.Services;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for DowngradeWarningService.
    /// Tests downgrade detection and warning dialog behavior.
    /// </summary>
    public class DowngradeWarningServiceTests
    {
        [Fact]
        public async Task CheckDowngradeAsync_WhenUpgrade_ReturnsTrue()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            mockComparator
                .Setup(c => c.IsDowngrade("2.0.0", "2.1.0"))
                .Returns(false);

            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            var result = await service.CheckDowngradeAsync("2.0.0", "2.1.0");

            // Assert
            Assert.True(result);
            mockComparator.Verify(c => c.IsDowngrade("2.0.0", "2.1.0"), Times.Once);
        }

        [Fact]
        public async Task CheckDowngradeAsync_WhenSameVersion_ReturnsTrue()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            mockComparator
                .Setup(c => c.IsDowngrade("2.0.0", "2.0.0"))
                .Returns(false);

            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            var result = await service.CheckDowngradeAsync("2.0.0", "2.0.0");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckDowngradeAsync_WhenCurrentVersionNull_ReturnsTrue()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            var result = await service.CheckDowngradeAsync(null, "2.0.0");

            // Assert
            Assert.True(result);
            mockComparator.Verify(c => c.IsDowngrade(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckDowngradeAsync_WhenTargetVersionNull_ReturnsTrue()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            var result = await service.CheckDowngradeAsync("2.0.0", null);

            // Assert
            Assert.True(result);
            mockComparator.Verify(c => c.IsDowngrade(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckDowngradeAsync_WhenEmptyVersions_ReturnsTrue()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            var result = await service.CheckDowngradeAsync("", "2.0.0");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task CheckDowngradeAsync_WhenDowngradeDetected_CallsComparator()
        {
            // Arrange
            var mockComparator = new Mock<IVersionComparator>();
            mockComparator
                .Setup(c => c.IsDowngrade("2.1.0", "2.0.0"))
                .Returns(true);

            var service = new DowngradeWarningService(mockComparator.Object);

            // Act
            // Note: We cannot test the actual dialog without mocking MessageBox,
            // but we can verify that the comparator would be called.
            // The dialog test would require WPF/UI testing frameworks.
            // For unit testing, we verify the comparator is called by checking the return.

            try
            {
                await service.CheckDowngradeAsync("2.1.0", "2.0.0");
            }
            catch
            {
                // Dialog may fail in test environment, but comparator should still be called
            }

            // Assert
            mockComparator.Verify(c => c.IsDowngrade("2.1.0", "2.0.0"), Times.Once);
        }

        [Fact]
        public async Task CheckDowngradeAsync_UsesDefaultComparator_WhenNoneProvided()
        {
            // Arrange
            var service = new DowngradeWarningService(null);

            // Act
            // When no comparator is provided, VersionComparator is used by default
            // For upgrade case (no dialog shown)
            var result = await service.CheckDowngradeAsync("2.0.0", "2.1.0");

            // Assert
            // No downgrade detected with default comparator, so result should be true
            // and no dialog shown
            Assert.True(result);
        }

        [Theory]
        [InlineData("2.0.0", "2.1.0", false)]  // Upgrade
        [InlineData("2.0.0", "2.0.0", false)]  // Same
        public async Task CheckDowngradeAsync_WithDefaultComparator_CorrectlyDetectsNonDowngrade(
            string current, string target, bool shouldShowDialog)
        {
            // Arrange
            var service = new DowngradeWarningService();

            // Act
            var result = await service.CheckDowngradeAsync(current, target);

            // Assert
            // For non-downgrade cases, result should be true and no dialog shown
            Assert.True(result);
        }
    }
}
