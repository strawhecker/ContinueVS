using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// xUnit tests for PlanOutputService (gap43_4, gap84).
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
            await service.SavePlanAsync("My Plan", "# My Plan\nStep 1: Do something.");

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
            var filePath = await service.SavePlanAsync("Implementation Plan", content);

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
            var filePath = await service.SavePlanAsync("Plan title", "# Plan content");

            // Assert
            Assert.True(Path.IsPathRooted(filePath), "Returned path should be absolute.");
            Assert.True(File.Exists(filePath));
        }

        [Fact]
        public async Task SavePlanAsync_FileNameFollowsTitleStemAndTimestampPattern()
        {
            // Arrange
            var service = CreateService();

            // Act
            var filePath = await service.SavePlanAsync("My Great Plan", "# Plan");
            var fileName = Path.GetFileName(filePath);

            // Assert
            Assert.Matches(new Regex(@"^My_Great_Plan_\d{8}_\d{6}\.md$"), fileName);
        }

        [Fact]
        public async Task SavePlanAsync_EmptyTitle_UsesPlanFallbackStem()
        {
            // Arrange
            var service = CreateService();

            // Act
            var filePath = await service.SavePlanAsync("", "# Plan");
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
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync("title", null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync("title", "   "));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SavePlanAsync("title", string.Empty));
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

        [Theory]
        [InlineData("My Plan", "My_Plan")]
        [InlineData("Plan 1 Build Deploy", "Plan_1_Build_Deploy")]
        [InlineData("A/B\\C:D*E?F\"G<H>I|J", "A_B_C_D_E_F_G_H_I_J")]
        [InlineData("", "plan")]
        [InlineData("   ", "plan")]
        [InlineData("Trailing dots...", "Trailing_dots")]
        [InlineData("plan", "plan")]
        public void ToSafeFileStem_ProducesSafeStem(string input, string expected)
        {
            // Act
            var stem = PlanOutputService.ToSafeFileStem(input);

            // Assert
            Assert.Equal(expected, stem);
        }

        [Fact]
        public void ToSafeFileStem_CapsLengthAtSixty()
        {
            // Arrange
            var longTitle = new string('a', 120);

            // Act
            var stem = PlanOutputService.ToSafeFileStem(longTitle);

            // Assert
            Assert.True(stem.Length <= 60);
            Assert.All(stem, c => Assert.DoesNotContain(c, Path.GetInvalidFileNameChars()));
        }
    }
}
