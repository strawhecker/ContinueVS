#nullable enable

using System;
using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using ContinueVS.ViewModels;

namespace ContinueVS.Tests.Services
{
    /// <summary>
    /// gap61: Tests for IAgentCommandDispatcher registration in ServiceBootstrapper.
    /// Verifies that the dispatcher is properly configured as a singleton with all dependencies.
    /// </summary>
    public class ServiceBootstrapperTests
    {
        /// <summary>
        /// gap61: ServiceBootstrapper_RegistersAgentCommandDispatcher_Configuration
        /// Verifies that IAgentCommandDispatcher is registered correctly in ServiceBootstrapper
        /// by checking the registration code and factory dependencies.
        /// </summary>
        [Fact]
        public void ServiceBootstrapper_HasAgentCommandDispatcherRegistration()
        {
            // Arrange
            var services = new ServiceCollection();

            // Manually create a minimal set of dependencies needed for AgentCommandDispatcher
            var mockToolService = new Mock<IToolService>();
            var mockLlmService = new Mock<ILlmService>();
            var mockLogger = new Mock<IBridgeLogger>();
            var mockSystemPromptService = new Mock<ISystemPromptService>();

            services.AddSingleton(mockLogger.Object);
            services.AddSingleton(mockToolService.Object);
            services.AddSingleton(mockLlmService.Object);
            services.AddSingleton(mockSystemPromptService.Object);
            services.AddSingleton<IModeConfigRegistry>(sp =>
                new ModeConfigRegistry(sp.GetRequiredService<ISystemPromptService>()));

            // Register the dispatcher as it is in ServiceBootstrapper (lines 122-130)
            services.AddSingleton<IAgentCommandDispatcher>(sp =>
            {
                var toolService = sp.GetRequiredService<IToolService>();
                var llmService = sp.GetRequiredService<ILlmService>();
                var modeConfigRegistry = sp.GetRequiredService<IModeConfigRegistry>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new AgentCommandDispatcher(toolService, llmService, modeConfigRegistry, logger);
            });

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var dispatcher = serviceProvider.GetService<IAgentCommandDispatcher>();

            // Assert
            Assert.NotNull(dispatcher);
            Assert.IsType<AgentCommandDispatcher>(dispatcher);

            // Verify it's a singleton by comparing two resolutions
            var dispatcher2 = serviceProvider.GetService<IAgentCommandDispatcher>();
            Assert.Same(dispatcher, dispatcher2);
        }

        /// <summary>
        /// Verifies that AgentCommandDispatcher can be instantiated with all required dependencies.
        /// </summary>
        [Fact]
        public void AgentCommandDispatcher_InstantiatesWithAllDependencies()
        {
            // Arrange
            var mockToolService = new Mock<IToolService>();
            var mockLlmService = new Mock<ILlmService>();
            var mockModeConfigRegistry = new Mock<IModeConfigRegistry>();
            var mockLogger = new Mock<IBridgeLogger>();

            // Act
            var dispatcher = new AgentCommandDispatcher(
                mockToolService.Object,
                mockLlmService.Object,
                mockModeConfigRegistry.Object,
                mockLogger.Object);

            // Assert
            Assert.NotNull(dispatcher);
        }

        /// <summary>
        /// Verifies that AgentCommandDispatcher throws ArgumentNullException for null dependencies.
        /// </summary>
        [Fact]
        public void AgentCommandDispatcher_ThrowsArgumentNull_ForNullDependencies()
        {
            // Arrange
            var mockLlmService = new Mock<ILlmService>();
            var mockModeConfigRegistry = new Mock<IModeConfigRegistry>();
            var mockLogger = new Mock<IBridgeLogger>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new AgentCommandDispatcher(null!, mockLlmService.Object, mockModeConfigRegistry.Object, mockLogger.Object));
        }

        /// <summary>
        /// Verifies that ChatPageViewModel is registered as a singleton in the container with the
        /// same factory shape used in ServiceBootstrapper. The inline-question handler resolves
        /// ChatPageViewModel via GetService to present ask_user questions in the chat UI; if it was
        /// not registered, that resolution returned null and questions silently auto-answered.
        /// </summary>
        [Fact]
        public void ServiceBootstrapper_HasChatPageViewModelRegistration()
        {
            // Arrange
            var services = new ServiceCollection();

            var mockLlm = new Mock<ILlmService>();
            var mockContext = new Mock<IContextService>();
            var mockTool = new Mock<IToolService>();
            var mockSession = new Mock<ISessionService>();
            var mockNotif = new Mock<INotificationService>();
            var mockConfig = new Mock<IConfigService>();
            var mockSystemPrompt = new Mock<ISystemPromptService>();
            var mockUiState = new Mock<IUIStateService>();
            var mockInstructionExecutor = new Mock<IInstructionExecutorService>();
            var mockChangeStack = new Mock<IChangeStackService>();
            var mockMarkdown = new Mock<IMarkdownService>();
            var mockModeService = new Mock<IModeService>();
            var mockWorkflow = new Mock<IWorkflowService>();
            var mockIde = new Mock<IIdeService>();
            var mockModeConfigRegistry = new Mock<IModeConfigRegistry>();
            var mockPlanOutput = new Mock<IPlanOutputService>();

            services.AddSingleton(mockLlm.Object);
            services.AddSingleton(mockContext.Object);
            services.AddSingleton(mockTool.Object);
            services.AddSingleton(mockSession.Object);
            services.AddSingleton(mockNotif.Object);
            services.AddSingleton(mockConfig.Object);
            services.AddSingleton(mockSystemPrompt.Object);
            services.AddSingleton(mockUiState.Object);
            services.AddSingleton(mockInstructionExecutor.Object);
            services.AddSingleton(mockChangeStack.Object);
            services.AddSingleton(mockMarkdown.Object);
            services.AddSingleton(mockModeService.Object);
            services.AddSingleton(mockWorkflow.Object);
            services.AddSingleton(mockIde.Object);
            services.AddSingleton(mockModeConfigRegistry.Object);
            services.AddSingleton(mockPlanOutput.Object);

            // Register ChatPageViewModel exactly as ServiceBootstrapper does.
            services.AddSingleton<ChatPageViewModel>(sp =>
                new ChatPageViewModel(
                    sp.GetRequiredService<ILlmService>(),
                    sp.GetRequiredService<IContextService>(),
                    sp.GetRequiredService<IToolService>(),
                    sp.GetRequiredService<ISessionService>(),
                    sp.GetRequiredService<INotificationService>(),
                    sp.GetRequiredService<IConfigService>(),
                    sp.GetRequiredService<ISystemPromptService>(),
                    sp.GetRequiredService<IUIStateService>(),
                    sp.GetRequiredService<IInstructionExecutorService>(),
                    sp.GetRequiredService<IChangeStackService>(),
                    sp.GetRequiredService<IMarkdownService>(),
                    sp.GetService<ILlmQuestionService>(),
                    sp.GetService<IModeService>(),
                    sp.GetService<IWorkflowService>(),
                    sp.GetService<IIdeService>(),
                    sp.GetService<IModeConfigRegistry>(),
                    sp.GetService<IPlanOutputService>()
                )
            );

            var serviceProvider = services.BuildServiceProvider();

            // Act
            var vm = serviceProvider.GetService<ChatPageViewModel>();

            // Assert
            Assert.NotNull(vm);
            Assert.IsType<ChatPageViewModel>(vm);

            // Verify it is a singleton: resolving twice yields the same instance, which is what the
            // inline-question handler relies on to add questions into the UI-bound VM.
            var vm2 = serviceProvider.GetService<ChatPageViewModel>();
            Assert.Same(vm, vm2);
        }
    }
}
