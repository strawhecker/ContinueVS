#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// Integration tests for LlmService ↔ MessageDispatcher streaming (Step 101).
    /// 
    /// Verifies that LlmService.StreamAsync correctly delegates to IMessengerService.StreamAsync
    /// and yields chunks from the messenger in the correct order, with proper cancellation and
    /// error propagation.
    /// 
    /// Test isolation:
    /// - Each test creates fresh mocks of IMessengerService
    /// - Real LlmService instance (not mocked) for actual delegation verification
    /// - No side effects on disk or config
    /// </summary>
    public class MessageDispatcherLlmServiceStreamingTests
    {
        /// <summary>
        /// Test 1: StreamAsync receives a single mocked chunk from messenger and yields it correctly.
        /// 
        /// Arrange: Create real LlmService with mocked IMessengerService
        /// Mock IMessengerService.StreamAsync to return one chunk
        /// Act: Call LlmService.StreamAsync with test messages
        /// Assert: Received chunk matches mocked chunk; content and type are correct
        /// </summary>
        [Fact]
        public async Task StreamAsync_SingleChunk_YieldsChunkCorrectlyAsync()
        {
            // Arrange
            var mockChunk = new CoreTypes.CompletionChunk
            {
                Type = CoreTypes.ChunkType.Text,
                Content = "Hello, world!"
            };

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger
                .Setup(m => m.StreamAsync<ContinueVS.Services.Interfaces.StreamOptions, CoreTypes.CompletionChunk>(
                    It.IsAny<string>(),
                    It.IsAny<ContinueVS.Services.Interfaces.StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(new[] { mockChunk }));

            var service = new LlmService(mockMessenger.Object);
            var messages = new[] { new CoreTypes.ChatMessage { Role = CoreTypes.ChatMessageRole.User, Content = "test" } };
            var options = new ContinueVS.Services.Interfaces.StreamOptions { Temperature = 0.7 };

            // Act
            var chunks = new List<CoreTypes.CompletionChunk>();
            await foreach (var chunk in service.StreamAsync(messages, options))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.Single(chunks);
            Assert.Equal("Hello, world!", chunks[0].Content);
            Assert.Equal(CoreTypes.ChunkType.Text, chunks[0].Type);
        }

        /// <summary>
        /// Test 2: StreamAsync receives multiple chunks (3+) from messenger and yields them in order.
        /// 
        /// Arrange: Create real LlmService with mocked IMessengerService
        /// Mock IMessengerService.StreamAsync to return 4 chunks in sequence
        /// Act: Enumerate all chunks from LlmService.StreamAsync
        /// Assert: All chunks received in correct order with correct content
        /// </summary>
        [Fact]
        public async Task StreamAsync_MultipleChunks_YieldsAllInOrderAsync()
        {
            // Arrange
            var mockChunks = new[]
            {
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "First" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Second" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Third" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Done, Content = null }
            };

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger
                .Setup(m => m.StreamAsync<ContinueVS.Services.Interfaces.StreamOptions, CoreTypes.CompletionChunk>(
                    It.IsAny<string>(),
                    It.IsAny<ContinueVS.Services.Interfaces.StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(mockChunks));

            var service = new LlmService(mockMessenger.Object);
            var messages = new[] { new CoreTypes.ChatMessage { Role = CoreTypes.ChatMessageRole.User, Content = "test" } };

            // Act
            var chunks = new List<CoreTypes.CompletionChunk>();
            await foreach (var chunk in service.StreamAsync(messages))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.Equal(4, chunks.Count);
            Assert.Equal("First", chunks[0].Content);
            Assert.Equal("Second", chunks[1].Content);
            Assert.Equal("Third", chunks[2].Content);
            Assert.Null(chunks[3].Content);
            Assert.Equal(CoreTypes.ChunkType.Done, chunks[3].Type);
        }

        /// <summary>
        /// Test 3: CancellationToken passed to StreamAsync stops enumeration early.
        /// 
        /// Arrange: Create real LlmService with mocked IMessengerService
        /// Mock IMessengerService.StreamAsync to emit chunks; cancel after first chunk
        /// Act: Call LlmService.StreamAsync with CancellationToken, cancel during enumeration
        /// Assert: Only first chunk received; others skipped due to cancellation
        /// </summary>
        [Fact]
        public async Task StreamAsync_CancellationToken_StopsEnumerationAsync()
        {
            // Arrange
            var mockChunks = new[]
            {
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "First" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Second" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Third" }
            };

            var cancellationTokenReceived = default(CancellationToken?);

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger
                .Setup(m => m.StreamAsync<ContinueVS.Services.Interfaces.StreamOptions, CoreTypes.CompletionChunk>(
                    It.IsAny<string>(),
                    It.IsAny<ContinueVS.Services.Interfaces.StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ContinueVS.Services.Interfaces.StreamOptions, CancellationToken>(
                    (msgType, opts, ct) =>
                    {
                        cancellationTokenReceived = ct;
                        return new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(mockChunks, ct);
                    });

            var service = new LlmService(mockMessenger.Object);
            var messages = new[] { new CoreTypes.ChatMessage { Role = CoreTypes.ChatMessageRole.User, Content = "test" } };

            // Act
            var cts = new CancellationTokenSource();
            var chunks = new List<CoreTypes.CompletionChunk>();

            try
            {
                await foreach (var chunk in service.StreamAsync(messages, ct: cts.Token))
                {
                    chunks.Add(chunk);
                    if (chunks.Count == 1)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation occurs
            }

            // Assert
            Assert.NotNull(cancellationTokenReceived);
            Assert.Equal(cancellationTokenReceived.Value, cts.Token);
            // With cancellation, we may get one or more chunks before the token is checked
            Assert.True(chunks.Count < mockChunks.Length);
        }

        /// <summary>
        /// Test 4: StreamOptions are passed through to IMessengerService.StreamAsync.
        /// 
        /// Arrange: Create real LlmService with mocked IMessengerService
        /// Mock IMessengerService.StreamAsync to capture the StreamOptions parameter
        /// Act: Call LlmService.StreamAsync with specific StreamOptions
        /// Assert: Messenger received the same StreamOptions with correct values
        /// </summary>
        [Fact]
        public async Task StreamAsync_StreamOptions_PassedToMessengerAsync()
        {
            // Arrange
            var mockChunk = new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "test" };
            var capturedOptions = default(ContinueVS.Services.Interfaces.StreamOptions?);

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger
                .Setup(m => m.StreamAsync<ContinueVS.Services.Interfaces.StreamOptions, CoreTypes.CompletionChunk>(
                    It.IsAny<string>(),
                    It.IsAny<ContinueVS.Services.Interfaces.StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ContinueVS.Services.Interfaces.StreamOptions, CancellationToken>(
                    (msgType, opts, ct) =>
                    {
                        capturedOptions = opts;
                        return new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(new[] { mockChunk });
                    });

            var service = new LlmService(mockMessenger.Object);
            var messages = new[] { new CoreTypes.ChatMessage { Role = CoreTypes.ChatMessageRole.User, Content = "test" } };
            var options = new ContinueVS.Services.Interfaces.StreamOptions
            {
                Temperature = 0.5,
                MaxTokens = 256,
                TopP = 0.9,
                SystemPrompt = "You are a test assistant."
            };

            // Act
            var chunks = new List<CoreTypes.CompletionChunk>();
            await foreach (var chunk in service.StreamAsync(messages, options))
            {
                chunks.Add(chunk);
            }

            // Assert
            Assert.NotNull(capturedOptions);
            Assert.Equal(0.5, capturedOptions!.Temperature);
            Assert.Equal(256, capturedOptions.MaxTokens);
            Assert.Equal(0.9, capturedOptions.TopP);
            Assert.Equal("You are a test assistant.", capturedOptions.SystemPrompt);
        }

        /// <summary>
        /// Test 5: Exception from IMessengerService.StreamAsync bubbles up to caller.
        /// 
        /// Arrange: Create real LlmService with mocked IMessengerService
        /// Mock IMessengerService.StreamAsync to throw an exception
        /// Act: Call LlmService.StreamAsync and attempt to enumerate
        /// Assert: Exception is propagated to caller (not swallowed)
        /// </summary>
        [Fact]
        public async Task StreamAsync_MessengerThrows_ExceptionBubblesUpAsync()
        {
            // Arrange
            var mockMessenger = new Mock<IMessengerService>();
            var testException = new InvalidOperationException("Messenger failed");

            mockMessenger
                .Setup(m => m.StreamAsync<ContinueVS.Services.Interfaces.StreamOptions, CoreTypes.CompletionChunk>(
                    It.IsAny<string>(),
                    It.IsAny<ContinueVS.Services.Interfaces.StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapperThrows<CoreTypes.CompletionChunk>(testException));

            var service = new LlmService(mockMessenger.Object);
            var messages = new[] { new CoreTypes.ChatMessage { Role = CoreTypes.ChatMessageRole.User, Content = "test" } };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var chunk in service.StreamAsync(messages))
                {
                    // Enumerate until exception
                }
            });

            Assert.Equal("Messenger failed", exception.Message);
        }
    }

    /// <summary>
    /// Helper wrapper to simulate async enumerable from mock.
    /// </summary>
    internal class AsyncEnumerableWrapper<T> : IAsyncEnumerable<T>
    {
        private readonly IEnumerable<T> _items;
        private readonly CancellationToken _ct;

        public AsyncEnumerableWrapper(IEnumerable<T> items, CancellationToken ct = default)
        {
            _items = items;
            _ct = ct;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                _ct.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// Helper wrapper that throws on enumeration.
    /// </summary>
    internal class AsyncEnumerableWrapperThrows<T> : IAsyncEnumerable<T>
    {
        private readonly Exception _exception;

        public AsyncEnumerableWrapperThrows(Exception exception)
        {
            _exception = exception;
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
#pragma warning disable CS0162 // Unreachable code
            throw _exception;
            await Task.CompletedTask;
            yield break;
#pragma warning restore CS0162 // Unreachable code
        }
    }
}
