using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services.Interfaces;
using ContinueVS.Services.Interfaces.Parsers;
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
            services.AddSingleton<ISystemPromptService, SystemPromptService>();
            services.AddSingleton<IMarkdownService, MarkdownService>();

            // Register HTTP client singleton for MessengerService
            // Set Timeout to Infinite for streaming operations (Ollama responses may take time)
            // Individual message timeouts are handled at the message level if needed
            services.AddSingleton<HttpClient>(sp => new HttpClient { Timeout = TimeSpan.FromMilliseconds(-1) });

            // Register core services as singletons (application lifetime)
            services.AddSingleton<IIdeService, VsIdeService>();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IContextDumpService>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                return new ContextDumpService(configService);
            });
            services.AddSingleton<IModelDiscoveryService>(sp =>
            {
                var httpClient = sp.GetRequiredService<HttpClient>();
                return (IModelDiscoveryService)new ModelDiscoveryService(httpClient);
            });
            services.AddSingleton<IMessengerService>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                var httpClient = sp.GetRequiredService<HttpClient>();
                var contextDumpService = sp.GetRequiredService<IContextDumpService>();
                return new MessengerService(configService, httpClient, null, contextDumpService);
            });
            services.AddSingleton<ITokenCountingService, SimpleTokenCounterService>();
            services.AddSingleton<ILlmService, LlmService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<IModeService, ModeService>();
            services.AddSingleton<IToolService>(sp =>
            {
                var ideService = sp.GetRequiredService<IIdeService>();
                var configService = sp.GetRequiredService<IConfigService>();
                var sessionService = sp.GetRequiredService<ISessionService>();
                var mcpService = sp.GetRequiredService<IMcpService>();
                return new ToolService(ideService, configService, sessionService, mcpService);
            });
            services.AddSingleton<IIndexingService, IndexingService>();
            services.AddSingleton<IContextService, ContextService>();
            services.AddSingleton<IMcpService, McpService>();

            // Stack trace parsing service and parsers (gap29_1)
            services.AddSingleton<IDotNetFrameworkParser, DotNetFrameworkStackTraceParser>();
            services.AddSingleton<IDotNetCoreParser, DotNetCoreStackTraceParser>();
            services.AddSingleton<ICppNativeParser, CppNativeStackTraceParser>();
            services.AddSingleton<IJavaScriptParser, JavaScriptStackTraceParser>();
            services.AddSingleton<IPythonParser, PythonStackTraceParser>();
            services.AddSingleton<IFormatDetector>(sp =>
            {
                var fwParser = sp.GetRequiredService<IDotNetFrameworkParser>();
                var coreParser = sp.GetRequiredService<IDotNetCoreParser>();
                var cppParser = sp.GetRequiredService<ICppNativeParser>();
                var jsParser = sp.GetRequiredService<IJavaScriptParser>();
                var pyParser = sp.GetRequiredService<IPythonParser>();
                return new StackTraceFormatDetector(fwParser, coreParser, cppParser, jsParser, pyParser);
            });
            services.AddSingleton<IStackTraceService>(sp =>
            {
                var detector = sp.GetRequiredService<IFormatDetector>();
                return new StackTraceService(detector);
            });

            // Test failure analysis service (gap29_2)
            services.AddSingleton<ITestFailureService>(sp =>
            {
                var ideService = sp.GetRequiredService<IIdeService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new TestFailureService(ideService, logger);
            });

            // Register debugger service for runtime event inspection (gap29_3)
            services.AddSingleton<ITimeoutHelper, TimeoutHelper>();
            services.AddSingleton<IDebuggerService>(sp =>
            {
                var dteProvider = sp.GetRequiredService<IDteProvider>();
                var timeoutHelper = sp.GetRequiredService<ITimeoutHelper>();
                return new DebuggerService(dteProvider, timeoutHelper);
            });

            services.AddSingleton<INotificationService>(sp =>
            {
                // Use a lazy factory for MainViewModel to avoid circular dependency
                // MainViewModel is transient, so we'll get it when needed, not at singleton creation time
                Func<MainViewModel?> getMainViewModel = () => sp.GetService<MainViewModel>();
                return new WpfNotificationService(null, null, getMainViewModel);
            });

            // Breadcrumb trail recording service (gap29_4)
            services.AddSingleton<IBreadcrumbService>(sp =>
            {
                var notificationService = sp.GetRequiredService<INotificationService>();
                return new BreadcrumbService(notificationService);
            });

            services.AddSingleton<IUIStateService>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                return new UIStateService(configService);
            });
            services.AddSingleton<ILocalStorageService>(new LocalStorageService());

            // MainViewModel must be a singleton so every consumer gets the same instance
            // (the UI DataContext, the notification service overlay, etc.)
            services.AddSingleton<MainViewModel>(sp =>
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
