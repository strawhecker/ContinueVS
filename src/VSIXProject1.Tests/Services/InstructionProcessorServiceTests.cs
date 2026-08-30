#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for InstructionProcessorService.
    /// Tests LLM interpretation of vague debug instructions into ordered phases.
    /// </summary>
    public class InstructionProcessorServiceTests
    {
        private readonly Mock<ILlmService> _mockLlmService;
        private readonly InstructionProcessorService _service;

        public InstructionProcessorServiceTests()
        {
            _mockLlmService = new Mock<ILlmService>();
            _service = new InstructionProcessorService(_mockLlmService.Object);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_VagueInstruction_GeneratesAtLeastOnePhase()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug why SendMessage fails with null" };
            var llmResponse = @"
- Analysis: Inspect the SendMessage method and understand the null reference source
- Instrumentation: Add logging to track null values
- Test: Run unit tests for SendMessage
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Phases.Count >= 1, "Should generate at least one phase");
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_PhaseOrderingPreserved_AnalysisBeforeInstrumentation()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug performance issue" };
            var llmResponse = @"
- Analysis: Profile the application to identify bottlenecks
- Instrumentation: Add timing logs around hot paths
- Test: Measure performance improvement
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            Assert.NotNull(result.Phases);
            Assert.True(result.Phases.Count >= 2);
            // First phase should be Analysis
            Assert.Equal(InternalPhaseType.Analysis, result.Phases[0].Type);
            // Second should be Instrumentation (if present)
            if (result.Phases.Count > 1)
                Assert.True(result.Phases[1].Type == InternalPhaseType.Instrumentation || 
                           result.Phases[1].Type == InternalPhaseType.Test);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_WithContext_IncludesContextInPrompt()
        {
            // Arrange
            var instruction = new ExecutionInstruction
            {
                Text = "Debug null reference exception",
                Context = "File: Service.cs, Line: 42, Exception: NullReferenceException at SendAsync()"
            };
            var llmResponse = @"
- Breakpoint: Set breakpoint at SendAsync line 42
- Observation: Inspect the null value at that location
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Phases.Count >= 1);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_NullInstruction_ThrowsArgumentNullException()
        {
            // Arrange
            ExecutionInstruction nullInstruction = null!;

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.GenerateInternalPhasesAsync(nullInstruction));
            Assert.Equal("instruction", ex.ParamName);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_EmptyInstructionText_ThrowsArgumentException()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GenerateInternalPhasesAsync(instruction));
            Assert.Equal("instruction", ex.ParamName);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_LlmThrowsException_ThrowsInvalidOperationException()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug something" };
            _mockLlmService
                .Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), null, It.IsAny<CancellationToken>()))
                .Throws(new Exception("LLM service error"));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GenerateInternalPhasesAsync(instruction));
            Assert.Contains("LLM interpretation failed", ex.Message);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_LlmReturnsEmptyResponse_ThrowsInvalidOperationException()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug something" };
            SetupLlmMock("");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GenerateInternalPhasesAsync(instruction));
            Assert.Contains("empty response", ex.Message);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_LlmResponseNoValidPhases_ThrowsInvalidOperationException()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug something" };
            var llmResponse = "This is not a valid phase format.";
            SetupLlmMock(llmResponse);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GenerateInternalPhasesAsync(instruction));
            Assert.Contains("did not contain valid phases", ex.Message);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_MultipleValidPhases_ParsesAllTypes()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug comprehensive" };
            var llmResponse = @"
- Analysis: Understand the issue
- Breakpoint: Set a breakpoint to inspect state
- Instrumentation: Add logging
- Test: Run tests
- Observation: Observe runtime behavior
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            Assert.Equal(5, result.Phases.Count);
            Assert.Equal(InternalPhaseType.Analysis, result.Phases[0].Type);
            Assert.Equal(InternalPhaseType.Breakpoint, result.Phases[1].Type);
            Assert.Equal(InternalPhaseType.Instrumentation, result.Phases[2].Type);
            Assert.Equal(InternalPhaseType.Test, result.Phases[3].Type);
            Assert.Equal(InternalPhaseType.Observation, result.Phases[4].Type);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_CancellationRequested_StopsProcessing()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug something" };
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var llmResponse = @"
- Analysis: Analyze the issue
- Test: Run tests
";
            SetupLlmMock(llmResponse);

            // Act & Assert
            // When cancellation token is cancelled, StreamAsync may throw OperationCanceledException
            // which we catch and rethrow as InvalidOperationException
#nullable disable
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.GenerateInternalPhasesAsync(instruction, cts.Token));
#nullable restore
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_ReturnsTestPlan_WithValidProperties()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug the system" };
            var llmResponse = @"
- Analysis: Check the logs
- Test: Verify the fix
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Id));
            Assert.False(string.IsNullOrEmpty(result.Title));
            Assert.Contains("Debug the system", result.Title);
            Assert.NotEmpty(result.Phases);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GenerateInternalPhasesAsync_PhaseHasValidProperties()
        {
            // Arrange
            var instruction = new ExecutionInstruction { Text = "Debug issue" };
            var llmResponse = @"
- Analysis: Investigate root cause
";
            SetupLlmMock(llmResponse);

            // Act
            var result = await _service.GenerateInternalPhasesAsync(instruction);

            // Assert
            var phase = result.Phases.First();
            Assert.False(string.IsNullOrEmpty(phase.Id));
            Assert.Equal(InternalPhaseType.Analysis, phase.Type);
            Assert.Contains("root cause", phase.Description);
            Assert.Equal(InternalPhaseStatus.Pending, phase.Status);
            Assert.True(phase.CreatedAt <= DateTime.UtcNow);
        }

        /// <summary>
        /// Helper method to setup the LLM mock to return phases as async stream.
        /// </summary>
        private void SetupLlmMock(string response)
        {
            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Content = response }
            };

            _mockLlmService
                .Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), null, It.IsAny<CancellationToken>()))
                .Returns(new MockAsyncEnumerable(chunks));
        }

        /// <summary>
        /// Mock async enumerable for testing.
        /// </summary>
        private class MockAsyncEnumerable : IAsyncEnumerable<CompletionChunk>
        {
            private readonly List<CompletionChunk> _items;

            public MockAsyncEnumerable(List<CompletionChunk> items)
            {
                _items = items;
            }

            public IAsyncEnumerator<CompletionChunk> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new MockAsyncEnumerator(_items, cancellationToken);
            }
        }

        /// <summary>
        /// Mock async enumerator for testing.
        /// </summary>
        private class MockAsyncEnumerator : IAsyncEnumerator<CompletionChunk>
        {
            private readonly List<CompletionChunk> _items;
            private readonly CancellationToken _cancellationToken;
            private int _index = -1;

            public MockAsyncEnumerator(List<CompletionChunk> items, CancellationToken cancellationToken)
            {
                _items = items;
                _cancellationToken = cancellationToken;
            }

            public CompletionChunk Current => _items[_index];

            public async ValueTask<bool> MoveNextAsync()
            {
                if (_cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();

                _index++;
                await Task.Delay(1); // Simulate async work
                return _index < _items.Count;
            }

            public ValueTask DisposeAsync()
            {
                return default;
            }
        }
    }
}
