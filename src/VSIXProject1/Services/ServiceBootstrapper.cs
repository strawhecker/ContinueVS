using System;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using ContinueVS.UI.Navigation;

namespace ContinueVS.Services
{
    /// <summary>
    /// Dependency injection bootstrapper for the Continue VS extension.
    /// Centralizes service registration and provides the service provider to the application.
    /// </summary>
    public static class ServiceBootstrapper
    {
        /// <summary>
        /// Configures all application services and returns the service provider.
        /// This method must be called once during application startup to initialize the DI container.
        /// </summary>
        /// <returns>
        /// An IServiceProvider instance containing all registered singletons and factory delegates.
        /// </returns>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Register UI/Navigation services
            services.AddSingleton<IPageNavigator, PageNavigator>();
            services.AddSingleton<IThemeService, ThemeService>();

            // Register core services as singletons (application lifetime)
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<ILlmService, LlmService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IToolService, ToolService>();
            services.AddSingleton<IIndexingService, IndexingService>();
            services.AddSingleton<IContextService, ContextService>();
            services.AddSingleton<IMcpService, McpService>();

            // Build and return
            return services.BuildServiceProvider();
        }
    }
}
