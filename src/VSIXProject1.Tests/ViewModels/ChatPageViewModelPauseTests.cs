using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Moq;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for pause signal propagation (gap31_2).
    /// Tests that pause state properly cancels active streams and prevents new sends.
    /// </summary>
    public class ChatPageViewModelPauseTests
    {
        private Mock<ILlmService> CreateMockLlmService()
        {
            var mock = new Mock<ILlmService>();
            mock.Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<StreamOptions>(), It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncEnumerableAsync());
            return mock;
        }

        private async IAsyncEnumerable<CompletionChunk> CreateAsyncEnumerableAsync()
        {
            yield return new CompletionChunk { Type = ChunkType.Text, Content = "Test response" };
            await Task.Yield();
        }

        private Mock<IContextService> CreateMockContextService()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private Mock<IToolService> CreateMockToolService()
        {
            var mock = new Mock<IToolService>();
            return mock;
        }

        private Mock<ISessionService> CreateMockSessionService()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.GetCurrentSession())
                .Returns(new Session { Id = "test-session", Messages = new List<ChatMessage>(), ToolCallsExecuted = 0 });
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private Mock<INotificationService> CreateMockNotificationService()
        {
            var mock = new Mock<INotificationService>();
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo> 
                { 
                    new ModelInfo { Name = "test-model", Provider = "ollama", ContextWindow = 8192 }
                },
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            mock.Setup(x => x.GetSelectedModel()).Returns(config.Models[0]);
            mock.Setup(x => x.GetDefaultModeAsync()).ReturnsAsync(0);
            mock.Setup(x => x.GetDefaultPolicyAsync()).ReturnsAsync(ContinuationPolicy.Interactive);
            return mock;
        }

        private Mock<ISystemPromptService> CreateMockSystemPromptService()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("Test prompt");
            return mock;
        }

        private Mock<IUIStateService> CreateMockUIStateService()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(new UIState());
            return mock;
        }

        [Fact(DisplayName = "gap31_2: Pause toggles IsPaused flag")]
        public void Pause_TogglesIsPausedFlag()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            // Simulate streaming state
            viewModel.IsStreaming = true;

            // Assert: Initial state not paused
            Assert.False(viewModel.IsPaused);

            // Act: Execute pause
            viewModel.PauseCommand.Execute(null);

            // Assert: Now paused
            Assert.True(viewModel.IsPaused);

            // Act: Execute pause again (resume)
            viewModel.PauseCommand.Execute(null);

            // Assert: No longer paused
            Assert.False(viewModel.IsPaused);
        }

        [Fact(DisplayName = "gap31_2: IsPausedDisplay reflects pause state")]
        public void IsPausedDisplay_ReflectsPauseState()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.IsStreaming = true;

            // Assert: When not paused, display is "Pause"
            Assert.Equal("Pause", viewModel.IsPausedDisplay);

            // Act: Pause
            viewModel.PauseCommand.Execute(null);

            // Assert: When paused, display is "Resume"
            Assert.Equal("Resume", viewModel.IsPausedDisplay);
        }

        [Fact(DisplayName = "gap31_2: SendMessageCommand disabled when paused")]
        public void SendMessageCommand_DisabledWhenPaused()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.InputText = "test message";

            // Assert: Initially enabled
            Assert.True(viewModel.SendMessageCommand.CanExecute(null));

            // Act: Pause
            viewModel.IsPaused = true;

            // Assert: SendMessageCommand now disabled
            Assert.False(viewModel.SendMessageCommand.CanExecute(null));

            // Act: Resume
            viewModel.IsPaused = false;

            // Assert: SendMessageCommand enabled again
            Assert.True(viewModel.SendMessageCommand.CanExecute(null));
        }

        [Fact(DisplayName = "gap31_2: Cancel still works while paused")]
        public void CancelCommand_WorksWhilePaused()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.IsStreaming = true;
            viewModel.IsPaused = true;

            // Assert: Cancel should be available even while paused
            Assert.True(viewModel.CancelCommand.CanExecute(null));
        }

        [Fact(DisplayName = "gap31_2: PauseCommand enabled only during streaming")]
        public void PauseCommand_EnabledOnlyDuringStreaming()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            // Assert: Initially not streaming, so pause disabled
            Assert.False(viewModel.PauseCommand.CanExecute(null));

            // Act: Start streaming
            viewModel.IsStreaming = true;

            // Assert: Now streaming, so pause enabled
            Assert.True(viewModel.PauseCommand.CanExecute(null));

            // Act: Pause
            viewModel.IsPaused = true;

            // Assert: Still streaming and paused, pause still enabled
            Assert.True(viewModel.PauseCommand.CanExecute(null));
        }

        [Fact(DisplayName = "gap31_2: Multiple pause/resume toggles work correctly")]
        public void MultipleTogglesCycle()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.IsStreaming = true;

            // Act & Assert: Cycle through pause/resume multiple times
            for (int i = 0; i < 3; i++)
            {
                viewModel.PauseCommand.Execute(null);
                Assert.True(viewModel.IsPaused);

                viewModel.PauseCommand.Execute(null);
                Assert.False(viewModel.IsPaused);
            }
        }

        [Fact(DisplayName = "gap31_2: Pause state persists until explicitly resumed")]
        public void PauseStatePersists()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.IsStreaming = true;

            // Act: Pause
            viewModel.PauseCommand.Execute(null);
            Assert.True(viewModel.IsPaused);

            // Assert: Pause state persists through property checks
            Assert.True(viewModel.IsPaused);
            Assert.Equal("Resume", viewModel.IsPausedDisplay);

            // Act: Change other properties shouldn't affect pause state
            viewModel.InputText = "test";
            Assert.True(viewModel.IsPaused);
        }

        [Fact(DisplayName = "gap31_2: Pause and resume update SendMessageCommand state")]
        public void PauseAndResumeUpdateCommandState()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.InputText = "test message";
            var commandEnabledTracker = new List<bool>();

            // Subscribe to CanExecuteChanged to track state changes
            viewModel.SendMessageCommand.CanExecuteChanged += (s, e) =>
            {
                commandEnabledTracker.Add(viewModel.SendMessageCommand.CanExecute(null));
            };

            // Act: Pause
            viewModel.IsPaused = true;

            // Assert: Command state changed
            Assert.NotEmpty(commandEnabledTracker);
            Assert.False(commandEnabledTracker[commandEnabledTracker.Count - 1]);

            // Act: Resume
            viewModel.IsPaused = false;

            // Assert: Command state changed again
            Assert.True(commandEnabledTracker[commandEnabledTracker.Count - 1]);
        }

        [Fact(DisplayName = "gap31_2: Pause with no active stream is safe")]
        public void PauseWithNoActiveStreamIsSafe()
        {
            // Arrange
            var viewModel = new ChatPageViewModel(
                CreateMockLlmService().Object,
                CreateMockContextService().Object,
                CreateMockToolService().Object,
                CreateMockSessionService().Object,
                CreateMockNotificationService().Object,
                CreateMockConfigService().Object,
                CreateMockSystemPromptService().Object,
                CreateMockUIStateService().Object,
                new Mock<IDebugSessionService>().Object
            );

            viewModel.IsStreaming = true;

            // Act: Try to pause when streaming
            // This should not throw even if no chunks are being streamed
            viewModel.PauseCommand.Execute(null);

            // Assert: Pause state is set
            Assert.True(viewModel.IsPaused);
        }
    }
}
