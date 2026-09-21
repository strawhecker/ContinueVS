#nullable enable

using System.Collections.Generic;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    /// <summary>
    /// Tests confirming that all five chat modes produce correct ModeConfig policy values
    /// through the unified pipeline (gap44_3). Regression companion to
    /// ChatPageViewModelAgentModeTests.
    /// </summary>
    public class ChatPageViewModelModeTests
    {
        // -- Shared mock helpers (mirrors ChatPageViewModelAgentModeTests pattern) --------

        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            return new Mock<ILlmService>();
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(System.Threading.Tasks.Task.CompletedTask);
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo { Name = "Llama 3.1", Provider = "ollama", BaseUrl = "http://localhost:11434" }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns<string>(mode => $"prompt-{mode}");
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var uiState = new UIState { ToolSettings = new Dictionary<string, ToolPolicy>() };
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync()).ReturnsAsync(uiState);
            return mock;
        }

        private ChatPageViewModel CreateViewModel(IModeConfigRegistry? registry = null)
        {
            var systemPromptService = CreateSystemPromptServiceMock();
            return new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                new Mock<IToolService>().Object,
                CreateSessionServiceMock().Object,
                new Mock<INotificationService>().Object,
                CreateConfigServiceMock().Object,
                systemPromptService.Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object,
                null,  // llmQuestionService
                null,  // modeService
                null,  // workflowService
                null,  // ideService
                registry ?? new ModeConfigRegistry(systemPromptService.Object));  // modeConfigRegistry
        }

        // -- Mode-switching tests --------------------------------------------------------

        [Fact]
        public void CurrentMode_CanBeSetToAsk()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Ask;

            // Assert
            Assert.Equal(ChatMode.Ask, vm.CurrentMode);
        }

        [Fact]
        public void CurrentMode_CanBeSetToAgent()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.Equal(ChatMode.Agent, vm.CurrentMode);
        }

        [Fact]
        public void CurrentMode_CanBeSetToPlan()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Plan;

            // Assert
            Assert.Equal(ChatMode.Plan, vm.CurrentMode);
        }

        [Fact]
        public void CurrentMode_CanBeSetToDebug()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Debug;

            // Assert
            Assert.Equal(ChatMode.Debug, vm.CurrentMode);
        }

        [Fact]
        public void CurrentMode_CanBeSetToReason()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act
            vm.CurrentMode = ChatMode.Reason;

            // Assert
            Assert.Equal(ChatMode.Reason, vm.CurrentMode);
        }

        // -- ModeConfig policy correctness via registry injection ----------------------

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   false)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        public void ModeConfig_WriteTools_CorrectPerMode(
            ChatMode mode, bool expectedWriteTools)
        {
            // Arrange — use the same registry the ViewModel will use
            var systemPromptService = CreateSystemPromptServiceMock();
            IModeConfigRegistry registry = new ModeConfigRegistry(systemPromptService.Object);

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expectedWriteTools, cfg.AllowWriteTools);
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  false)]
        [InlineData(ChatMode.Plan,   false)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        public void ModeConfig_RequiresDebuggerContext_TrueOnlyForDebug(
            ChatMode mode, bool expected)
        {
            // Arrange
            var systemPromptService = CreateSystemPromptServiceMock();
            IModeConfigRegistry registry = new ModeConfigRegistry(systemPromptService.Object);

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expected, cfg.RequiresDebuggerContext);
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   true)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        public void ModeConfig_ExportsPlanFile_TrueForAgentPlanDebug(
            ChatMode mode, bool expected)
        {
            // Arrange
            var systemPromptService = CreateSystemPromptServiceMock();
            IModeConfigRegistry registry = new ModeConfigRegistry(systemPromptService.Object);

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expected, cfg.ExportsPlanFile);
        }

        // -- No-surprises continuation policy across mode transitions -----------------

        [Fact]
        public void Policy_ResetsToInteractiveWhenEnteringAgentFromNonToolMode()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Ask;
            vm.SelectedPolicy = ContinuationPolicy.Deferred;

            // Act - move into a tool mode from a non-tool mode
            vm.CurrentMode = ChatMode.Agent;

            // Assert - the user is "using" Interactive here now, so reset to it
            Assert.Equal(ContinuationPolicy.Interactive, vm.SelectedPolicy);
        }

        [Fact]
        public void Policy_PreservedWhenSwitchingBetweenAgentAndDebug()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Agent;
            vm.SelectedPolicy = ContinuationPolicy.Deferred;

            // Act - Agent -> Debug should preserve the user's choice
            vm.CurrentMode = ChatMode.Debug;

            // Assert
            Assert.Equal(ContinuationPolicy.Deferred, vm.SelectedPolicy);

            // And Debug -> Agent should also preserve
            vm.CurrentMode = ChatMode.Agent;
            Assert.Equal(ContinuationPolicy.Deferred, vm.SelectedPolicy);
        }

        [Fact]
        public void Policy_PreservedWhenSwitchingAgentToDebugAndBackPreservesChoice()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Agent;
            vm.SelectedPolicy = ContinuationPolicy.Auto;

            // Act
            vm.CurrentMode = ChatMode.Debug;
            vm.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.Equal(ContinuationPolicy.Auto, vm.SelectedPolicy);
        }

        [Fact]
        public void Policy_NotChangedWhenLeavingToolModeToNonToolMode()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Agent;
            vm.SelectedPolicy = ContinuationPolicy.Auto;

            // Act - leaving to a non-tool mode just disables the dropdown
            vm.CurrentMode = ChatMode.Ask;

            // Assert - value is preserved (just disabled), so re-entering later
            // still has no surprises when switching tool<->tool.
            Assert.Equal(ContinuationPolicy.Auto, vm.SelectedPolicy);
        }

        [Fact]
        public void Policy_ResetsForEachEntryToToolModeFromNonToolMode()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Ask;
            vm.SelectedPolicy = ContinuationPolicy.Deferred;

            // Act - enter Agent (resets to Interactive), leave, and re-enter Debug (resets again)
            vm.CurrentMode = ChatMode.Agent;
            Assert.Equal(ContinuationPolicy.Interactive, vm.SelectedPolicy);
            vm.CurrentMode = ChatMode.Ask;
            vm.CurrentMode = ChatMode.Debug;

            // Assert - entering Debug from Ask also resets to Interactive
            Assert.Equal(ContinuationPolicy.Interactive, vm.SelectedPolicy);
        }

        [Fact]
        public void Policy_ResetsToInteractiveWhenEnteringAgentFromPlan()
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = ChatMode.Plan; // non-tool mode
            vm.SelectedPolicy = ContinuationPolicy.Deferred;

            // Act
            vm.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.Equal(ContinuationPolicy.Interactive, vm.SelectedPolicy);
        }

        // -- Continuation policy dropdown visibility/enablement regression -------------

        [Fact]
        public void IsPolicyVisible_IsAlwaysTrue_SoDropdownNeverFlickers()
        {
            // Arrange
            var vm = CreateViewModel();

            // Act/Assert: the dropdown must never disappear (regardless of mode)
            foreach (var mode in new[]
                     {
                         ChatMode.Ask, ChatMode.Agent, ChatMode.Plan,
                         ChatMode.Debug, ChatMode.Reason
                     })
            {
                vm.CurrentMode = mode;
                Assert.True(vm.IsPolicyVisible, $"IsPolicyVisible should be true in {mode}");
            }
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   false)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        public void IsPolicyEnabled_TrueOnlyForAgentAndDebug(ChatMode mode, bool expected)
        {
            // Arrange
            var vm = CreateViewModel();
            vm.CurrentMode = mode;

            // Assert
            Assert.Equal(expected, vm.IsPolicyEnabled);
        }

        [Theory]
        [InlineData(ChatMode.Ask,    false)]
        [InlineData(ChatMode.Agent,  true)]
        [InlineData(ChatMode.Plan,   false)]
        [InlineData(ChatMode.Debug,  true)]
        [InlineData(ChatMode.Reason, false)]
        public void ModeConfig_AllowPhaseExecution_TrueForAgentAndDebug(
            ChatMode mode, bool expected)
        {
            // Arrange
            var systemPromptService = CreateSystemPromptServiceMock();
            IModeConfigRegistry registry = new ModeConfigRegistry(systemPromptService.Object);

            // Act
            var cfg = registry.GetConfig(mode);

            // Assert
            Assert.Equal(expected, cfg.AllowPhaseExecution);
        }
    }
}
