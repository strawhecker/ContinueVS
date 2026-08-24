using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class TestFailureIterationTests
    {
        private readonly Mock<IIdeService> _mockIdeService;
        private readonly Mock<IBridgeLogger> _mockLogger;
        private readonly ITestFailureService _service;

        public TestFailureIterationTests()
        {
            _mockIdeService = new Mock<IIdeService>();
            _mockLogger = new Mock<IBridgeLogger>();
            _service = new TestFailureService(_mockIdeService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task SingleIterationAnalysis_ReturnsResultWithFrameData()
        {
            // Arrange
            var testPath = "MyTest.ShouldPass";
            var expectedResult = new TestRunResult(0, "Test output", "", "Test passed")
            {
                FrameCount = 2
            };

            _mockIdeService
                .Setup(s => s.RunTestAsync(testPath, It.IsAny<TestRunOptions>(), default))
                .ReturnsAsync(expectedResult);

            _mockLogger
                .Setup(l => l.WriteInfoAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.AnalyzeFailureAsync(testPath, 0);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Test output", result.Stdout);
            Assert.Equal(2, result.FrameCount);
            Assert.True(result.Succeeded);

            _mockIdeService.Verify(
                s => s.RunTestAsync(testPath, It.IsAny<TestRunOptions>(), default),
                Times.Once);
        }

        [Fact]
        public async Task MultiStepAnalysis_IncrementIterationCountAndRefineOutput()
        {
            // Arrange
            var testPath = "MyTest.ShouldFail";
            var result1 = new TestRunResult(1, "First attempt output", "Error 1", "Test failed");
            var result2 = new TestRunResult(0, "Second attempt output", "", "Test passed");

            _mockIdeService
                .SetupSequence(s => s.RunTestAsync(testPath, It.IsAny<TestRunOptions>(), default))
                .ReturnsAsync(result1)
                .ReturnsAsync(result2);

            _mockLogger
                .Setup(l => l.WriteInfoAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act - First iteration
            var firstResult = await _service.AnalyzeFailureAsync(testPath, 0);

            // Assert - First iteration
            Assert.NotNull(firstResult);
            Assert.Equal(1, firstResult.ExitCode);
            Assert.False(firstResult.Succeeded);

            // Act - Second iteration
            var secondResult = await _service.AnalyzeFailureAsync(testPath, 1);

            // Assert - Second iteration
            Assert.NotNull(secondResult);
            Assert.Equal(0, secondResult.ExitCode);
            Assert.True(secondResult.Succeeded);

            _mockLogger.Verify(l => l.WriteInfoAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task StopAfter5Iterations_ThrowsTestAnalysisException()
        {
            // Arrange
            var testPath = "MyTest.PersistentlyFailing";

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TestAnalysisException>(
                () => _service.AnalyzeFailureAsync(testPath, 5));

            Assert.NotNull(ex);
            Assert.Equal(5, ex.IterationCount);
            Assert.Contains("exceeded maximum 5 iterations", ex.Message);

            _mockIdeService.Verify(
                s => s.RunTestAsync(It.IsAny<string>(), It.IsAny<TestRunOptions>(), default),
                Times.Never);
        }
    }
}
