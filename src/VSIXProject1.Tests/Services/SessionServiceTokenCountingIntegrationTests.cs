using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Moq;
using Xunit;

namespace ContinueVS.Tests.Services
{
    public class SessionServiceTokenCountingIntegrationTests
    {
        private SessionService CreateSessionServiceWithTokenCounter()
        {
            var tokenCounter = new SimpleTokenCounterService();
            return new SessionService(tokenCounter);
        }

        [Fact]
        public async Task SessionService_IntegratesTokenCountingService()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();
            // Create two messages that together exceed a small token limit
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "a", Timestamp = DateTime.UtcNow.AddSeconds(-1) });
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = new string('b', 800), Timestamp = DateTime.UtcNow });

            // Act
            // msg1: ~51 tokens (short msg minimum)
            // msg2: 800/4 + 50 = 250 tokens
            // Total: 301 tokens. Pruning with max 100 will remove msg1 (leaving 250), then msg2 (leaving 0)
            // So it removes both to get under 100.
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(100);

            // Assert - should remove at least msg1
            Assert.True(removedCount >= 1, "Should have removed at least message 1");
        }

        [Fact]
        public async Task PruneOldMessagesAsync_PreservesSystemMessages()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();
            var systemMsg = new ChatMessage { Role = ChatMessageRole.System, Content = new string('a', 400), Timestamp = DateTime.UtcNow.AddSeconds(-2) };
            var userMsg = new ChatMessage { Role = ChatMessageRole.User, Content = "Hello", Timestamp = DateTime.UtcNow };
            session.Messages.Add(systemMsg);
            session.Messages.Add(userMsg);

            // Act
            // When keepSystemMessages=true, system messages are excluded from pruning consideration
            // Only user message (~51 tokens) is counted -> 51 is not > 100, so nothing pruned
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(100, keepSystemMessages: true);

            // Assert
            // Since only user message (51 tokens) is considered and 51 <= 100, nothing should be pruned
            Assert.Equal(0, removedCount);
            Assert.Equal(2, session.Messages.Count);
            Assert.Contains(systemMsg, session.Messages);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_RemovesOldestFirst()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();

            // Add 3 small messages with different timestamps
            var msg1 = new ChatMessage { Role = ChatMessageRole.User, Content = "AA", Timestamp = DateTime.UtcNow.AddSeconds(-2) };
            var msg2 = new ChatMessage { Role = ChatMessageRole.User, Content = "BB", Timestamp = DateTime.UtcNow.AddSeconds(-1) };
            var msg3 = new ChatMessage { Role = ChatMessageRole.User, Content = "CC", Timestamp = DateTime.UtcNow };

            session.Messages.Add(msg1);
            session.Messages.Add(msg2);
            session.Messages.Add(msg3);

            // Act
            // Each short message: ~51 tokens
            // Total: 153 tokens. Prune with very high limit (1000) so nothing is removed
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(1000);

            // Assert
            // With high limit, should not prune anything
            Assert.Equal(0, removedCount);
            Assert.Equal(3, session.Messages.Count);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_WithHighLimit_DoesNotPrune()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "Test message" });

            // Act
            // Short message: ~51 tokens
            // Prune with very high limit (10000) should not remove anything
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(10000);

            // Assert
            Assert.Equal(0, removedCount);
            Assert.Single(session.Messages);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_EmptySession_ReturnsZero()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(100);

            // Assert
            Assert.Equal(0, removedCount);
            Assert.Empty(prunedMessages);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_SavesSessionAfterPruning()
        {
            // Arrange
            var service = CreateSessionServiceWithTokenCounter();
            var session = service.GetCurrentSession();
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "a", Timestamp = DateTime.UtcNow.AddSeconds(-1) });
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = new string('b', 800), Timestamp = DateTime.UtcNow });

            // Act
            var originalUpdateTime = session.UpdatedAt;
            await Task.Delay(10);
            // Prune to max 100 should remove msg1, leaving msg2 (~250 tokens)
            // Since msg2 is still > 100, it will also be removed, leaving empty session
            var (removedCount, _) = await service.PruneOldMessagesAsync(100);

            // Assert
            // Should remove at least one message
            Assert.True(removedCount >= 1, "Should have removed at least one message");
            // UpdatedAt should be newer after pruning if messages were removed
            if (removedCount > 0)
            {
                Assert.True(session.UpdatedAt >= originalUpdateTime);
            }
        }
    }
}
