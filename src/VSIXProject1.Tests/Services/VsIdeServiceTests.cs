using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services.Tests
{
    public class VsIdeServiceTests
    {
        [Fact]
        public void Constructor_InitializesService()
        {
            var service = new VsIdeService(null, null, null);
            Assert.NotNull(service);
        }

        [Fact]
        public async Task ReadFileAsync_ThrowsArgumentNullException_WhenFilepathIsNull()
        {
            var service = new VsIdeService(null, null, null);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ReadFileAsync(null!));
        }

        [Fact]
        public async Task ReadFileAsync_ThrowsArgumentNullException_WhenFilepathIsEmpty()
        {
            var service = new VsIdeService(null, null, null);
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.ReadFileAsync(""));
        }

        [Fact]
        public async Task ReadFileAsync_ReturnsContent_WhenFileExists()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
            var expectedContent = "Hello, World!";

            try
            {
                File.WriteAllText(tempFile, expectedContent);

                var service = new VsIdeService(null, null, null);
                var result = await service.ReadFileAsync(tempFile);

                Assert.Equal(expectedContent, result);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ReadFileAsync_ThrowsInvalidOperationException_WhenFileDoesNotExist()
        {
            var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.txt");

            var service = new VsIdeService(null, null, null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadFileAsync(nonExistentFile));
            Assert.Contains("Failed to read file", ex.Message);
        }

        [Fact]
        public async Task ReadFileAsync_ReturnsCorrectContent_ForMultilineFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"multiline_{Guid.NewGuid()}.txt");
            var expectedContent = "Line 1\nLine 2\nLine 3";

            try
            {
                File.WriteAllText(tempFile, expectedContent);

                var service = new VsIdeService(null, null, null);
                var result = await service.ReadFileAsync(tempFile);

                Assert.Equal(expectedContent, result);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
