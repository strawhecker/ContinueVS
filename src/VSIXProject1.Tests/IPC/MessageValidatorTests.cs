using ContinueVS.IPC;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ContinueVS.Tests.IPC
{
    /// <summary>
    /// Unit tests for MessageValidator static utility.
    ///
    /// Tests message envelope validation, payload validation (requests/responses),
    /// and error response building per JSON-RPC 2.0 spec.
    ///
    /// Related: Step 73 (validation), Step 14 (dispatcher), Step 47 (middleware)
    /// </summary>
    public class MessageValidatorTests
    {
        // =====================================================================
        // Suite 1: Envelope Validation — Happy Path (2 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidateEnvelope: Valid message with all fields returns true")]
        public void ValidateEnvelope_ValidMessage_ReturnsTrue()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "bridge:test",
                MessageId = "msg-001",
                Data = new JObject { ["method"] = "test" },
            };

            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(message);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact(DisplayName = "ValidateEnvelope: Message with whitespace fields returns false")]
        public void ValidateEnvelope_WhitespaceFields_ReturnsFalse()
        {
            // Arrange: message with empty messageType
            var message = new Message
            {
                MessageType = "   ",
                MessageId = "msg-001",
                Data = new JObject(),
            };

            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(message);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(error);
            Assert.Contains("messageType", error);
        }

        // =====================================================================
        // Suite 2: Envelope Validation — Invalid (4 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidateEnvelope: Null message returns false")]
        public void ValidateEnvelope_NullMessage_ReturnsFalse()
        {
            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(null);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(error);
            Assert.Contains("null", error);
        }

        [Fact(DisplayName = "ValidateEnvelope: Empty messageId returns false")]
        public void ValidateEnvelope_EmptyMessageId_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "bridge:test",
                MessageId = "",
                Data = new JObject(),
            };

            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(message);

            // Assert
            Assert.False(isValid);
            Assert.Contains("messageId", error);
        }

        [Fact(DisplayName = "ValidateEnvelope: Null data is allowed (for some message types)")]
        public void ValidateEnvelope_NullData_Allowed()
        {
            // Arrange: null data is allowed for messages without payload
            var message = new Message
            {
                MessageType = "bridge:test",
                MessageId = "msg-001",
                Data = null,
            };

            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(message);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact(DisplayName = "ValidateEnvelope: Null messageType returns false")]
        public void ValidateEnvelope_NullMessageType_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = null,
                MessageId = "msg-001",
                Data = new JObject(),
            };

            // Act
            var (isValid, error) = MessageValidator.ValidateEnvelope(message);

            // Assert
            Assert.False(isValid);
            Assert.Contains("messageType", error);
        }

        // =====================================================================
        // Suite 3: Request Payload Validation — Happy Path (2 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidatePayload: Valid request with method and id returns true")]
        public void ValidatePayload_ValidRequest_ReturnsTrue()
        {
            // Arrange
            var data = new JObject
            {
                ["method"] = "search",
                ["params"] = new JObject { ["query"] = "test" },
                ["id"] = 1,
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: true);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
            Assert.Null(code);
        }

        [Fact(DisplayName = "ValidatePayload: Notification (no id) returns true")]
        public void ValidatePayload_Notification_ReturnsTrue()
        {
            // Arrange
            var data = new JObject { ["method"] = "onStateChange" };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: true);

            // Assert
            Assert.True(isValid);
        }

        // =====================================================================
        // Suite 4: Request Payload Validation — Invalid (3 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidatePayload: Missing method returns false with code -32600")]
        public void ValidatePayload_MissingMethod_ReturnsFalse()
        {
            // Arrange
            var data = new JObject { ["params"] = new JObject(), ["id"] = 1 };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: true);

            // Assert
            Assert.False(isValid);
            Assert.Contains("method", error);
            Assert.Equal(-32600, code);
        }

        [Fact(DisplayName = "ValidatePayload: Invalid params type returns false with code -32602")]
        public void ValidatePayload_InvalidParamsType_ReturnsFalse()
        {
            // Arrange
            var data = new JObject
            {
                ["method"] = "test",
                ["params"] = "string instead of object", // Invalid
                ["id"] = 1,
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: true);

            // Assert
            Assert.False(isValid);
            Assert.Contains("params", error);
            Assert.Equal(-32602, code);
        }

        [Fact(DisplayName = "ValidatePayload: Invalid id type returns false")]
        public void ValidatePayload_InvalidIdType_ReturnsFalse()
        {
            // Arrange
            var data = new JObject
            {
                ["method"] = "test",
                ["id"] = JValue.CreateString("true"), // Boolean-like string, should be number/string
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: true);

            // Assert
            // Note: string id should be allowed, so let's test with object id
            var dataWithObjectId = new JObject
            {
                ["method"] = "test",
                ["id"] = new JObject(), // Object id is invalid
            };

            var (isValid2, error2, code2) =
                MessageValidator.ValidatePayload(dataWithObjectId, isRequest: true);
            Assert.False(isValid2);
            Assert.Equal(-32600, code2);
        }

        // =====================================================================
        // Suite 5: Response Payload Validation — Happy Path (2 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidatePayload: Response with result returns true")]
        public void ValidatePayload_ResponseWithResult_ReturnsTrue()
        {
            // Arrange
            var data = new JObject
            {
                ["id"] = 1,
                ["result"] = new JObject { ["success"] = true },
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: false);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact(DisplayName = "ValidatePayload: Response with error returns true")]
        public void ValidatePayload_ResponseWithError_ReturnsTrue()
        {
            // Arrange
            var data = new JObject
            {
                ["id"] = 1,
                ["error"] = new JObject
                {
                    ["code"] = -32601,
                    ["message"] = "Method not found",
                },
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: false);

            // Assert
            Assert.True(isValid);
        }

        // =====================================================================
        // Suite 6: Response Payload Validation — Invalid (3 tests)
        // =====================================================================

        [Fact(DisplayName = "ValidatePayload: Response with both result and error returns false")]
        public void ValidatePayload_ResponseBothResultAndError_ReturnsFalse()
        {
            // Arrange
            var data = new JObject
            {
                ["id"] = 1,
                ["result"] = "value",
                ["error"] = new JObject { ["code"] = -1, ["message"] = "err" },
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: false);

            // Assert
            Assert.False(isValid);
            Assert.Contains("not both", error);
            Assert.Equal(-32603, code);
        }

        [Fact(DisplayName = "ValidatePayload: Response with neither result nor error returns false")]
        public void ValidatePayload_ResponseNeitherResultNorError_ReturnsFalse()
        {
            // Arrange
            var data = new JObject { ["id"] = 1 };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: false);

            // Assert
            Assert.False(isValid);
            Assert.Contains("either", error);
            Assert.Equal(-32603, code);
        }

        [Fact(DisplayName = "ValidatePayload: Response error missing message returns false")]
        public void ValidatePayload_ResponseErrorMissingMessage_ReturnsFalse()
        {
            // Arrange
            var data = new JObject
            {
                ["id"] = 1,
                ["error"] = new JObject { ["code"] = -1 }, // Missing message
            };

            // Act
            var (isValid, error, code) = MessageValidator.ValidatePayload(data, isRequest: false);

            // Assert
            Assert.False(isValid);
            Assert.Contains("message", error);
            Assert.Equal(-32603, code);
        }

        // =====================================================================
        // Suite 7: Error Response Building (2 tests)
        // =====================================================================

        [Fact(DisplayName = "BuildErrorResponse: Creates correct structure with error code")]
        public void BuildErrorResponse_ValidInput_ReturnsCorrectStructure()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "bridge:test",
                MessageId = "msg-001",
                Data = new JObject { ["method"] = "test" },
            };

            // Act
            var errorResponse = MessageValidator.BuildErrorResponse(
                original,
                -32600,
                "Invalid Request"
            );

            // Assert
            Assert.NotNull(errorResponse);
            Assert.Equal("rpc:error", errorResponse.MessageType);
            Assert.Equal("msg-001", errorResponse.MessageId);
            var dataObj = (JObject)errorResponse.Data;
            Assert.Equal(-32600, dataObj["error"]["code"]);
            Assert.Equal("Invalid Request", dataObj["error"]["message"]);
            Assert.NotNull(dataObj["originalMessage"]);
        }

        [Fact(DisplayName = "BuildErrorResponse: Preserves original messageId")]
        public void BuildErrorResponse_PreservesMessageId()
        {
            // Arrange
            var original = new Message
            {
                MessageType = "bridge:test",
                MessageId = "correlation-xyz",
                Data = new JObject(),
            };

            // Act
            var errorResponse = MessageValidator.BuildErrorResponse(
                original,
                -32603,
                "Internal Error"
            );

            // Assert
            Assert.Equal("correlation-xyz", errorResponse.MessageId);
        }
    }
}
