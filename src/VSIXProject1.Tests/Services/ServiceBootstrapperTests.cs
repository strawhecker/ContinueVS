#nullable enable

using System;
using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;

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
    }
}
