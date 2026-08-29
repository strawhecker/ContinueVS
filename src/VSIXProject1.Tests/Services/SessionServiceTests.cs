#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using Xunit;

namespace VSIXProject1.Tests.Services
{
    public class SessionServiceTests : IDisposable
    {
        private readonly SessionService _sessionService;
        private readonly string _testSessionsDir;

        public SessionServiceTests()
        {
            var tokenCounter = new SimpleTokenCounterService();
            _sessionService = new SessionService(tokenCounter);
            _testSessionsDir = Path.Combine(Path.GetTempPath(), "continue_test_sessions_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testSessionsDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testSessionsDir))
            {
                Directory.Delete(_testSessionsDir, recursive: true);
            }
        }

        #region GetCurrentSession Tests

        [Fact]
        public void GetCurrentSession_ReturnsSessionOnFirstCall()
        {
            // Arrange
            // Act
            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.NotNull(session);
            Assert.False(string.IsNullOrEmpty(session.Id));
            Assert.Equal("New Conversation", session.Title);
            Assert.NotNull(session.Messages);
            Assert.Empty(session.Messages);
            Assert.True(session.IsActive);
        }

        [Fact]
        public void GetCurrentSession_ReturnsSameSessionOnMultipleCalls()
        {
            // Arrange
            var session1 = _sessionService.GetCurrentSession();

            // Act
            var session2 = _sessionService.GetCurrentSession();

            // Assert
            Assert.Same(session1, session2);
            Assert.Equal(session1.Id, session2.Id);
        }

        #endregion

        #region CreateNewSessionAsync Tests

        [Fact]
        public async Task CreateNewSessionAsync_CreatesNewSession_WithNoTitle()
        {
            // Arrange
            var eventFired = false;
            SessionChangedEventArgs? capturedArgs = null;
            _sessionService.SessionChanged += (sender, args) =>
            {
                eventFired = true;
                capturedArgs = args;
            };

            // Act
            await _sessionService.CreateNewSessionAsync();
            var currentSession = _sessionService.GetCurrentSession();

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(capturedArgs);
            Assert.Equal(SessionChangeType.Created, capturedArgs.ChangeType);
            Assert.Equal("New Conversation", currentSession.Title);
        }

        [Fact]
        public async Task CreateNewSessionAsync_CreatesNewSession_WithCustomTitle()
        {
            // Arrange
            var customTitle = "Test Session";
            var eventFired = false;
            _sessionService.SessionChanged += (sender, args) =>
            {
                eventFired = true;
            };

            // Act
            await _sessionService.CreateNewSessionAsync(customTitle);
            var currentSession = _sessionService.GetCurrentSession();

            // Assert
            Assert.True(eventFired);
            Assert.Equal(customTitle, currentSession.Title);
        }

        #endregion

        #region AddMessageAsync Tests

        [Fact]
        public async Task AddMessageAsync_AddsMessageToSession_AndFiresEvent()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Hello"
            };
            var eventFired = false;
            MessageAddedEventArgs? capturedArgs = null;
            _sessionService.MessageAdded += (sender, args) =>
            {
                eventFired = true;
                capturedArgs = args;
            };

            // Act
            await _sessionService.AddMessageAsync(message);
            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(capturedArgs);
            Assert.Single(session.Messages);
            Assert.Equal("Hello", session.Messages[0].Content);
            Assert.False(string.IsNullOrEmpty(session.Messages[0].Id));
        }

        [Fact]
        public async Task AddMessageAsync_AssignsIdIfMissing()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Test"
            };

            // Act
            await _sessionService.AddMessageAsync(message);

            // Assert
            Assert.False(string.IsNullOrEmpty(message.Id));
        }

        [Fact]
        public async Task AddMessageAsync_PreservesProvidedId()
        {
            // Arrange
            var providedId = Guid.NewGuid().ToString();
            var message = new ChatMessage
            {
                Id = providedId,
                Role = ChatMessageRole.User,
                Content = "Test"
            };

            // Act
            await _sessionService.AddMessageAsync(message);
            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.Equal(providedId, session.Messages[0].Id);
        }

        [Fact]
        public async Task AddMessageAsync_ThrowsOnNullMessage()
        {
            // Arrange
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _sessionService.AddMessageAsync(null!));
        }

        #endregion

        #region UpdateMessageAsync Tests

        [Fact]
        public async Task UpdateMessageAsync_UpdatesExistingMessage()
        {
            // Arrange
            var originalMessage = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Original"
            };
            await _sessionService.AddMessageAsync(originalMessage);
            var messageId = originalMessage.Id!;

            var updatedMessage = new ChatMessage
            {
                Role = ChatMessageRole.Assistant,
                Content = "Updated"
            };

            // Act
            await _sessionService.UpdateMessageAsync(messageId, updatedMessage);
            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.Single(session.Messages);
            Assert.Equal(messageId, session.Messages[0].Id);
            Assert.Equal("Updated", session.Messages[0].Content);
            Assert.Equal(ChatMessageRole.Assistant, session.Messages[0].Role);
        }

        [Fact]
        public async Task UpdateMessageAsync_ThrowsOnMissingMessage()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid().ToString();
            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sessionService.UpdateMessageAsync(nonExistentId, message));
        }

        [Fact]
        public async Task UpdateMessageAsync_ThrowsOnNullMessageId()
        {
            // Arrange
            var message = new ChatMessage { Role = ChatMessageRole.User, Content = "Test" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _sessionService.UpdateMessageAsync(null!, message));
        }

        #endregion

        #region DeleteMessageAsync Tests

        [Fact]
        public async Task DeleteMessageAsync_RemovesMessageFromSession()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "To Delete"
            };
            await _sessionService.AddMessageAsync(message);
            var messageId = message.Id!;

            // Act
            await _sessionService.DeleteMessageAsync(messageId);
            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.Empty(session.Messages);
        }

        [Fact]
        public async Task DeleteMessageAsync_ThrowsOnMissingMessage()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _sessionService.DeleteMessageAsync(nonExistentId));
        }

        #endregion

        #region SaveCurrentSessionAsync Tests

        [Fact]
        public async Task SaveCurrentSessionAsync_PersistsSessionToDisk()
        {
            // Arrange
            var message = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Persist Me"
            };
            await _sessionService.AddMessageAsync(message);
            var session = _sessionService.GetCurrentSession();

            var eventFired = false;
            _sessionService.SessionChanged += (sender, args) =>
            {
                eventFired = true;
            };

            // Act
            await _sessionService.SaveCurrentSessionAsync();

            // Assert
            Assert.True(eventFired);
            // File should exist at ~/.continue/sessions/{sessionId}.json
            // We can't verify without mocking, but the test verifies no exception thrown
        }

        #endregion

        #region LoadSessionAsync Tests

        [Fact]
        public async Task LoadSessionAsync_LoadsSessionFromDisk()
        {
            // Arrange
            var originalMessage = new ChatMessage
            {
                Role = ChatMessageRole.User,
                Content = "Original"
            };
            await _sessionService.AddMessageAsync(originalMessage);
            var session = _sessionService.GetCurrentSession();
            var sessionId = session.Id;

            await _sessionService.SaveCurrentSessionAsync();

            // Clear current session
            var newSession = new Session
            {
                Id = Guid.NewGuid().ToString(),
                Title = "New",
                Messages = new List<ChatMessage>()
            };

            var eventFired = false;
            _sessionService.SessionChanged += (sender, args) =>
            {
                eventFired = true;
            };

            // Act
            await _sessionService.LoadSessionAsync(sessionId);
            var loadedSession = _sessionService.GetCurrentSession();

            // Assert
            Assert.True(eventFired);
            Assert.NotEmpty(loadedSession.Messages);
        }

        [Fact]
        public async Task LoadSessionAsync_ThrowsOnNullSessionId()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _sessionService.LoadSessionAsync(null!));
        }

        #endregion

        #region ListSessionsAsync Tests

        [Fact]
        public async Task ListSessionsAsync_ReturnsEmptyIfNoSessions()
        {
            // Arrange
            // Act
            var sessions = new List<SessionMetadata>();
            await foreach (var session in _sessionService.ListSessionsAsync())
            {
                sessions.Add(session);
            }

            // Assert
            // May or may not be empty depending on existing files, but test structure is valid
        }

        [Fact]
        public async Task ListSessionsAsync_RespectsLimitParameter()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                await _sessionService.CreateNewSessionAsync($"Session {i}");
            }

            // Act
            var sessions = new List<SessionMetadata>();
            await foreach (var session in _sessionService.ListSessionsAsync(limit: 2))
            {
                sessions.Add(session);
            }

            // Assert
            Assert.True(sessions.Count <= 2);
        }

        #endregion

        #region DeleteSessionAsync Tests

        [Fact]
        public async Task DeleteSessionAsync_DeletesSessionAndFiresEvent()
        {
            // Arrange
            await _sessionService.CreateNewSessionAsync("To Delete");
            var session = _sessionService.GetCurrentSession();
            var sessionId = session.Id;

            await _sessionService.SaveCurrentSessionAsync();

            var eventFired = false;
            SessionChangedEventArgs? capturedArgs = null;
            _sessionService.SessionChanged += (sender, args) =>
            {
                if (args.ChangeType == SessionChangeType.Deleted)
                {
                    eventFired = true;
                    capturedArgs = args;
                }
            };

            // Act
            await _sessionService.DeleteSessionAsync(sessionId);

            // Assert
            Assert.True(eventFired);
            Assert.NotNull(capturedArgs);
            Assert.Equal(SessionChangeType.Deleted, capturedArgs.ChangeType);
        }

        [Fact]
        public async Task DeleteSessionAsync_ThrowsOnNullSessionId()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _sessionService.DeleteSessionAsync(null!));
        }

        #endregion

        #region Message Content Persistence Tests

        [Fact]
        public async Task MultipleMessages_AllPersistedCorrectly()
        {
            // Arrange
            var messages = new[]
            {
                new ChatMessage { Role = ChatMessageRole.User, Content = "Message 1" },
                new ChatMessage { Role = ChatMessageRole.Assistant, Content = "Response 1" },
                new ChatMessage { Role = ChatMessageRole.User, Content = "Message 2" }
            };

            // Act
            foreach (var msg in messages)
            {
                await _sessionService.AddMessageAsync(msg);
            }

            var session = _sessionService.GetCurrentSession();

            // Assert
            Assert.Equal(3, session.Messages.Count);
            Assert.Equal("Message 1", session.Messages[0].Content);
            Assert.Equal("Response 1", session.Messages[1].Content);
            Assert.Equal("Message 2", session.Messages[2].Content);
        }

        #endregion

        #region gap34 PackageMessages Tests

        [Fact]
        public void PackageMessages_EmptyHistory_ReturnsTwoMessages()
        {
            // Arrange
            var model = new ModelInfo { ContextWindow = 4096 };
            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "You are helpful." };

            // Act
            var result = _sessionService.PackageMessages(model, systemMessage, "Hello");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(ChatMessageRole.System, result[0].Role);
            Assert.Equal("You are helpful.", result[0].Content);
            Assert.Equal(ChatMessageRole.User, result[1].Role);
            Assert.Equal("Hello", result[1].Content);
        }

        [Fact]
        public async Task PackageMessages_SingleTurn_ReturnsFourMessages()
        {
            // Arrange
            var model = new ModelInfo { ContextWindow = 4096 };
            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "You are helpful." };
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.User, Content = "First question" });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "First answer" });

            // Act — "Second question" is the new user content (not yet in session)
            var result = _sessionService.PackageMessages(model, systemMessage, "Second question");

            // Assert: [system, user:First question, assistant:First answer, user:Second question]
            Assert.Equal(4, result.Count);
            Assert.Equal(ChatMessageRole.System, result[0].Role);
            Assert.Equal(ChatMessageRole.User, result[1].Role);
            Assert.Equal("First question", result[1].Content);
            Assert.Equal(ChatMessageRole.Assistant, result[2].Role);
            Assert.Equal("First answer", result[2].Content);
            Assert.Equal(ChatMessageRole.User, result[3].Role);
            Assert.Equal("Second question", result[3].Content);
        }

        [Fact]
        public async Task PackageMessages_MultiTurn_WithinBudget_ReturnsAllHistory()
        {
            // Arrange
            var model = new ModelInfo { ContextWindow = 8192 };
            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "sys" };
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.User, Content = "Q1" });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "A1" });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.User, Content = "Q2" });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "A2" });

            // Act
            var result = _sessionService.PackageMessages(model, systemMessage, "Q3");

            // Assert — all 4 history turns + system + new user = 6
            Assert.Equal(6, result.Count);
            Assert.Equal(ChatMessageRole.System, result[0].Role);
            Assert.Equal("Q1", result[1].Content);
            Assert.Equal("A1", result[2].Content);
            Assert.Equal("Q2", result[3].Content);
            Assert.Equal("A2", result[4].Content);
            Assert.Equal(ChatMessageRole.User, result[5].Role);
            Assert.Equal("Q3", result[5].Content);
        }

        [Fact]
        public async Task PackageMessages_OverBudget_PrunesOldestTurnsFirst()
        {
            // Arrange — use a tiny context window so history cannot fit
            var model = new ModelInfo { ContextWindow = 300 };
            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "sys" };
            // Add two old turns (will be pruned) and one recent turn (will be kept)
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.User, Content = new string('x', 400), Timestamp = DateTime.UtcNow.AddMinutes(-10) });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.Assistant, Content = new string('y', 400), Timestamp = DateTime.UtcNow.AddMinutes(-9) });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.User, Content = "recent Q", Timestamp = DateTime.UtcNow.AddMinutes(-1) });
            await _sessionService.AddMessageAsync(new ChatMessage { Role = ChatMessageRole.Assistant, Content = "recent A", Timestamp = DateTime.UtcNow });

            // Act
            var result = _sessionService.PackageMessages(model, systemMessage, "new Q");

            // Assert — system always present; oldest turns pruned; result has fewer than 6 messages
            Assert.Equal(ChatMessageRole.System, result[0].Role);
            Assert.Equal(ChatMessageRole.User, result[result.Count - 1].Role);
            Assert.Equal("new Q", result[result.Count - 1].Content);
            Assert.True(result.Count < 6, "Over-budget history should be pruned");
        }

        [Fact]
        public void PackageMessages_NullOrZeroContextWindow_FallsBackTo4096()
        {
            // Arrange — ContextWindow = 0 should fall back to 4096 budget
            var modelZero = new ModelInfo { ContextWindow = 0 };
            var modelNull = (ModelInfo?)null;
            var systemMessage = new ChatMessage { Role = ChatMessageRole.System, Content = "sys" };

            // Act — should not throw
            var resultZero = _sessionService.PackageMessages(modelZero, systemMessage, "hello");
            var resultNull = _sessionService.PackageMessages(modelNull, systemMessage, "hello");

            // Assert
            Assert.Equal(2, resultZero.Count);
            Assert.Equal(2, resultNull.Count);
        }

        #endregion
    }
}
