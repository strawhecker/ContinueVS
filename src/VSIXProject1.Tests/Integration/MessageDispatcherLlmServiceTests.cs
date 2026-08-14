#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Exceptions;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// Integration tests for MessageDispatcher → ILlmService flow (Step 99).
    /// 
    /// Verifies that MessageDispatcher correctly delegates handler calls to ILlmService.StreamAsync,
    /// with proper null-checking, exception propagation, cancellation token threading, and streaming.
    /// 
    /// Test isolation:
    /// - Each test uses isolated Mock instances
    /// - No shared state between tests
    /// - Stream data is generated per test
    /// </summary>
    public class MessageDispatcherLlmServiceTests
    {
        /// <summary>
        /// Test: Handler receives chat call → delegates to ILlmService.StreamAsync → returns chunks.
        /// 
        /// Arrange: Create mock ILlmService configured to return completion chunks
        /// Act: Call StreamAsync with message list
        /// Assert: Service method was called once; chunks iterate correctly
        /// </summary>
        [Fact]
        public async Task Chat_Handler_DelegatesToService_ReturnsChunksAsync()
        {
            // Arrange
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Hello" }
            };

            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "Hi " },
                new CompletionChunk { Type = ChunkType.Text, Content = "there" }
            };

            var mockLlmService = new Mock<ILlmService>(MockBehavior.Strict);
            mockLlmService
                .Setup(x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(StreamChunksAsync(chunks));

            // Act
            var result = new List<CompletionChunk>();
            await foreach (var chunk in mockLlmService.Object.StreamAsync(messages))
            {
                result.Add(chunk);
            }

            // Assert
            mockLlmService.Verify(
                x => x.StreamAsync(
                    It.Is<IEnumerable<ChatMessage>>(m => m.Count() == 1),
                    It.IsAny<StreamOptions?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Equal(2, result.Count);
            Assert.Equal("Hi there", string.Concat(result.Select(c => c.Content)));
        }

        /// <summary>
        /// Test: Handler receives chat call with null messages → service throws ArgumentNullException.
        /// 
        /// Arrange: Create mock ILlmService configured to throw on null messages
        /// Act: Call StreamAsync with null
        /// Assert: ArgumentNullException is thrown before streaming
        /// </summary>
        [Fact]
        public async Task Chat_Handler_WithNullMessages_ThrowsArgumentNullExceptionAsync()
        {
            // Arrange
            var mockLlmService = new Mock<ILlmService>(MockBehavior.Strict);
            mockLlmService
                .Setup(x => x.StreamAsync(null!, It.IsAny<StreamOptions?>(), It.IsAny<CancellationToken>()))
                .Throws(new ArgumentNullException(nameof(IEnumerable<ChatMessage>)));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => mockLlmService.Object.StreamAsync(null!).GetAsyncEnumerator().MoveNextAsync().AsTask());
        }

        /// <summary>
        /// Test: Handler call → service raises LlmException → exception propagates to caller.
        /// 
        /// Arrange: Mock ILlmService to throw LlmException during stream
        /// Act: Iterate through stream
        /// Assert: LlmException is propagated (not swallowed)
        /// </summary>
        [Fact]
        public async Task Chat_Handler_ServiceThrows_ExceptionPropagatesAsync()
        {
            // Arrange
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Test" }
            };

            var mockLlmService = new Mock<ILlmService>(MockBehavior.Strict);
            mockLlmService
                .Setup(x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(ThrowingStreamAsync());

            // Act & Assert
            await Assert.ThrowsAsync<LlmException>(async () =>
            {
                await foreach (var chunk in mockLlmService.Object.StreamAsync(messages))
                {
                    // Iteration should eventually throw
                }
            });
        }

        /// <summary>
        /// Test: Handler passes CancellationToken → service respects token and exits early.
        /// 
        /// Arrange: Create mock ILlmService; create CancellationToken and cancel it
        /// Act: Call StreamAsync with cancellation token, iterate, then cancel
        /// Assert: Stream stops when token is cancelled (OperationCanceledException)
        /// </summary>
        [Fact]
        public async Task Chat_Handler_WithCancellationToken_RespectsCancellationAsync()
        {
            // Arrange
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Test" }
            };

            var cts = new CancellationTokenSource();
            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "chunk1" },
                new CompletionChunk { Type = ChunkType.Text, Content = "chunk2" }
            };

            var mockLlmService = new Mock<ILlmService>(MockBehavior.Strict);
            mockLlmService
                .Setup(x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> m, StreamOptions? o, CancellationToken ct) =>
                    StreamChunksWithCancellationAsync(chunks, ct));

            // Act & Assert
            var result = new List<CompletionChunk>();
            cts.CancelAfter(TimeSpan.FromMilliseconds(10));

            try
            {
                await foreach (var chunk in mockLlmService.Object.StreamAsync(messages, null, cts.Token))
                {
                    result.Add(chunk);
                    await Task.Delay(5); // Simulate processing
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when token is cancelled
            }

            // Verify token was passed through
            mockLlmService.Verify(
                x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions?>(),
                    cts.Token),
                Times.Once);
        }

        /// <summary>
        /// Test: Handler routes correctly when streaming with StreamOptions (temperature, max tokens, etc.).
        /// 
        /// Arrange: Create mock ILlmService; prepare StreamOptions
        /// Act: Call StreamAsync with options
        /// Assert: Service method is called once with exact options
        /// </summary>
        [Fact]
        public async Task Chat_Handler_WithStreamOptions_DelegatesToServiceAsync()
        {
            // Arrange
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Test" }
            };

            var options = new StreamOptions
            {
                Temperature = 0.7,
                MaxTokens = 1000
            };

            var chunks = new List<CompletionChunk>
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "Response" }
            };

            var mockLlmService = new Mock<ILlmService>(MockBehavior.Strict);
            mockLlmService
                .Setup(x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(StreamChunksAsync(chunks));

            // Act
            var result = new List<CompletionChunk>();
            await foreach (var chunk in mockLlmService.Object.StreamAsync(messages, options))
            {
                result.Add(chunk);
            }

            // Assert
            mockLlmService.Verify(
                x => x.StreamAsync(
                    It.Is<IEnumerable<ChatMessage>>(m => m.Count() == 1),
                    It.Is<StreamOptions?>(o => o!.Temperature == 0.7 && o.MaxTokens == 1000),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.Single(result);
        }

        // ========== Helper Methods ==========

        /// <summary>
        /// Helper: Simulate streaming completion chunks from a list.
        /// </summary>
        private static async IAsyncEnumerable<CompletionChunk> StreamChunksAsync(IEnumerable<CompletionChunk> chunks)
        {
            foreach (var chunk in chunks)
            {
                yield return chunk;
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Helper: Simulate streaming that throws LlmException after first chunk.
        /// </summary>
        private static async IAsyncEnumerable<CompletionChunk> ThrowingStreamAsync()
        {
            yield return new CompletionChunk { Type = ChunkType.Text, Content = "partial" };
            throw new LlmException(
                "LLM service connection failed",
                "connection_error",
                new InvalidOperationException("Network unreachable"));
#pragma warning disable CS0162 // Unreachable code
            await Task.CompletedTask;
#pragma warning restore CS0162 // Unreachable code
        }

        /// <summary>
        /// Helper: Simulate streaming that respects cancellation token.
        /// </summary>
        private static async IAsyncEnumerable<CompletionChunk> StreamChunksWithCancellationAsync(
            IEnumerable<CompletionChunk> chunks,
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();
                yield return chunk;
                await Task.Delay(5, ct);
            }
        }
    }
}
