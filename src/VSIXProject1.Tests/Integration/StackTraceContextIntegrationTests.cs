#nullable enable

using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;

namespace ContinueVS.Tests.Integration
{
    public class StackTraceContextIntegrationTests : TestFixtureBase
    {
        private readonly DotNetFrameworkStackTraceParser _frameParser = new();
        private readonly DotNetCoreStackTraceParser _coreParser = new();
        private readonly CppNativeStackTraceParser _cppParser = new();
        private readonly JavaScriptStackTraceParser _jsParser = new();
        private readonly PythonStackTraceParser _pyParser = new();
        private readonly StackTraceFormatDetector _detector;
        private readonly StackTraceService _service;

        public StackTraceContextIntegrationTests()
        {
            _detector = new StackTraceFormatDetector(
                _frameParser,
                _coreParser,
                _cppParser,
                _jsParser,
                _pyParser);

            _service = new StackTraceService(_detector);
        }

        [Fact]
        public async Task ParseResult_FramesConvertToContextItems()
        {
            var trace =
                "System.ArgumentException: Invalid\r\n" +
                "  at MyClass.Method(String arg) in C:\\file.cs:line 42";

            var result = await _service.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.Single(result.Frames);

            var frame = result.Frames[0];
            var contextItem = new ContextItem
            {
                Type = ContextItemType.File,
                FilePath = frame.FilePath,
                StartLine = frame.LineNumber,
                EndLine = frame.LineNumber,
                Content = $"{frame.MethodName} (line {frame.LineNumber})",
                Source = "StackTraceParser",
                Relevance = 0.95
            };

            Assert.NotNull(contextItem.FilePath);
            Assert.Equal(42, contextItem.StartLine);
        }

        [Fact]
        public async Task MultipleFrames_CreateMultipleContextItems()
        {
            var trace =
                "System.Exception: Error\r\n" +
                "  at Service.Method1() in file1.cs:line 10\r\n" +
                "  at Client.Method2() in file2.cs:line 20";

            var result = await _service.ParseAsync(trace);

            Assert.Equal(2, result.Frames.Length);
            Assert.All(result.Frames, frame =>
            {
                Assert.NotNull(frame.FilePath);
                Assert.NotNull(frame.MethodName);
                Assert.True(frame.LineNumber > 0);
            });
        }

        [Fact]
        public async Task FrameFilePaths_AvailableForSymbolLinking()
        {
            var trace =
                "System.IOException: File not found\r\n" +
                "  at FileReader.ReadFile(String path) in C:\\Source\\FileReader.cs:line 88";

            var mockIdeService = new Mock<IIdeService>();
            mockIdeService
                .Setup(s => s.OpenFileInEditorAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(true));

            var result = await _service.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            var frame = result.Frames[0];
            Assert.NotNull(frame.FilePath);
            Assert.Equal(88, frame.LineNumber);

            await mockIdeService.Object.OpenFileInEditorAsync(frame.FilePath);
            mockIdeService.Verify(s => s.OpenFileInEditorAsync(frame.FilePath), Times.Once);
        }

        [Fact]
        public async Task ExceptionMetadata_CapturedInFrames()
        {
            var trace =
                "System.UnauthorizedAccessException: Access denied\r\n" +
                "  at FileSystem.CheckPermissions() in C:\\permission.cs:line 15";

            var result = await _service.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            var frame = result.Frames[0];
            Assert.Equal("System.UnauthorizedAccessException", frame.ExceptionType);
            Assert.Contains("denied", frame.ExceptionMessage ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ParseErrors_IncludeDiagnostics()
        {
            var invalidTrace = "Not a stack trace";

            var result = await _service.ParseAsync(invalidTrace);

            Assert.False(result.IsSuccessful);
            Assert.NotEmpty(result.Errors);
            Assert.NotNull(result.DiagnosticsMessage);
        }

        [Fact]
        public async Task SuccessfulParserName_IndicatesWichParserSucceeded()
        {
            var trace = "System.Exception: Test\r\n  at Method() in file.cs:line 10";

            var result = await _service.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotNull(result.SuccessfulParserName);
            Assert.Equal("DotNetFramework", result.SuccessfulParserName);
        }

        [Fact]
        public async Task DiagnosticsMessage_ProvidesFriendlyFailureInfo()
        {
            var invalidTrace = "This will not parse";

            var result = await _service.ParseAsync(invalidTrace);

            Assert.False(result.IsSuccessful);
            Assert.NotNull(result.DiagnosticsMessage);
            Assert.NotEmpty(result.DiagnosticsMessage);
        }
    }
}
