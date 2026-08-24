#nullable enable

using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for Python stack trace parsing (gap29_1c).
    /// Covers standard traceback, pytest format, chained exceptions, and multi-line messages.
    /// </summary>
    public class PythonStackTraceParsingTests
    {
        private readonly PythonStackTraceParser _parser = new PythonStackTraceParser();

        #region CanParse Detection Tests

        [Fact]
        public void CanParse_StandardTraceback_ReturnsTrue()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""app.py"", line 42, in run_analysis
    result = process_data(user_input)
  File ""utils.py"", line 15, in process_data
    validate(data)
ValueError: Invalid data";

            // Act
            bool result = _parser.CanParse(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanParse_PytestFormat_ReturnsTrue()
        {
            // Arrange
            string input = @"E   File ""test_app.py"", line 99, in test_feature
E       assert result == expected
E   AssertionError: Expected 'foo', got 'bar'";

            // Act
            bool result = _parser.CanParse(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanParse_ChainedExceptions_ReturnsTrue()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""handler.py"", line 10, in process
    validate(data)
ValueError: Validation failed

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""handler.py"", line 20, in handle
    cleanup()
RuntimeError: Cleanup failed";

            // Act
            bool result = _parser.CanParse(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanParse_MultilineMessage_ReturnsTrue()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""main.py"", line 100, in execute
    result = complex_operation()
  File ""module.py"", line 50, in complex_operation
    assert condition
AssertionError: Expected A to equal B
  because integration logic failed
  and secondary validation is required";

            // Act
            bool result = _parser.CanParse(input);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void CanParse_EmptyInput_ReturnsFalse()
        {
            // Act
            bool result = _parser.CanParse("");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanParse_NullInput_ReturnsFalse()
        {
            // Act
            bool result = _parser.CanParse(null);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Parse Standard Traceback Tests

        [Fact]
        public async Task Parse_StandardTraceback_ExtractsFrames()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""app.py"", line 42, in run_analysis
    result = process_data(user_input)
  File ""utils.py"", line 15, in process_data
    validate(data)
  File ""validators.py"", line 8, in validate
    raise ValueError(""Invalid data"")
ValueError: Invalid data";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Frames.Length);
            Assert.Equal("app.py", result.Frames[0].FilePath);
            Assert.Equal(42, result.Frames[0].LineNumber);
            Assert.Equal("run_analysis", result.Frames[0].MethodName);
            Assert.Equal("ValueError", result.Frames[0].ExceptionType);
            Assert.Equal("Invalid data", result.Frames[0].ExceptionMessage);
            Assert.Null(result.Frames[1].ExceptionType);
        }

        [Fact]
        public async Task Parse_StandardTraceback_WithoutFunctionName_UsesModule()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""script.py"", line 5
    func()
RuntimeError: Script error";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.Single(result.Frames);
            Assert.Equal("script.py", result.Frames[0].FilePath);
            Assert.Equal(5, result.Frames[0].LineNumber);
            Assert.Equal("<module>", result.Frames[0].MethodName);
        }

        #endregion

        #region Parse Pytest Format Tests

        [Fact]
        public async Task Parse_PytestFormat_StripsEPrefix()
        {
            // Arrange
            string input = @"E   File ""test_app.py"", line 99, in test_feature
E       assert result == expected
E   AssertionError: Expected 'foo', got 'bar'";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.Single(result.Frames);
            Assert.Equal("test_app.py", result.Frames[0].FilePath);
            Assert.Equal(99, result.Frames[0].LineNumber);
            Assert.Equal("test_feature", result.Frames[0].MethodName);
            Assert.Contains("pytest", result.DiagnosticsMessage);
        }

        [Fact]
        public async Task Parse_PytestAssertionError_ExtractsMessage()
        {
            // Arrange
            string input = @"E   File ""test_module.py"", line 42, in test_something
E       assert x == y
E   AssertionError: x != y";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.True(result.Frames.Length >= 1);
            Assert.Contains("pytest", result.DiagnosticsMessage);
        }

        #endregion

        #region Parse Chained Exceptions Tests

        [Fact]
        public async Task Parse_ChainedExceptions_DetectsChain()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""handler.py"", line 10, in process
    validate(data)
  File ""validators.py"", line 8, in validate
    raise ValueError(""Invalid data"")
ValueError: Invalid data

During handling of the above exception, another exception occurred:

Traceback (most recent call last):
  File ""handler.py"", line 20, in handle
    cleanup()
  File ""cleanup.py"", line 5, in cleanup
    destroy()
RuntimeError: Cleanup failed";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.True(result.Frames.Length >= 2);
            Assert.Contains("chained exception: RuntimeError", result.DiagnosticsMessage);
        }

        #endregion

        #region Parse Multiline Message Tests

        [Fact]
        public async Task Parse_MultilineMessage_CollectsFullMessage()
        {
            // Arrange
            string input = @"Traceback (most recent call last):
  File ""config.py"", line 100, in load_config
    validate_settings()
ValueError: Invalid configuration
  - Setting 'timeout' must be positive
  - Setting 'retries' must be > 0";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.Single(result.Frames);
            Assert.Equal("config.py", result.Frames[0].FilePath);
            Assert.NotNull(result.Frames[0].ExceptionMessage);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Parse_NullInput_ReturnsError()
        {
            // Act
            var result = await _parser.ParseAsync(null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Frames);
            Assert.True(result.Errors.Length > 0);
            Assert.Equal(ParseErrorSeverity.Error, result.Errors[0].Severity);
        }

        [Fact]
        public async Task Parse_EmptyInput_ReturnsError()
        {
            // Act
            var result = await _parser.ParseAsync("");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Frames);
            Assert.True(result.Errors.Length > 0);
        }

        [Fact]
        public async Task Parse_NoFrames_ReturnsWarning()
        {
            // Arrange
            string input = "Just some random text without Python traceback format";

            // Act
            var result = await _parser.ParseAsync(input);

            // Assert
            Assert.Empty(result.Frames);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(ParseErrorSeverity.Warning, result.Errors[0].Severity);
        }

        #endregion
    }
}
