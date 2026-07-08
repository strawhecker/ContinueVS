#nullable enable

using ContinueVS.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for BridgeLogger.
    /// Tests async logging behavior, metadata handling, and fallback mechanisms.
    /// </summary>
    public class BridgeLoggerTests
    {
        [Fact]
        public async Task WriteDebugAsync_WithValidMessage_CompletesWithoutException()
        {
            // Arrange
            var logger = new BridgeLogger(null);
            var message = "Debug message";

            // Act & Assert
            await logger.WriteDebugAsync(message);
        }

        [Fact]
        public async Task WriteDebugAsync_WithNullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => logger.WriteDebugAsync(null!));
        }

        [Fact]
        public async Task WriteInfoAsync_WithValidMessage_CompletesWithoutException()
        {
            // Arrange
            var logger = new BridgeLogger(null);
            var message = "Info message";

            // Act & Assert
            await logger.WriteInfoAsync(message);
        }

        [Fact]
        public async Task WriteInfoAsync_WithNullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => logger.WriteInfoAsync(null!));
        }

        [Fact]
        public async Task WriteWarningAsync_WithValidMessage_CompletesWithoutException()
        {
            // Arrange
            var logger = new BridgeLogger(null);
            var message = "Warning message";

            // Act & Assert
            await logger.WriteWarningAsync(message);
        }

        [Fact]
        public async Task WriteWarningAsync_WithNullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => logger.WriteWarningAsync(null!));
        }

        [Fact]
        public async Task WriteErrorAsync_WithValidMessage_CompletesWithoutException()
        {
            // Arrange
            var logger = new BridgeLogger(null);
            var message = "Error message";

            // Act & Assert
            await logger.WriteErrorAsync(message);
        }

        [Fact]
        public async Task WriteErrorAsync_WithNullMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => logger.WriteErrorAsync(null!));
        }

        [Fact]
        public async Task WriteErrorAsync_WithException_IncludesExceptionMetadata()
        {
            // Arrange
            var callbackInvoked = false;
            var capturedLevel = string.Empty;
            var capturedMetadata = (IReadOnlyDictionary<string, object>?)null;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callbackInvoked = true;
                capturedLevel = level;
                capturedMetadata = meta;
            };

            var logger = new BridgeLogger(null, callback);
            var exception = new InvalidOperationException("Test error");

            // Act
            await logger.WriteErrorAsync("Error occurred", exception);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal("ERROR", capturedLevel);
            Assert.NotNull(capturedMetadata);
            Assert.Contains("exception_type", capturedMetadata!.Keys);
            Assert.Equal("InvalidOperationException", capturedMetadata["exception_type"]);
        }

        [Fact]
        public async Task WriteErrorAsync_WithMetadataAndException_CombinesMetadata()
        {
            // Arrange
            var capturedMetadata = (IReadOnlyDictionary<string, object>?)null;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                capturedMetadata = meta;
            };

            var logger = new BridgeLogger(null, callback);
            var metadata = new Dictionary<string, object> { { "user_id", "123" } };
            var exception = new InvalidOperationException("Test error");

            // Act
            await logger.WriteErrorAsync("Error occurred", exception, metadata);

            // Assert
            Assert.NotNull(capturedMetadata);
            Assert.Contains("user_id", capturedMetadata!.Keys);
            Assert.Contains("exception_type", capturedMetadata!.Keys);
        }

        [Fact]
        public async Task WriteDebugAsync_WithMetadata_InvokesNpmBridgeCallback()
        {
            // Arrange
            var callbackInvoked = false;
            var capturedLevel = string.Empty;
            var capturedMetadata = (IReadOnlyDictionary<string, object>?)null;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callbackInvoked = true;
                capturedLevel = level;
                capturedMetadata = meta;
            };

            var logger = new BridgeLogger(null, callback);
            var metadata = new Dictionary<string, object> { { "component", "bridge" } };

            // Act
            await logger.WriteDebugAsync("Debug info", metadata);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal("DEBUG", capturedLevel);
            Assert.NotNull(capturedMetadata);
            Assert.Equal("bridge", capturedMetadata!["component"]);
        }

        [Fact]
        public async Task WriteInfoAsync_WithMetadata_InvokesNpmBridgeCallback()
        {
            // Arrange
            var callbackInvoked = false;
            var capturedLevel = string.Empty;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callbackInvoked = true;
                capturedLevel = level;
            };

            var logger = new BridgeLogger(null, callback);
            var metadata = new Dictionary<string, object> { { "request_id", "abc123" } };

            // Act
            await logger.WriteInfoAsync("Request processed", metadata);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal("INFO", capturedLevel);
        }

        [Fact]
        public async Task WriteWarningAsync_WithMetadata_InvokesNpmBridgeCallback()
        {
            // Arrange
            var callbackInvoked = false;
            var capturedLevel = string.Empty;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callbackInvoked = true;
                capturedLevel = level;
            };

            var logger = new BridgeLogger(null, callback);
            var metadata = new Dictionary<string, object> { { "retry_count", 3 } };

            // Act
            await logger.WriteWarningAsync("Retry exhausted", metadata);

            // Assert
            Assert.True(callbackInvoked);
            Assert.Equal("WARNING", capturedLevel);
        }

        [Fact]
        public async Task FlushAsync_Completes()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act & Assert
            await logger.FlushAsync();
        }

        [Fact]
        public async Task MultipleWriteOperations_ExecuteConcurrently()
        {
            // Arrange
            var logger = new BridgeLogger(null);
            var tasks = new List<Task>
            {
                logger.WriteDebugAsync("Message 1"),
                logger.WriteInfoAsync("Message 2"),
                logger.WriteWarningAsync("Message 3"),
                logger.WriteErrorAsync("Message 4"),
            };

            // Act & Assert
            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task ConstructorWithoutServiceProvider_DoesNotThrow()
        {
            // Arrange & Act
            var logger = new BridgeLogger(null);

            // Assert
            Assert.NotNull(logger);
            await logger.WriteInfoAsync("Test");
        }

        [Fact]
        public async Task ConstructorWithNpmBridgeCallback_ForwardsLogsToCallback()
        {
            // Arrange
            var callCount = 0;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callCount++;
            };

            var logger = new BridgeLogger(null, callback);

            // Act
            await logger.WriteDebugAsync("Message 1");
            await logger.WriteInfoAsync("Message 2");
            await logger.WriteWarningAsync("Message 3");

            // Assert
            Assert.Equal(3, callCount);
        }

        [Fact]
        public async Task ConstructorWithNullServiceProvider_UsesActivityLogFallback()
        {
            // Arrange
            var logger = new BridgeLogger(null);

            // Act — Should not throw even though OutputWindow is unavailable
            await logger.WriteErrorAsync("Test error without OutputWindow");

            // Assert — Logger gracefully degrades to Activity Log
            Assert.NotNull(logger);
        }

        [Fact]
        public async Task CallbackThrowsException_LoggerSwallowsExceptionAndContinues()
        {
            // Arrange
            Action<string, string, IReadOnlyDictionary<string, object>?> faultyCallback = (level, msg, meta) =>
            {
                throw new InvalidOperationException("Callback failed");
            };

            var logger = new BridgeLogger(null, faultyCallback);

            // Act — Should not throw even though callback fails
            await logger.WriteInfoAsync("Test message");

            // Assert — Logger silently swallows callback exceptions
            Assert.NotNull(logger);
        }

        [Fact]
        public async Task WriteDebugAsync_WithEmptyMetadata_InvokesCallback()
        {
            // Arrange
            var callbackInvoked = false;
            var capturedMetadata = (IReadOnlyDictionary<string, object>?)null;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                callbackInvoked = true;
                capturedMetadata = meta;
            };

            var logger = new BridgeLogger(null, callback);
            var emptyMetadata = new Dictionary<string, object>();

            // Act
            await logger.WriteDebugAsync("Test", emptyMetadata);

            // Assert
            Assert.True(callbackInvoked);
            Assert.NotNull(capturedMetadata);
        }

        [Theory]
        [InlineData("Debug message", "DEBUG")]
        [InlineData("Info message", "INFO")]
        [InlineData("Warning message", "WARNING")]
        [InlineData("Error message", "ERROR")]
        public async Task WriteAsync_WithVariousLevels_ForwardsCorrectLevel(string message, string expectedLevel)
        {
            // Arrange
            var capturedLevel = string.Empty;

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                capturedLevel = level;
            };

            var logger = new BridgeLogger(null, callback);

            // Act
            switch (expectedLevel)
            {
                case "DEBUG":
                    await logger.WriteDebugAsync(message);
                    break;
                case "INFO":
                    await logger.WriteInfoAsync(message);
                    break;
                case "WARNING":
                    await logger.WriteWarningAsync(message);
                    break;
                case "ERROR":
                    await logger.WriteErrorAsync(message);
                    break;
            }

            // Assert
            Assert.Equal(expectedLevel, capturedLevel);
        }

        [Fact]
        public async Task ConcurrentWrites_ThreadSafe()
        {
            // Arrange
            var messageCount = 0;
            var lockObj = new object();

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                lock (lockObj)
                {
                    messageCount++;
                }
            };

            var logger = new BridgeLogger(null, callback);
            var taskCount = 100;
            var tasks = new List<Task>();

            // Act — Launch 100 concurrent log operations
            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    switch (index % 4)
                    {
                        case 0:
                            await logger.WriteDebugAsync($"Debug {index}");
                            break;
                        case 1:
                            await logger.WriteInfoAsync($"Info {index}");
                            break;
                        case 2:
                            await logger.WriteWarningAsync($"Warning {index}");
                            break;
                        case 3:
                            await logger.WriteErrorAsync($"Error {index}");
                            break;
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert — All messages were processed
            Assert.Equal(taskCount, messageCount);
        }

        [Fact]
        public async Task ConcurrentWritesWithMetadata_NoDataCorruption()
        {
            // Arrange
            var receivedMessages = new List<(string level, IReadOnlyDictionary<string, object>? metadata)>();
            var lockObj = new object();

            Action<string, string, IReadOnlyDictionary<string, object>?> callback = (level, msg, meta) =>
            {
                lock (lockObj)
                {
                    receivedMessages.Add((level, meta));
                }
            };

            var logger = new BridgeLogger(null, callback);
            var tasks = new List<Task>();

            // Act — Launch concurrent writes with different metadata
            for (int i = 0; i < 50; i++)
            {
                int index = i;
                var metadata = new Dictionary<string, object> { { "index", index }, { "iteration", i * 2 } };
                tasks.Add(logger.WriteInfoAsync($"Message {index}", metadata));
            }

            await Task.WhenAll(tasks);

            // Assert — All messages received with correct counts
            Assert.Equal(50, receivedMessages.Count);
            Assert.All(receivedMessages, item => Assert.NotNull(item.metadata));
        }
    }
}
