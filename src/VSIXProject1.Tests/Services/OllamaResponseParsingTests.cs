#nullable enable

using System;
using System.Collections.Generic;
using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class OllamaResponseParsingTests
    {
        [Fact]
        public void OllamaResponse_DeserializesWithToolCalls_FromMockJson()
        {
            // Arrange - tool call response
            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "Calling tools",
                ToolCalls = new List<ToolCallSchema>
                {
                    new ToolCallSchema
                    {
                        Id = "call_001",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "test_tool",
                            Arguments = @"{""key"":""value""}"
                        }
                    }
                }
            };

            var response = new OllamaResponse
            {
                Model = "ollama:test",
                Done = true,
                DoneReason = "tool_calls",
                Message = message
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.True(deserialized.Done);
            Assert.Equal("tool_calls", deserialized.DoneReason);
            Assert.NotNull(deserialized.Message?.ToolCalls);
            Assert.Single(deserialized.Message.ToolCalls);
            Assert.Equal("test_tool", deserialized.Message.ToolCalls[0].Function?.Name);
        }

        [Fact]
        public void ToolCallSchema_PopulatedCorrectly_FromResponse()
        {
            // Arrange
            var toolCall = new ToolCallSchema
            {
                Id = "call_xyz",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "export_data",
                    Arguments = @"{""format"":""json"",""compress"":""true""}"
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(toolCall);
            var deserialized = JsonConvert.DeserializeObject<ToolCallSchema>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("call_xyz", deserialized.Id);
            Assert.Equal("function", deserialized.Type);
            Assert.Equal("export_data", deserialized.Function?.Name);
            Assert.NotNull(deserialized.Function?.Arguments);
        }

        [Fact]
        public void OllamaResponse_HandlesHybridResponse_BothContentAndToolCalls()
        {
            // Arrange
            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "Let me search for that.",
                ToolCalls = new List<ToolCallSchema>
                {
                    new ToolCallSchema
                    {
                        Id = "call_search",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "search_web",
                            Arguments = @"{""query"":""example""}"
                        }
                    }
                }
            };

            var response = new OllamaResponse
            {
                Model = "llama2",
                Done = true,
                DoneReason = "stop",
                Message = message
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized?.Message);
            Assert.Equal("Let me search for that.", deserialized.Message.Content);
            Assert.NotNull(deserialized.Message.ToolCalls);
            Assert.Single(deserialized.Message.ToolCalls);
        }

        [Fact]
        public void OllamaResponse_HandlesToolOnlyResponse_WithoutContent()
        {
            // Arrange
            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "",
                ToolCalls = new List<ToolCallSchema>
                {
                    new ToolCallSchema
                    {
                        Id = "call_001",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "get_info",
                            Arguments = "{}"
                        }
                    }
                }
            };

            var response = new OllamaResponse
            {
                Model = "ollama:latest",
                Done = true,
                DoneReason = "tool_calls",
                Message = message
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized?.Message);
            Assert.True(string.IsNullOrEmpty(deserialized.Message.Content));
            Assert.NotNull(deserialized.Message.ToolCalls);
            Assert.Single(deserialized.Message.ToolCalls);
        }

        [Fact]
        public void OllamaResponse_MultipleToolCalls_AllAccumulated()
        {
            // Arrange
            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "Executing multiple tools",
                ToolCalls = new List<ToolCallSchema>
                {
                    new ToolCallSchema
                    {
                        Id = "call_1",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "tool_a",
                            Arguments = @"{""param"":""value1""}"
                        }
                    },
                    new ToolCallSchema
                    {
                        Id = "call_2",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "tool_b",
                            Arguments = @"{""param"":""value2""}"
                        }
                    },
                    new ToolCallSchema
                    {
                        Id = "call_3",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = "tool_c",
                            Arguments = @"{""param"":""value3""}"
                        }
                    }
                }
            };

            var response = new OllamaResponse
            {
                Model = "multi-tool-test",
                Done = true,
                DoneReason = "tool_calls",
                Message = message
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized?.Message?.ToolCalls);
            Assert.Equal(3, deserialized.Message.ToolCalls.Count);
            Assert.Equal("tool_a", deserialized.Message.ToolCalls[0].Function?.Name);
            Assert.Equal("tool_b", deserialized.Message.ToolCalls[1].Function?.Name);
            Assert.Equal("tool_c", deserialized.Message.ToolCalls[2].Function?.Name);
        }

        [Fact]
        public void OllamaResponse_TextOnlyResponse_NoToolCalls()
        {
            // Arrange
            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "This is just text, no tools.",
                ToolCalls = null
            };

            var response = new OllamaResponse
            {
                Model = "text-only-model",
                Done = true,
                DoneReason = "stop",
                Message = message
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized?.Message);
            Assert.Equal("This is just text, no tools.", deserialized.Message.Content);
            Assert.Null(deserialized.Message.ToolCalls);
        }

        [Fact]
        public void OllamaResponse_NullMessage_StillValid()
        {
            // Arrange
            var response = new OllamaResponse
            {
                Model = "test-model",
                Done = true,
                DoneReason = "stop",
                Message = null
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Null(deserialized.Message);
            Assert.True(deserialized.Done);
        }
    }
}
