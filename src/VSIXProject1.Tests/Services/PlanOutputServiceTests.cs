using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// xUnit tests for PlanOutputService (gap43_4).
    /// Validates plan file persistence behavior in isolation using temp directories.
    /// </summary>
    public class PlanOutputServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public PlanOutputServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private PlanOutputService CreateService() => new PlanOutputService(_tempDir);

        [Fact]
        public async Task SavePlanAsync_CreatesPlansDirectory_WhenNotExists()
        {
            // Arrange
            var service = CreateService();
            Assert.False(Directory.Exists(service.GetPlansDirectory()));

            // Act
            await service.SavePlanAsync("# My Plan\nStep 1: Do something.");

            // Assert
            Assert.True(Directory.Exists(service.GetPlansDirectory()));
        }

        [Fact]
        public async Task SavePlanAsync_WritesContentToFile()
        {
            // Arrange
            var service = CreateService();
            const string content = "# Implementation Plan\n\n## Steps\n1. Create service\n2. Wire DI";

            // Act
            var filePath = await service.SavePlanAsync(content);

            // Assert
            Assert.True(File.Exists(filePath));
            var written = File.ReadAllText(filePath);
            Assert.Equal(content, written);
        }

        [Fact]
        public async Task SavePlanAsync_ReturnsAbsoluteFilePath()
        {
            // Arrange
            var service = CreateService();

            // Act
            var filePath = await service.SavePlanAsync("# Plan content");

            // Assert
            Assert.True(Path.IsPathRooted(filePath), "Returned path should be absolute.");
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task SavePlanAsync_FileNameFollowsTimestampPattern()
        {
            // Arrange
            var service = CreateService();

            // Act
            var filePath = await service.SavePlanAsync("# Plan");
            var fileName = Path.GetFileName(filePath);

            // Assert
            Assert.Matches(new Regex(@"^plan_\d{8}_\d{6}\.md$"), fileName);
        }

        [Fact]
        public async Task SavePlanAsync_ThrowsArgumentException_WhenContentIsNullOrWhitespace()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync("   "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync(string.Empty));
        }

        [Fact]
        public void GetPlansDirectory_ReturnsPathEndingWithPlans()
        {
            // Arrange
            var service = CreateService();

            // Act
            var dir = service.GetPlansDirectory();

            // Assert
            Assert.EndsWith("plans", dir, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(_tempDir, dir, StringComparison.OrdinalIgnoreCase);
        }
    }
}
