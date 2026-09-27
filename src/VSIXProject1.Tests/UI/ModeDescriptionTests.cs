#nullable enable

using System.Collections.Generic;
using System.Linq;
using Xunit;
using Moq;
using ContinueVS.Core;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;
using ContinueVS.ViewModels.Models;

namespace ContinueVS.Tests.UI
{
    /// <summary>
    /// Tests for mode option descriptions (gap27_6).
    /// Verifies that each mode has the correct user-friendly description text.
    /// </summary>
    public class ModeDescriptionTests
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

            var uiStateMock = new Mock<IUIStateService>();

            return new ChatPageViewModel(
                llmMock.Object,
                contextMock.Object,
                toolMock.Object,
                sessionMock.Object,
                notifMock.Object,
                configMock.Object,
                promptMock.Object,
                uiStateMock.Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object
            );
        }

        [Fact]
        public void AskModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var askMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Ask);

            // Assert
            Assert.Equal("Basic Q&A with optional Apply button for code suggestions.", askMode.Description);
        }

        [Fact]
        public void AgentModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var agentMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Agent);

            // Assert
            Assert.Equal("Autonomous tool calling and code editing with user approval.", agentMode.Description);
        }

        [Fact]
        public void PlanModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var planMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Plan);

            // Assert
            Assert.Equal("Read-only plan generation and review.", planMode.Description);
        }

        [Fact]
        public void DebugModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var debugMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Debug);

            // Assert
            Assert.Equal("Instrumentation-driven error diagnosis with interactive refinement.", debugMode.Description);
        }

        [Fact]
        public void DebugModeIcon_Should_Be_Wrench()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var debugMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Debug);

            // Assert
            Assert.Equal("🔧", debugMode.Icon);
        }

        [Fact]
        public void ReasonModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var reasonMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Reason);

            // Assert
            Assert.Equal("Structured chain-of-thought reasoning before answering.", reasonMode.Description);
        }

        [Fact]
        public void ReasonModeIcon_Should_Be_Brain()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var reasonMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Reason);

            // Assert
            Assert.Equal("🧠", reasonMode.Icon);
        }

        // ---- gap97 bare mode ----

        [Fact]
        public void BareModeDescription_Should_Be_Correct()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            var bareMode = vm.AvailableModes.Single(m => m.Value == ChatMode.Bare);

            // Assert
            Assert.Equal("Raw, boundary-free interaction — no system prompt, no command line, read-only tools only.", bareMode.Description);
        }

        [Fact]
        public void BareMode_Should_Be_Last_In_List()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act & Assert
            // gap97: Bare is a retention escape hatch — it must be the LAST entry so the user
            // never needs another application for a raw LLM session, and it must never precede
            // Ask or Agent in the mode list.
            Assert.Equal(ChatMode.Bare, vm.AvailableModes.Last().Value);
            var bareIndex = vm.AvailableModes.ToList().FindIndex(m => m.Value == ChatMode.Bare);
            var askIndex = vm.AvailableModes.ToList().FindIndex(m => m.Value == ChatMode.Ask);
            var agentIndex = vm.AvailableModes.ToList().FindIndex(m => m.Value == ChatMode.Agent);
            Assert.True(bareIndex > askIndex, "Bare must be positioned after Ask");
            Assert.True(bareIndex > agentIndex, "Bare must be positioned after Agent");
        }
    }
}
