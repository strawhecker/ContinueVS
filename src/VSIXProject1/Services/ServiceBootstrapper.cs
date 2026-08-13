using System;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using ContinueVS.ViewModels;
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

            // Register logging infrastructure first
            services.AddSingleton<IBridgeLogger>(sp => new BridgeLogger(null));

            // Register UI/Navigation services
            services.AddSingleton<IPageNavigator, PageNavigator>();

            // Register core services as singletons (application lifetime)
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<ILlmService, LlmService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IToolService, ToolService>();
            services.AddSingleton<IIndexingService, IndexingService>();
            services.AddSingleton<IContextService, ContextService>();
            services.AddSingleton<IMcpService, McpService>();
            services.AddSingleton<IIdeService, VsIdeService>();
            services.AddSingleton<IMessengerService, MessengerService>();
            services.AddSingleton<INotificationService, WpfNotificationService>();

            // Register ViewModel factories (Step 60 / 61)
            services.AddSingleton<Func<MainViewModel>>(sp => () => new MainViewModel(
                sp.GetRequiredService<ISessionService>(),
                sp.GetRequiredService<IMessengerService>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IPageNavigator>()));

            services.AddSingleton<Func<ChatPageViewModel>>(sp => () => new ChatPageViewModel(
                sp.GetRequiredService<ILlmService>(),
                sp.GetRequiredService<IContextService>(),
                sp.GetRequiredService<IToolService>(),
                sp.GetRequiredService<ISessionService>(),
                sp.GetRequiredService<INotificationService>()));

            services.AddSingleton<Func<ConfigPageViewModel>>(sp => () => new ConfigPageViewModel(
                sp.GetRequiredService<IConfigService>(),
                sp.GetRequiredService<IIndexingService>()));

            // Build provider for accessing services in factory methods
            var provider = services.BuildServiceProvider();

            return provider;
        }
    }
}
