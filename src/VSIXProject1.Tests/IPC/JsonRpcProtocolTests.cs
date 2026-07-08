using System;
using Newtonsoft.Json.Linq;
using Xunit;
using ContinueVS.IPC;

namespace ContinueVS.Tests.IPC
{
    /// <summary>
    /// Unit tests for JsonRpcProtocol (Step 21).
    /// 
    /// Tests cover:
    /// - Message creation (request, response, error)
    /// - Message validation (envelope compliance)
    /// - Message inspection (type checks, ID extraction, error extraction)
    /// - Error code constants and helper methods
    /// - Edge cases (null inputs, empty strings, malformed data)
    /// </summary>
    public class JsonRpcProtocolTests
    {
        // =====================================================================
        // Request Creation Tests
        // =====================================================================

        [Fact]
        public void CreateRequest_WithMessageType_GeneratesUniqueMessageId()
        {
            // Arrange
            string messageType = "bridge:getEditorState";

            // Act
            var request1 = JsonRpcProtocol.CreateRequest(messageType);
            var request2 = JsonRpcProtocol.CreateRequest(messageType);

            // Assert
            Assert.NotNull(request1.MessageId);
            Assert.NotNull(request2.MessageId);
            Assert.NotEqual(request1.MessageId, request2.MessageId);
        }

        [Fact]
        public void CreateRequest_WithMessageType_SetsMessageType()
        {
            // Arrange
            string messageType = "bridge:getEditorState";

            // Act
            var request = JsonRpcProtocol.CreateRequest(messageType);

            // Assert
            Assert.Equal(messageType, request.MessageType);
        }

        [Fact]
        public void CreateRequest_WithMessageTypeAndData_SetsData()
        {
            // Arrange
            string messageType = "bridge:ping";
            var data = JObject.FromObject(new { timestamp = DateTime.UtcNow.Ticks });

            // Act
            var request = JsonRpcProtocol.CreateRequest(messageType, data);

            // Assert
            Assert.Equal(data, request.Data);
        }

        [Fact]
        public void CreateRequest_WithoutData_HasNullData()
        {
            // Arrange
            string messageType = "bridge:ping";

            // Act
            var request = JsonRpcProtocol.CreateRequest(messageType);

            // Assert
            Assert.Null(request.Data);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateRequest_WithInvalidMessageType_ThrowsArgumentException(string messageType)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => JsonRpcProtocol.CreateRequest(messageType));
        }

        // =====================================================================
        // Response Creation Tests
        // =====================================================================

        [Fact]
        public void CreateResponse_WithMessageIdAndData_SetsCorrectValues()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();
            var data = JObject.FromObject(new { result = "success" });

            // Act
            var response = JsonRpcProtocol.CreateResponse(messageId, data);

            // Assert
            Assert.Equal("rpc:response", response.MessageType);
            Assert.Equal(messageId, response.MessageId);
            Assert.Equal(data, response.Data);
        }

        [Fact]
        public void CreateResponse_WithoutData_HasNullData()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();

            // Act
            var response = JsonRpcProtocol.CreateResponse(messageId);

            // Assert
            Assert.Equal("rpc:response", response.MessageType);
            Assert.Equal(messageId, response.MessageId);
            Assert.Null(response.Data);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateResponse_WithInvalidMessageId_ThrowsArgumentException(string messageId)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => JsonRpcProtocol.CreateResponse(messageId));
        }

        // =====================================================================
        // Error Creation Tests
        // =====================================================================

        [Fact]
        public void CreateError_WithMessageIdCodeAndMessage_SetCorrectValues()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();
            int code = JsonRpcProtocol.METHOD_NOT_FOUND;
            string message = "Handler not found";

            // Act
            var error = JsonRpcProtocol.CreateError(messageId, code, message);

            // Assert
            Assert.Equal("rpc:error", error.MessageType);
            Assert.Equal(messageId, error.MessageId);
            Assert.NotNull(error.Data);
            Assert.IsType<JObject>(error.Data);

            var errorObj = (JObject)error.Data;
            Assert.Equal(code, errorObj.Value<int>("code"));
            Assert.Equal(message, errorObj.Value<string>("message"));
        }

        [Fact]
        public void CreateError_WithErrorData_IncludesDataField()
        {
            // Arrange
            string messageId = Guid.NewGuid().ToString();
            int code = JsonRpcProtocol.BRIDGE_TIMEOUT;
            string message = "Request timed out";
            var errorData = JObject.FromObject(new { timeoutMs = 5000 });

            // Act
            var error = JsonRpcProtocol.CreateError(messageId, code, message, errorData);

            // Assert
            var errorObj = (JObject)error.Data;
            Assert.Equal(code, errorObj.Value<int>("code"));
            Assert.Equal(message, errorObj.Value<string>("message"));
            Assert.NotNull(errorObj.Value<JObject>("data"));
            Assert.Equal(5000, errorObj.Value<JObject>("data")?.Value<int>("timeoutMs"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateError_WithInvalidMessageId_ThrowsArgumentException(string messageId)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                JsonRpcProtocol.CreateError(messageId, -32600, "Invalid")
            );
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateError_WithInvalidMessage_ThrowsArgumentException(string message)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                JsonRpcProtocol.CreateError("msg-123", -32600, message)
            );
        }

        // =====================================================================
        // Message Validation Tests
        // =====================================================================

        [Fact]
        public void ValidateMessage_WithValidMessage_ReturnsTrue()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "bridge:ping",
                MessageId = Guid.NewGuid().ToString(),
                Data = null
            };

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(message);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
        }

        [Fact]
        public void ValidateMessage_WithNullMessage_ReturnsFalse()
        {
            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(null);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(error);
            Assert.Contains("null", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateMessage_WithNullMessageType_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = null!,
                MessageId = "msg-123"
            };

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(message);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(error);
        }

        [Fact]
        public void ValidateMessage_WithEmptyMessageType_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "",
                MessageId = "msg-123"
            };

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(message);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public void ValidateMessage_WithNullMessageId_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "bridge:ping",
                MessageId = null!
            };

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(message);

            // Assert
            Assert.False(isValid);
            Assert.NotNull(error);
        }

        [Fact]
        public void ValidateMessage_WithEmptyMessageId_ReturnsFalse()
        {
            // Arrange
            var message = new Message
            {
                MessageType = "bridge:ping",
                MessageId = ""
            };

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(message);

            // Assert
            Assert.False(isValid);
        }

        // =====================================================================
        // Message Inspection Tests
        // =====================================================================

        [Fact]
        public void ExtractMessageId_WithValidMessage_ReturnsMessageId()
        {
            // Arrange
            var messageId = Guid.NewGuid().ToString();
            var message = new Message { MessageType = "test", MessageId = messageId };

            // Act
            var extracted = JsonRpcProtocol.ExtractMessageId(message);

            // Assert
            Assert.Equal(messageId, extracted);
        }

        [Fact]
        public void ExtractMessageId_WithNullMessage_ReturnsNull()
        {
            // Act
            var extracted = JsonRpcProtocol.ExtractMessageId(null);

            // Assert
            Assert.Null(extracted);
        }

        [Fact]
        public void ExtractMessageId_WithEmptyMessageId_ReturnsNull()
        {
            // Arrange
            var message = new Message { MessageType = "test", MessageId = "" };

            // Act
            var extracted = JsonRpcProtocol.ExtractMessageId(message);

            // Assert
            Assert.Null(extracted);
        }

        [Theory]
        [InlineData("rpc:response", true)]
        [InlineData("rpc:error", true)]
        [InlineData("bridge:ping", false)]
        [InlineData("custom:type", false)]
        [InlineData(null, false)]
        public void IsResponseMessage_WithVariousTypes_ReturnsCorrectValue(string messageType, bool expected)
        {
            // Act
            var result = JsonRpcProtocol.IsResponseMessage(messageType);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("rpc:error", true)]
        [InlineData("rpc:response", false)]
        [InlineData("bridge:ping", false)]
        [InlineData(null, false)]
        public void IsErrorMessage_WithVariousTypes_ReturnsCorrectValue(string messageType, bool expected)
        {
            // Act
            var result = JsonRpcProtocol.IsErrorMessage(messageType);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("rpc:response", true)]
        [InlineData("rpc:error", false)]
        [InlineData("bridge:ping", false)]
        [InlineData(null, false)]
        public void IsSuccessMessage_WithVariousTypes_ReturnsCorrectValue(string messageType, bool expected)
        {
            // Act
            var result = JsonRpcProtocol.IsSuccessMessage(messageType);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractErrorCode_WithErrorMessage_ReturnsCode()
        {
            // Arrange
            int expectedCode = JsonRpcProtocol.METHOD_NOT_FOUND;
            var error = JsonRpcProtocol.CreateError("msg-1", expectedCode, "Not found");

            // Act
            var code = JsonRpcProtocol.ExtractErrorCode(error);

            // Assert
            Assert.Equal(expectedCode, code);
        }

        [Fact]
        public void ExtractErrorCode_WithSuccessMessage_ReturnsNull()
        {
            // Arrange
            var response = JsonRpcProtocol.CreateResponse("msg-1");

            // Act
            var code = JsonRpcProtocol.ExtractErrorCode(response);

            // Assert
            Assert.Null(code);
        }

        [Fact]
        public void ExtractErrorCode_WithNullMessage_ReturnsNull()
        {
            // Act
            var code = JsonRpcProtocol.ExtractErrorCode(null);

            // Assert
            Assert.Null(code);
        }

        [Fact]
        public void ExtractErrorMessage_WithErrorMessage_ReturnsMessage()
        {
            // Arrange
            string expectedMessage = "Method not found";
            var error = JsonRpcProtocol.CreateError("msg-1", JsonRpcProtocol.METHOD_NOT_FOUND, expectedMessage);

            // Act
            var message = JsonRpcProtocol.ExtractErrorMessage(error);

            // Assert
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public void ExtractErrorMessage_WithSuccessMessage_ReturnsNull()
        {
            // Arrange
            var response = JsonRpcProtocol.CreateResponse("msg-1");

            // Act
            var message = JsonRpcProtocol.ExtractErrorMessage(response);

            // Assert
            Assert.Null(message);
        }

        // =====================================================================
        // Error Code Constant Tests
        // =====================================================================

        [Fact]
        public void ErrorCodes_StandardCodesAreNegative()
        {
            // Assert
            Assert.True(JsonRpcProtocol.PARSE_ERROR < 0);
            Assert.True(JsonRpcProtocol.INVALID_REQUEST < 0);
            Assert.True(JsonRpcProtocol.METHOD_NOT_FOUND < 0);
            Assert.True(JsonRpcProtocol.INVALID_PARAMS < 0);
            Assert.True(JsonRpcProtocol.INTERNAL_ERROR < 0);
        }

        [Fact]
        public void ErrorCodes_BridgeCodesAreNegative()
        {
            // Assert
            Assert.True(JsonRpcProtocol.BRIDGE_TIMEOUT < 0);
            Assert.True(JsonRpcProtocol.BRIDGE_PROCESS_DEAD < 0);
            Assert.True(JsonRpcProtocol.BRIDGE_INVALID_STATE < 0);
        }

        [Fact]
        public void IsReservedErrorCode_WithStandardCodes_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(JsonRpcProtocol.IsReservedErrorCode(JsonRpcProtocol.PARSE_ERROR));
            Assert.True(JsonRpcProtocol.IsReservedErrorCode(JsonRpcProtocol.INVALID_REQUEST));
            Assert.True(JsonRpcProtocol.IsReservedErrorCode(JsonRpcProtocol.BRIDGE_TIMEOUT));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(-1)]
        public void IsReservedErrorCode_WithNonReservedCodes_ReturnsFalse(int code)
        {
            // Act & Assert
            Assert.False(JsonRpcProtocol.IsReservedErrorCode(code));
        }

        [Fact]
        public void IsBridgeErrorCode_WithBridgeCodes_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(JsonRpcProtocol.IsBridgeErrorCode(JsonRpcProtocol.BRIDGE_TIMEOUT));
            Assert.True(JsonRpcProtocol.IsBridgeErrorCode(JsonRpcProtocol.BRIDGE_PROCESS_DEAD));
            Assert.True(JsonRpcProtocol.IsBridgeErrorCode(JsonRpcProtocol.BRIDGE_INVALID_STATE));
        }

        [Fact]
        public void IsBridgeErrorCode_WithStandardCodes_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(JsonRpcProtocol.IsBridgeErrorCode(JsonRpcProtocol.PARSE_ERROR));
            Assert.False(JsonRpcProtocol.IsBridgeErrorCode(JsonRpcProtocol.INVALID_REQUEST));
        }

        [Fact]
        public void GetStandardErrorDescription_WithKnownCode_ReturnsDescription()
        {
            // Act
            var desc = JsonRpcProtocol.GetStandardErrorDescription(JsonRpcProtocol.METHOD_NOT_FOUND);

            // Assert
            Assert.NotNull(desc);
            Assert.NotEmpty(desc);
        }

        [Fact]
        public void GetStandardErrorDescription_WithUnknownCode_ReturnsNull()
        {
            // Act
            var desc = JsonRpcProtocol.GetStandardErrorDescription(-999);

            // Assert
            Assert.Null(desc);
        }

        [Fact]
        public void GetBridgeErrorDescription_WithKnownCode_ReturnsDescription()
        {
            // Act
            var desc = JsonRpcProtocol.GetBridgeErrorDescription(JsonRpcProtocol.BRIDGE_TIMEOUT);

            // Assert
            Assert.NotNull(desc);
            Assert.NotEmpty(desc);
        }

        [Fact]
        public void GetBridgeErrorDescription_WithUnknownCode_ReturnsNull()
        {
            // Act
            var desc = JsonRpcProtocol.GetBridgeErrorDescription(-999);

            // Assert
            Assert.Null(desc);
        }

        // =====================================================================
        // Integration Tests (Round-trip)
        // =====================================================================

        [Fact]
        public void RoundTrip_CreateRequestAndValidate_Succeeds()
        {
            // Arrange
            var request = JsonRpcProtocol.CreateRequest("bridge:test");

            // Act
            var (isValid, error) = JsonRpcProtocol.ValidateMessage(request);
            var extracted = JsonRpcProtocol.ExtractMessageId(request);

            // Assert
            Assert.True(isValid);
            Assert.Null(error);
            Assert.Equal(request.MessageId, extracted);
        }

        [Fact]
        public void RoundTrip_CreateErrorAndExtract_Succeeds()
        {
            // Arrange
            string msgId = "msg-123";
            int expectedCode = JsonRpcProtocol.BRIDGE_TIMEOUT;
            string expectedMsg = "Request timed out";
            var error = JsonRpcProtocol.CreateError(msgId, expectedCode, expectedMsg);

            // Act
            var (isValid, _) = JsonRpcProtocol.ValidateMessage(error);
            var extractedId = JsonRpcProtocol.ExtractMessageId(error);
            var extractedCode = JsonRpcProtocol.ExtractErrorCode(error);
            var extractedMsg = JsonRpcProtocol.ExtractErrorMessage(error);

            // Assert
            Assert.True(isValid);
            Assert.Equal(msgId, extractedId);
            Assert.Equal(expectedCode, extractedCode);
            Assert.Equal(expectedMsg, extractedMsg);
        }
    }
}
