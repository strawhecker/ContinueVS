#nullable enable

using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Infrastructure;

namespace ContinueVS.Tests.Services
{
    public class StackTraceParsingTests : TestFixtureBase
    {
        private readonly DotNetFrameworkStackTraceParser _dotNetFrameworkParser = new();
        private readonly DotNetCoreStackTraceParser _dotNetCoreParser = new();
        private readonly CppNativeStackTraceParser _cppNativeParser = new();
        private readonly JavaScriptStackTraceParser _javaScriptParser = new();
        private readonly PythonStackTraceParser _pythonParser = new();
        private readonly StackTraceFormatDetector _formatDetector;
        private readonly StackTraceService _stackTraceService;

        public StackTraceParsingTests()
        {
            _formatDetector = new StackTraceFormatDetector(
                _dotNetFrameworkParser,
                _dotNetCoreParser,
                _cppNativeParser,
                _javaScriptParser,
                _pythonParser);
            _stackTraceService = new StackTraceService(_formatDetector);
        }

        [Fact]
        public async Task ParseSingleFrameDotNetFramework_Success()
        {
            var trace =
                "System.NullReferenceException: Object reference not set.\r\n" +
                "  at MyService.DoSomething() in C:\\Service.cs:line 42";

            var result = await _dotNetFrameworkParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.Single(result.Frames);
            Assert.Equal("MyService.DoSomething()", result.Frames[0].MethodName);
            Assert.Equal("C:\\Service.cs", result.Frames[0].FilePath);
            Assert.Equal(42, result.Frames[0].LineNumber);
            Assert.Equal("System.NullReferenceException", result.Frames[0].ExceptionType);
        }

        [Fact]
        public async Task ParseMultipleFramesDotNetCore_Success()
        {
            var trace =
                "System.InvalidOperationException: Error\r\n" +
                "  at Service.Process() in C:\\file1.cs:line 100\r\n" +
                "  at Client.Call() in C:\\file2.cs:line 20";

            var result = await _dotNetCoreParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.Equal(2, result.Frames.Length);
            Assert.Equal(0, result.Frames[0].FrameIndex);
            Assert.Equal(1, result.Frames[1].FrameIndex);
        }

        [Fact]
        public async Task FormatDetector_IdentifiesDotNetFramework()
        {
            var trace = "System.Exception: Test\r\n  at Method() in file.cs:line 5";

            var strategies = await _formatDetector.DetectFormatsAsync(trace);

            Assert.NotEmpty(strategies);
            Assert.True(strategies[0].Parser?.Name == "DotNetFramework" || strategies[0].Confidence >= 0.9);
        }

        [Fact]
        public async Task InvalidTrace_ReturnsErrors()
        {
            var invalid = "Not a stack trace";

            var result = await _stackTraceService.ParseAsync(invalid);

            Assert.False(result.IsSuccessful);
            Assert.Empty(result.Frames);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task NullInput_ReturnsError()
        {
            var result = await _dotNetFrameworkParser.ParseAsync(null);

            Assert.False(result.IsSuccessful);
            Assert.Empty(result.Frames);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task CanParse_DotNetFramework_DetectsFormat()
        {
            var trace = "  at Method() in file.cs:line 5";

            var canParse = _dotNetFrameworkParser.CanParse(trace);

            Assert.True(canParse);
        }

        [Fact]
        public async Task CanParse_InvalidFormat_ReturnsFalse()
        {
            var trace = "Random text";

            var canParse = _dotNetFrameworkParser.CanParse(trace);

            Assert.False(canParse);
        }

        [Fact]
        public async Task ExceptionType_ExtractedFromFirstLine()
        {
            var trace = "System.ArgumentNullException: param is null\r\n  at Method() in file.cs:line 10";

            var result = await _dotNetFrameworkParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.Equal("System.ArgumentNullException", result.Frames[0].ExceptionType);
        }

        [Fact]
        public async Task Service_ReturnsFirstSuccessfulParser()
        {
            var trace = "System.Exception: Test\r\n  at Method() in file.cs:line 10";

            var result = await _stackTraceService.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotNull(result.SuccessfulParserName);
            Assert.Equal("DotNetFramework", result.SuccessfulParserName);
        }

        [Fact]
        public async Task ScaffoldedParsers_NotImplemented()
        {
            var result = await _cppNativeParser.ParseAsync("Some C++ trace");

            Assert.False(result.IsSuccessful);
            Assert.Contains("gap29_1a", result.Errors[0].ErrorMessage ?? "");
        }

        [Fact]
        public async Task FormatDetector_ReturnsPrioritizedParsers()
        {
            var trace = "System.Exception\r\n  at Method() in file.cs:line 5";

            var strategies = await _formatDetector.DetectFormatsAsync(trace);

            Assert.NotEmpty(strategies);
            if (strategies.Count > 1)
            {
                Assert.True(strategies[0].Confidence >= strategies[1].Confidence);
            }
        }
    }
}
