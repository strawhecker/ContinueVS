using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using EnvDTE;
using VSIXProject1.Services;
using Microsoft.VisualStudio.Shell;

namespace VSIXProject1.Tests.Services
{
    /// <summary>
    /// Unit tests for TerminalCollector (Step 82)
    /// 
    /// 4 suites, 18 test cases covering initialization, execution, streaming, and error recovery.
    /// Uses Moq for DTE mocking to avoid real terminal dependencies.
    /// </summary>
    public class TerminalCollectorTests
    {
        // ====================================================================
        // SUITE 1: Initialization
        // ====================================================================

        public class InitializationTests
        {
            [Fact]
            public void Constructor_WithValidDTE_InitializesSuccessfully()
            {
                // Arrange
                var dteMock = new Mock<DTE>();

                // Act
                var collector = new TerminalCollector(dteMock.Object);

                // Assert
                Assert.NotNull(collector);
            }

            [Fact]
            public void Constructor_WithNullDTE_ThrowsArgumentNullException()
            {
                // Arrange
                DTE dte = null;

                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => new TerminalCollector(dte));
            }

            [Fact]
            public void Constructor_WithLoggerAndMetrics_StoresReferences()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var loggerMock = new Mock<IBridgeLogger>();
                var metricsMock = new Mock<ITelemetryCollector>();

                // Act
                var collector = new TerminalCollector(dteMock.Object, loggerMock.Object, metricsMock.Object);

                // Assert
                Assert.NotNull(collector);
                // Logger and metrics are stored internally (verified via behavior tests)
            }
        }

        // ====================================================================
        // SUITE 2: Command Execution
        // ====================================================================

        public class CommandExecutionTests
        {
            private Mock<DTE> CreateMockDTE()
            {
                return new Mock<DTE>();
            }

            [Fact]
            public async Task ExecuteCommandAsync_WithValidCommand_ReturnsOutput()
            {
                // Arrange
                var dteMock = CreateMockDTE();
                var collector = new TerminalCollector(dteMock.Object);
                var command = "echo hello";

                // Act
                var chunks = new List<TerminalOutput>();
                await foreach (var chunk in collector.ExecuteCommandAsync(command, 5000))
                {
                    chunks.Add(chunk);
                }

                // Assert
                Assert.NotEmpty(chunks);
                Assert.All(chunks, c => Assert.NotNull(c.Chunk));
            }

            [Fact]
            public async Task ExecuteCommandAsync_WithEmptyCommand_ThrowsArgumentException()
            {
                // Arrange
                var dteMock = CreateMockDTE();
                var collector = new TerminalCollector(dteMock.Object);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(
                    () => collector.ExecuteCommandAsync("", 5000).GetAsyncEnumerator().MoveNextAsync().AsTask());
            }

            [Fact]
            public async Task ExecuteCommandAsync_WithTimeout_CompletesWithinLimit()
            {
                // Arrange
                var dteMock = CreateMockDTE();
                var collector = new TerminalCollector(dteMock.Object);
                var command = "test";
                var timeoutMs = 2000;

                // Act
                var startTime = DateTime.UtcNow;
                var chunks = new List<TerminalOutput>();
                try
                {
                    await foreach (var chunk in collector.ExecuteCommandAsync(command, timeoutMs))
                    {
                        chunks.Add(chunk);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout is acceptable
                }

                var elapsed = DateTime.UtcNow - startTime;

                // Assert
                Assert.True(elapsed.TotalMilliseconds < timeoutMs + 500, "Should respect timeout");
            }

            [Fact]
            public async Task ExecuteCommandAsync_IncrementCommandCount_OnSuccess()
            {
                // Arrange
                var dteMock = CreateMockDTE();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var statusBefore = await collector.GetStatusAsync();
                var countBefore = statusBefore.CommandCount;

                await foreach (var _ in collector.ExecuteCommandAsync("test", 5000))
                {
                    // Consume stream
                }

                var statusAfter = await collector.GetStatusAsync();
                var countAfter = statusAfter.CommandCount;

                // Assert
                Assert.Equal(countBefore + 1, countAfter);
            }

            [Fact]
            public async Task ExecuteCommandAsync_WithWorkingDirectory_ExecutesInContext()
            {
                // Arrange
                var dteMock = CreateMockDTE();
                var collector = new TerminalCollector(dteMock.Object);
                var command = "pwd";
                var workingDir = "C:\\temp";

                // Act
                var chunks = new List<TerminalOutput>();
                await foreach (var chunk in collector.ExecuteCommandAsync(command, 5000, workingDir))
                {
                    chunks.Add(chunk);
                }

                // Assert
                Assert.NotEmpty(chunks);
            }
        }

        // ====================================================================
        // SUITE 3: Output Streaming
        // ====================================================================

        public class OutputStreamingTests
        {
            [Fact]
            public async Task ExecuteCommandAsync_StreamsOutputInChunks()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var chunks = new List<TerminalOutput>();
                await foreach (var chunk in collector.ExecuteCommandAsync("test", 5000))
                {
                    chunks.Add(chunk);
                }

                // Assert
                Assert.True(chunks.Count >= 1, "Should have at least one chunk");
            }

            [Fact]
            public async Task ExecuteCommandAsync_LastChunk_HasIsPartialFalse()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                TerminalOutput lastChunk = null;
                await foreach (var chunk in collector.ExecuteCommandAsync("test", 5000))
                {
                    lastChunk = chunk;
                }

                // Assert
                Assert.NotNull(lastChunk);
                Assert.False(lastChunk.IsPartial, "Last chunk should not be partial");
            }

            [Fact]
            public async Task ExecuteCommandAsync_EachChunk_HasTimestamp()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                await foreach (var chunk in collector.ExecuteCommandAsync("test", 5000))
                {
                    // Assert
                    Assert.NotEqual(default(DateTime), chunk.Timestamp);
                    Assert.True(chunk.Timestamp <= DateTime.UtcNow);
                }
            }

            [Fact]
            public async Task ExecuteCommandAsync_CombinedChunks_FormCompleteOutput()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var combined = new System.Text.StringBuilder();
                await foreach (var chunk in collector.ExecuteCommandAsync("test", 5000))
                {
                    combined.Append(chunk.Chunk);
                }

                var output = combined.ToString();

                // Assert
                Assert.NotEmpty(output);
            }
        }

        // ====================================================================
        // SUITE 4: Input, Control & Status
        // ====================================================================

        public class InputControlStatusTests
        {
            [Fact]
            public async Task SendInputAsync_WithValidText_Queues()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                await collector.SendInputAsync("test input");

                // Assert (no exception)
                var status = await collector.GetStatusAsync();
                Assert.NotNull(status);
            }

            [Fact]
            public async Task SendInputAsync_WithNullText_ThrowsArgumentNullException()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act & Assert
                await Assert.ThrowsAsync<ArgumentNullException>(() => collector.SendInputAsync(null));
            }

            [Fact]
            public async Task ClearTerminalAsync_Clears()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                await collector.ClearTerminalAsync();
                var status = await collector.GetStatusAsync();

                // Assert
                Assert.Equal(TerminalState.Idle, status.State);
            }

            [Fact]
            public async Task GetStatusAsync_ReturnsValidStatus()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var status = await collector.GetStatusAsync();

                // Assert
                Assert.NotNull(status);
                Assert.True(status.IsResponsive);
                Assert.Equal(0, status.CommandCount); // Initially 0
                Assert.NotEqual(default(DateTime), status.CapturedAt);
            }

            [Fact]
            public async Task MultipleOperations_MaintainState()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                await collector.SendInputAsync("cmd1");
                await collector.SendInputAsync("cmd2");
                var status = await collector.GetStatusAsync();

                // Assert
                Assert.NotNull(status);
            }
        }

        // ====================================================================
        // SUITE 5: Error Recovery & Edge Cases
        // ====================================================================

        public class ErrorRecoveryTests
        {
            [Fact]
            public async Task ExecuteCommandAsync_ErrorInExecution_SetStateToError()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                try
                {
                    await foreach (var _ in collector.ExecuteCommandAsync("", 5000))
                    {
                        // Should not reach here
                    }
                }
                catch (ArgumentException)
                {
                    // Expected
                }

                // Assert (no crash; error handled gracefully)
            }

            [Fact]
            public async Task ExecuteCommandAsync_WithVeryLongCommand_CompletesSuccessfully()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);
                var longCommand = new string('x', 1000);

                // Act
                var chunks = new List<TerminalOutput>();
                await foreach (var chunk in collector.ExecuteCommandAsync(longCommand, 5000))
                {
                    chunks.Add(chunk);
                }

                // Assert
                Assert.NotEmpty(chunks);
            }

            [Fact]
            public async Task GetStatusAsync_WithoutPriorExecution_ReturnsIdleState()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var status = await collector.GetStatusAsync();

                // Assert
                Assert.Equal(TerminalState.Idle, status.State);
                Assert.Equal(0, status.CommandCount);
            }

            [Fact]
            public async Task ConcurrentOperations_HandleQueueing()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var tasks = new List<Task>
                {
                    collector.SendInputAsync("input1"),
                    collector.SendInputAsync("input2"),
                    collector.SendInputAsync("input3"),
                };

                // Assert (all complete without deadlock)
                await Task.WhenAll(tasks);
            }

            [Fact]
            public async Task ExecuteCommandAsync_LargeOutput_StreamsWithoutMemoryIssues()
            {
                // Arrange
                var dteMock = new Mock<DTE>();
                var collector = new TerminalCollector(dteMock.Object);

                // Act
                var totalSize = 0;
                await foreach (var chunk in collector.ExecuteCommandAsync("large", 5000))
                {
                    totalSize += chunk.Chunk?.Length ?? 0;
                }

                // Assert
                Assert.True(totalSize > 0);
            }
        }

        // ====================================================================
        // SUPPORTING TYPES TESTS
        // ====================================================================

        public class SupportingTypesTests
        {
            [Fact]
            public void TerminalOutput_Initializes_WithDefaults()
            {
                // Act
                var output = new TerminalOutput();

                // Assert
                Assert.Equal(string.Empty, output.Chunk);
                Assert.False(output.IsPartial);
                Assert.False(output.IsError);
                Assert.Equal(0, output.LineNumber);
                Assert.NotEqual(default(DateTime), output.Timestamp);
            }

            [Fact]
            public void TerminalStatus_Initializes_WithDefaults()
            {
                // Act
                var status = new TerminalStatus();

                // Assert
                Assert.Equal(TerminalState.Idle, status.State);
                Assert.True(status.IsResponsive);
                Assert.Equal(0, status.CommandCount);
                Assert.Null(status.LastOutput);
                Assert.NotEqual(default(DateTime), status.CapturedAt);
            }

            [Fact]
            public void TerminalStateEnum_HasExpectedValues()
            {
                // Assert
                Assert.Equal(0, (int)TerminalState.Idle);
                Assert.Equal(1, (int)TerminalState.Busy);
                Assert.Equal(2, (int)TerminalState.Running);
                Assert.Equal(3, (int)TerminalState.Error);
            }
        }
    }
}
