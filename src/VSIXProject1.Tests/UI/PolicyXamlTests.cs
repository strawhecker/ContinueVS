#nullable enable

using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Tests for the policy dropdown XAML control (gap27_13).
    /// Verifies visibility binding and rendering in different chat modes.
    /// </summary>
    public class PolicyXamlTests
    {
        private static ChatPageViewModel CreateViewModel()
        {
            var llmMock = new Mock<ILlmService>();

            var contextMock = new Mock<IContextService>();
            contextMock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());

            var toolMock = new Mock<IToolService>();

            var sessionMock = new Mock<ISessionService>();
            sessionMock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();

            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Test Model", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            var configMock = new Mock<IConfigService>();
            configMock.Setup(x => x.GetCurrentConfig()).Returns(config);

            var promptMock = new Mock<ISystemPromptService>();
            promptMock.Setup(x => x.LoadAsync()).Returns(System.Threading.Tasks.Task.CompletedTask);
            promptMock.Setup(x => x.GetPromptForMode(It.IsAny<string>())).Returns("Test prompt");

            var uiState = new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() };
            var uiStateMock = new Mock<IUIStateService>();
            uiStateMock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(uiState);

            var debugSessionMock = new Mock<IDebugSessionService>();

            return new ChatPageViewModel(
                llmMock.Object,
                contextMock.Object,
                toolMock.Object,
                sessionMock.Object,
                notifMock.Object,
                configMock.Object,
                promptMock.Object,
                uiStateMock.Object,
                debugSessionMock.Object,
                null,
                null);
        }

        [Fact]
        public void PolicyDropdown_Visible_In_AgentMode()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.True(vm.IsPolicyVisible);
        }

        [Fact]
        public void PolicyDropdown_Visible_In_PlanMode()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Plan;

            // Assert
            Assert.True(vm.IsPolicyVisible);
        }

        [Fact]
        public void PolicyDropdown_Hidden_In_AskMode()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Ask;

            // Assert
            Assert.False(vm.IsPolicyVisible);
        }
    }
}
