#nullable enable

using System.Collections.Generic;
using System.Linq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Utilities;
using Xunit;

namespace ContinueVS.Tests.Services.Utilities
{
    /// <summary>
    /// gap99: atomic ordered tool request/result pairing on outbound messages.
    /// </summary>
    public class ToolCallPairingTests
    {
        private static ChatMessage User(string content) =>
            new ChatMessage { Role = ChatMessageRole.User, Content = content };

        private static ChatMessage Thinking(string content) =>
            new ChatMessage { Role = ChatMessageRole.Thinking, Content = content };

        private static ChatMessage Assistant(string content, params string[] callIds) =>
            new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                Content = content,
                ToolCalls = callIds.Length == 0
                    ? null
                    : callIds.Select(id => new ToolCall { Id = id, Name = "read_file" }).ToList()
            };

        private static ChatMessage Tool(string? callId, string content = "result") =>
            new ChatMessage { Role = ChatMessageRole.Tool, Content = content, ToolCallId = callId };

        [Fact]
        public void FilterPairs_NullInput_ReturnsEmpty()
        {
            Assert.Empty(ToolCallPairing.FilterPairs(null));
        }

        [Fact]
        public void FilterPairs_CompletePair_PreservesThinkingAssistantToolOrder()
        {
            // Arrange
            var user = User("go");
            var thinking = Thinking("plan");
            var assistant = Assistant(string.Empty, "c1");
            var tool = Tool("c1");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, thinking, assistant, tool });

            // Assert
            Assert.Equal(new[] { user, thinking, assistant, tool }, result);
            Assert.Same(assistant, result[2]);
        }

        [Fact]
        public void FilterPairs_DeletedResult_DropsRequestAndItsThinking()
        {
            // Arrange
            var user = User("go");
            var tool = Tool("c1");
            tool.IsDeleted = true;

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, Thinking("plan"), Assistant(string.Empty, "c1"), tool });

            // Assert
            Assert.Equal(new[] { user }, result);
        }

        [Fact]
        public void FilterPairs_SupersededResult_DropsPair()
        {
            // Arrange
            var user = User("go");
            var tool = Tool("c1");
            tool.FreshnessState = FreshnessState.Superseded;

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, Assistant(string.Empty, "c1"), tool });

            // Assert
            Assert.Equal(new[] { user }, result);
        }

        [Fact]
        public void FilterPairs_DeletedRequest_DropsItsResultAndPrecedingThinking()
        {
            // Arrange
            var user = User("go");
            var assistant = Assistant(string.Empty, "c1");
            assistant.IsDeleted = true;
            var later = User("next");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, Thinking("plan"), assistant, Tool("c1"), later });

            // Assert
            Assert.Equal(new[] { user, later }, result);
        }

        [Fact]
        public void FilterPairs_DeletedRequestWithProse_DropsWholeGroup()
        {
            // Arrange
            var user = User("go");
            var assistant = Assistant("I'll read it", "c1");
            assistant.IsDeleted = true;

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, Thinking("plan"), assistant, Tool("c1") });

            // Assert
            Assert.Equal(new[] { user }, result);
        }

        [Fact]
        public void FilterPairs_MissingResult_NoPlaceholderCreated()
        {
            // Arrange
            var user = User("go");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { user, Assistant(string.Empty, "c1") });

            // Assert
            Assert.Equal(new[] { user }, result);
            Assert.DoesNotContain(result, m => m.Role == ChatMessageRole.Tool);
        }

        [Fact]
        public void FilterPairs_OrphanDuplicateAndIdlessResults_DroppedSilently()
        {
            // Arrange
            var user = User("go");
            var assistant = Assistant(string.Empty, "c1");
            var first = Tool("c1", "first");
            var duplicate = Tool("c1", "second");
            var unknown = Tool("zzz");
            var noId = Tool(null);
            var orphanLater = Tool("c1", "orphan");
            var finalAnswer = Assistant("done");

            // Act
            var result = ToolCallPairing.FilterPairs(new[]
            {
                user, assistant, first, duplicate, unknown, noId, finalAnswer, orphanLater
            });

            // Assert
            Assert.Equal(new[] { user, assistant, first, finalAnswer }, result);
        }

        [Fact]
        public void FilterPairs_MultiCall_PartialSurvivalCopiesRequestWithoutMutatingInput()
        {
            // Arrange
            var assistant = Assistant("text", "c1", "c2", "c3");
            var r1 = Tool("c1");
            var r2 = Tool("c2");
            r2.IsDeleted = true;
            var r3 = Tool("c3");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { User("go"), assistant, r1, r2, r3 });

            // Assert
            Assert.Equal(4, result.Count);
            var sentAssistant = result[1];
            Assert.NotSame(assistant, sentAssistant);
            Assert.Equal("text", sentAssistant.Content);
            Assert.Equal(new[] { "c1", "c3" }, sentAssistant.ToolCalls!.Select(c => c.Id));
            Assert.Same(r1, result[2]);
            Assert.Same(r3, result[3]);
            Assert.Equal(3, assistant.ToolCalls!.Count);
        }

        [Fact]
        public void FilterPairs_ResultsOutOfOrder_EmittedInRequestOrder()
        {
            // Arrange
            var assistant = Assistant(string.Empty, "c1", "c2");
            var r2 = Tool("c2");
            var r1 = Tool("c1");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { assistant, r2, r1 });

            // Assert
            Assert.Equal(new[] { assistant, r1, r2 }, result);
        }

        [Fact]
        public void FilterPairs_AllCallsDropped_ProseAssistantKeptWithoutToolCalls()
        {
            // Arrange
            var thinking = Thinking("plan");
            var assistant = Assistant("I'll read it", "c1");

            // Act
            var result = ToolCallPairing.FilterPairs(new[] { thinking, assistant });

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Same(thinking, result[0]);
            Assert.Equal("I'll read it", result[1].Content);
            Assert.Null(result[1].ToolCalls);
            Assert.NotNull(assistant.ToolCalls);
        }

        [Fact]
        public void FilterPairs_NonToolMessages_PassThroughUnchangedAndInOrder()
        {
            // Arrange
            var system = new ChatMessage { Role = ChatMessageRole.System, Content = "sys" };
            var user = User("hi");
            var thinking = Thinking("t");
            var assistant = Assistant("a");
            var deleted = User("gone");
            deleted.IsDeleted = true;

            // Act
            var result = ToolCallPairing.FilterPairs(new List<ChatMessage> { system, deleted, user, thinking, assistant });

            // Assert
            Assert.Equal(new[] { system, user, thinking, assistant }, result);
        }
    }
}
