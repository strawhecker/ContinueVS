using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for gap74: Context Budget Tracking & Backtracking Algorithm.
    /// Tests SessionService methods: GetContextBudgetState, EstimateTokensUsed, BacktrackAndOptimizeAsync.
    /// </summary>
    public class SessionServiceContextBudgetTests
    {
        private readonly SessionService _sessionService;

        public SessionServiceContextBudgetTests()
        {
            var tokenCountingService = new MockTokenCountingService();
            _sessionService = new SessionService(tokenCountingService);
        }

        [Fact]
        public void EstimateTokensUsed_ReturnsApproximateTokenCount()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Hello, world!" },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Hi there!" }
            };

            int tokens = _sessionService.EstimateTokensUsed(history);

            Assert.True(tokens > 0, "Tokens should be greater than 0");
        }

        [Fact]
        public void EstimateTokensUsed_AccountsForToolCallOverhead()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatMessageRole.Assistant,
                    Content = "I'll help you.",
                    ToolCalls = new List<ToolCall>
                    {
                        new ToolCall { Id = "1", Name = "ls", Arguments = new Dictionary<string, object>() }
                    }
                }
            };

            int tokens = _sessionService.EstimateTokensUsed(history);

            Assert.True(tokens >= 150, "Should account for tool call overhead");
        }

        [Fact]
        public void GetContextBudgetState_ReturnsSafe_WhenBelowThreshold()
        {
            var session = _sessionService.GetCurrentSession();
            session.Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Short message" }
            };

            var state = _sessionService.GetContextBudgetState();

            Assert.Equal(ContextBudgetState.Safe, state);
        }

        [Fact]
        public void GetContextBudgetState_ReturnsLocked_WhenExceedsThreshold()
        {
            var session = _sessionService.GetCurrentSession();
            string veryLongContent = new('x', 5000);
            session.Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = veryLongContent },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = veryLongContent },
                new ChatMessage { Role = ChatMessageRole.User, Content = veryLongContent }
            };

            var state = _sessionService.GetContextBudgetState();

            Assert.Equal(ContextBudgetState.Locked, state);
        }

        [Fact]
        public async Task BacktrackAndOptimizeAsync_RemovesNewestUnit_WhenOverBudget()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Id = "1", Role = ChatMessageRole.User, Content = "First message" },
                new ChatMessage { Id = "2", Role = ChatMessageRole.Assistant, Content = new string('x', 3000) },
                new ChatMessage { Id = "3", Role = ChatMessageRole.Tool, Content = "Result 1" },
                new ChatMessage { Id = "4", Role = ChatMessageRole.User, Content = new string('x', 3000) },
                new ChatMessage { Id = "5", Role = ChatMessageRole.Assistant, Content = new string('x', 3000) }
            };
            int maxTokens = 1000;
            int reserve = 100;

            var (trimmed, summary) = await _sessionService.BacktrackAndOptimizeAsync(history, maxTokens, reserve);

            Assert.True(trimmed.Count < history.Count, "Should remove messages");
            Assert.NotNull(summary);
            Assert.Contains("Removed", summary);
        }

        [Fact]
        public async Task BacktrackAndOptimizeAsync_PreservesOldestContext_WhenTrimming()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Id = "1", Role = ChatMessageRole.System, Content = "System prompt" },
                new ChatMessage { Id = "2", Role = ChatMessageRole.User, Content = "Original question" },
                new ChatMessage { Id = "3", Role = ChatMessageRole.Assistant, Content = new string('x', 5000) },
                new ChatMessage { Id = "4", Role = ChatMessageRole.User, Content = new string('x', 5000) },
                new ChatMessage { Id = "5", Role = ChatMessageRole.Assistant, Content = new string('x', 5000) }
            };
            int maxTokens = 2000;
            int reserve = 500;

            var (trimmed, summary) = await _sessionService.BacktrackAndOptimizeAsync(history, maxTokens, reserve);

            Assert.True(trimmed.Count > 0, "Should preserve at least some messages");
            Assert.Equal("1", trimmed[0].Id);
        }

        private class MockTokenCountingService : ITokenCountingService
        {
            public int EstimateTokenCount(string text) 
                => (text?.Length ?? 0) / 4;

            public int EstimateTokenCount(List<ChatMessage> messages)
            {
                int total = 0;
                foreach (var msg in messages)
                {
                    total += (msg.Content?.Length ?? 0) / 4;
                }
                return total;
            }

            public int CountMessageTokens(ChatMessage message)
                => (message?.Content?.Length ?? 0) / 4;

            public int CountMessagesTokens(List<ChatMessage> messages)
                => EstimateTokenCount(messages);

            public int EstimateFutureMessageTokens(string content)
                => (content?.Length ?? 0) / 4;

            public int CharactersPerToken => 4;
        }
    }
}
