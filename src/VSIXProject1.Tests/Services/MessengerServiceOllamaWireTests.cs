#nullable enable

using System;
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
    /// gap99: Wire-format tests for the real MessengerService Ollama serializer. A fake handler
    /// captures the POST body sent to /api/chat.
    /// </summary>
    public class MessengerServiceOllamaWireTests
    {
        private sealed class CapturingHandler : HttpMessageHandler
        {
            public string? ChatRequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                if (path.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
                {
                    ChatRequestBody = request.Content == null
                        ? null
                        : await request.Content.ReadAsStringAsync();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"done\":true}\n", Encoding.UTF8, "application/x-ndjson")
                    };
                }

                // /api/tags diagnostics probe
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }
        }

        private static (MessengerService Messenger, CapturingHandler Handler, HttpClient Client) CreateMessenger()
        {
            var handler = new CapturingHandler();
            var client = new HttpClient(handler);

            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.GetSelectedModel()).Returns(new ModelInfo
            {
                Name = "ollama-test",
                Provider = "ollama",
                BaseUrl = "http://localhost:11434",
                ContextWindow = 8192
            });

            var toolService = new Mock<IToolService>();
            var messenger = new MessengerService(configService.Object, toolService.Object, client);
            return (messenger, handler, client);
        }

        private static async Task<JArray> SendAsync(MessengerService messenger, CapturingHandler handler, List<ChatMessage> messages)
        {
            var options = new StreamOptions { SuppressTools = true, Messages = messages };
            await foreach (var chunk in messenger.StreamAsync<StreamOptions, CompletionChunk>(
                "llm:stream", options, CancellationToken.None))
            {
                // Enumeration drives the HTTP call.
            }

            Assert.NotNull(handler.ChatRequestBody);
            return (JArray)JObject.Parse(handler.ChatRequestBody!)["messages"]!;
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

        [Fact]
        public async Task OllamaRequest_ThinkingIsNotSentAsAnyRole()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                // Act
                var messages = await SendAsync(messenger, handler, new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = "Hi" },
                    new ChatMessage { Role = ChatMessageRole.Thinking, Content = "secret reasoning" },
                    new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Answer" }
                });

                // Assert
                Assert.Equal(2, messages.Count);
                Assert.Equal("user", (string?)messages[0]["role"]);
                Assert.Equal("Hi", (string?)messages[0]["content"]);
                Assert.Equal("assistant", (string?)messages[1]["role"]);
                Assert.DoesNotContain(messages, m => (string?)m["content"] == "secret reasoning");
            }
        }

        [Fact]
        public async Task OllamaRequest_CompletePair_SentInOrderWithMatchingIds()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                // Act
                var messages = await SendAsync(messenger, handler, new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                    new ChatMessage { Role = ChatMessageRole.Thinking, Content = "need it" },
                    ToolCallAssistant("call_1"),
                    ToolResult("call_1")
                });

                // Assert
                Assert.Equal(3, messages.Count);
                Assert.Equal("assistant", (string?)messages[1]["role"]);
                Assert.Equal("call_1", (string?)((JArray)messages[1]["tool_calls"]!)[0]["id"]);
                Assert.Equal("tool", (string?)messages[2]["role"]);
                Assert.Equal("call_1", (string?)messages[2]["tool_call_id"]);
            }
        }

        [Fact]
        public async Task OllamaRequest_SupersededResult_DropsWholePair()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                var superseded = ToolResult("call_1");
                superseded.FreshnessState = FreshnessState.Superseded;

                // Act
                var messages = await SendAsync(messenger, handler, new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = "Read a.cs" },
                    ToolCallAssistant("call_1"),
                    superseded,
                    new ChatMessage { Role = ChatMessageRole.User, Content = "next" }
                });

                // Assert
                Assert.Equal(2, messages.Count);
                Assert.All(messages, m => Assert.Equal("user", (string?)m["role"]));
            }
        }

        [Fact]
        public async Task OllamaRequest_OrphanToolResult_Dropped()
        {
            // Arrange
            var (messenger, handler, client) = CreateMessenger();
            using (client)
            {
                // Act
                var messages = await SendAsync(messenger, handler, new List<ChatMessage>
                {
                    new ChatMessage { Role = ChatMessageRole.User, Content = "hi" },
                    ToolResult("call_unknown"),
                    new ChatMessage { Role = ChatMessageRole.Assistant, Content = "hello" }
                });

                // Assert
                Assert.Equal(2, messages.Count);
                Assert.Equal("user", (string?)messages[0]["role"]);
                Assert.Equal("assistant", (string?)messages[1]["role"]);
            }
        }
    }
}
