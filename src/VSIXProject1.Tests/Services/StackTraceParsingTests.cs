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
        public async Task CppNative_ParseMangledNames_Success()
        {
            var trace = "my.exe!?MyClass@@UEAAXH@Z [C:\\src\\app.cpp:42]\r\n" +
                        "0x401020 kernel32!CreateProcessA";

            var result = await _cppNativeParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            // Should have parsed at least one frame
            var firstFrame = result.Frames[0];
            Assert.Contains("MyClass", firstFrame.MethodName);
        }

        [Fact]
        public async Task CppNative_AddressResolution_Success()
        {
            var trace = "0x401020 module!GetMessage [C:\\Program Files\\app.cpp:123]\r\n" +
                        "0x402030 module!ProcessData [C:\\Program Files\\data.cpp:456]";

            var result = await _cppNativeParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.True(result.Frames.Length >= 1);
            Assert.Equal("C:\\Program Files\\app.cpp", result.Frames[0].FilePath);
            Assert.Equal(123, result.Frames[0].LineNumber);
        }

        [Fact]
        public async Task CppNative_MSVCDebuggerFormat_Success()
        {
            var trace = "kernel32!WaitForSingleObject+0x23 [kernel32.dll:1234]\r\n" +
                        "ntdll!ZwWaitForSingleObject [ntdll.dll:5678]\r\n" +
                        "user32!MessageBoxA";

            var result = await _cppNativeParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            // Should detect Windows API pattern
            var firstFrame = result.Frames[0];
            Assert.True(!string.IsNullOrWhiteSpace(firstFrame.MethodName));
        }

        [Fact]
        public async Task CppNative_CrashDumpFormat_Success()
        {
            var trace = "Exception code: 0xC0000374 (Heap corruption)\r\n" +
                        "0x7FFE0234 ProgramName!MainFunction [main.cpp:100]\r\n" +
                        "0x7FFE0240 ProgramName!HelperFunction [helper.cpp:250]";

            var result = await _cppNativeParser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            Assert.True(result.Frames.Length >= 1);
        }

        [Fact]
        public async Task CppNative_CanParse_DetectsCppFormat()
        {
            var trace = "0x401020 kernel32!CreateProcessA [kernel32.dll:1000]";

            var canParse = _cppNativeParser.CanParse(trace);

            Assert.True(canParse);
        }

        [Fact]
        public async Task CppNative_CanParse_RejectsNonCppFormat()
        {
            var trace = "System.Exception: Error\r\n  at Method() in file.cs:line 42";

            var canParse = _cppNativeParser.CanParse(trace);

            Assert.False(canParse);
        }

        [Fact]
        public async Task CppNative_NullInput_ReturnsError()
        {
            var result = await _cppNativeParser.ParseAsync(null);

            Assert.False(result.IsSuccessful);
            Assert.NotEmpty(result.Errors);
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
