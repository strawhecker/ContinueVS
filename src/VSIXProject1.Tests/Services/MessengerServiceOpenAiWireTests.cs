#nullable enable

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Wire-format regression tests that exercise the real MessengerService OpenAI-compatible
    /// serializer. A fake HttpMessageHandler captures the outgoing request body so the assertions
    /// inspect the exact JSON that would be sent to the provider.
    /// </summary>
    public class MessengerServiceOpenAiWireTests
    {
        /// <summary>
        /// Captures the outgoing request body and replies with an empty, successful SSE stream.
        /// </summary>
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                RequestBody = request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("data: [DONE]\n", Encoding.UTF8, "text/event-stream")
                };
            }
        }

        private static (MessengerService Messenger, CapturingHandler Handler, HttpClient Client) CreateMessenger(
            string modelName = "deepseek-test")
        {
            var handler = new CapturingHandler();
            var client = new HttpClient(handler);

            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.GetSelectedModel()).Returns(new ModelInfo
            {
                Name = modelName,
                Provider = "openai",
                BaseUrl = "https://example.test",
                ApiKey = "not-required"
            });

            var toolService = new Mock<IToolService>();

            var messenger = new MessengerService(configService.Object, toolService.Object, client);
            return (messenger, handler, client);
        }

        private static async Task SendAsync(MessengerService messenger, StreamOptions options)
        {
            await foreach (var chunk in messenger.StreamAsync<StreamOptions, CompletionChunk>(
                "llm:stream", options, CancellationToken.None))
            {
                // The fake response contains no chunks; enumeration drives the HTTP call.
            }
        }

        [Fact]
        public async Task OpenAiRequest_ThinkingIsOmittedAndReplayedAsReasoningContentOnNextAssistant()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" },
                        new ChatMessage { Role = ChatMessageRole.Thinking, Content = "first " },
                        new ChatMessage { Role = ChatMessageRole.Thinking, Content = "second" },
                        new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Answer" }
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                Assert.NotNull(handler.RequestBody);
                var root = JObject.Parse(handler.RequestBody!);
                var messages = (JArray)root["messages"]!;

                // Thinking entries must never be sent as their own message.
                Assert.Equal(2, messages.Count);

                Assert.Equal("user", (string?)messages[0]["role"]);
                Assert.Equal("Hi", (string?)messages[0]["content"]);
                Assert.Null(messages[0]["reasoning_content"]);

                Assert.Equal("assistant", (string?)messages[1]["role"]);
                Assert.Equal("Answer", (string?)messages[1]["content"]);
                Assert.Equal("first second", (string?)messages[1]["reasoning_content"]);
            }
        }

        [Fact]
        public async Task OpenAiRequest_PreservesToolCallIdOnAssistantToolCallAndMatchingToolResult()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        new ChatMessage
                        {
                            Role = ChatMessageRole.Assistant,
                            Content = string.Empty,
                            ToolCalls = new List<ToolCall>
                            {
                                new ToolCall
                                {
                                    Id = "call_abc",
                                    Name = "read_file",
                                    Arguments = new Dictionary<string, object> { { "path", "a.cs" } }
                                }
                            }
                        },
                        new ChatMessage
                        {
                            Role = ChatMessageRole.Tool,
                            Content = "file contents",
                            ToolCallId = "call_abc",
                            ToolName = "read_file"
                        }
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                Assert.NotNull(handler.RequestBody);
                var root = JObject.Parse(handler.RequestBody!);
                var messages = (JArray)root["messages"]!;
                Assert.Equal(3, messages.Count);

                var toolCalls = (JArray)messages[1]["tool_calls"]!;
                Assert.Single(toolCalls);
                Assert.Equal("call_abc", (string?)toolCalls[0]["id"]);

                Assert.Equal("tool", (string?)messages[2]["role"]);
                Assert.Equal("call_abc", (string?)messages[2]["tool_call_id"]);
            }
        }

        private static ChatMessage ToolCallAssistant(string callId) =>
            new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                Content = string.Empty,
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = callId,
                        Name = "read_file",
                        Arguments = new Dictionary<string, object> { { "path", "a.cs" } }
                    }
                }
            };

        private static ChatMessage ToolResult(string? callId) =>
            new ChatMessage
            {
                Role = ChatMessageRole.Tool,
                Content = "file contents",
                ToolCallId = callId,
                ToolName = "read_file"
            };

        /// <summary>
        /// gap99 regression: DeepSeek thinking-mode tool loop follow-up. The tool-call assistant turn
        /// must carry reasoning_content from its literal Thinking entry, followed by its tool result.
        /// </summary>
        [Fact]
        public async Task OpenAiRequest_DeepSeekToolLoopFollowUp_CarriesReasoningContentOnToolCallAssistant()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.System, Content = "sys" },
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        new ChatMessage { Role = ChatMessageRole.Thinking, Content = "need the file" },
                        ToolCallAssistant("call_1"),
                        ToolResult("call_1")
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(4, messages.Count);
                Assert.Equal("assistant", (string?)messages[2]["role"]);
                Assert.Equal("need the file", (string?)messages[2]["reasoning_content"]);
                Assert.Equal("call_1", (string?)((JArray)messages[2]["tool_calls"]!)[0]["id"]);
                Assert.Equal("tool", (string?)messages[3]["role"]);
                Assert.Equal("call_1", (string?)messages[3]["tool_call_id"]);
            }
        }

        [Fact]
        public async Task OpenAiRequest_DeletedToolResult_DropsRequestResultAndItsReasoning()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var deleted = ToolResult("call_1");
                deleted.IsDeleted = true;
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        new ChatMessage { Role = ChatMessageRole.Thinking, Content = "need the file" },
                        ToolCallAssistant("call_1"),
                        deleted,
                        new ChatMessage { Role = ChatMessageRole.User, Content = "next" }
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(2, messages.Count);
                Assert.All(messages, m => Assert.Equal("user", (string?)m["role"]));
                Assert.All(messages, m => Assert.Null(m["tool_calls"]));
                Assert.All(messages, m => Assert.Null(m["reasoning_content"]));
            }
        }

        [Fact]
        public async Task OpenAiRequest_MissingResult_SendsNeitherRequestNorPlaceholder()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        ToolCallAssistant("call_1"),
                        new ChatMessage { Role = ChatMessageRole.User, Content = "next" }
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(2, messages.Count);
                Assert.All(messages, m => Assert.Equal("user", (string?)m["role"]));
            }
        }

        [Fact]
        public async Task OpenAiRequest_OrphanAndIdlessToolResults_DroppedWithoutThrowing()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "hi" },
                        ToolResult("call_unknown"),
                        ToolResult(null),
                        new ChatMessage { Role = ChatMessageRole.Assistant, Content = "hello" }
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(2, messages.Count);
                Assert.Equal("user", (string?)messages[0]["role"]);
                Assert.Equal("assistant", (string?)messages[1]["role"]);
            }
        }

        /// <summary>
        /// gap99_1 regression: DeepSeek thinking mode rejects any assistant turn without
        /// reasoning_content (HTTP 400). A tool-call turn that streamed no reasoning has no
        /// Thinking entry; it must still be replayed with a literal empty reasoning_content.
        /// </summary>
        [Fact]
        public async Task OpenAiRequest_DeepSeekAssistantWithoutThinking_CarriesEmptyReasoningContent()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        new ChatMessage { Role = ChatMessageRole.Thinking, Content = "need the file" },
                        ToolCallAssistant("call_1"),
                        ToolResult("call_1"),
                        ToolCallAssistant("call_2"),
                        ToolResult("call_2")
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(5, messages.Count);
                Assert.Equal("need the file", (string?)messages[1]["reasoning_content"]);

                Assert.Equal("assistant", (string?)messages[3]["role"]);
                Assert.NotNull(messages[3]["tool_calls"]);
                Assert.NotNull(messages[3]["reasoning_content"]);
                Assert.Equal(string.Empty, (string?)messages[3]["reasoning_content"]);

                Assert.Null(messages[0]["reasoning_content"]);
                Assert.Null(messages[2]["reasoning_content"]);
                Assert.Null(messages[4]["reasoning_content"]);
            }
        }

        [Fact]
        public async Task OpenAiRequest_NonDeepSeekAssistantWithoutThinking_OmitsReasoningContent()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger("gpt-4o");
            using (client)
            {
                var options = new StreamOptions
                {
                    SuppressTools = true,
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                        ToolCallAssistant("call_1"),
                        ToolResult("call_1")
                    }
                };

                // Act
                await SendAsync(messenger, options);

                // Assert
                var messages = (JArray)JObject.Parse(handler.RequestBody!)["messages"]!;
                Assert.Equal(3, messages.Count);
                Assert.All(messages, m => Assert.Null(m["reasoning_content"]));
            }
        }
    }
}
