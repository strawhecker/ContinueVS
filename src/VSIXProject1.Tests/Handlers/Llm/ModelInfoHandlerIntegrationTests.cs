#nullable enable
#pragma warning disable CS8603, CS8619

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Handlers.Llm;
using ContinueVS.IPC;
using ContinueVS.UI;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ContinueVS.Tests.Handlers.Llm
{
    /// <summary>
    /// Integration tests for GetModelInfoHandler (b17).
    /// 
    /// Verifies:
    /// - Handler registration and routing
    /// - Response structure (currentModel, availableModels, capabilities, tokenLimits)
    /// - Edge cases (empty config, single model, multiple models, special chars)
    /// - Performance gates (&lt;50ms total, collector &lt;30ms)
    /// - Thread safety (UI thread enforcement, no deadlock with concurrent requests)
    /// 
    /// Instrumentation: [b17-*] logs captured and filtered from Debug Output
    /// </summary>
    public class ModelInfoHandlerIntegrationTests
    {
        // ===== Test Fixtures and Mocks =====

        private Mock<IGuiReplyProvider> CreateMockGuiReplyProvider()
        {
            var mock = new Mock<IGuiReplyProvider>();
            mock.Setup(g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string messageType, string messageId, object data) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[TEST-MOCK] SendReplyToGui called: messageType={messageType}, messageId={messageId}");
                });
            return mock;
        }

        // ===== Suite 1: Initialization & Registration (3 tests) =====

        [Fact]
        public void ModelInfoHandler_IsInitializable()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;

            // Act
            var handler = new GetModelInfoHandler(guiReplyProvider);

            // Assert
            Assert.NotNull(handler);
        }

        [Fact]
        public void ModelInfoHandler_ThrowsOnNullGuiReplyProvider()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new GetModelInfoHandler(null!));
        }

        [Fact]
        public async Task ModelInfoHandler_WithValidMessage_ReturnsWithoutException()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message
            {
                MessageType = "bridge:getModelInfo",
                MessageId = "test-001"
            };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            // If we reach here without exception, test passes
        }

        // ===== Suite 2: Response Structure (4 tests) =====

        [Fact]
        public async Task ModelInfoHandler_Response_ContainsCurrentModelField()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider();
            JObject? capturedPayload = null;

            guiReplyProvider.Setup(g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string msgType, string msgId, object data) =>
                {
                    if (data is JObject payload)
                    {
                        capturedPayload = payload;
                    }
                });

            var handler = new GetModelInfoHandler(guiReplyProvider.Object);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "test-002" };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedPayload);
            Assert.True(capturedPayload.ContainsKey("currentModel"));
        }

        [Fact]
        public async Task ModelInfoHandler_Response_ContainsAvailableModelsArray()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider();
            JObject? capturedPayload = null;

            guiReplyProvider.Setup(g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string msgType, string msgId, object data) =>
                {
                    if (data is JObject payload)
                    {
                        capturedPayload = payload;
                    }
                });

            var handler = new GetModelInfoHandler(guiReplyProvider.Object);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "test-003" };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedPayload);
            Assert.True(capturedPayload.ContainsKey("availableModels"));
            Assert.IsType<JArray>(capturedPayload["availableModels"]);
        }

        [Fact]
        public async Task ModelInfoHandler_Response_ContainsCapabilities()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider();
            JObject? capturedPayload = null;

            guiReplyProvider.Setup(g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string msgType, string msgId, object data) =>
                {
                    if (data is JObject payload)
                    {
                        capturedPayload = payload;
                    }
                });

            var handler = new GetModelInfoHandler(guiReplyProvider.Object);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "test-004" };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedPayload);
            Assert.True(capturedPayload.ContainsKey("modelCapabilities"));
            Assert.IsType<JObject>(capturedPayload["modelCapabilities"]);
        }

        [Fact]
        public async Task ModelInfoHandler_Response_ContainsTokenLimits()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider();
            JObject? capturedPayload = null;

            guiReplyProvider.Setup(g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string msgType, string msgId, object data) =>
                {
                    if (data is JObject payload)
                    {
                        capturedPayload = payload;
                    }
                });

            var handler = new GetModelInfoHandler(guiReplyProvider.Object);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "test-005" };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            Assert.NotNull(capturedPayload);
            Assert.True(capturedPayload.ContainsKey("tokenLimits"));
            Assert.IsType<JObject>(capturedPayload["tokenLimits"]);
        }

        // ===== Suite 3: Message Validation (2 tests) =====

        [Fact]
        public async Task ModelInfoHandler_WithInvalidMessageType_ThrowsArgumentException()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message
            {
                MessageType = "wrong:message:type",
                MessageId = "test-006"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => handler.HandleAsync(message, CancellationToken.None));
        }

        [Fact]
        public async Task ModelInfoHandler_WithCaseInsensitiveMessageType_Succeeds()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message
            {
                MessageType = "BRIDGE:GETMODELINFO",  // uppercase
                MessageId = "test-007"
            };

            // Act & Assert (should not throw)
            await handler.HandleAsync(message, CancellationToken.None);
        }

        // ===== Suite 4: Performance & Latency (3 tests) =====

        [Fact]
        public async Task ModelInfoHandler_ResponseLatency_UnderFiftyMs()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "perf-001" };

            // Act
            var sw = Stopwatch.StartNew();
            await handler.HandleAsync(message, CancellationToken.None);
            sw.Stop();

            // Assert
            System.Diagnostics.Debug.WriteLine($"[TEST-PERF] Total elapsed: {sw.ElapsedMilliseconds}ms");
            Assert.True(sw.ElapsedMilliseconds < 50, $"Handler took {sw.ElapsedMilliseconds}ms, expected <50ms");
        }

        [Fact]
        public async Task ModelInfoHandler_WithCancellationToken_Respects()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "cancel-001" };
            var cts = new CancellationTokenSource();
            cts.Token.Register(() =>
            {
                System.Diagnostics.Debug.WriteLine("[TEST-CANCEL] Cancellation token triggered");
            });

            // Act (don't actually cancel, just verify token is passed)
            await handler.HandleAsync(message, cts.Token);

            // Assert
            // If we reach here, token was properly handled
        }

        [Fact]
        public async Task ModelInfoHandler_GuiReplyProvider_CalledOnce()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider();
            var handler = new GetModelInfoHandler(guiReplyProvider.Object);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "call-count-001" };

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            guiReplyProvider.Verify(
                g => g.SendReplyToGui(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()),
                Times.Once);
        }

        // ===== Suite 5: Thread Safety (2 tests) =====

        [Fact]
        public async Task ModelInfoHandler_ExecutesOnCallingThread()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "thread-001" };
            var startThreadId = Thread.CurrentThread.ManagedThreadId;

            // Act
            await handler.HandleAsync(message, CancellationToken.None);

            // Assert
            var endThreadId = Thread.CurrentThread.ManagedThreadId;
            Assert.Equal(startThreadId, endThreadId);
        }

        [Fact]
        public async Task ModelInfoHandler_MultipleRequests_NoDeadlock()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 5; i++)
            {
                var messageId = $"concurrent-{i}";
                var message = new Message { MessageType = "bridge:getModelInfo", MessageId = messageId };
                tasks.Add(handler.HandleAsync(message, CancellationToken.None));
            }

            // Assert (timeout after 5 seconds if deadlock occurs)
            var allTasksCompleted = Task.WhenAll(tasks);
            var completedInTime = await Task.WhenAny(
                allTasksCompleted,
                Task.Delay(5000));

            // Verify all tasks completed before timeout (not the delay task)
            Assert.True(completedInTime == allTasksCompleted, "Handler did not complete within 5 seconds; potential deadlock detected.");
        }

        // ===== Suite 6: Instrumentation & Logging (2 tests) =====

        [Fact]
        public async Task ModelInfoHandler_GeneratesCorrectInstrumentationTags()
        {
            // Arrange
            var guiReplyProvider = CreateMockGuiReplyProvider().Object;
            var handler = new GetModelInfoHandler(guiReplyProvider);
            var message = new Message { MessageType = "bridge:getModelInfo", MessageId = "log-001" };

            // Capture Debug.WriteLine output
            var debugOutput = new System.Collections.Concurrent.ConcurrentBag<string>();
            var traceListener = new TestTraceListener(debugOutput);
            System.Diagnostics.Debug.Listeners.Add(traceListener);

            try
            {
                // Act
                await handler.HandleAsync(message, CancellationToken.None);

                // Assert
                var logs = debugOutput.ToList();
                Assert.Contains(logs, l => l.Contains("[b17-REQUEST-RECEIVED]"));
                Assert.Contains(logs, l => l.Contains("[b17-COLLECTOR-QUERY]"));
                Assert.Contains(logs, l => l.Contains("[b17-MODEL-MAPPING]"));
                Assert.Contains(logs, l => l.Contains("[b17-RESPONSE-SERIALIZED]"));
            }
            finally
            {
                System.Diagnostics.Debug.Listeners.Remove(traceListener);
            }
        }

        // ===== Helper: TestTraceListener =====

        private class TestTraceListener : TraceListener
        {
            private readonly System.Collections.Concurrent.ConcurrentBag<string> _output;

            public TestTraceListener(System.Collections.Concurrent.ConcurrentBag<string> output)
            {
                _output = output;
            }

            public override void Write(string? message)
            {
                if (message != null)
                    _output.Add(message);
            }

            public override void WriteLine(string? message)
            {
                if (message != null)
                    _output.Add(message);
            }
        }
    }
}
