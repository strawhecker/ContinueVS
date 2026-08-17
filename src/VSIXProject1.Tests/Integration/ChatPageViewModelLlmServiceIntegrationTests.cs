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
using ContinueVS.Services.Events;
using ContinueVS.Services.Interfaces;
using ContinueVS.Tests.Infrastructure;
using ContinueVS.ViewModels;
using CoreTypes = ContinueVS.Core.Types;

namespace ContinueVS.Tests.Integration
{
    /// <summary>
    /// Integration tests for ChatPageViewModel.SendMessage → ILlmService.StreamAsync flow (Step 102).
    /// 
    /// Verifies that ChatPageViewModel correctly:
    /// - Calls ILlmService.StreamAsync with user message
    /// - Accumulates streamed chunks into StreamingResponse
    /// - Adds messages to session and UI collections
    /// - Updates UI state (IsStreaming, InputText cleared)
    /// - Handles errors and cancellation with proper notifications
    /// 
    /// Test isolation:
    /// - Real ChatPageViewModel instance to verify actual state mutations
    /// - Mocked ILlmService.StreamAsync with controlled chunk sequences
    /// - All other dependencies (IContextService, ISessionService, INotificationService, IToolService) loosely mocked
    /// - No side effects on disk or config
    /// </summary>
    public class ChatPageViewModelLlmServiceIntegrationTests : TestFixtureBase
    {
        private Mock<IConfigService> CreateConfigServiceMock()
        {
            var mock = CreateLooseMock<IConfigService>();
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Default Model", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            mock.Setup(m => m.GetCurrentConfig()).Returns(config);
            return mock;
        }

        /// <summary>
        /// Test 1: SendMessage with single text chunk updates UI correctly.
        /// 
        /// Arrange: Real ChatPageViewModel with mocked ILlmService
        /// Mock ILlmService.StreamAsync to return one text chunk
        /// Act: Execute SendMessageCommand
        /// Assert: StreamingResponse contains chunk, user+assistant messages added, InputText cleared
        /// </summary>
        [Fact]
        public async Task SendMessage_WithSingleTextChunk_UpdatesUICorrectlyAsync()
        {
            // Arrange
            var mockChunk = new CoreTypes.CompletionChunk
            {
                Type = CoreTypes.ChunkType.Text,
                Content = "Hello, world!"
            };

            var mockLlmService = new Mock<ILlmService>();
            mockLlmService
                .Setup(m => m.StreamAsync(
                    It.IsAny<IEnumerable<CoreTypes.ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(new[] { mockChunk }));

            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            viewModel.InputText = "Test message";

            // Act
            viewModel.SendMessageCommand.Execute(null);
            await Task.Delay(500); // Wait for async SendMessage to complete

            // Assert
            Assert.Equal("Hello, world!", viewModel.StreamingResponse);
            Assert.Equal(2, viewModel.Messages.Count);
            Assert.Equal(ChatMessageRole.User, viewModel.Messages[0].Role);
            Assert.Equal("Test message", viewModel.Messages[0].Content);
            Assert.Equal(ChatMessageRole.Assistant, viewModel.Messages[1].Role);
            Assert.Equal("Hello, world!", viewModel.Messages[1].Content);
            Assert.Empty(viewModel.InputText);
            Assert.False(viewModel.IsStreaming);
        }

        /// <summary>
        /// Test 2: SendMessage with multiple chunks accumulates response correctly.
        /// 
        /// Arrange: Real ChatPageViewModel with mocked ILlmService
        /// Mock ILlmService.StreamAsync to return 3 text chunks in sequence
        /// Act: Execute SendMessageCommand
        /// Assert: StreamingResponse contains all chunks concatenated in order
        /// </summary>
        [Fact]
        public async Task SendMessage_WithMultipleChunks_AccumulatesResponseCorrectlyAsync()
        {
            // Arrange
            var mockChunks = new[]
            {
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Hello, " },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "world" },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "!" }
            };

            var mockLlmService = new Mock<ILlmService>();
            mockLlmService
                .Setup(m => m.StreamAsync(
                    It.IsAny<IEnumerable<CoreTypes.ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new AsyncEnumerableWrapper<CoreTypes.CompletionChunk>(mockChunks));

            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            viewModel.InputText = "Test";

            // Act
            viewModel.SendMessageCommand.Execute(null);
            await Task.Delay(500);

            // Assert
            Assert.Equal("Hello, world!", viewModel.StreamingResponse);
            Assert.Equal(2, viewModel.Messages.Count);
            Assert.Equal(ChatMessageRole.User, viewModel.Messages[0].Role);
            Assert.Equal("Test", viewModel.Messages[0].Content);
            Assert.Equal(ChatMessageRole.Assistant, viewModel.Messages[1].Role);
            Assert.Equal("Hello, world!", viewModel.Messages[1].Content);
            mockSessionService.Verify(
                s => s.AddMessageAsync(It.IsAny<CoreTypes.ChatMessage>()),
                Times.Exactly(2),
                "ISessionService.AddMessageAsync should be called twice (user + assistant)");
        }

        /// <summary>
        /// Test 3: SendMessage with streaming error shows notification.
        /// 
        /// Arrange: Real ChatPageViewModel with mocked ILlmService that throws
        /// Mock ILlmService.StreamAsync to throw InvalidOperationException
        /// Act: Execute SendMessageCommand
        /// Assert: INotificationService.ShowNotificationAsync called with error, IsStreaming false
        /// </summary>
        [Fact]
        public async Task SendMessage_WithStreamingError_ShowsNotificationAsync()
        {
            // Arrange
            var mockLlmService = new Mock<ILlmService>();
            mockLlmService
                .Setup(m => m.StreamAsync(
                    It.IsAny<IEnumerable<CoreTypes.ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEnumerable<CoreTypes.ChatMessage>, StreamOptions, CancellationToken>(
                    (msgs, opts, ct) => throw new InvalidOperationException("Stream failed"));

            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            mockNotificationService
                .Setup(n => n.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);
            var mockConfigService = CreateConfigServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            viewModel.InputText = "Test";

            // Act
            viewModel.SendMessageCommand.Execute(null);
            await Task.Delay(500);

            // Assert
            Assert.False(viewModel.IsStreaming);
#pragma warning disable VSTHRD110
            mockNotificationService.Verify(
                n => n.ShowNotificationAsync(
                    It.IsAny<string>(),
                    It.Is<string>(msg => msg.Contains("Stream failed")),
                    It.IsAny<NotificationType>()),
                Times.Once,
                "INotificationService.ShowNotificationAsync should be called with error message");
#pragma warning restore VSTHRD110
        }

        /// <summary>
        /// Test 4: SendMessage with cancellation stops streaming and marks UI.
        /// 
        /// Arrange: Real ChatPageViewModel with mocked ILlmService that yields chunks slowly
        /// Mock the cancellation token to be cancellable
        /// Act: Call SendMessageCommand, then CancelCommand
        /// Assert: IsStreaming becomes false, StreamingResponse contains cancellation marker
        /// </summary>
        [Fact]
        public async Task SendMessage_WithCancellation_StopsStreamingAsync()
        {
            // Arrange
            var mockChunks = new[]
            {
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Part 1 " },
                new CoreTypes.CompletionChunk { Type = CoreTypes.ChunkType.Text, Content = "Part 2" }
            };

            var mockLlmService = new Mock<ILlmService>();
            CancellationToken capturedToken = CancellationToken.None;

            mockLlmService
                .Setup(m => m.StreamAsync(
                    It.IsAny<IEnumerable<CoreTypes.ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Callback<IEnumerable<CoreTypes.ChatMessage>, StreamOptions, CancellationToken>(
                    (msgs, opts, ct) => capturedToken = ct)
                .Returns<IEnumerable<CoreTypes.ChatMessage>, StreamOptions, CancellationToken>(
                    (msgs, opts, ct) => SlowAsyncEnumerableAsync(mockChunks, ct));

            var mockContextService = CreateLooseMock<IContextService>();
            var mockToolService = CreateLooseMock<IToolService>();
            var mockSessionService = CreateLooseMock<ISessionService>();
            var mockNotificationService = CreateLooseMock<INotificationService>();
            var mockConfigService = CreateConfigServiceMock();

            var viewModel = new ChatPageViewModel(
                mockLlmService.Object,
                mockContextService.Object,
                mockToolService.Object,
                mockSessionService.Object,
                mockNotificationService.Object,
                mockConfigService.Object);

            viewModel.InputText = "Test";

            // Act
            viewModel.SendMessageCommand.Execute(null);
            await Task.Delay(100); // Let it start streaming
            viewModel.CancelCommand.Execute(null);
            await Task.Delay(500); // Wait for cancellation to propagate

            // Assert
            Assert.False(viewModel.IsStreaming);
            Assert.Contains("[Cancelled by user]", viewModel.StreamingResponse);
        }

        private static async IAsyncEnumerable<CoreTypes.CompletionChunk> SlowAsyncEnumerableAsync(
            IEnumerable<CoreTypes.CompletionChunk> chunks,
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(200, ct);
                yield return chunk;
            }
        }
    }
}
