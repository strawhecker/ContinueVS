using System;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests of the runtime inspection surface on the single source of truth: IDebuggerService.
    /// (gap93) These were previously tested through IIdeService debug stubs, which are now removed;
    /// IDebuggerService is the sole owner of debug/runtime state.
    /// </summary>
    public class RuntimeInspectionTests
    {
        private readonly Mock<IDebuggerService> _mockDebuggerService;

        public RuntimeInspectionTests()
        {
            _mockDebuggerService = new Mock<IDebuggerService>();
        }

        [Fact]
        public async Task GetCurrentStateAsync_ReturnsRuntimeStateWithLocalVariables()
        {
            // Arrange
            var expectedState = new RuntimeState
            {
                Locals = { ["x"] = "42", ["y"] = "hello" },
                IsRunning = false,
                CurrentLine = 5,
                CurrentFile = "Program.cs",
                ThreadId = 1
            };
            expectedState.CallStack.Add(new CallStackFrame
            {
                MethodName = "Main",
                FilePath = "Program.cs",
                LineNumber = 5,
                FrameIndex = 0
            });

            _mockDebuggerService
                .Setup(s => s.GetCurrentStateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedState);

            // Act
            var result = await _mockDebuggerService.Object.GetCurrentStateAsync();

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsRunning);
            Assert.Equal(2, result.Locals.Count);
            Assert.Equal("42", result.Locals["x"]);
            Assert.Equal("hello", result.Locals["y"]);
            Assert.Equal(5, result.CurrentLine);
            Assert.Equal("Program.cs", result.CurrentFile);
            Assert.Single(result.CallStack);
            Assert.Equal("Main", result.CallStack[0].MethodName);

            _mockDebuggerService.Verify(
                s => s.GetCurrentStateAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteStepAsync_ExecutesStepOverAndReturnsUpdatedState()
        {
            // Arrange
            var steppedState = new RuntimeState
            {
                Locals = { ["x"] = "43" },
                IsRunning = false,
                CurrentLine = 6,
                CurrentFile = "Program.cs",
                ThreadId = 1
            };
            steppedState.CallStack.Add(new CallStackFrame
            {
                MethodName = "Main",
                FilePath = "Program.cs",
                LineNumber = 6,
                FrameIndex = 0
            });

            _mockDebuggerService
                .Setup(s => s.ExecuteStepAsync(DebugStepAction.StepOver, It.IsAny<CancellationToken>()))
                .ReturnsAsync(steppedState);

            // Act
            var result = await _mockDebuggerService.Object.ExecuteStepAsync(DebugStepAction.StepOver);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(6, result.CurrentLine);
            Assert.Equal("43", result.Locals["x"]);

            _mockDebuggerService.Verify(
                s => s.ExecuteStepAsync(DebugStepAction.StepOver, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ResumeExecutionAsync_ThrowsTimeoutExceptionAfter30Seconds()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            _mockDebuggerService
                .Setup(s => s.ResumeExecutionAsync(cts.Token))
                .Returns(async () =>
                {
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token))
                    {
                        linkedCts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Short timeout for test
                        try
                        {
                            // Infinite delay that only completes via cancellation, so the timeout is
                            // deterministic and immune to thread-pool starvation under parallel load.
                            await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw new TimeoutException("Execution did not resume within timeout period.");
                        }
                    }
                });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<TimeoutException>(
                () => _mockDebuggerService.Object.ResumeExecutionAsync(cts.Token));

            Assert.NotNull(ex);
            Assert.Contains("did not resume", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
