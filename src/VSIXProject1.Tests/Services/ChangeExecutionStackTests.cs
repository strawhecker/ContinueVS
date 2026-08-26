using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Enums;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services;
using ContinueConfig = ContinueVS.Core.Types.ContinueConfig;

namespace ContinueVS.Tests.Services
{
    public class ChangeExecutionStackTests
    {
        private readonly Mock<IChangeStackService> _mockChangeStack;
        private readonly Mock<IFailureAnalyzerService> _mockFailureAnalyzer;
        private readonly Mock<IConfigService> _mockConfigService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly ChangeExecutionStack _service;

        public ChangeExecutionStackTests()
        {
            _mockChangeStack = new Mock<IChangeStackService>();
            _mockFailureAnalyzer = new Mock<IFailureAnalyzerService>();
            _mockConfigService = new Mock<IConfigService>();
            _mockLogger = new Mock<IBridgeLogger>();

            // Setup logger to not throw
            _mockLogger.Setup(l => l.WriteInfoAsync(It.IsAny<string>(), null)).Returns(Task.CompletedTask);
            _mockLogger.Setup(l => l.WriteErrorAsync(It.IsAny<string>(), null, null)).Returns(Task.CompletedTask);

            // Setup config service to return default config
            var defaultConfig = new ContinueConfig { MaxRetriesPerChange = 3 };
            _mockConfigService.Setup(c => c.GetCurrentConfig())
                .Returns(defaultConfig);

            _service = new ChangeExecutionStack(
                _mockChangeStack.Object,
                _mockFailureAnalyzer.Object,
                _mockConfigService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task AttemptChangeAsync_WhenChangeSucceedsOnFirstAttempt_ReturnsSuccess()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { Console.WriteLine(\"test\"); }",
                Description = "Add logging"
            };
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";

            _mockChangeStack.Setup(c => c.ApplyChangeAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.AttemptChangeAsync(
                change,
                changeStack,
                filePath,
                isAutonomousMode: true,
                CancellationToken.None);

            // Assert
            Assert.Equal(ChangeExecutionResult.StatusCode.Success, result.Status);
            Assert.Equal(1, result.ExecutedAttemptCount);
            Assert.NotNull(result.FinalChange);
            Assert.Equal(change.ChangeId, result.FinalChange.ChangeId);
            Assert.Empty(result.RefinementHistory);
            Assert.Contains("successfully", result.Evidence, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AttemptChangeAsync_WhenChangeFailsOnFirstAttemptThenSucceedsAfterRefinement_ReturnsRetriedSuccess()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { undefined(); }",
                Description = "Initial change"
            };
            var refinedChange = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { int x = 42; Console.WriteLine(x); }",
                Description = "Refined change"
            };
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";

            var callCount = 0;
            _mockChangeStack.Setup(c => c.ApplyChangeAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>()))
                .Callback(() => callCount++)
                .Returns<string, CodeChange, string>((id, ch, path) =>
                {
                    if (callCount == 1)
                    {
                        // First call fails
                        throw new InvalidOperationException("Compilation error: undefined method");
                    }
                    // Second call (refined) succeeds
                    return Task.CompletedTask;
                });

            var refinementAttempt = new RefinementAttempt(
                new ErrorAnalysisResult(ErrorType.Compilation, "undefined method", "Compilation")
                {
                    FilePath = "Test.cs",
                    LineNumber = 1
                },
                attemptNumber: 1)
            {
                Hypotheses = new List<string> { "Method name is incorrect", "Missing using statement" },
                RefinedChange = refinedChange,
                ConfidenceScore = 0.85,
                ApproachDescription = "Replace undefined method with proper implementation"
            };

            _mockFailureAnalyzer.Setup(f => f.AnalyzeFailureAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(refinementAttempt);

            // Act
            var result = await _service.AttemptChangeAsync(
                change,
                changeStack,
                filePath,
                isAutonomousMode: true,
                CancellationToken.None);

            // Assert
            Assert.Equal(ChangeExecutionResult.StatusCode.RetriedSuccess, result.Status);
            Assert.Equal(2, result.ExecutedAttemptCount);
            Assert.NotNull(result.FinalChange);
            Assert.Equal(refinedChange.ChangeId, result.FinalChange.ChangeId);
            Assert.Single(result.RefinementHistory);
            Assert.Equal(0.85, result.RefinementHistory[0].ConfidenceScore);
        }

        [Fact]
        public async Task AttemptChangeAsync_WhenChangeFailsAllAttempts_ReturnsRetryThresholdExceededWithoutRollback()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "INVALID CODE",
                Description = "Broken change"
            };
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";

            _mockChangeStack.Setup(c => c.ApplyChangeAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>()))
                .Throws(new InvalidOperationException("Persistent compilation error"));

            var refinementAttempt = new RefinementAttempt(
                new ErrorAnalysisResult(ErrorType.Compilation, "syntax error", "Compilation"),
                attemptNumber: 1)
            {
                Hypotheses = new List<string> { "Syntax is invalid" },
                RefinedChange = null,
                ConfidenceScore = 0.1,
                ApproachDescription = "No viable refinement found"
            };

            _mockFailureAnalyzer.Setup(f => f.AnalyzeFailureAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(refinementAttempt);

            // Act
            var result = await _service.AttemptChangeAsync(
                change,
                changeStack,
                filePath,
                isAutonomousMode: true,
                CancellationToken.None);

            // Assert
            Assert.Equal(ChangeExecutionResult.StatusCode.RetryThresholdExceeded, result.Status);
            Assert.Equal(3, result.ExecutedAttemptCount);
            Assert.NotNull(result.FinalChange);
            Assert.Contains("failed after", result.Evidence, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no automatic rollback", result.Evidence, StringComparison.OrdinalIgnoreCase);

            // Verify RollbackChangeAsync was never called
            _mockChangeStack.Verify(c => c.RollbackChangeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task AttemptChangeAsync_WhenExecutionCancelled_ReturnsExecutionCancelled()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { /* modified */ }",
                Description = "Test change"
            };
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _service.AttemptChangeAsync(
                change,
                changeStack,
                filePath,
                isAutonomousMode: true,
                cts.Token);

            // Assert
            Assert.Equal(ChangeExecutionResult.StatusCode.ExecutionCancelled, result.Status);
            Assert.Contains("cancelled", result.Evidence, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AttemptChangeAsync_WithNullChange_ThrowsArgumentNullException()
        {
            // Arrange
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.AttemptChangeAsync(null!, changeStack, filePath, true));
        }

        [Fact]
        public async Task AttemptChangeAsync_WithNullChangeStack_ThrowsArgumentNullException()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "old",
                NewContent = "new",
                Description = "test"
            };
            var filePath = "Test.cs";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.AttemptChangeAsync(change, null!, filePath, true));
        }

        [Fact]
        public async Task AttemptChangeAsync_WithEmptyFilePath_ThrowsArgumentException()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "old",
                NewContent = "new",
                Description = "test"
            };
            var changeStack = new ChangeStack();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.AttemptChangeAsync(change, changeStack, string.Empty, true));
        }

        [Fact]
        public async Task AttemptChangeAsync_DuringRetry_RefinementHistoryIsPopulated()
        {
            // Arrange
            var change = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { undefined(); }",
                Description = "Initial change"
            };
            var refinedChange = new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = "Test.cs",
                OldContent = "public void Foo() { }",
                NewContent = "public void Foo() { int x = 42; }",
                Description = "Refined"
            };
            var changeStack = new ChangeStack();
            var filePath = "Test.cs";

            var callCount = 0;
            _mockChangeStack.Setup(c => c.ApplyChangeAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>()))
                .Callback(() => callCount++)
                .Returns<string, CodeChange, string>((id, ch, path) =>
                {
                    if (callCount == 1)
                        throw new InvalidOperationException("Error");
                    return Task.CompletedTask;
                });

            var refinementAttempt = new RefinementAttempt(
                new ErrorAnalysisResult(ErrorType.Compilation, "undefined", "Compilation"),
                attemptNumber: 1)
            {
                RefinedChange = refinedChange,
                ConfidenceScore = 0.75,
                ApproachDescription = "Refined based on error analysis"
            };

            _mockFailureAnalyzer.Setup(f => f.AnalyzeFailureAsync(
                It.IsAny<string>(),
                It.IsAny<CodeChange>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(refinementAttempt);

            // Act
            var result = await _service.AttemptChangeAsync(
                change,
                changeStack,
                filePath,
                isAutonomousMode: true,
                CancellationToken.None);

            // Assert
            Assert.Single(result.RefinementHistory);
            var attempt = result.RefinementHistory[0];
            Assert.NotNull(attempt.OriginalError);
            Assert.Equal(ErrorType.Compilation, attempt.OriginalError.ErrorType);
            Assert.NotNull(attempt.RefinedChange);
            Assert.Equal(0.75, attempt.ConfidenceScore);
            Assert.Equal("Refined based on error analysis", attempt.ApproachDescription);
        }
    }
}
