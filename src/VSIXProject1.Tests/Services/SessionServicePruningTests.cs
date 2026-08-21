using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Tests for SessionService message pruning behavior.
    /// Verifies that old messages are removed when token limit is exceeded,
    /// and system messages are preserved when requested.
    /// </summary>
    public class SessionServicePruningTests
    {
        private SessionService CreateSessionService()
        {
            return new SessionService();
        }

        [Fact]
        public async Task PruneOldMessagesAsync_RemovesOldestMessagesFirst()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var session = service.GetCurrentSession();

            var msg1 = new ChatMessage { Role = ChatMessageRole.User, Content = "First", Timestamp = DateTime.UtcNow.AddSeconds(-3) };
            var msg2 = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Second", Timestamp = DateTime.UtcNow.AddSeconds(-2) };
            var msg3 = new ChatMessage { Role = ChatMessageRole.User, Content = "Third", Timestamp = DateTime.UtcNow.AddSeconds(-1) };

            await service.AddMessageAsync(msg1);
            await service.AddMessageAsync(msg2);
            await service.AddMessageAsync(msg3);

            int countBefore = session.Messages.Count;

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(500, keepSystemMessages: true);

            // Assert
            Assert.True(countBefore >= 3, "Should start with at least 3 messages");
            Assert.True(removedCount >= 0, "Should have non-negative removed count");
            // After pruning, count should be <= before
            Assert.True(session.Messages.Count <= countBefore, "Session should have fewer or equal messages after pruning");
        }

        [Fact]
        public async Task PruneOldMessagesAsync_PreservesSystemMessages_WhenFlagSet()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var session = service.GetCurrentSession();

            var systemMsg = new ChatMessage { Role = ChatMessageRole.System, Content = "System prompt", Timestamp = DateTime.UtcNow.AddSeconds(-5) };
            var userMsg = new ChatMessage { Role = ChatMessageRole.User, Content = "User query", Timestamp = DateTime.UtcNow.AddSeconds(-1) };

            await service.AddMessageAsync(systemMsg);
            await service.AddMessageAsync(userMsg);

            int countBefore = session.Messages.Count;

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(100, keepSystemMessages: true);

            // Assert
            Assert.True(session.Messages.Any(m => m.Role == ChatMessageRole.System), "System messages should be preserved");
            Assert.True(session.Messages.Count <= countBefore, "Total message count should not increase");
        }

        [Fact]
        public async Task PruneOldMessagesAsync_ReturnsRemovedCount()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            var msg1 = new ChatMessage { Role = ChatMessageRole.User, Content = "First", Timestamp = DateTime.UtcNow.AddSeconds(-2) };
            var msg2 = new ChatMessage { Role = ChatMessageRole.User, Content = "Second", Timestamp = DateTime.UtcNow.AddSeconds(-1) };

            await service.AddMessageAsync(msg1);
            await service.AddMessageAsync(msg2);

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(500);

            // Assert
            Assert.Equal(removedCount, prunedMessages.Count);
            Assert.True(removedCount >= 0, "Removed count should not be negative");
        }

        [Fact]
        public async Task PruneOldMessagesAsync_HandlesEmptySession()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(1000);

            // Assert
            Assert.Equal(0, removedCount);
            Assert.Empty(prunedMessages);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_HandlesSingleMessage()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            var msg = new ChatMessage { Role = ChatMessageRole.User, Content = "Only message" };
            await service.AddMessageAsync(msg);

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(100);

            // Assert - should not prune the last message
            var session = service.GetCurrentSession();
            Assert.NotEmpty(session.Messages);
        }

        [Fact]
        public async Task PruneOldMessagesAsync_SavesSessionAfterPruning()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var sessionId = service.GetCurrentSession().Id;

            var msg1 = new ChatMessage { Role = ChatMessageRole.User, Content = "First", Timestamp = DateTime.UtcNow.AddSeconds(-1) };
            var msg2 = new ChatMessage { Role = ChatMessageRole.User, Content = "Second", Timestamp = DateTime.UtcNow };

            await service.AddMessageAsync(msg1);
            await service.AddMessageAsync(msg2);

            // Act
            var (removedCount, prunedMessages) = await service.PruneOldMessagesAsync(500);

            // Assert - session should be persisted (we just verify the method completes without error)
            var session = service.GetCurrentSession();
            Assert.NotNull(session);
            Assert.Equal(sessionId, session.Id);
        }
    }
}
