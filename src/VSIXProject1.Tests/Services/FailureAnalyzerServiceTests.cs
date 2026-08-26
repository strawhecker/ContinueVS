using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Enums;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services;

namespace ContinueVS.Tests.Services
{
    public class FailureAnalyzerServiceTests
    {
        private readonly Mock<ILlmService> _mockLlmService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly FailureAnalyzerService _service;

        public FailureAnalyzerServiceTests()
        {
            _mockLlmService = new Mock<ILlmService>();
            _mockLogger = new Mock<IBridgeLogger>();
            _mockLogger.Setup(l => l.WriteInfoAsync(It.IsAny<string>(), null)).Returns(Task.CompletedTask);
            _mockLogger.Setup(l => l.WriteErrorAsync(It.IsAny<string>(), null, null)).Returns(Task.CompletedTask);

            _service = new FailureAnalyzerService(_mockLlmService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task AnalyzeFailureAsync_WithCompilationError_ParsesErrorCorrectly()
        {
            // Arrange
            var compilationError = "MyClass.cs(42, 10): error CS0103: The name 'undefined' does not exist in the current context";
            var previousChange = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "MyClass.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { undefined(); }",
                Description = "Add method"
            };

            SetupLlmMockForHypotheses();

            // Act
            var result = await _service.AnalyzeFailureAsync(
                compilationError,
                previousChange,
                "Additional context",
                false,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.OriginalError);
            Assert.Equal(ErrorType.Compilation, result.OriginalError.ErrorType);
            Assert.Contains("undefined", result.OriginalError.Message);
            Assert.NotEmpty(result.Hypotheses);
            Assert.True(result.ConfidenceScore >= 0.0 && result.ConfidenceScore <= 1.0);
        }

        [Fact]
        public async Task AnalyzeFailureAsync_WithTestAssertion_ParsesTestFailureCorrectly()
        {
            // Arrange
            var testFailure = "Failed: MyNamespace.MyTests.TestMethod\nAssert.Equal() Failure: Expected: 10 Actual: 5";
            var previousChange = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "MyClass.cs",
                OldContent = "return 10;",
                NewContent = "return value * 2;",
                Description = "Fix logic"
            };

            SetupLlmMockForHypotheses();

            // Act
            var result = await _service.AnalyzeFailureAsync(
                testFailure,
                previousChange,
                "",
                true,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ErrorType.TestFailure, result.OriginalError.ErrorType);
            Assert.NotEmpty(result.Hypotheses);
        }

        [Fact]
        public async Task AnalyzeFailureAsync_WithException_ParsesExceptionCorrectly()
        {
            // Arrange
            var exceptionOutput = "NullReferenceException: Object reference not set to an instance of an object.\n   at MyClass.ProcessData(MyClass.cs:15)";
            var previousChange = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "MyClass.cs",
                OldContent = "var result = obj.Value;",
                NewContent = "var result = obj?.Value ?? 0;",
                Description = "Process method"
            };

            SetupLlmMockForHypotheses();

            // Act
            var result = await _service.AnalyzeFailureAsync(
                exceptionOutput,
                previousChange,
                "",
                false,
                CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(ErrorType.Exception, result.OriginalError.ErrorType);
            Assert.Contains("Object reference not set to an instance", result.OriginalError.Message);
            Assert.Equal("NullReferenceException", result.OriginalError.Category);
        }

        [Fact]
        public async Task AnalyzeFailureAsync_WithEmptyErrorOutput_ThrowsArgumentException()
        {
            // Arrange
            var change = new CodeChange { ChangeId = "1", FilePath = "Test.cs", Description = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.AnalyzeFailureAsync("", change, "", false));
        }

        [Fact]
        public async Task AnalyzeFailureAsync_WithNullChange_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.AnalyzeFailureAsync("error message", null, "", false));
        }

        [Fact]
        public async Task AnalyzeFailureAsync_CalculatesConfidenceScore()
        {
            // Arrange
            var errorOutput = "error CS0103: undefined identifier";
            var change = new CodeChange { ChangeId = "1", FilePath = "Test.cs", Description = "Test" };

            SetupLlmMockForHypotheses();

            // Act
            var result = await _service.AnalyzeFailureAsync(
                errorOutput,
                change,
                "",
                false,
                CancellationToken.None);

            // Assert
            Assert.True(result.ConfidenceScore >= 0.0);
            Assert.True(result.ConfidenceScore <= 1.0);
        }

        private void SetupLlmMockForHypotheses()
        {
            var jsonResponse = @"{ ""hypotheses"": [""Hypothesis 1: Add null check before accessing property"", ""Hypothesis 2: Initialize variable before use""] }";

            _mockLlmService
                .Setup(l => l.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(MockAsyncEnumerableAsync(jsonResponse));
        }

        private static async IAsyncEnumerable<CompletionChunk> MockAsyncEnumerableAsync(string content)
        {
            yield return new CompletionChunk { Content = content };
            await Task.CompletedTask;
        }
    }
}
