using System;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
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
        public async Task HighIterationNumber_ContinuesNormally_WithIterationInLogging()
        {
            // Arrange - iteration limits are controlled by outer orchestrator, not this service
            var testPath = "MyTest.Iteration20";
            var iteration = 20; // User's config allows this; no service-level restriction
            var expectedResult = new TestRunResult(0, "Success after many iterations", "", "Test passed");

            _mockIdeService
                .Setup(s => s.RunTestAsync(testPath, It.IsAny<TestRunOptions>(), default))
                .ReturnsAsync(expectedResult);

            _mockLogger
                .Setup(l => l.WriteInfoAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.AnalyzeFailureAsync(testPath, iteration);

            // Assert - service allows any iteration count; orchestrator enforces limits
            Assert.NotNull(result);
            Assert.True(result.Succeeded);

            // Verify logging includes iteration number
            _mockLogger.Verify(
                l => l.WriteInfoAsync(It.Is<string>(s => s.Contains("iteration") || s.Contains("21"))),
                Times.Once);
        }
    }
}

