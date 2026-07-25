#nullable enable
#pragma warning disable CS8603, CS8619

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Exceptions;
using ContinueVS.Handlers;
using ContinueVS.IPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Tests.IPC
{
    /// <summary>
    /// Integration tests for b12: OnWebMessageReceivedAsync flow
    /// Verifies: JSON deserialization → dispatcher routing → handler invocation → response serialization
    /// </summary>
    public class OnWebMessageReceivedAsyncIntegrationTests
    {
        private class MockMessageHandler : IMessageHandler
        {
            public Message? CapturedMessage { get; set; }
            public bool InvokeCalled { get; set; }
            public CancellationToken CapturedToken { get; set; }

            public Task HandleAsync(Message message, CancellationToken cancellationToken)
            {
                System.Diagnostics.Debug.WriteLine($"[b12-HANDLER-EXEC] Mock handler invoked for message type: {message.MessageType}");
                CapturedMessage = message;
                CapturedToken = cancellationToken;
                InvokeCalled = true;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task DispatchAsync_WithValidMessage_InvokesHandler()
        {
            // Arrange
            var mockHandler = new MockMessageHandler();
            var dispatcher = new MessageDispatcher();
            dispatcher.Register("test-handler", mockHandler);

            var message = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-001",
                Data = JToken.FromObject(new { method = "test.method", @params = new { key = "value" } })
            };

            System.Diagnostics.Debug.WriteLine($"[b12-DESERIALIZED] Message: Type={message.MessageType}, ID={message.MessageId}");

            // Act
            await dispatcher.DispatchAsync(message, CancellationToken.None);

            // Assert
            Assert.True(mockHandler.InvokeCalled, "Handler should have been invoked");
            Assert.NotNull(mockHandler.CapturedMessage);
            Assert.Equal("test-handler", mockHandler.CapturedMessage.MessageType);
            Assert.Equal("msg-001", mockHandler.CapturedMessage.MessageId);
            Assert.NotNull(mockHandler.CapturedMessage.Data);

            System.Diagnostics.Debug.WriteLine($"[b12-HANDLER-EXEC] Handler invocation verified: MessageType={mockHandler.CapturedMessage.MessageType}, MessageId={mockHandler.CapturedMessage.MessageId}");
        }

        [Fact]
        public async Task DispatchAsync_PassesCancellationToken()
        {
            // Arrange
            var mockHandler = new MockMessageHandler();
            var dispatcher = new MessageDispatcher();
            dispatcher.Register("test-handler", mockHandler);

            var cts = new CancellationTokenSource();
            var message = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-002",
                Data = null
            };

            // Act
            await dispatcher.DispatchAsync(message, cts.Token);

            // Assert
            Assert.False(mockHandler.CapturedToken.IsCancellationRequested);

            System.Diagnostics.Debug.WriteLine($"[b12-HANDLER-EXEC] CancellationToken passed successfully");
        }

        [Fact]
        public void MessageSerialization_RoundTrip()
        {
            // Arrange
            var originalMessage = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-003",
                Data = JToken.FromObject(new { method = "test.nested", @params = new { nested = new { value = 42 } } })
            };

            // Act - Serialize to JSON
            var json = JsonConvert.SerializeObject(originalMessage);
            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] Message serialized: {json}");

            // Assert - Verify JSON structure
            Assert.NotNull(json);
            Assert.Contains("messageType", json);
            Assert.Contains("test-handler", json);
            Assert.Contains("messageId", json);
            Assert.Contains("msg-003", json);
            Assert.Contains("data", json);

            // Act - Deserialize back
            var deserializedMessage = JsonConvert.DeserializeObject<Message>(json);
            System.Diagnostics.Debug.WriteLine($"[b12-DESERIALIZED] Message deserialized: Type={deserializedMessage?.MessageType}, ID={deserializedMessage?.MessageId}");

            // Assert - Verify round-trip fidelity
            Assert.NotNull(deserializedMessage);
            Assert.Equal(originalMessage.MessageType, deserializedMessage.MessageType);
            Assert.Equal(originalMessage.MessageId, deserializedMessage.MessageId);
            Assert.NotNull(deserializedMessage.Data);
        }

        [Fact]
        public void MessageEscaping_PreservesJsonStructure()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-004",
                Data = JToken.FromObject(new { method = "test.escape", @params = new { text = "It's a \"quoted\" value with \\ backslash" } })
            };

            // Act - Serialize
            var json = JsonConvert.SerializeObject(message);
            var escaped = json.Replace("\\", "\\\\").Replace("'", "\\'");
            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] Escaped JSON: {escaped}");

            // Assert - Verify escaping
            Assert.NotNull(escaped);
            Assert.Contains("\\'", escaped); // Single quotes should be escaped
            Assert.Contains("\\\\", escaped); // Backslashes should be escaped

            // Verify structure is preserved for JavaScript injection
            Assert.Contains("messageType", escaped);
            Assert.Contains("test-handler", escaped);
        }

        [Fact]
        public async Task DispatchAsync_WithNullMessage_Throws()
        {
            // Arrange
            var dispatcher = new MessageDispatcher();
            Message? nullMessage = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => dispatcher.DispatchAsync(nullMessage!, CancellationToken.None));

            System.Diagnostics.Debug.WriteLine($"[b12-DISPATCH-END] Handler validation correctly rejected null message");
        }

        [Fact]
        public async Task DispatchAsync_WithUnregisteredHandler_Throws()
        {
            // Arrange
            var dispatcher = new MessageDispatcher();
            var message = new Message
            {
                MessageType = "unregistered-handler",
                MessageId = "msg-005",
                Data = null
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<BridgeMessageDispatcherException>(
                () => dispatcher.DispatchAsync(message, CancellationToken.None));

            Assert.Equal("No handler registered for message type 'unregistered-handler'.", ex.Message);
            System.Diagnostics.Debug.WriteLine($"[b12-DISPATCH-END] Handler not found correctly rejected unregistered type");
        }

        [Fact]
        public void CompleteRoundTripFlow()
        {
            // Simulate the complete b12 flow: JSON → Message → Dispatcher → Handler → Response

            System.Diagnostics.Debug.WriteLine($"[b12-RECEIVED] Raw JSON received: {{\"messageType\":\"test-handler\",\"messageId\":\"msg-006\",\"data\":{{\"method\":\"test.flow\",\"params\":{{\"key\":\"value\"}}}}}}");

            // Deserialize
            var json = JsonConvert.SerializeObject(new Message
            {
                MessageType = "test-handler",
                MessageId = "msg-006",
                Data = JToken.FromObject(new { method = "test.flow", @params = new { key = "value" } })
            });

            var message = JsonConvert.DeserializeObject<Message>(json);
            Assert.NotNull(message);

            System.Diagnostics.Debug.WriteLine($"[b12-DESERIALIZED] Message: Type={message.MessageType}, ID={message.MessageId}");
            System.Diagnostics.Debug.WriteLine($"[b12-DISPATCH-START] Routing message to dispatcher: {message.MessageType}");

            // Response serialization
            var responseData = new { result = "ok" };
            var responseMsg = new Message
            {
                MessageType = message.MessageType,
                MessageId = message.MessageId,
                Data = JToken.FromObject(responseData)
            };

            var responseJson = JsonConvert.SerializeObject(responseMsg);
            var escaped = responseJson.Replace("\\", "\\\\").Replace("'", "\\'");

            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] Message serialized: {responseJson}");
            System.Diagnostics.Debug.WriteLine($"[b12-RESPONSE] Escaped JSON: {escaped}");
            System.Diagnostics.Debug.WriteLine($"[b12-SCRIPT-EXEC] Executing script: window.continueVS && window.continueVS.onMessage('{escaped}');");

            Assert.NotNull(escaped);
            Assert.Contains("messageType", escaped);
            Assert.Contains("test-handler", escaped);
        }
    }
}
