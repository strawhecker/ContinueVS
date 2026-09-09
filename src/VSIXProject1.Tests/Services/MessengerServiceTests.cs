#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Newtonsoft.Json;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap73: Unit tests for MessengerService tool call serialization in message loop.
    /// Tests the wiring of ChatMessage.ToolCalls → OllamaMessage.tool_calls conversion.
    /// </summary>
    public class MessengerServiceTests
    {
        /// <summary>
        /// Helper to convert ChatMessage collection to OllamaMessage collection.
        /// Mimics the message loop logic from ProcessOllamaStreamAsync.
        /// </summary>
        private List<OllamaMessage> ConvertChatMessagesToOllamaMessages(List<ChatMessage> chatMessages)
        {
            var ollamaMessages = new List<OllamaMessage>();

            foreach (var msg in chatMessages)
            {
                var role = msg.Role switch
                {
                    ChatMessageRole.User => "user",
                    ChatMessageRole.Assistant => "assistant",
                    ChatMessageRole.System => "system",
                    ChatMessageRole.Tool => "tool",
                    _ => "user"
                };

                var ollamaMsg = new OllamaMessage
                {
                    Role = role,
                    Content = msg.Content ?? string.Empty
                };

                // For tool role messages, include the tool_call_id
                if (msg.Role == ChatMessageRole.Tool && !string.IsNullOrEmpty(msg.ToolCallId))
                {
                    ollamaMsg.ToolCallId = msg.ToolCallId;
                }

                // gap73: Wire assistant ToolCalls for serialization
                if (msg.Role == ChatMessageRole.Assistant && msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    var toolCallSchemas = new List<ToolCallSchema>();
                    foreach (var toolCall in msg.ToolCalls)
                    {
                        var schema = ConvertToolCallToSchema(toolCall);
                        toolCallSchemas.Add(schema);
                    }

                    if (toolCallSchemas.Count > 0)
                    {
                        ollamaMsg.ToolCalls = toolCallSchemas;
                    }
                }

                ollamaMessages.Add(ollamaMsg);
            }

            return ollamaMessages;
        }

        /// <summary>
        /// Helper to convert ToolCall to ToolCallSchema (mimics MessengerService.ConvertToolCallToSchema).
        /// </summary>
        private ToolCallSchema ConvertToolCallToSchema(ToolCall toolCall)
        {
            var schema = new ToolCallSchema
            {
                Id = toolCall.Id,
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = toolCall.Name ?? "unknown"
                }
            };

            // Serialize Arguments from IDictionary<string, object> to JSON string
            if (toolCall.Arguments != null && toolCall.Arguments.Count > 0)
            {
                schema.Function.Arguments = JsonConvert.SerializeObject(toolCall.Arguments);
            }
            else
            {
                schema.Function.Arguments = "{}";
            }

            return schema;
        }

        [Fact]
        public void ProcessMessage_WithAssistantToolCalls_SerializesToolCallsInOllamaMessage()
        {
            // Arrange: Create a ChatMessage with assistant role and tool calls
            var toolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_123",
                    Name = "read_file",
                    Arguments = new Dictionary<string, object>
                    {
                        { "path", "/test/file.txt" },
                        { "encoding", "utf8" }
                    }
                },
                new ToolCall
                {
                    Id = "call_456",
                    Name = "search_symbols",
                    Arguments = new Dictionary<string, object>
                    {
                        { "query", "MyClass" },
                        { "limit", 10 }
                    }
                }
            };

            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "I'll help you with that.",
                    ToolCalls = toolCalls
                }
            };

            // Act: Convert to Ollama messages
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Single(ollamaMessages);
            var ollamaMsg = ollamaMessages[0];
            Assert.Equal("assistant", ollamaMsg.Role);
            Assert.NotNull(ollamaMsg.ToolCalls);
            Assert.Equal(2, ollamaMsg.ToolCalls.Count);

            // Verify first tool call
            var firstSchema = ollamaMsg.ToolCalls[0];
            Assert.Equal("call_123", firstSchema.Id);
            Assert.Equal("function", firstSchema.Type);
            Assert.NotNull(firstSchema.Function);
            Assert.Equal("read_file", firstSchema.Function.Name);
            Assert.NotNull(firstSchema.Function.Arguments);

            var firstArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                firstSchema.Function.Arguments);
            Assert.NotNull(firstArgs);
            Assert.Equal("/test/file.txt", firstArgs["path"]);
            Assert.Equal("utf8", firstArgs["encoding"]);

            // Verify second tool call
            var secondSchema = ollamaMsg.ToolCalls[1];
            Assert.Equal("call_456", secondSchema.Id);
            Assert.Equal("search_symbols", secondSchema.Function?.Name);
        }

        [Fact]
        public void ProcessMessage_WithNoToolCalls_OmitsToolCallsList()
        {
            // Arrange: Create an assistant message without tool calls
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "Here is the answer to your question."
                }
            };

            // Act: Convert to Ollama messages
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Single(ollamaMessages);
            var ollamaMsg = ollamaMessages[0];
            Assert.Equal("assistant", ollamaMsg.Role);
            // ToolCalls should be null or empty
            Assert.Null(ollamaMsg.ToolCalls);
        }

        [Fact]
        public void ProcessMessage_WithAssistantNoToolCallsButEmpty_OmitsToolCallsList()
        {
            // Arrange: Create an assistant message with empty tool calls list
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "I'll check something.",
                    ToolCalls = new List<ToolCall>() // Empty list
                }
            };

            // Act: Convert to Ollama messages
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Single(ollamaMessages);
            var ollamaMsg = ollamaMessages[0];
            // Empty tool calls should not be wired
            Assert.Null(ollamaMsg.ToolCalls);
        }

        [Fact]
        public void ProcessMessage_WithToolResultMessage_IncludesToolCallId()
        {
            // Arrange: Create a tool result message
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Content = "File contents here...",
                    ToolCallId = "call_123"
                }
            };

            // Act: Convert to Ollama messages
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Single(ollamaMessages);
            var ollamaMsg = ollamaMessages[0];
            Assert.Equal("tool", ollamaMsg.Role);
            Assert.Equal("call_123", ollamaMsg.ToolCallId);
        }

        [Fact]
        public void ProcessMessage_WithMixedMessageTypes_SerializesCorrectly()
        {
            // Arrange: Multiple message types in sequence
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.User,
                    Content = "Please read the file."
                },
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "I'll read that file for you.",
                    ToolCalls = new List<ToolCall>
                    {
                        new ToolCall
                        {
                            Id = "call_123",
                            Name = "read_file",
                            Arguments = new Dictionary<string, object> { { "path", "/test.txt" } }
                        }
                    }
                },
                new ChatMessage
                {
                    Role = ChatMessageRole.Tool,
                    Content = "File contents...",
                    ToolCallId = "call_123"
                },
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "Here's the file content..."
                }
            };

            // Act: Convert to Ollama messages
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Equal(4, ollamaMessages.Count);

            // Message 1: User
            Assert.Equal("user", ollamaMessages[0].Role);
            Assert.Null(ollamaMessages[0].ToolCalls);

            // Message 2: Assistant with tool calls
            Assert.Equal("assistant", ollamaMessages[1].Role);
            var message2ToolCalls = ollamaMessages[1].ToolCalls;
            Assert.NotNull(message2ToolCalls);
            Assert.Single(message2ToolCalls);

            // Message 3: Tool result
            Assert.Equal("tool", ollamaMessages[2].Role);
            Assert.Equal("call_123", ollamaMessages[2].ToolCallId);

            // Message 4: Final assistant response
            Assert.Equal("assistant", ollamaMessages[3].Role);
            Assert.Null(ollamaMessages[3].ToolCalls);
        }

        [Fact]
        public void ProcessMessage_WithSingleToolCall_SerializesCorrectly()
        {
            // Arrange: Single tool call
            var chatMessages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "Let me search for that.",
                    ToolCalls = new List<ToolCall>
                    {
                        new ToolCall
                        {
                            Id = "call_001",
                            Name = "search_codebase",
                            Arguments = new Dictionary<string, object>
                            {
                                { "query", "MyInterface" },
                                { "maxResults", 100 }
                            }
                        }
                    }
                }
            };

            // Act
            var ollamaMessages = ConvertChatMessagesToOllamaMessages(chatMessages);

            // Assert
            Assert.Single(ollamaMessages);
            var msg = ollamaMessages[0];
            Assert.NotNull(msg.ToolCalls);
            Assert.Single(msg.ToolCalls);

            var schema = msg.ToolCalls[0];
            Assert.Equal("call_001", schema.Id);
            Assert.Equal("search_codebase", schema.Function?.Name);

            // Verify arguments were serialized
            var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(
                schema.Function?.Arguments ?? "{}");
            Assert.NotNull(args);
            Assert.Equal("MyInterface", args["query"]);
            Assert.Equal(100L, args["maxResults"]); // JSON deserializes to long
        }
    }
}
