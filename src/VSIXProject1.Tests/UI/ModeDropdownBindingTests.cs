#nullable enable

using System.Collections.Generic;
using System.ComponentModel;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Models;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Tests for the mode dropdown data binding (gap27_1).
    /// Verifies AvailableModes collection, SelectedMode binding, and CurrentMode sync.
    /// </summary>
    public class ModeDropdownBindingTests
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

            return new ChatPageViewModel(
                llmMock.Object,
                contextMock.Object,
                toolMock.Object,
                sessionMock.Object,
                notifMock.Object,
                configMock.Object,
                promptMock.Object,
                uiStateMock.Object,
                null,
                null);
        }

        [Fact]
        public void AvailableModes_LoadsWith4Options()
        {
            // Arrange / Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.AvailableModes);
            Assert.Equal(4, vm.AvailableModes.Count);
        }

        [Fact]
        public void AvailableModes_ContainsAskAgentPlanDebug()
        {
            // Arrange / Act
            var vm = CreateViewModel();

            // Assert
            Assert.Contains(vm.AvailableModes, m => m.Value == ChatMode.Ask);
            Assert.Contains(vm.AvailableModes, m => m.Value == ChatMode.Agent);
            Assert.Contains(vm.AvailableModes, m => m.Value == ChatMode.Plan);
            Assert.Contains(vm.AvailableModes, m => m.Value == ChatMode.Debug);
        }

        [Fact]
        public void SelectedMode_DefaultsToAsk()
        {
            // Arrange / Act
            var vm = CreateViewModel();

            // Assert
            Assert.NotNull(vm.SelectedMode);
            Assert.Equal(ChatMode.Ask, vm.SelectedMode!.Value);
        }

        [Fact]
        public void SelectedMode_WhenSet_UpdatesCurrentMode()
        {
            // Arrange
            var vm = CreateViewModel();
            var agentOption = vm.AvailableModes[1]; // Agent

            // Act
            vm.SelectedMode = agentOption;

            // Assert
            Assert.Equal(ChatMode.Agent, vm.CurrentMode);
        }

        [Fact]
        public void CurrentMode_WhenSet_UpdatesSelectedMode()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Plan;

            // Assert
            Assert.NotNull(vm.SelectedMode);
            Assert.Equal(ChatMode.Plan, vm.SelectedMode!.Value);
        }

        [Fact]
        public void SelectedMode_WhenChanged_RaisesPropertyChanged()
        {
            // Arrange
            var vm = CreateViewModel();
            var raised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.SelectedMode))
                    raised = true;
            };

            // Act
            vm.SelectedMode = vm.AvailableModes[2]; // Plan

            // Assert
            Assert.True(raised);
        }

        [Fact]
        public void DebugMode_IsSelectable()
        {
            // Arrange
            var vm = CreateViewModel();
            var debugOption = vm.AvailableModes.Single(m => m.Value == ChatMode.Debug);

            // Act
            vm.SelectedMode = debugOption;

            // Assert
            Assert.Equal(ChatMode.Debug, vm.CurrentMode);
            Assert.NotNull(vm.SelectedMode);
            Assert.Equal(ChatMode.Debug, vm.SelectedMode.Value);
        }
    }
}
