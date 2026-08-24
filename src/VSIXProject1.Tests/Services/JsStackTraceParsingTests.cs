#nullable enable

using System;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Tests.Infrastructure;

namespace ContinueVS.Tests.Services
{
    public class JsStackTraceParsingTests : TestFixtureBase
    {
        private readonly JavaScriptStackTraceParser _parser = new();

        [Fact]
        public async Task JsStackTrace_NodeJsFormat_Success()
        {
            var trace =
                "Error: Cannot find module\r\n" +
                "at Function.Module._resolveFilename (internal/modules/cjs/loader.js:902:15)\r\n" +
                "at Module.require (internal/modules/cjs/loader.js:738:15)\r\n" +
                "at require (internal/modules/cjs/loader.js:1144:3)";

            var result = await _parser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            Assert.Equal("Function.Module._resolveFilename", result.Frames[0].MethodName);
            Assert.Equal("internal/modules/cjs/loader.js", result.Frames[0].FilePath);
            Assert.Equal(902, result.Frames[0].LineNumber);
            Assert.Equal(15, result.Frames[0].ColumnNumber);
            Assert.Equal("Error", result.Frames[0].ExceptionType);
        }

        [Fact]
        public async Task JsStackTrace_BrowserConsoleFormat_Success()
        {
            var trace =
                "TypeError: Cannot read property 'foo' of undefined\r\n" +
                "at Object.bar (app.js:45:12)\r\n" +
                "at processData (data.js:10:5)";

            var result = await _parser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            Assert.Equal("TypeError", result.Frames[0].ExceptionType);
            Assert.Contains("app.js", result.Frames[0].FilePath);
            Assert.Equal(45, result.Frames[0].LineNumber);
            Assert.Equal(12, result.Frames[0].ColumnNumber);
        }

        [Fact]
        public async Task JsStackTrace_AsyncStackTrace_Success()
        {
            var trace =
                "Error: Async operation failed\r\n" +
                "at async fetchData (fetch.js:25:14)\r\n" +
                "at async processAsync (process.js:50:7)\r\n" +
                "at async main (main.js:100:3)";

            var result = await _parser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            Assert.Equal("Error", result.Frames[0].ExceptionType);
            Assert.True(result.Frames.Length >= 1);
        }

        [Fact]
        public async Task JsStackTrace_ErrorObjectStack_Success()
        {
            var trace =
                "ReferenceError: variable is not defined\r\n" +
                "at Object.<anonymous> (http://localhost:8080/bundle.js:42:5)\r\n" +
                "at Module._compile (internal/modules/cjs/loader.js:1058:26)";

            var result = await _parser.ParseAsync(trace);

            Assert.True(result.IsSuccessful);
            Assert.NotEmpty(result.Frames);
            Assert.Equal("ReferenceError", result.Frames[0].ExceptionType);
        }

        [Fact]
        public async Task CanParse_NodeJsFormat_ReturnsTrue()
        {
            var trace = "at Function.Module._resolveFilename (loader.js:902:15)";

            var canParse = _parser.CanParse(trace);

            Assert.True(canParse);
        }

        [Fact]
        public async Task CanParse_BrowserFormat_ReturnsTrue()
        {
            var trace = "at Object.foo (https://example.com/app.js:45:12)";

            var canParse = _parser.CanParse(trace);

            Assert.True(canParse);
        }

        [Fact]
        public async Task CanParse_InvalidText_ReturnsFalse()
        {
            var trace = "This is just random text with no stack trace";

            var canParse = _parser.CanParse(trace);

            Assert.False(canParse);
        }

        [Fact]
        public async Task NullInput_ReturnsError()
        {
            var result = await _parser.ParseAsync(null);

            Assert.False(result.IsSuccessful);
            Assert.Empty(result.Frames);
            Assert.NotEmpty(result.Errors);
            Assert.Equal("Input is null or empty", result.Errors[0].ErrorMessage);
        }

        [Fact]
        public async Task EmptyInput_ReturnsError()
        {
            var result = await _parser.ParseAsync("   ");

            Assert.False(result.IsSuccessful);
            Assert.Empty(result.Frames);
            Assert.NotEmpty(result.Errors);
        }
    }
}
