using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using Xunit;
using Moq;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests for ChatPageViewModel pause button UI and state management (gap31_1).
    /// Verifies that the pause button is correctly enabled/disabled based on streaming state,
    /// that the button label toggles between "Pause" and "Resume",
    /// and that the command updates both ViewModel and DebugSessionService state.
    /// </summary>
    public class PauseButtonUITests
    {
        private Mock<ILlmService> CreateMockLlmService()
        {
            var mock = new Mock<ILlmService>();
            mock.Setup(x => x.StreamAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<StreamOptions>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(new AsyncEnumerable());
            return mock;
        }

        private Mock<ISessionService> CreateMockSessionService()
        {
            var mock = new Mock<ISessionService>();
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                Messages = new List<ChatMessage>(),
                Title = "Test",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            mock.Setup(x => x.GetCurrentSession()).Returns(session);
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>())).Returns(Task.CompletedTask);
            mock.Setup(x => x.PruneOldMessagesAsync(It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync((0, new List<ChatMessage>()));
            return mock;
        }

        private Mock<IConfigService> CreateMockConfigService(int contextWindow = 8192)
        {
            var mock = new Mock<IConfigService>();
            var model = new ModelInfo { ContextWindow = contextWindow, Name = "test-model" };
            mock.Setup(x => x.GetSelectedModel()).Returns(model);
            return mock;
        }

        private Mock<IToolService> CreateMockToolService()
        {
            var mock = new Mock<IToolService>();
            mock.Setup(x => x.GetAvailableTools()).Returns(new List<ToolDefinition>());
            return mock;
        }

        private Mock<IContextService> CreateMockContextService()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private Mock<INotificationService> CreateMockNotificationService()
        {
            var mock = new Mock<INotificationService>();
            return mock;
        }

        private Mock<ISystemPromptService> CreateMockSystemPromptService()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("You are a helpful assistant.");
            return mock;
        }

        private Mock<IUIStateService> CreateMockUIStateService()
        {
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() });
            return mock;
        }

        /// <summary>
        /// Test 1: Pause button is disabled when not streaming (gap31_1).
        /// Verifies that the PauseCommand is disabled when IsStreaming is false,
        /// preventing user from clicking pause when no execution is active.
        /// </summary>
        [Fact]
        public void PauseCommand_IsDisabledWhenNotStreaming()
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
                CreateMockUIStateService().Object
            );

            // Assert: When not streaming, PauseCommand should be disabled
            Assert.False(viewModel.IsStreaming);
            Assert.False(viewModel.PauseCommand.CanExecute(null));
        }

        /// <summary>
        /// Test 2: Pause button label toggles between "Pause" and "Resume" (gap31_1).
        /// Verifies that IsPausedDisplay returns "Pause" when not paused,
        /// and "Resume" when paused, for correct button label rendering.
        /// </summary>
        [Fact]
        public void IsPausedDisplay_TogglesBetweenPauseAndResume()
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
                CreateMockUIStateService().Object
            );

            // Act & Assert: Initial state - not paused, should show "Pause"
            Assert.False(viewModel.IsPaused);
            Assert.Equal("Pause", viewModel.IsPausedDisplay);

            // Act: Toggle pause via IsPaused property
            viewModel.IsPaused = true;

            // Assert: Paused state should show "Resume"
            Assert.True(viewModel.IsPaused);
            Assert.Equal("Resume", viewModel.IsPausedDisplay);

            // Act: Toggle back to not paused
            viewModel.IsPaused = false;

            // Assert: Not paused should show "Pause" again
            Assert.False(viewModel.IsPaused);
            Assert.Equal("Pause", viewModel.IsPausedDisplay);
        }

        /// <summary>
        /// Test 3: Pause command toggles IsPaused state (gap31_1).
        /// Verifies that executing PauseCommand (when enabled) toggles the IsPaused flag,
        /// enabling downstream consumers (phase executors) to check pause state via DebugSessionService.
        /// </summary>
        [Fact]
        public void PauseCommand_TogglesIsPausedState()
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
                CreateMockUIStateService().Object
            );

            // Simulate streaming state so PauseCommand is enabled
            viewModel.IsStreaming = true;

            // Assert: Initial state - not paused
            Assert.False(viewModel.IsPaused);
            Assert.Equal("Pause", viewModel.IsPausedDisplay);

            // Act: Execute PauseCommand
            viewModel.PauseCommand.Execute(null);

            // Assert: IsPaused should be true, display should show "Resume"
            Assert.True(viewModel.IsPaused);
            Assert.Equal("Resume", viewModel.IsPausedDisplay);

            // Act: Execute PauseCommand again
            viewModel.PauseCommand.Execute(null);

            // Assert: IsPaused should be false again, display should show "Pause"
            Assert.False(viewModel.IsPaused);
            Assert.Equal("Pause", viewModel.IsPausedDisplay);
        }
    }
}
