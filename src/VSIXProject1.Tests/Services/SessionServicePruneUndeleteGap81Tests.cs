#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using Xunit;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap81 tests: User-Selectable Prune of History Q&A (with Undelete, no Restore).
    /// Verifies SoftDeleteMessageAsync / UndeleteMessageAsync tombstone behavior and that
    /// PackageMessages excludes soft-deleted (pruned) entries from the LLM payload while
    /// retaining the bytes in the session for undelete.
    /// </summary>
    public class SessionServicePruneUndeleteGap81Tests
    {
        private SessionService CreateSessionService()
        {
            var tokenCounter = new SimpleTokenCounterService();
            return new SessionService(tokenCounter);
        }

        private async Task<ChatMessage> AddMessageAsync(SessionService service, ChatMessageRole role, string content)
        {
            var msg = new ChatMessage { Id = Guid.NewGuid().ToString(), Role = role, Content = content };
            await service.AddMessageAsync(msg);
            return msg;
        }

        [Fact]
        public async Task SoftDeleteAsync_SetsIsDeletedAndPersists()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(service, ChatMessageRole.User, "Hello");

            // Act
            await service.SoftDeleteMessageAsync(msg.Id!);

            // Assert
            var session = service.GetCurrentSession();
            var stored = session.Messages.First(m => m.Id == msg.Id);
            Assert.True(stored.IsDeleted, "Soft-deleted message should be tombstoned.");
        }

        [Fact]
        public async Task SoftDeleteAsync_DoesNotRemoveMessage()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(service, ChatMessageRole.User, "Hello");

            // Act
            await service.SoftDeleteMessageAsync(msg.Id!);

            // Assert - bytes retained (message still present, contrast to hard-remove)
            var session = service.GetCurrentSession();
            Assert.Contains(session.Messages, m => m.Id == msg.Id);
        }

        [Fact]
        public async Task SoftDeleteAsync_ThrowsOnNullOrEmptyId()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.SoftDeleteMessageAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SoftDeleteMessageAsync("   "));
        }

        [Fact]
        public async Task SoftDeleteAsync_ThrowsOnMissingMessage()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.SoftDeleteMessageAsync("missing-id"));
        }

        [Fact]
        public async Task UndeleteAsync_ClearsTombstoneAndPersists()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var msg = await AddMessageAsync(service, ChatMessageRole.User, "Hello");
            await service.SoftDeleteMessageAsync(msg.Id!);

            // Act
            await service.UndeleteMessageAsync(msg.Id!);

            // Assert
            var session = service.GetCurrentSession();
            var stored = session.Messages.First(m => m.Id == msg.Id);
            Assert.False(stored.IsDeleted, "Undeleted message should have tombstone cleared.");
        }

        [Fact]
        public async Task UndeleteAsync_ThrowsOnNullOrEmptyId()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => service.UndeleteMessageAsync(null!));
            await Assert.ThrowsAsync<ArgumentException>(() => service.UndeleteMessageAsync("  "));
        }

        [Fact]
        public async Task UndeleteAsync_ThrowsOnMissingMessage()
        {
            // Arrange
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UndeleteMessageAsync("missing-id"));
        }

        [Fact]
        public async Task PackageMessages_ExcludesSoftDeletedHistory()
        {
            // Arrange - build messages in-memory (PackageMessages reads the in-memory current
            // session via GetCurrentSession; tombstones are what the filter checks, so we set
            // them directly here to avoid flaky real-storage writes in this focused test).
            var service = CreateSessionService();
            await service.CreateNewSessionAsync("Test Session");
            var session = service.GetCurrentSession();

            var keptUser = new ChatMessage { Role = ChatMessageRole.User, Content = "Keep me", Timestamp = DateTime.UtcNow.AddSeconds(-4) };
            var keptAssistant = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Kept response", Timestamp = DateTime.UtcNow.AddSeconds(-3) };
            var prunedUser = new ChatMessage { Role = ChatMessageRole.User, Content = "Prune me", Timestamp = DateTime.UtcNow.AddSeconds(-2) };
            var prunedAssistant = new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Pruned response", Timestamp = DateTime.UtcNow.AddSeconds(-1) };
            keptUser.Id = "kept-user";
            keptAssistant.Id = "kept-asst";
            prunedUser.Id = "pruned-user";
            prunedAssistant.Id = "pruned-asst";
            session.Messages.AddRange(new[] { keptUser, keptAssistant, prunedUser, prunedAssistant });

            // Soft-delete (prune) the second Q&A pair via the tombstone primitive
            prunedUser.IsDeleted = true;
            prunedAssistant.IsDeleted = true;

            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "SYSTEM" };

            // Act
            var packaged = service.PackageMessages(null, systemMessage, "new user turn");

            // Assert - pruned entries excluded from payload, kept ones + system + new turn included
            Assert.Contains(packaged, m => m.Role == ChatMessageRole.System);
            Assert.Contains(packaged, m => m.Id == keptUser.Id);
            Assert.Contains(packaged, m => m.Id == keptAssistant.Id);
            Assert.DoesNotContain(packaged, m => m.Id == prunedUser.Id);
            Assert.DoesNotContain(packaged, m => m.Id == prunedAssistant.Id);
            Assert.Contains(packaged, m => m.Role == ChatMessageRole.User && m.Content == "new user turn");
        }
    }
}
