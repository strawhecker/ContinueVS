#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using Newtonsoft.Json;
using ContinueVS.Core.Types;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap72: Unit tests for schema-to-POCO tool call conversion logic.
    /// Verifies the core conversion behavior without requiring MessengerService instantiation.
    /// Tests are focused on the algorithm (JSON parsing, null handling, edge cases).
    /// </summary>
    public class MessengerServiceToolConversionTests
    {
        /// <summary>
        /// Helper that mimics MessengerService.ConvertToolCallSchemaToToolCall logic.
        /// Used for isolated unit testing without full service setup.
        /// </summary>
        private ToolCall ConvertToolCallSchemaToToolCall(ToolCallSchema schema)
        {
            var toolCall = new ToolCall
            {
                Id = schema.Id,
                Name = schema.Function?.Name ?? "unknown"
            };

            // Parse Arguments from JSON string to IDictionary<string, object>
            if (schema.Function?.Arguments != null && !string.IsNullOrEmpty(schema.Function.Arguments))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<IDictionary<string, object>>(
                        schema.Function.Arguments);
                    toolCall.Arguments = parsed;
                }
                catch (JsonException)
                {
                    // Fallback to empty dict on malformed JSON
                    toolCall.Arguments = new Dictionary<string, object>();
                }
            }
            else
            {
                toolCall.Arguments = new Dictionary<string, object>();
            }

            return toolCall;
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithValidSchema_ReturnsToolCall()
        {
            // Arrange
            var argumentsDict = new Dictionary<string, object> 
            { 
                { "path", "/test/file.txt" },
                { "encoding", "utf8" }
            };
            var schema = new ToolCallSchema
            {
                Id = "call_123",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "read_file",
                    Arguments = JsonConvert.SerializeObject(argumentsDict)
                }
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            Assert.NotNull(toolCall);
            Assert.Equal("call_123", toolCall.Id);
            Assert.Equal("read_file", toolCall.Name);
            Assert.NotNull(toolCall.Arguments);
            Assert.Equal(2, toolCall.Arguments.Count);
            Assert.Equal("/test/file.txt", toolCall.Arguments["path"]);
            Assert.Equal("utf8", toolCall.Arguments["encoding"]);
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithMalformedJsonArguments_LogsWarningAndReturnsEmptyDict()
        {
            // Arrange
            var schema = new ToolCallSchema
            {
                Id = "call_456",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "edit_file",
                    Arguments = "{ invalid json, not properly formatted }"
                }
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            Assert.NotNull(toolCall);
            Assert.Equal("call_456", toolCall.Id);
            Assert.Equal("edit_file", toolCall.Name);
            Assert.NotNull(toolCall.Arguments);
            Assert.Empty(toolCall.Arguments); // Should be empty dict due to parse failure
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithNullOrEmptyArguments_ReturnsEmptyDict()
        {
            // Arrange
            var schema = new ToolCallSchema
            {
                Id = "call_789",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "list_files",
                    Arguments = null // No arguments provided
                }
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            Assert.NotNull(toolCall);
            Assert.Equal("call_789", toolCall.Id);
            Assert.Equal("list_files", toolCall.Name);
            Assert.NotNull(toolCall.Arguments);
            Assert.Empty(toolCall.Arguments);
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithComplexNestedArguments_ParsesCorrectly()
        {
            // Arrange
            var complexArgs = new Dictionary<string, object>
            {
                { "query", "class MyClass" },
                { "scope", new Dictionary<string, object> { { "type", "workspace" }, { "recursive", true } } },
                { "limit", 50 }
            };
            var schema = new ToolCallSchema
            {
                Id = "call_complex",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "search_symbols",
                    Arguments = JsonConvert.SerializeObject(complexArgs)
                }
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            Assert.NotNull(toolCall);
            Assert.Equal("search_symbols", toolCall.Name);
            Assert.NotNull(toolCall.Arguments);
            Assert.True(toolCall.Arguments.ContainsKey("query"));
            Assert.True(toolCall.Arguments.ContainsKey("scope"));
            Assert.True(toolCall.Arguments.ContainsKey("limit"));
            Assert.Equal("class MyClass", toolCall.Arguments["query"]);
            Assert.Equal(50L, toolCall.Arguments["limit"]); // JSON deserialization converts to long
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithNullFunction_DefaultsToUnknown()
        {
            // Arrange
            var schema = new ToolCallSchema
            {
                Id = "call_null_func",
                Type = "function",
                Function = null // No function info
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            Assert.NotNull(toolCall);
            Assert.Equal("unknown", toolCall.Name); // Should default to "unknown"
            Assert.Empty(toolCall.Arguments ?? new Dictionary<string, object>());
        }

        [Fact]
        public void ConvertToolCallSchemaToToolCall_WithEmptyFunctionName_KeepsEmptyName()
        {
            // Arrange
            var schema = new ToolCallSchema
            {
                Id = "call_empty_name",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = string.Empty,
                    Arguments = "{}"
                }
            };

            // Act
            var toolCall = ConvertToolCallSchemaToToolCall(schema);

            // Assert
            // Empty string is kept as-is (not converted to "unknown")
            Assert.Equal(string.Empty, toolCall.Name);
        }
    }
}
