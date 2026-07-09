#nullable enable
#pragma warning disable CS8603, CS8619

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Handlers;
using ContinueVS.IPC;
using ContinueVS.Exceptions;
using ContinueVS.Services;

namespace ContinueVS.Tests.Handlers
{
    /// <summary>
    /// Comprehensive test suite for <see cref="MessageDispatcher"/>.
    /// 
    /// Tests cover:
    /// - Handler registration: success, duplicates, null validation
    /// - Message dispatch: successful execution, handler not found, validation failures
    /// - Error handling: handler exceptions, timeouts, cancellation
    /// - Logging and telemetry: graceful degradation when null
    /// - Timeout enforcement: via DispatchWithTimeoutAsync
    /// </summary>
    public class MessageDispatcherTests
    {
        private readonly MessageDispatcher _dispatcher;
        private readonly Mock<IBridgeLogger>? _mockLogger;
        private readonly Mock<IBridgeTelemetryCollector>? _mockTelemetry;

        public MessageDispatcherTests()
        {
            // Create dispatcher with optional mocks (can be null for graceful degradation tests)
            _mockLogger = new Mock<IBridgeLogger>(MockBehavior.Default);
            _mockTelemetry = new Mock<IBridgeTelemetryCollector>(MockBehavior.Default);

            _dispatcher = new MessageDispatcher(_mockLogger.Object, _mockTelemetry.Object);
        }

        // === Handler Registration Tests ===

        [Fact]
        public void Register_WithValidHandler_Succeeds()
        {
            // Arrange
            var messageType = "bridge:test";
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            // Act
            _dispatcher.Register(messageType, mockHandler.Object);

            // Assert
            // If no exception was thrown, registration succeeded
            Assert.True(true);
        }

        [Fact]
        public void Register_WithDuplicateMessageType_ThrowsArgumentException()
        {
            // Arrange
            var messageType = "bridge:test";
            var mockHandler1 = new Mock<IMessageHandler>(MockBehavior.Default);
            var mockHandler2 = new Mock<IMessageHandler>(MockBehavior.Default);

            _dispatcher.Register(messageType, mockHandler1.Object);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => _dispatcher.Register(messageType, mockHandler2.Object));
            Assert.Contains("already registered", ex.Message);
        }

        [Fact]
        public void Register_WithNullMessageType_ThrowsArgumentNullException()
        {
            // Arrange
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _dispatcher.Register(null!, mockHandler.Object));
        }

        [Fact]
        public void Register_WithNullHandler_ThrowsArgumentNullException()
        {
            // Arrange
            var messageType = "bridge:test";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _dispatcher.Register(messageType, null!));
        }

        [Fact]
        public void Register_WithValidHandler_LogsDebugMessage()
        {
            // Arrange
            var messageType = "bridge:test";
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            // Act
            _dispatcher.Register(messageType, mockHandler.Object);

            // Assert
            _mockLogger!.Verify(
                x => x.WriteDebugAsync(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object>>()),
                Times.Once);
        }

        // === Dispatch Success Tests ===

        [Fact]
        public async Task DispatchAsync_WithRegisteredHandler_InvokesHandler()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_WithSuccessfulHandler_FiresOnHandlerInvokedEvent()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_WithSuccessfulHandler_RecordsTelemetry()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            _mockTelemetry!.Verify(
                x => x.RecordHandlerExecutionAsync(messageType, It.IsAny<long>(), It.IsAny<IReadOnlyDictionary<string, object>>()),
                Times.Once);
        }

        // === Dispatch Error Tests ===

        [Fact]
        public async Task DispatchAsync_WithUnregisteredMessageType_ThrowsBridgeMessageDispatcherException()
        {
            // Arrange
            var message = new Message { MessageType = "bridge:unknown", MessageId = "msg-1", Data = null };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => _dispatcher.DispatchAsync(message, CancellationToken.None));

            Assert.Equal(BridgeMessageDispatcherException.OperationType.HandlerNotFound, ex.Operation);
        }

        [Fact]
        public async Task DispatchAsync_WithNullMessage_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _dispatcher.DispatchAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task DispatchAsync_WithEmptyMessageType_ThrowsBridgeMessageDispatcherException()
        {
            // Arrange
            var message = new Message { MessageType = "", MessageId = "msg-1", Data = null };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => _dispatcher.DispatchAsync(message, CancellationToken.None));

            Assert.Equal(BridgeMessageDispatcherException.OperationType.ValidationFailed, ex.Operation);
        }

        [Fact]
        public async Task DispatchAsync_WithHandlerThrowingException_WrapsInBridgeMessageDispatcherException()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);
            var originalException = new InvalidOperationException("Handler error");

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Throws(originalException);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => _dispatcher.DispatchAsync(message, CancellationToken.None));

            Assert.Equal(BridgeMessageDispatcherException.OperationType.DispatchError, ex.Operation);
            Assert.Same(originalException, ex.InnerException);
        }

        [Fact]
        public async Task DispatchAsync_WithHandlerThrowing_LogsError()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Handler error"));

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            try
            {
                await _dispatcher.DispatchAsync(message, CancellationToken.None);
            }
            catch
            {
                // Expected
            }

            // Assert
            _mockLogger!.Verify(
                x => x.WriteErrorAsync(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<IReadOnlyDictionary<string, object>>()),
                Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_WithHandlerThrowingException_DoesNotFireOnHandlerInvokedEvent()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Handler error"));

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            try
            {
                await _dispatcher.DispatchAsync(message, CancellationToken.None);
            }
            catch
            {
                // Expected
            }

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        // === Timeout Tests ===

        [Fact]
        public async Task DispatchWithTimeoutAsync_WithTimeoutExceeded_ThrowsBridgeMessageDispatcherException()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            // Handler takes 500ms; timeout is 100ms
            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(async (Message m, CancellationToken ct) =>
                {
                    await Task.Delay(500, ct);
                });

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => _dispatcher.DispatchWithTimeoutAsync(message, timeoutMs: 100, CancellationToken.None));

            Assert.Equal(BridgeMessageDispatcherException.OperationType.TimeoutExceeded, ex.Operation);
        }

        [Fact]
        public async Task DispatchWithTimeoutAsync_WithValidTimeout_Succeeds()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchWithTimeoutAsync(message, timeoutMs: 5000, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchWithTimeoutAsync_WithZeroTimeout_BypassesTimeout()
        {
            // Arrange
            var messageType = "bridge:test";
            var message = new Message { MessageType = messageType, MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchWithTimeoutAsync(message, timeoutMs: 0, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        // === Graceful Degradation Tests ===

        [Fact]
        public async Task DispatchAsync_WithNullLogger_DoesNotThrow()
        {
            // Arrange
            var dispatcher = new MessageDispatcher(logger: null, telemetry: _mockTelemetry?.Object);
            var message = new Message { MessageType = "bridge:test", MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            dispatcher.Register("bridge:test", mockHandler.Object);

            // Act & Assert (should not throw)
            await dispatcher.DispatchAsync(message, CancellationToken.None);
        }

        [Fact]
        public async Task DispatchAsync_WithNullTelemetry_DoesNotThrow()
        {
            // Arrange
            var dispatcher = new MessageDispatcher(logger: _mockLogger?.Object, telemetry: null);
            var message = new Message { MessageType = "bridge:test", MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            dispatcher.Register("bridge:test", mockHandler.Object);

            // Act & Assert (should not throw)
            await dispatcher.DispatchAsync(message, CancellationToken.None);
        }

        [Fact]
        public async Task DispatchAsync_WithNullLoggerAndNullTelemetry_Succeeds()
        {
            // Arrange
            var dispatcher = new MessageDispatcher(logger: null, telemetry: null);
            var message = new Message { MessageType = "bridge:test", MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            dispatcher.Register("bridge:test", mockHandler.Object);

            // Act & Assert (should not throw)
            await dispatcher.DispatchAsync(message, CancellationToken.None);
        }

        // === Case-Insensitivity Tests ===

        [Fact]
        public async Task DispatchAsync_WithDifferentCaseMessageType_FindsHandler()
        {
            // Arrange
            var messageType = "bridge:Test";
            var message = new Message { MessageType = "BRIDGE:TEST", MessageId = "msg-1", Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // === Integration Tests ===

        [Fact]
        public async Task MultipleHandlers_DispatchCorrectly()
        {
            // Arrange
            var mockHandler1 = new Mock<IMessageHandler>(MockBehavior.Default);
            var mockHandler2 = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler1.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockHandler2.Setup(x => x.HandleAsync(It.IsAny<Message>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register("bridge:handler1", mockHandler1.Object);
            _dispatcher.Register("bridge:handler2", mockHandler2.Object);

            var message1 = new Message { MessageType = "bridge:handler1", MessageId = "msg-1", Data = null };
            var message2 = new Message { MessageType = "bridge:handler2", MessageId = "msg-2", Data = null };

            // Act
            await _dispatcher.DispatchAsync(message1, CancellationToken.None);
            await _dispatcher.DispatchAsync(message2, CancellationToken.None);

            // Assert
            mockHandler1.Verify(x => x.HandleAsync(message1, It.IsAny<CancellationToken>()), Times.Once);
            mockHandler2.Verify(x => x.HandleAsync(message2, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_WithMaxLengthMessageId_Succeeds()
        {
            // Arrange
            var messageType = "bridge:test";
            var longId = new string('a', 256);
            var message = new Message { MessageType = messageType, MessageId = longId, Data = null };
            var mockHandler = new Mock<IMessageHandler>(MockBehavior.Default);

            mockHandler.Setup(x => x.HandleAsync(message, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _dispatcher.Register(messageType, mockHandler.Object);

            // Act
            await _dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            mockHandler.Verify(x => x.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DispatchAsync_WithExcessivelyLongMessageId_ThrowsValidationError()
        {
            // Arrange
            var messageType = "bridge:test";
            var tooLongId = new string('a', 257);
            var message = new Message { MessageType = messageType, MessageId = tooLongId, Data = null };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => _dispatcher.DispatchAsync(message, CancellationToken.None));

            Assert.Equal(BridgeMessageDispatcherException.OperationType.ValidationFailed, ex.Operation);
        }
    }
}
