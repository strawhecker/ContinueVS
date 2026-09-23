#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Events;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Session-title tests: the first line of the first send of a new session becomes
    /// the session title, shown at the top of the chat (CurrentSessionTitle) and persisted
    /// via ISessionService.SetSessionTitleAsync. Subsequent sends and loaded sessions are
    /// never overwritten.
    /// </summary>
    public class ChatPageViewModelSessionTitleTests
    {
        // -- Mock helpers (mirrors ChatPageAgentModeE2ETests pattern) ------------------

        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            var mock = new Mock<ILlmService>();
            // Ask mode: emit a text chunk then done (no tools) so the send completes.
            mock.Setup(x => x.StreamAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<StreamOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns((IEnumerable<ChatMessage> _, StreamOptions? __, CancellationToken ct) =>
                    GenerateChunksAsync(ct));
            return mock;
        }

        private static async IAsyncEnumerable<CompletionChunk> GenerateChunksAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return new CompletionChunk { Type = ChunkType.Text, Content = "ok" };
            yield return new CompletionChunk { Type = ChunkType.Done, IsDone = true };
            await Task.CompletedTask;
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            var mock = new Mock<IToolService>();
            mock.Setup(x => x.GetAvailableTools()).Returns(new List<ToolDefinition>());
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            mock.Setup(x => x.ShowNotificationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationType>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo> { new ModelInfo { Name = "test", ContextWindow = 4096 } },
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerAction, 100 }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            mock.Setup(x => x.GetSelectedModel()).Returns(config.Models[0]);
            mock.Setup(x => x.GetDefaultModeAsync()).ReturnsAsync(0);
            mock.Setup(x => x.GetDefaultPolicyAsync()).ReturnsAsync(ContinuationPolicy.Interactive);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("prompt");
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var uiState = new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() };
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(uiState);
            return mock;
        }

        /// <summary>
        /// Builds a session service mock backed by a live mutable Session so message count
        /// and title evolve across sends (AddMessageAsync appends, SetSessionTitleAsync sets).
        /// </summary>
        private static (
            Mock<ISessionService> mock,
            Session session,
            List<ChatMessage> store) CreateSessionServiceMock()
        {
            var session = new Session
            {
                Id = "title-session",
                Title = "New Conversation",
                Messages = new List<ChatMessage>()
            };
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.GetCurrentSession()).Returns(session);
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Callback<ChatMessage>(m => session.Messages.Add(m))
                .Returns(Task.CompletedTask);
            mock.Setup(x => x.SetSessionTitleAsync(It.IsAny<string>()))
                .Callback<string>(t => session.Title = t)
                .Returns(Task.CompletedTask);
            // PackageMessages returns system + new user turn so the loop can stream.
            mock.Setup(x => x.PackageMessages(
                    It.IsAny<ModelInfo>(), It.IsAny<ChatMessage>(), It.IsAny<string>()))
                .Returns((ModelInfo _, ChatMessage system, string user) =>
                    new List<ChatMessage>
                    {
                        system,
                        new ChatMessage { Role = ChatMessageRole.User, Content = user }
                    });
            return (mock, session, session.Messages);
        }

        private static ChatPageViewModel CreateViewModel(Mock<ISessionService> sessionMock)
        {
            return new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                sessionMock.Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);
        }

        // -- Tests ----------------------------------------------------------------------

        [Fact]
        public async Task FirstSend_SetsSessionTitle_FromFirstLine()
        {
            // Arrange
            var (sessionMock, _, _) = CreateSessionServiceMock();
            var vm = CreateViewModel(sessionMock);
            await vm.InitializeAsync();
            vm.InputText = "Refactor the login handler\nand add tests";

            // Act
            if (vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
                await Task.Delay(200);
            }

            // Assert — title derived from the first non-empty line and shown in the header
            sessionMock.Verify(x => x.SetSessionTitleAsync("Refactor the login handler"), Times.Once);
            Assert.Equal("Refactor the login handler", vm.CurrentSessionTitle);
        }

        [Fact]
        public async Task SecondSend_DoesNotOverwriteSessionTitle()
        {
            // Arrange — a session that already has one message and a real title
            var (sessionMock, session, _) = CreateSessionServiceMock();
            session.Messages.Add(new ChatMessage { Role = ChatMessageRole.User, Content = "first" });
            session.Title = "Existing Title";

            var vm = CreateViewModel(sessionMock);
            await vm.InitializeAsync();
            vm.InputText = "second message";

            // Act
            if (vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
                await Task.Delay(200);
            }

            // Assert — SetSessionTitleAsync must NOT be called on subsequent sends
            sessionMock.Verify(x => x.SetSessionTitleAsync(It.IsAny<string>()), Times.Never);
            Assert.Equal("Existing Title", session.Title);
        }

        [Fact]
        public void CurrentSessionTitle_FallsBackToDefault_WhenNoSessionSet()
        {
            // Arrange
            var (sessionMock, _, _) = CreateSessionServiceMock();
            var vm = CreateViewModel(sessionMock);

            // Act — CurrentSession is null; no explicit title set
            vm.CurrentSession = null;

            // Assert
            Assert.Equal("New Conversation", vm.CurrentSessionTitle);
        }

        [Fact]
        public void CurrentSessionTitle_ReflectsCurrentSessionTitle()
        {
            // Arrange
            var (sessionMock, session, _) = CreateSessionServiceMock();
            var vm = CreateViewModel(sessionMock);
            session.Title = "My Session";

            // Act
            vm.CurrentSession = new SessionMetadata { Id = session.Id, Title = "My Session" };

            // Assert
            Assert.Equal("My Session", vm.CurrentSessionTitle);
        }
    }
}
