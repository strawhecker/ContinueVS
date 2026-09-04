#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using Newtonsoft.Json;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class OllamaRequestResponseTests
    {
        [Fact]
        public void OllamaRequest_SerializesWithToolsField_WhenToolsPopulated()
        {
            // Arrange
            var tools = new List<ToolSchema>
            {
                new ToolSchema
                {
                    Type = "function",
                    Function = new ToolFunctionSchema
                    {
                        Name = "read_file",
                        Description = "Read a file from disk",
                        Parameters = new ParametersSchema()
                    }
                }
            };

            var request = new OllamaRequest
            {
                Model = "ollama-test",
                Stream = true,
                Messages = new List<OllamaMessage> { new OllamaMessage { Role = "user", Content = "Hello" } },
                Tools = tools
            };

            // Act
            var json = JsonConvert.SerializeObject(request);

            // Assert
            Assert.Contains("\"tools\":", json);
            Assert.Contains("\"name\":\"read_file\"", json);
        }

        [Fact]
        public void OllamaRequest_SerializesWithEmptyTools_WhenToolsIsEmptyList()
        {
            // Arrange
            var request = new OllamaRequest
            {
                Model = "ollama-test",
                Stream = true,
                Messages = new List<OllamaMessage> { new OllamaMessage { Role = "user", Content = "Hello" } },
                Tools = new List<ToolSchema>()
            };

            // Act
            var json = JsonConvert.SerializeObject(request);

            // Assert
            Assert.Contains("\"tools\":[]", json);
        }

        [Fact]
        public void OllamaRequest_IncludesNullTools_InSerialization()
        {
            // Arrange
            var request = new OllamaRequest
            {
                Model = "ollama-test",
                Stream = true,
                Messages = new List<OllamaMessage> { new OllamaMessage { Role = "user", Content = "Hello" } },
                Tools = null
            };

            // Act
            var json = JsonConvert.SerializeObject(request);

            // Assert
            // Newtonsoft serializes null values by default
            Assert.Contains("\"tools\":null", json);
        }

        [Fact]
        public void OllamaRequest_RemainsBackwardCompatible_WithoutToolsField()
        {
            // Arrange
            var request = new OllamaRequest
            {
                Model = "ollama-test",
                Stream = true,
                Messages = new List<OllamaMessage> { new OllamaMessage { Role = "user", Content = "Hello" } }
            };

            // Act
            var json = JsonConvert.SerializeObject(request);
            var deserialized = JsonConvert.DeserializeObject<OllamaRequest>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal("ollama-test", deserialized.Model);
            Assert.Null(deserialized.Tools);
        }

        [Fact]
        public void OllamaMessage_SerializesWithToolCalls_WhenToolCallsPopulated()
        {
            // Arrange
            var toolCalls = new List<ToolCallSchema>
            {
                new ToolCallSchema
                {
                    Id = "call_123",
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = "read_file",
                        Arguments = @"{""path"": ""/tmp/test.txt""}"
                    }
                }
            };

            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "I'll read that file",
                ToolCalls = toolCalls
            };

            // Act
            var json = JsonConvert.SerializeObject(message);

            // Assert
            Assert.Contains("\"tool_calls\":", json);
            Assert.Contains("\"call_123\"", json);
            Assert.Contains("\"read_file\"", json);
        }

        [Fact]
        public void OllamaMessage_DeserializesToolCalls_FromJson()
        {
            // Arrange
            var json = @"{""role"": ""assistant"", ""content"": ""Calling a tool"", ""tool_calls"": [{""id"": ""call_456"", ""type"": ""function"", ""function"": {""name"": ""write_file"", ""arguments"": ""{\""path\"":\""test.txt\"",\""content\"":\""hello\""}}""}}]}";

            // Act
            var message = JsonConvert.DeserializeObject<OllamaMessage>(json);

            // Assert
            Assert.NotNull(message);
            Assert.NotNull(message.ToolCalls);
            Assert.Single(message.ToolCalls);
            Assert.Equal("call_456", message.ToolCalls[0].Id);
            Assert.Equal("write_file", message.ToolCalls[0].Function?.Name);
        }

        [Fact]
        public void OllamaResponse_DeserializesToolCalls_FromNdjson()
        {
            // Arrange
            var toolCall = new ToolCallSchema
            {
                Id = "call_789",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "search_codebase",
                    Arguments = @"{""query"":""test""}"
                }
            };

            var message = new OllamaMessage
            {
                Role = "assistant",
                Content = "Executing tool",
                ToolCalls = new List<ToolCallSchema> { toolCall }
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
            Assert.NotNull(deserialized);
            Assert.True(deserialized.Done);
            Assert.Equal("tool_calls", deserialized.DoneReason);
            Assert.NotNull(deserialized.Message?.ToolCalls);
            Assert.Single(deserialized.Message.ToolCalls);
            Assert.Equal("search_codebase", deserialized.Message.ToolCalls[0].Function?.Name);
        }

        [Fact]
        public void OllamaResponse_HandlesHybridResponse_WithBothContentAndToolCalls()
        {
            // Arrange
            var response = new OllamaResponse
            {
                Model = "ollama:latest",
                Done = true,
                DoneReason = "stop",
                Message = new OllamaMessage
                {
                    Role = "assistant",
                    Content = "I'm reading a file and also making a tool call.",
                    ToolCalls = new List<ToolCallSchema>
                    {
                        new ToolCallSchema
                        {
                            Id = "call_hybrid",
                            Type = "function",
                            Function = new ToolCallFunction
                            {
                                Name = "read_file",
                                Arguments = @"{""path"":""data.json""}"
                            }
                        }
                    }
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

            // Assert
            Assert.NotNull(deserialized?.Message);
            Assert.NotEmpty(deserialized?.Message?.Content ?? "");
            Assert.NotNull(deserialized?.Message?.ToolCalls);
            Assert.Single(deserialized?.Message?.ToolCalls ?? Enumerable.Empty<ToolCallSchema>());
        }

        [Fact]
        public void ToolCallSchema_PreservesArgumentsAsJsonString_NotParsed()
        {
            // Arrange
            var toolCall = new ToolCallSchema
            {
                Id = "call_test",
                Type = "function",
                Function = new ToolCallFunction
                {
                    Name = "test_tool",
                    Arguments = @"{""key"": ""value"", ""nested"": {""obj"": true}}"
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(toolCall);
            var deserialized = JsonConvert.DeserializeObject<ToolCallSchema>(json);

            // Assert
            Assert.NotNull(deserialized?.Function?.Arguments);
            Assert.Contains("\"key\"", deserialized.Function.Arguments);
            Assert.Contains("\"nested\"", deserialized.Function.Arguments);
            Assert.IsType<string>(deserialized.Function.Arguments);
        }

        [Fact]
        public void OllamaResponse_CapturesDoneReason_IncludingToolCalls()
        {
            // Arrange
            var response = new OllamaResponse
            {
                Model = "llama2",
                Done = true,
                DoneReason = "tool_calls",
                Message = new OllamaMessage { Role = "assistant", Content = "" }
            };

            // Act
            var json = JsonConvert.SerializeObject(response);
            var deserialized = JsonConvert.DeserializeObject<OllamaResponse>(json);

                         // Assert
                         Assert.NotNull(deserialized);
                         Assert.Equal("tool_calls", deserialized.DoneReason);
                     }

                     [Fact]
                     public void OllamaRequest_SerializesWithNumCtx_WhenContextWindowDefined()
                     {
                         // Arrange
                         var request = new OllamaRequest
                         {
                             Model = "llama3",
                             Stream = true,
                             Messages = new List<OllamaMessage> { new OllamaMessage { Role = "user", Content = "Hello" } },
                             Options = new OllamaOptions
                             {
                                 Temperature = 0.7,
                                 MaxTokens = 2048,
                                 TopP = 0.9,
                                 ContextWindow = 8192
                             }
                         };

                         // Act
                         var json = JsonConvert.SerializeObject(request);

                         // Assert
                         Assert.Contains("\"num_ctx\":8192", json);
                     }
                 }
            }
