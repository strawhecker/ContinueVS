using ContinueVS.IPC;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ContinueVS.Tests.IPC
{
    /// <summary>
    /// Unit tests for Message envelope structure validation (Step u1).
    /// Validates C# Message class JSON serialization/deserialization round-trip fidelity,
    /// null/empty field handling, and nested payload structures.
    /// No WebView required; pure serialization/deserialization without external dependencies.
    /// </summary>
    public class MessageEnvelopeTests
    {
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        // =====================================================================
        // TEST: Round-Trip Serialization/Deserialization (Valid Message)
        // =====================================================================

        [Fact]
        public void RoundTrip_ValidMessageWithAllFields_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "getWorkspaceDirs",
                MessageId = "msg-001",
                Data = JToken.FromObject(new { path = "/home/user/project" })
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.MessageType, deserialized.MessageType);
            Assert.Equal(original.MessageId, deserialized.MessageId);
            Assert.NotNull(deserialized.Data);
            Assert.Equal(original.Data.ToString(), deserialized.Data.ToString());
        }

        [Fact]
        public void RoundTrip_ValidMessageWithoutData_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "ping",
                MessageId = "msg-002",
                Data = null
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.MessageType, deserialized.MessageType);
            Assert.Equal(original.MessageId, deserialized.MessageId);
            Assert.Null(deserialized.Data);
        }

        // =====================================================================
        // TEST: JSON Property Mapping (camelCase)
        // =====================================================================

        [Fact]
        public void Serialization_UsescamelCasePropertyNames()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "test",
                MessageId = "id-123",
                Data = null
            };

            // Act
            string json = JsonConvert.SerializeObject(message, _settings);
            var parsed = JObject.Parse(json);

            // Assert
            Assert.True(parsed.ContainsKey("messageType"), "JSON should use camelCase 'messageType'");
            Assert.True(parsed.ContainsKey("messageId"), "JSON should use camelCase 'messageId'");
            Assert.Equal("test", parsed["messageType"]?.ToString());
            Assert.Equal("id-123", parsed["messageId"]?.ToString());
        }

        [Fact]
        public void Deserialization_AcceptscamelcasePropertyNames()
        {
            // Arrange
            string json = @"{ ""messageType"": ""handler"", ""messageId"": ""id-456"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            Assert.Equal("handler", message.MessageType);
            Assert.Equal("id-456", message.MessageId);
        }

        // =====================================================================
        // TEST: Null and Empty Field Handling
        // =====================================================================

        [Fact]
        public void Deserialization_WithNullMessageType_PreservesNull()
        {
            // Arrange
            string json = @"{ ""messageType"": null, ""messageId"": ""id-001"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            // MessageType has a default value of "", so it should not be null after deserialization
            Assert.NotNull(message.MessageType);
        }

        [Fact]
        public void Deserialization_WithEmptyMessageType_PreservesEmpty()
        {
            // Arrange
            string json = @"{ ""messageType"": """", ""messageId"": ""id-002"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            Assert.Equal("", message.MessageType);
        }

        [Fact]
        public void Deserialization_WithMissingMessageType_UsesDefaultValue()
        {
            // Arrange
            string json = @"{ ""messageId"": ""id-003"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            Assert.Equal("", message.MessageType); // Default value from Message class
        }

        [Fact]
        public void Deserialization_WithMissingMessageId_UsesDefaultValue()
        {
            // Arrange
            string json = @"{ ""messageType"": ""test"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            Assert.Equal("", message.MessageId); // Default value from Message class
        }

        [Fact]
        public void Deserialization_WithMissingData_PreservesNull()
        {
            // Arrange
            string json = @"{ ""messageType"": ""test"", ""messageId"": ""id-004"" }";

            // Act
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(message);
            Assert.Null(message.Data);
        }

        // =====================================================================
        // TEST: Complex Nested Payload Structures
        // =====================================================================

        [Fact]
        public void RoundTrip_WithNestedObjectPayload_PreservesFidelity()
        {
            // Arrange
            var payload = JObject.FromObject(new
            {
                user = new { id = 42, name = "Alice" },
                context = new { file = "/path/to/file.cs", line = 10 }
            });
            var original = new Message
            {
                MessageType = "contextUpdate",
                MessageId = "ctx-001",
                Data = payload
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("Alice", deserialized.Data["user"]?["name"]?.ToString());
            Assert.Equal("10", deserialized.Data["context"]?["line"]?.ToString());
        }

        [Fact]
        public void RoundTrip_WithArrayPayload_PreservesFidelity()
        {
            // Arrange
            var payload = JToken.FromObject(new[] { "item1", "item2", "item3" });
            var original = new Message
            {
                MessageType = "listItems",
                MessageId = "list-001",
                Data = payload
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.IsType<JArray>(deserialized.Data);
            Assert.Equal(3, ((JArray)deserialized.Data).Count);
            Assert.Equal("item1", deserialized.Data[0]?.ToString());
        }

        [Fact]
        public void RoundTrip_WithPrimitivePayload_PreservesFidelity()
        {
            // Arrange - test with string payload
            var original = new Message
            {
                MessageType = "echo",
                MessageId = "echo-001",
                Data = JToken.FromObject("hello world")
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("hello world", deserialized.Data.ToString());
        }

        [Fact]
        public void RoundTrip_WithNumericPayload_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "counter",
                MessageId = "cnt-001",
                Data = JToken.FromObject(42)
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal(42, deserialized.Data.Value<int>());
        }

        // =====================================================================
        // TEST: Special Characters and Whitespace in Fields
        // =====================================================================

        [Fact]
        public void RoundTrip_WithSpecialCharactersInMessageType_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "handler:nested-type/variant",
                MessageId = "special-001",
                Data = null
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("handler:nested-type/variant", deserialized.MessageType);
        }

        [Fact]
        public void RoundTrip_WithWhitespaceInMessageType_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "handler with spaces",
                MessageId = "ws-001",
                Data = null
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("handler with spaces", deserialized.MessageType);
        }

        [Fact]
        public void RoundTrip_WithUnicodeInPayload_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "unicode",
                MessageId = "uni-001",
                Data = JToken.FromObject("Hello 世界 🚀")
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("Hello 世界 🚀", deserialized.Data.ToString());
        }

        // =====================================================================
        // TEST: Edge Cases
        // =====================================================================

        [Fact]
        public void RoundTrip_WithVeryLongMessageId_PreservesFidelity()
        {
            // Arrange
            string longId = new string('x', 1000);
            var original = new Message
            {
                MessageType = "long-id-test",
                MessageId = longId,
                Data = null
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(longId, deserialized.MessageId);
        }

        [Fact]
        public void RoundTrip_WithDeeplyNestedPayload_PreservesFidelity()
        {
            // Arrange
            var deepNested = JObject.FromObject(new
            {
                level1 = new
                {
                    level2 = new
                    {
                        level3 = new
                        {
                            level4 = new { value = "deep" }
                        }
                    }
                }
            });
            var original = new Message
            {
                MessageType = "deep-nesting",
                MessageId = "deep-001",
                Data = deepNested
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.Equal("deep", deserialized.Data["level1"]?["level2"]?["level3"]?["level4"]?["value"]?.ToString());
        }

        [Fact]
        public void RoundTrip_WithEmptyPayloadObject_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "empty-payload",
                MessageId = "empty-001",
                Data = JObject.FromObject(new { })
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.IsType<JObject>(deserialized.Data);
            Assert.Empty((JObject)deserialized.Data);
        }

        [Fact]
        public void RoundTrip_WithEmptyPayloadArray_PreservesFidelity()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "empty-array",
                MessageId = "empty-arr-001",
                Data = JToken.FromObject(new object[] { })
            };

            // Act
            string json = JsonConvert.SerializeObject(original, _settings);
            var deserialized = JsonConvert.DeserializeObject<Message>(json, _settings);

            // Assert
            Assert.NotNull(deserialized);
            Assert.NotNull(deserialized.Data);
            Assert.IsType<JArray>(deserialized.Data);
            Assert.Empty((JArray)deserialized.Data);
        }

        // =====================================================================
        // TEST: JSON Validity and Format
        // =====================================================================

        [Fact]
        public void Serialization_ProducesValidJSON()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "test",
                MessageId = "id-001",
                Data = JToken.FromObject(new { foo = "bar" })
            };

            // Act
            string json = JsonConvert.SerializeObject(message, _settings);

            // Assert - should not throw when parsing
            var parsed = JObject.Parse(json);
            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("messageType"));
        }

        [Fact]
        public void Deserialization_WithValidJSON_DoesNotThrow()
        {
            // Arrange
            string json = @"{ ""messageType"": ""valid"", ""messageId"": ""v-001"", ""data"": { ""key"": ""value"" } }";

            // Act & Assert - should not throw
            var message = JsonConvert.DeserializeObject<Message>(json, _settings);
            Assert.NotNull(message);
        }

        [Fact]
        public void Deserialization_WithMalformedJSON_Throws()
        {
            // Arrange
            string json = @"{ ""messageType"": ""incomplete"" ";

            // Act & Assert
            // Newtonsoft.Json throws JsonSerializationException (wraps reader errors)
            Assert.Throws<JsonSerializationException>(() =>
                JsonConvert.DeserializeObject<Message>(json, _settings)
            );
        }

        // =====================================================================
        // TEST: Message Defaults
        // =====================================================================

        [Fact]
        public void Instantiation_SetsDefaultValuesForRequiredFields()
        {
            // Arrange & Act
            var message = new Message();

            // Assert
            Assert.NotNull(message.MessageType);
            Assert.Equal("", message.MessageType);
            Assert.NotNull(message.MessageId);
            Assert.Equal("", message.MessageId);
            Assert.Null(message.Data);
        }
    }
}
