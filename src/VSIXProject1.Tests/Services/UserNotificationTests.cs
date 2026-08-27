#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// Unit tests for gap23_4_4: User Notification & Warnings.
    /// Tests banner visibility thresholds (80% warning, 100% error) and auto-dismiss behavior.
    /// </summary>
    public class UserNotificationTests
    {
        private readonly Mock<ILlmService> _mockLlmService;
        private readonly Mock<IContextService> _mockContextService;
        private readonly Mock<IToolService> _mockToolService;
        private readonly Mock<ISessionService> _mockSessionService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<IConfigService> _mockConfigService;
        private readonly Mock<ISystemPromptService> _mockSystemPromptService;
        private readonly Mock<IUIStateService> _mockUIStateService;

        public UserNotificationTests()
        {
            _mockLlmService = new Mock<ILlmService>();
            _mockContextService = new Mock<IContextService>();
            _mockToolService = new Mock<IToolService>();
            _mockSessionService = new Mock<ISessionService>();
            _mockNotificationService = new Mock<INotificationService>();
            _mockConfigService = new Mock<IConfigService>();
            _mockSystemPromptService = new Mock<ISystemPromptService>();
            _mockUIStateService = new Mock<IUIStateService>();
        }

        /// <summary>
        /// Test 1: Show warning banner at 80% of tool call limit (gap23_4_4).
        /// Verifies that ShowWarningBanner is true when tool calls reach 80% of max.
        /// </summary>
        [Fact]
        public void ShowWarningBanner_At80Percent()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ToolCallsExecuted = 80  // 80 of 100 = 80%
            };

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };

            _mockSessionService.Setup(s => s.GetCurrentSession()).Returns(session);
            _mockConfigService.Setup(c => c.GetCurrentConfig()).Returns(config);
            _mockSystemPromptService.Setup(s => s.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            _mockUIStateService.Setup(u => u.GetUIStateAsync()).ReturnsAsync(new UIState());

            var viewModel = new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );

            // Act - Trigger the check via reflection to set banner state
            var method = typeof(ChatPageViewModel).GetMethod("CheckToolCallLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(viewModel, null);

            // Assert
            Assert.True(viewModel.ShowWarningBanner, "Warning banner should be shown at 80% threshold");
            Assert.False(viewModel.ShowErrorBanner, "Error banner should not be shown at 80%");
        }

        /// <summary>
        /// Test 2: Show error banner at 100% of tool call limit (gap23_4_4).
        /// Verifies that ShowErrorBanner is true and send is disabled when at 100%.
        /// </summary>
        [Fact]
        public void ShowErrorBanner_At100Percent()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ToolCallsExecuted = 100  // 100 of 100 = 100%
            };

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };

            _mockSessionService.Setup(s => s.GetCurrentSession()).Returns(session);
            _mockConfigService.Setup(c => c.GetCurrentConfig()).Returns(config);
            _mockSystemPromptService.Setup(s => s.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            _mockUIStateService.Setup(u => u.GetUIStateAsync()).ReturnsAsync(new UIState());

            var viewModel = new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );

            // Act - Trigger the check
            var method = typeof(ChatPageViewModel).GetMethod("CheckToolCallLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(viewModel, null);

            // Assert
            Assert.True(viewModel.ShowErrorBanner, "Error banner should be shown at 100% threshold");
            Assert.False(viewModel.ShowWarningBanner, "Warning banner should not be shown at 100%");
            // Verify that SendMessageCommand is disabled (CanSendMessage returns false)
            Assert.False(viewModel.SendMessageCommand.CanExecute(null), "Send message should be disabled when error banner is shown");
        }

        /// <summary>
        /// Test 3: Dismiss warning banner on user click (gap23_4_4).
        /// Verifies that warning banner closes when user clicks the dismiss button.
        /// </summary>
        [Fact]
        public void DismissWarningOnClick()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ToolCallsExecuted = 80
            };

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };

            _mockSessionService.Setup(s => s.GetCurrentSession()).Returns(session);
            _mockConfigService.Setup(c => c.GetCurrentConfig()).Returns(config);
            _mockSystemPromptService.Setup(s => s.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            _mockUIStateService.Setup(u => u.GetUIStateAsync()).ReturnsAsync(new UIState());

            var viewModel = new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );

            // First show the warning
            var checkMethod = typeof(ChatPageViewModel).GetMethod("CheckToolCallLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkMethod?.Invoke(viewModel, null);
            Assert.True(viewModel.ShowWarningBanner, "Warning banner should be visible initially");

            // Act - Call dismiss command
            viewModel.DismissWarningBannerCommand();

            // Assert
            Assert.False(viewModel.ShowWarningBanner, "Warning banner should be dismissed after clicking X");
        }

        /// <summary>
        /// Test 4: Do not show warning below 80% threshold (gap23_4_4).
        /// Verifies that no banners are shown when tool calls are below 80%.
        /// </summary>
        [Fact]
        public void NoNotification_Below80Percent()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ToolCallsExecuted = 50  // 50 of 100 = 50%
            };

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };

            _mockSessionService.Setup(s => s.GetCurrentSession()).Returns(session);
            _mockConfigService.Setup(c => c.GetCurrentConfig()).Returns(config);
            _mockSystemPromptService.Setup(s => s.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            _mockUIStateService.Setup(u => u.GetUIStateAsync()).ReturnsAsync(new UIState());

            var viewModel = new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );

            // Act
            var method = typeof(ChatPageViewModel).GetMethod("CheckToolCallLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(viewModel, null);

            // Assert
            Assert.False(viewModel.ShowWarningBanner, "Warning banner should not be shown below 80%");
            Assert.False(viewModel.ShowErrorBanner, "Error banner should not be shown below 80%");
        }

        /// <summary>
        /// Test 5: Verify send button is enabled below 100% (gap23_4_4).
        /// Ensures SendMessageCommand remains enabled when not at limit.
        /// </summary>
        [Fact]
        public void SendButtonEnabled_Below100Percent()
        {
            // Arrange
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ToolCallsExecuted = 80
            };

            var config = new ContinueConfig
            {
                CustomSettings = new Dictionary<string, object>
                {
                    { UserSettings.Agent_MaxToolCallsPerSession, 100 }
                }
            };

            _mockSessionService.Setup(s => s.GetCurrentSession()).Returns(session);
            _mockConfigService.Setup(c => c.GetCurrentConfig()).Returns(config);
            _mockSystemPromptService.Setup(s => s.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            _mockUIStateService.Setup(u => u.GetUIStateAsync()).ReturnsAsync(new UIState());

            var viewModel = new ChatPageViewModel(
                _mockLlmService.Object,
                _mockContextService.Object,
                _mockToolService.Object,
                _mockSessionService.Object,
                _mockNotificationService.Object,
                _mockConfigService.Object,
                _mockSystemPromptService.Object,
                _mockUIStateService.Object,
                new Mock<IDebugSessionService>().Object,
                null,
                null
            );
            viewModel.InputText = "test message";

            // Act - Check threshold
            var method = typeof(ChatPageViewModel).GetMethod("CheckToolCallLimit",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(viewModel, null);

            // Assert - At 80% warning but not error, send should still be enabled
            Assert.True(viewModel.SendMessageCommand.CanExecute(null), "Send should be enabled at 80% warning threshold");
        }
    }
}
