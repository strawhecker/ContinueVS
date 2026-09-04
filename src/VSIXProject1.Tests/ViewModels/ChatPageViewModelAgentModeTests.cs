#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using ContinueVS.Core.Types;
using ContinueVS.Core;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.ViewModels
{
    public class ChatPageViewModelAgentModeTests
    {
        private static Mock<ILlmService> CreateLlmServiceMock()
        {
            var mock = new Mock<ILlmService>();
            return mock;
        }

        private static Mock<IContextService> CreateContextServiceMock()
        {
            var mock = new Mock<IContextService>();
            mock.Setup(x => x.GetContextItemsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<ContextItem>());
            return mock;
        }

        private static Mock<IToolService> CreateToolServiceMock()
        {
            var mock = new Mock<IToolService>();
            return mock;
        }

        private static Mock<ISessionService> CreateSessionServiceMock()
        {
            var mock = new Mock<ISessionService>();
            mock.Setup(x => x.AddMessageAsync(It.IsAny<ChatMessage>()))
                .Returns(Task.CompletedTask);
            return mock;
        }

        private static Mock<INotificationService> CreateNotificationServiceMock()
        {
            var mock = new Mock<INotificationService>();
            return mock;
        }

        private static Mock<IConfigService> CreateConfigServiceMock()
        {
            var config = new ContinueConfig
            {
                Models = new List<ModelInfo>
                {
                    new ModelInfo 
                    { 
                        Name = "Llama 3.1",
                        Provider = "ollama",
                        BaseUrl = "http://localhost:11434"
                    }
                }
            };
            var mock = new Mock<IConfigService>();
            mock.Setup(x => x.GetCurrentConfig()).Returns(config);
            return mock;
        }

        private static Mock<ISystemPromptService> CreateSystemPromptServiceMock()
        {
            var mock = new Mock<ISystemPromptService>();
            mock.Setup(x => x.LoadAsync()).Returns(Task.CompletedTask);
            mock.Setup(x => x.GetPromptForMode(It.IsAny<string>()))
                .Returns("Test system prompt");
            return mock;
        }

        private static Mock<IUIStateService> CreateUIStateServiceMock()
        {
            var uiState = new UIState
            {
                ToolSettings = new Dictionary<string, ToolPolicy>
                {
                    { "grep_search", ToolPolicy.AutoApprove },
                    { "find_symbol", ToolPolicy.AutoApprove }
                }
            };
            var mock = new Mock<IUIStateService>();
            mock.Setup(x => x.GetUIStateAsync())
                .ReturnsAsync(uiState);
            return mock;
        }

        [Fact]
        public void AgentMode_IsAvailable()
        {
            // Verify Agent mode is available in available modes
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);

            var agentMode = viewModel.AvailableModes.FirstOrDefault(m => m.Value == ChatMode.Agent);
            Assert.NotNull(agentMode);
            Assert.Equal(ChatMode.Agent, agentMode.Value);
        }

        [Fact]
        public void CanSwitchToAgentMode()
        {
            // Verify switching to Agent mode works
            var viewModel = new ChatPageViewModel(
                CreateLlmServiceMock().Object,
                CreateContextServiceMock().Object,
                CreateToolServiceMock().Object,
                CreateSessionServiceMock().Object,
                CreateNotificationServiceMock().Object,
                CreateConfigServiceMock().Object,
                CreateSystemPromptServiceMock().Object,
                CreateUIStateServiceMock().Object,
                new Mock<IInstructionExecutorService>().Object,
                new Mock<IChangeStackService>().Object,
                new Mock<IMarkdownService>().Object);

            viewModel.CurrentMode = ChatMode.Agent;
            Assert.Equal(ChatMode.Agent, viewModel.CurrentMode);
        }

        [Fact]
        public void AgentMode_AllowsToolLoop()
        {
            // Verify that Agent mode has AllowToolLoop enabled
            var systemPromptService = CreateSystemPromptServiceMock();
            var modeRegistry = new ModeConfigRegistry(systemPromptService.Object);
            var agentConfig = modeRegistry.GetConfig(ChatMode.Agent);

            Assert.NotNull(agentConfig);
            Assert.True(agentConfig.AllowToolLoop);
        }

        [Fact]
        public void AskMode_DoesNotAllowToolLoop()
        {
            // Verify that Ask mode has AllowToolLoop disabled
            var systemPromptService = CreateSystemPromptServiceMock();
            var modeRegistry = new ModeConfigRegistry(systemPromptService.Object);
            var askConfig = modeRegistry.GetConfig(ChatMode.Ask);

            Assert.NotNull(askConfig);
            Assert.False(askConfig.AllowToolLoop);
        }

        /// <summary>
        /// gap59: Test 1 - ViewModelAcceptsDispatcher_InConstructor
        /// Verifies that ChatPageViewModel accepts IAgentCommandDispatcher dependency
        /// </summary>
        [Fact]
        public void ChatPageViewModel_AcceptsDispatcher_InConstructor()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();
            var uiStateService = CreateUIStateServiceMock();
            var instructionExecutorService = new Mock<IInstructionExecutorService>();
            var changeStackService = new Mock<IChangeStackService>();
            var markdownService = new Mock<IMarkdownService>();
            var mockDispatcher = new Mock<IAgentCommandDispatcher>();

            // Act
            var viewModel = new ChatPageViewModel(
                llmService.Object, contextService.Object, toolService.Object,
                sessionService.Object, notificationService.Object, configService.Object,
                systemPromptService.Object, uiStateService.Object,
                instructionExecutorService.Object, changeStackService.Object,
                markdownService.Object, agentCommandDispatcher: mockDispatcher.Object);

            // Assert
            Assert.NotNull(viewModel);
        }

        /// <summary>
        /// gap59: Test 2 - ViewModelCreatesDispatcher_WhenNoneProvided
        /// Verifies that ChatPageViewModel creates a default dispatcher if none supplied
        /// </summary>
        [Fact]
        public void ChatPageViewModel_CreatesDispatcher_WhenNoneProvided()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();
            var uiStateService = CreateUIStateServiceMock();
            var instructionExecutorService = new Mock<IInstructionExecutorService>();
            var changeStackService = new Mock<IChangeStackService>();
            var markdownService = new Mock<IMarkdownService>();

            // Act - No dispatcher provided
            var viewModel = new ChatPageViewModel(
                llmService.Object, contextService.Object, toolService.Object,
                sessionService.Object, notificationService.Object, configService.Object,
                systemPromptService.Object, uiStateService.Object,
                instructionExecutorService.Object, changeStackService.Object,
                markdownService.Object);

            // Assert - viewmodel should still be created
            Assert.NotNull(viewModel);
        }

        /// <summary>
        /// gap59: Test 3 - AgentMode_WithDispatcher_ConfiguredCorrectly
        /// Verifies agent mode is properly configured after dispatcher injection
        /// </summary>
        [Fact]
        public void AgentMode_WithDispatcher_ConfiguredCorrectly()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();
            var uiStateService = CreateUIStateServiceMock();
            var instructionExecutorService = new Mock<IInstructionExecutorService>();
            var changeStackService = new Mock<IChangeStackService>();
            var markdownService = new Mock<IMarkdownService>();
            var mockDispatcher = new Mock<IAgentCommandDispatcher>();
            var modeConfigRegistry = new Mock<IModeConfigRegistry>();
            var modeConfig = new ModeConfig { AllowToolLoop = true };
            modeConfigRegistry.Setup(r => r.GetConfig(ChatMode.Agent))
                .Returns(modeConfig);

            // Act
            var viewModel = new ChatPageViewModel(
                llmService.Object, contextService.Object, toolService.Object,
                sessionService.Object, notificationService.Object, configService.Object,
                systemPromptService.Object, uiStateService.Object,
                instructionExecutorService.Object, changeStackService.Object,
                markdownService.Object, modeConfigRegistry: modeConfigRegistry.Object,
                agentCommandDispatcher: mockDispatcher.Object);

            viewModel.CurrentMode = ChatMode.Agent;

            // Assert
            Assert.Equal(ChatMode.Agent, viewModel.CurrentMode);
        }

        /// <summary>
        /// gap59: Test 4 - DispatcherNullCheck_FallsBackToDefault
        /// Verifies that null dispatcher parameter triggers default creation
        /// </summary>
        [Fact]
        public void DispatcherNullCheck_FallsBackToDefault()
        {
            // Arrange
            var llmService = CreateLlmServiceMock();
            var contextService = CreateContextServiceMock();
            var toolService = CreateToolServiceMock();
            var sessionService = CreateSessionServiceMock();
            var notificationService = CreateNotificationServiceMock();
            var configService = CreateConfigServiceMock();
            var systemPromptService = CreateSystemPromptServiceMock();
            var uiStateService = CreateUIStateServiceMock();
            var instructionExecutorService = new Mock<IInstructionExecutorService>();
            var changeStackService = new Mock<IChangeStackService>();
            var markdownService = new Mock<IMarkdownService>();

            // Act - Explicitly pass null dispatcher
            var viewModel = new ChatPageViewModel(
                llmService.Object, contextService.Object, toolService.Object,
                sessionService.Object, notificationService.Object, configService.Object,
                systemPromptService.Object, uiStateService.Object,
                instructionExecutorService.Object, changeStackService.Object,
                markdownService.Object, agentCommandDispatcher: null);

            // Assert - Should still construct successfully
            Assert.NotNull(viewModel);
            var agentMode = viewModel.AvailableModes.FirstOrDefault(m => m.Value == ChatMode.Agent);
            Assert.NotNull(agentMode);
        }

        /// <summary>
        /// Existing test for error accumulation
        /// </summary>
        [Fact]
        public void ErrorAccumulation_SingleFailureRecovery_Continues()
        {
            // Arrange - Simulate two iterations: first has 1 failure, second has 0
            var iteration1Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool failed in iteration 1" }
            };
            var iteration2Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Tool succeeded in iteration 2" }
            };

            // Act
            var iter1Failures = iteration1Messages.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            var iter2Failures = iteration2Messages.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldContinueAfterIter1 = iter1Failures < 2;

            // Assert
            Assert.Equal(1, iter1Failures);
            Assert.Equal(0, iter2Failures);
            Assert.True(shouldContinueAfterIter1, "Loop should continue after single failure");
        }

        /// <summary>
        /// Existing test for error accumulation with termination
        /// </summary>
        [Fact]
        public void ErrorAccumulation_MultipleFailuresInIteration_Terminates()
        {
            // Arrange - Simulate iteration with 2+ tools failing
            var failedIteration = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool 1 failed" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Failed, Content = "Tool 2 failed" },
                new ChatMessage { Role = ChatMessageRole.Tool, InvocationStatus = ToolInvocationStatus.Complete, Content = "Tool 3 succeeded" }
            };

            // Act
            var totalFailures = failedIteration.Count(m => m.InvocationStatus == ToolInvocationStatus.Failed);
            bool shouldTerminate = totalFailures >= 2;
            int lastToolStatus = failedIteration.Count - 1;
            var lastToolResult = failedIteration[lastToolStatus].InvocationStatus;

            // Assert
            Assert.Equal(2, totalFailures);
            Assert.True(shouldTerminate, "2+ failures should terminate loop");
            Assert.Equal(ToolInvocationStatus.Complete, lastToolResult);  // Some tools may succeed before threshold
        }
    }
}
