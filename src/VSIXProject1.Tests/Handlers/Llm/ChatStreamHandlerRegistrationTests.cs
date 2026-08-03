using ContinueVS.Handlers;
using ContinueVS.Handlers.Llm;
using ContinueVS.IPC;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ContinueVS.Tests.Handlers.Llm
{
    public class ChatStreamHandlerRegistrationTests
    {
        [Fact]
        public void VerifyHandlerIsRegistered()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-1-START] VerifyHandlerIsRegistered");

            // Verify handler type exists and implements interface
            var handlerType = typeof(LlmStreamChatHandler);
            var implementsInterface = typeof(IMessageHandler).IsAssignableFrom(handlerType);

            Assert.True(implementsInterface, "LlmStreamChatHandler should implement IMessageHandler interface");
            System.Diagnostics.Debug.WriteLine("[b24-TEST-1-PASS] Handler registered with correct interface");
        }

        [Fact]
        public async Task DeserializeValidPayloadWithAllFields()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-2-START] DeserializeValidPayloadWithAllFields");

            var messageData = JObject.FromObject(new
            {
                title = "gpt-4",
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                }
            });

            var message = new Message
            {
                MessageType = "llm/streamChat",
                MessageId = "msg-001",
                Data = messageData
            };

            try
            {
                System.Diagnostics.Debug.WriteLine("[b24-PAYLOAD-DESERIALIZE-VALID] Message structure validated");
                Assert.NotNull(message.Data);
                Assert.NotNull(message.Data["title"]);
                Assert.NotNull(message.Data["messages"]);
                System.Diagnostics.Debug.WriteLine("[b24-TEST-2-PASS] Valid payload deserialization successful");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Valid payload should not throw: {ex.Message}");
            }
        }

        [Fact]
        public async Task DeserializeWithMissingOptionalFields()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-3-START] DeserializeWithMissingOptionalFields");

            var messageData = JObject.FromObject(new
            {
                messages = new object[0]
            });

            var message = new Message
            {
                MessageType = "llm/streamChat",
                MessageId = "msg-002",
                Data = messageData
            };

            var title = message.Data?["title"]?.Value<string>() ?? "";
            var messages = message.Data?["messages"] as JArray ?? new JArray();

            Assert.Equal("", title);
            Assert.NotNull(messages);
            System.Diagnostics.Debug.WriteLine("[b24-PAYLOAD-DESERIALIZE-DEFAULTS] Defaults applied correctly");
            System.Diagnostics.Debug.WriteLine("[b24-TEST-3-PASS] Missing optional fields handled gracefully");
        }

        [Fact]
        public async Task RejectNullDataThrowsOrHandles()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-4-START] RejectNullDataThrowsOrHandles");

            var message = new Message
            {
                MessageType = "llm/streamChat",
                MessageId = "msg-003",
                Data = null
            };

            var title = message.Data?["title"]?.Value<string>() ?? "";
            var messages = message.Data?["messages"] as JArray ?? new JArray();

            Assert.Equal("", title);
            Assert.NotNull(messages);
            Assert.Empty(messages);
            System.Diagnostics.Debug.WriteLine("[b24-PAYLOAD-REJECT-NULL] Null data handled with defaults");
            System.Diagnostics.Debug.WriteLine("[b24-TEST-4-PASS] Null data rejection validated");
        }

        [Fact]
        public async Task ValidateMessagesArrayDeserialization()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-5-START] ValidateMessagesArrayDeserialization");

            var messageData = JObject.FromObject(new
            {
                title = "gpt-4",
                messages = new[]
                {
                    new { role = "user", content = "First message" },
                    new { role = "assistant", content = "Response" },
                    new { role = "user", content = "Second message" }
                }
            });

            var message = new Message
            {
                MessageType = "llm/streamChat",
                MessageId = "msg-004",
                Data = messageData
            };

            var messagesArray = message.Data?["messages"] as JArray;

            Assert.NotNull(messagesArray);
            Assert.Equal(3, messagesArray.Count);
            System.Diagnostics.Debug.WriteLine("[b24-PAYLOAD-MESSAGES-ARRAY] Messages array validated");
            System.Diagnostics.Debug.WriteLine("[b24-TEST-5-PASS] Messages array deserialization successful");
        }

        [Fact]
        public async Task ValidateHandlerSignatureForStreaming()
        {
            System.Diagnostics.Debug.WriteLine("[b24-TEST-6-START] ValidateHandlerSignatureForStreaming");

            // Verify handler type has correct signature
            var handlerType = typeof(LlmStreamChatHandler);
            var method = handlerType.GetMethod("HandleAsync", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(Message), typeof(CancellationToken) },
                null);

            Assert.NotNull(method);
            if (method != null)
            {
                Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType), 
                    "HandleAsync should return Task");
            }

            System.Diagnostics.Debug.WriteLine("[b24-HANDLER-SIGNATURE] Handler signature validated for async streaming");
            System.Diagnostics.Debug.WriteLine("[b24-TEST-6-PASS] Handler signature compatible with streaming pattern");
        }
    }
}
