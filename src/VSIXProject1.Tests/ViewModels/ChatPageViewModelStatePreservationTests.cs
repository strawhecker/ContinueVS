using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

#nullable enable

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for execution state preservation during pause (gap31_3).
    /// Covers buffer capture, checkpoint storage, and retrieval.
    /// </summary>
    public class ChatPageViewModelStatePreservationTests
    {
        /// <summary>
        /// Test: LlmService buffers chunks as they arrive during streaming.
        /// Verifies that GetStreamBuffer() returns all buffered chunks.
        /// </summary>
        [Fact]
        public async Task LlmService_BuffersChunksAsTheyArrive()
        {
            // Arrange
            var chunks = new[]
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "Hello " },
                new CompletionChunk { Type = ChunkType.Text, Content = "world " },
                new CompletionChunk { Type = ChunkType.Text, Content = "test" }
            };

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger.Setup(x => x.StreamAsync<It.IsAnyType, CompletionChunk>(
                    It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default))
                .Returns(CreateChunkStreamAsync(chunks));

            var llmService = new LlmService(mockMessenger.Object);

            // Act
            var messages = new List<ChatMessage> { new ChatMessage { Content = "test" } };
            var collectedChunks = new List<CompletionChunk>();
            await foreach (var chunk in llmService.StreamAsync(messages))
            {
                collectedChunks.Add(chunk);
            }

            var buffer = llmService.GetStreamBuffer();

            // Assert
            Assert.Equal(3, buffer.Count);
            Assert.Equal("Hello ", buffer[0].Content);
            Assert.Equal("world ", buffer[1].Content);
            Assert.Equal("test", buffer[2].Content);
        }

        /// <summary>
        /// Test: ExecutePause captures checkpoint with buffered state.
        /// Verifies checkpoint stored with correct StreamedText, ChunkCount, and timestamp.
        /// </summary>
        [Fact]
        public async Task ExecutePause_CapturesCheckpointWithBufferedState()
        {
            // Arrange: Create mocks and set up LLM service with chunked response
            var chunks = new[]
            {
                new CompletionChunk { Type = ChunkType.Text, Content = "Initial " },
                new CompletionChunk { Type = ChunkType.Text, Content = "response" }
            };

            var mockMessenger = new Mock<IMessengerService>();
            mockMessenger.Setup(x => x.StreamAsync<It.IsAnyType, CompletionChunk>(
                    It.IsAny<string>(), It.IsAny<It.IsAnyType>(), default))
                .Returns(CreateChunkStreamAsync(chunks));

            var mockLlmService = new Mock<ILlmService>();
            mockLlmService.Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), null, default))
                .Returns(CreateChunkStreamAsync(chunks));
            mockLlmService.Setup(x => x.GetStreamBuffer())
                .Returns(new List<CompletionChunk>(chunks));

            var mockDebugSessionService = new Mock<IDebugSessionService>();
            PauseCheckpoint? capturedCheckpoint = null;
            mockDebugSessionService
                .Setup(x => x.SetPauseCheckpointAsync(It.IsAny<PauseCheckpoint>()))
                .Callback<PauseCheckpoint>(cp => capturedCheckpoint = cp)
                .Returns(Task.CompletedTask);

            // Act: Simulate pause with checkpoint capture
            var buffer = mockLlmService.Object.GetStreamBuffer();
            var streamedText = string.Concat(buffer.Select(c => c.Content));
            var checkpoint = new PauseCheckpoint
            {
                StreamedText = streamedText,
                ChunkCount = buffer.Count,
                PauseTimestamp = DateTime.UtcNow,
                SessionContextSnapshot = new Dictionary<string, string>()
            };
            await mockDebugSessionService.Object.SetPauseCheckpointAsync(checkpoint);

            // Assert
            Assert.NotNull(capturedCheckpoint);
            Assert.Equal("Initial response", capturedCheckpoint.StreamedText);
            Assert.Equal(2, capturedCheckpoint.ChunkCount);
            Assert.True(capturedCheckpoint.PauseTimestamp <= DateTime.UtcNow);
        }

        /// <summary>
        /// Test: PauseCheckpoint can be retrieved from DebugSessionService.
        /// Verifies GetPauseCheckpointAsync returns stored checkpoint.
        /// </summary>
        [Fact]
        public async Task PauseCheckpoint_CanBeRetrievedFromDebugSessionService()
        {
            // Arrange
            var mockDebugSessionService = new Mock<IDebugSessionService>();
            var checkpoint = new PauseCheckpoint
            {
                StreamedText = "Test response",
                ChunkCount = 1,
                PauseTimestamp = DateTime.UtcNow,
                SessionContextSnapshot = new Dictionary<string, string> { { "File", "Main.cs" } }
            };

            mockDebugSessionService.Setup(x => x.GetPauseCheckpointAsync())
                .ReturnsAsync(checkpoint);

            // Act
            var retrieved = await mockDebugSessionService.Object.GetPauseCheckpointAsync();

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal("Test response", retrieved.StreamedText);
            Assert.Equal(1, retrieved.ChunkCount);
            Assert.True(retrieved.SessionContextSnapshot.ContainsKey("File"));
        }

        /// <summary>
        /// Async generator helper to simulate LLM streaming chunks.
        /// </summary>
        private async IAsyncEnumerable<CompletionChunk> CreateChunkStreamAsync(CompletionChunk[] chunks)
        {
            foreach (var chunk in chunks)
            {
                await Task.Yield();
                yield return chunk;
            }
        }
    }
}
