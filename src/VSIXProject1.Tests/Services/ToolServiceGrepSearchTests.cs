using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Verifies grep_search filters binary and oversized files before reading so they
    /// never pollute the tool result with junk content.
    /// </summary>
    public class ToolServiceGrepSearchTests
    {
        /// <summary>
        /// Creates a service whose GetWorkspaceFiles returns the given real on-disk files.
        /// ReadFileAsync is mocked to return the file's on-disk content (so the filter can
        /// first sniff the real bytes, then the full content flows through the mock).
        /// </summary>
        private (Mock<IIdeService> ide, Mock<IConfigService> config, ToolService service)
            CreateServiceWithFiles(IEnumerable<string> files)
        {
            var ide = new Mock<IIdeService>();
            ide.Setup(s => s.GetWorkspaceFiles(It.IsAny<string>()))
                .Returns(files);

            ide.Setup(s => s.ReadFileAsync(It.IsAny<string>()))
                .ReturnsAsync((string p) => File.ReadAllText(p));

            var config = new Mock<IConfigService>();
            config.Setup(s => s.GetEnabledTools())
                .Returns(BuiltInToolsRegistry.GetAllBuiltInTools() as IEnumerable<ToolDefinition>);

            var service = new ToolService(ide.Object, config.Object);
            return (ide, config, service);
        }

        private Task<ToolResult> InvokeGrepAsync(ToolService service, string pattern)
        {
            return service.InvokeAsync("grep_search", new Dictionary<string, object>
            {
                { "pattern", pattern }
            });
        }

        private static string GetTempFilePath(string extension, string content)
        {
            var path = Path.Combine(Path.GetTempPath(), "grep_" + Guid.NewGuid() + extension);
            File.WriteAllText(path, content);
            return path;
        }

        private static string GetTempBinaryPath(string extension, byte[] bytes)
        {
            var path = Path.Combine(Path.GetTempPath(), "grep_" + Guid.NewGuid() + extension);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [Fact]
        public async Task GrepSearch_BinaryExtensionFiles_AreSkippedWithoutRead()
        {
            var textFile = GetTempFilePath(".cs", "found needle here\nmore lines\n");
            var binaryFile = GetTempBinaryPath(".dll", new byte[] { 1, 2, 3, 4, 5, 0, 0, 9 });

            try
            {
                var (ide, config, service) = CreateServiceWithFiles(new[] { textFile, binaryFile });

                var result = await InvokeGrepAsync(service, "needle");

                // Only the .cs file should ever be read; the .dll binary is filtered by extension.
                Assert.True(result.IsSuccess);
                Assert.Contains("found needle", result.Output);
                Assert.DoesNotContain(binaryFile, result.Output);

                ide.Verify(s => s.ReadFileAsync(binaryFile), Times.Never);
                ide.Verify(s => s.ReadFileAsync(textFile), Times.AtLeastOnce);
            }
            finally
            {
                if (File.Exists(textFile)) File.Delete(textFile);
                if (File.Exists(binaryFile)) File.Delete(binaryFile);
            }
        }

        [Fact]
        public async Task GrepSearch_NulByteContent_IsSkippedByContentSniff()
        {
            // .dat is not in the binary-extension blocklist, so the content sniff must catch it.
            var fake = GetTempBinaryPath(".dat",
                System.Text.Encoding.ASCII.GetBytes("needle binary").Concat(new byte[] { 0, 0, 0, 0 }).ToArray());

            try
            {
                var (ide, config, service) = CreateServiceWithFiles(new[] { fake });

                var result = await InvokeGrepAsync(service, "needle");

                // NUL-leading binary content is filtered before reading -> no full read, no match.
                Assert.True(result.IsSuccess);
                Assert.Equal("No matches found", result.Output);

                // The content sniff blocks it before the full ReadFileAsync is invoked.
                ide.Verify(s => s.ReadFileAsync(fake), Times.Never);
            }
            finally
            {
                if (File.Exists(fake)) File.Delete(fake);
            }
        }

        [Fact]
        public async Task GrepSearch_TextFileWithMatch_ReturnsLine()
        {
            var textFile = GetTempFilePath(".txt", "alpha beta\nneedle gamma\nomega");

            try
            {
                var (ide, config, service) = CreateServiceWithFiles(new[] { textFile });

                var result = await InvokeGrepAsync(service, "needle");

                Assert.True(result.IsSuccess);
                Assert.Contains("needle gamma", result.Output);
            }
            finally
            {
                if (File.Exists(textFile)) File.Delete(textFile);
            }
        }

        [Fact]
        public async Task GrepSearch_NoMatches_ReturnsNotFound()
        {
            var textFile = GetTempFilePath(".cs", "no match here\n");

            try
            {
                var (ide, config, service) = CreateServiceWithFiles(new[] { textFile });

                var result = await InvokeGrepAsync(service, "needle");

                Assert.True(result.IsSuccess);
                Assert.Equal("No matches found", result.Output);
            }
            finally
            {
                if (File.Exists(textFile)) File.Delete(textFile);
            }
        }

        [Fact]
        public async Task GrepSearch_NullPattern_ReturnsError()
        {
            var (ide, config, service) = CreateServiceWithFiles(Array.Empty<string>());
            var result = await service.InvokeAsync("grep_search", new Dictionary<string, object>
            {
                { "pattern", "" }
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("pattern", result.Output);
        }
    }
}
