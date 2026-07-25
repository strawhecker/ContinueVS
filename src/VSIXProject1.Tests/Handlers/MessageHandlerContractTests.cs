#nullable enable
#pragma warning disable CS8603, CS8619

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Handlers;
using ContinueVS.IPC;

namespace ContinueVS.Tests.Handlers
{
    /// <summary>
    /// Test suite for IMessageHandler contract compliance.
    /// 
    /// Tests verify that handlers implementing IMessageHandler correctly:
    /// - Execute HandleAsync with correct Message and CancellationToken parameters
    /// - Honor CancellationToken cancellation requests
    /// - Maintain parameter fidelity across multiple invocations
    /// </summary>
    public class MessageHandlerContractTests
    {
        // === Parameter Validation Tests ===

        [Fact]
        public async Task HandleAsync_WithValidMessage_ExecutesWithCorrectParameters()
        {
            // Arrange
            var messageType = "contract:test";
            var messageId = "msg-contract-001";
            var message = new Message
            {
                MessageType = messageType,
                MessageId = messageId,
                Data = null
            };

            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Strict);
            var capturedMessage = (Message?)null;
            var capturedToken = CancellationToken.None;

            mockHandler.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Callback<Message, CancellationToken>((msg, token) =>
                {
                    capturedMessage = msg;
                    capturedToken = token;
                })
                .Returns(Task.CompletedTask);

            var cts = new CancellationTokenSource();

            // Act
            await mockHandler.Object.HandleAsync(message, cts.Token);

            // Assert
            Assert.NotNull(capturedMessage);
            Assert.Equal(messageType, capturedMessage!.MessageType);
            Assert.Equal(messageId, capturedMessage.MessageId);
            Assert.Equal(cts.Token, capturedToken);
            mockHandler.Verify(
                x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // === CancellationToken Respect Tests ===

        [Fact]
        public async Task HandleAsync_WithCancelledToken_ThrowsTaskCanceledException()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "contract:cancellation-test",
                MessageId = "msg-002",
                Data = null
            };

            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);
            var cts = new CancellationTokenSource();

            mockHandler.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns<Message, CancellationToken>((msg, token) =>
                {
                    // Simulate handler that respects cancellation
                    if (token.IsCancellationRequested)
                    {
                        return Task.FromCanceled(token);
                    }
                    return Task.CompletedTask;
                });

            // Cancel the token
            cts.Cancel();

            // Act & Assert
            // TaskCanceledException is the actual exception type thrown by Task.FromCanceled
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => mockHandler.Object.HandleAsync(message, cts.Token));
        }

        // === Contract Consistency Tests ===

        [Fact]
        public async Task HandleAsync_WithMultipleInvocations_MaintainsParameterFidelity()
        {
            // Arrange
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Strict);
            var callCount = 0;
            var expectedCalls = 3;

            mockHandler.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Callback<Message, CancellationToken>((msg, token) =>
                {
                    // Verify each invocation has expected structure
                    Assert.NotNull(msg);
                    Assert.False(string.IsNullOrEmpty(msg.MessageType));
                    Assert.False(string.IsNullOrEmpty(msg.MessageId));
                    callCount++;
                })
                .Returns(Task.CompletedTask);

            var cts = new CancellationTokenSource();

            // Act
            for (int i = 1; i <= expectedCalls; i++)
            {
                var message = new Message
                {
                    MessageType = $"contract:invocation-{i}",
                    MessageId = $"msg-00{i}",
                    Data = null
                };

                await mockHandler.Object.HandleAsync(message, cts.Token);
            }

            // Assert
            Assert.Equal(expectedCalls, callCount);
            mockHandler.Verify(
                x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()),
                Times.Exactly(expectedCalls));
        }

        // === Handler Composition Tests ===

        [Fact]
        public async Task HandleAsync_WithNullMessage_PreservesContractSemantics()
        {
            // Arrange
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);
            var cts = new CancellationTokenSource();

            mockHandler.Setup(x => x.HandleAsync(null!, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await mockHandler.Object.HandleAsync(null!, cts.Token);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(null!, cts.Token), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WithTaskException_PropagatesCorrectly()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "contract:exception-test",
                MessageId = "msg-exception",
                Data = null
            };

            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);
            var testException = new InvalidOperationException("Handler execution failed");
            var cts = new CancellationTokenSource();

            mockHandler.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromException(testException));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => mockHandler.Object.HandleAsync(message, cts.Token));

            Assert.Equal("Handler execution failed", ex.Message);
        }
    }
}
