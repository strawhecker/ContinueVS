using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Implementations;
using ContinueVS.UI.Navigation;
using ContinueVS.ViewModels;

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

            // Register HTTP client singleton for MessengerService
            services.AddSingleton<HttpClient>(sp => new HttpClient { Timeout = TimeSpan.FromSeconds(300) });

            // Register core services as singletons (application lifetime)
            services.AddSingleton<IIdeService, VsIdeService>();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IMessengerService>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                var httpClient = sp.GetRequiredService<HttpClient>();
                return new MessengerService(configService, httpClient, null);
            });
            services.AddSingleton<ILlmService, LlmService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IToolService, ToolService>();
            services.AddSingleton<IIndexingService, IndexingService>();
            services.AddSingleton<IContextService, ContextService>();
            services.AddSingleton<IMcpService, McpService>();
            services.AddSingleton<INotificationService>(sp => new WpfNotificationService());

            // Register ViewModels as transient (create new instance each time via factory)
            services.AddTransient<MainViewModel>(sp =>
                new MainViewModel(
                    sp.GetRequiredService<ISessionService>(),
                    sp.GetRequiredService<IMessengerService>(),
                    sp.GetRequiredService<INotificationService>(),
                    sp.GetRequiredService<IConfigService>(),
                    sp.GetRequiredService<IPageNavigator>()
                )
            );
            services.AddTransient<Func<MainViewModel>>(sp => () => sp.GetRequiredService<MainViewModel>());

            // Build and return
            return services.BuildServiceProvider();
        }
    }
}
