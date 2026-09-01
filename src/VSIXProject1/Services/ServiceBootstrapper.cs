using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Shell;
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

            // BP:sv-di-build — breakpoint at BuildServiceProvider() below confirms all registrations succeeded
            System.Diagnostics.Debug.WriteLine("[sv-di] ConfigureServices START");

            // --- IBridgeLogger: must be first — many factory lambdas below require it ---
            System.Diagnostics.Debug.WriteLine("[sv-di] registering IBridgeLogger (FileLogger)");
            services.AddSingleton<IBridgeLogger, FileLogger>();
            System.Diagnostics.Debug.WriteLine("[sv-di] ✓ IBridgeLogger registered (logs to ~/.continueVS/logs/)");

            // --- IDteProvider: required by DebuggerService factory ---
            System.Diagnostics.Debug.WriteLine("[sv-di] registering IDteProvider (DteProvider)");
            services.AddSingleton<IDteProvider>(sp =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                System.Diagnostics.Debug.WriteLine($"[sv-di] IDteProvider: DTE resolved={dte != null}");
                if (dte == null)
                {
                    System.Diagnostics.Debug.WriteLine("[sv-di] ⚠ DTE is null — IDteProvider will throw on use; VS may not be fully loaded yet");
                    throw new InvalidOperationException("[sv-di] Cannot resolve EnvDTE.DTE from Package.GetGlobalService. Ensure ServiceBootstrapper is called after VS package initialization.");
                }
                return new DteProvider(dte);
            });
            System.Diagnostics.Debug.WriteLine("[sv-di] ✓ IDteProvider registered");

            // Register UI/Navigation services
            services.AddSingleton<IPageNavigator, PageNavigator>();
            services.AddSingleton<IThemeService, ThemeService>();

            services.AddSingleton<ISystemPromptService>(sp =>
                new SystemPromptService(sp.GetRequiredService<IWorkspaceStatsService>()));
            services.AddSingleton<IMarkdownService, MarkdownService>();
            // gap44_2: ModeConfigRegistry is the single source of truth for mode policy
            services.AddSingleton<IModeConfigRegistry>(sp =>
                new ModeConfigRegistry(sp.GetRequiredService<ISystemPromptService>()));

            // Register HTTP client singleton for MessengerService
            // Set Timeout to Infinite for streaming operations (Ollama responses may take time)
            // Individual message timeouts are handled at the message level if needed
            services.AddSingleton<HttpClient>(sp => new HttpClient { Timeout = TimeSpan.FromMilliseconds(-1) });

            // Register core services as singletons (application lifetime)
            services.AddSingleton<IIdeService, VsIdeService>();
            services.AddSingleton<IConfigService, ConfigService>();
            // gap43_2: Persist Plan mode output to ~/.continueVS/plans/
            services.AddSingleton<IPlanOutputService, PlanOutputService>();
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

            // Error fingerprinting and deduplication service (gap29_5)
            services.AddSingleton<IErrorFingerprintService, ErrorFingerprintService>();

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

            // Workspace stats service: collects runtime fields for system prompt context injection (gap38)
            // MUST be after IDebuggerService registration (on which it depends)
            System.Diagnostics.Debug.WriteLine("[sv-di] registering IWorkspaceStatsService (WorkspaceStatsService)");
            services.AddSingleton<IWorkspaceStatsService>(sp => new WorkspaceStatsService(
                sp.GetRequiredService<IIdeService>(),
                sp.GetRequiredService<IDebuggerService>(),
                sp.GetRequiredService<IConfigService>()));
            System.Diagnostics.Debug.WriteLine("[sv-di] ✓ IWorkspaceStatsService registered");

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

            // Distributed tracing service for trace header parsing and async context flow (gap29_6)
            services.AddSingleton<IDistributedTracingService, DistributedTracingService>();

            // Error repository for persistent error storage and querying (gap29_7)
            services.AddSingleton<IErrorRepository>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new ErrorRepository(configService, logger);
            });

            // Change stack service for per-change transaction tracking (gap29_8_2)
            services.AddSingleton<IChangeStackService, ChangeStackService>();

            // Instrumentation strategy generation and application services (gap29_8_5)
            services.AddSingleton<IDebugStrategyGeneratorService>(sp =>
            {
                var llmService = sp.GetRequiredService<ILlmService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new DebugStrategyGeneratorService(llmService, logger);
            });
            services.AddSingleton<IInstrumentationService>(sp =>
            {
                    var logger = sp.GetRequiredService<IBridgeLogger>();
                    return new InstrumentationService(logger);
                });

                // Failure analyzer service for refinement attempts (gap29_8_6)
                    services.AddSingleton<IFailureAnalyzerService>(sp =>
                    {
                        var llmService = sp.GetRequiredService<ILlmService>();
                        var logger = sp.GetRequiredService<IBridgeLogger>();
                        return new FailureAnalyzerService(llmService, logger);
                    });

                // Change executor service for change-level retry loop with LLM refinement (gap29_8_7)
                services.AddSingleton<IChangeExecutor>(sp =>
                {
                    var changeStackService = sp.GetRequiredService<IChangeStackService>();
                    var failureAnalyzer = sp.GetRequiredService<IFailureAnalyzerService>();
                    var configService = sp.GetRequiredService<IConfigService>();
                    var logger = sp.GetRequiredService<IBridgeLogger>();
                    return new ChangeExecutionStack(changeStackService, failureAnalyzer, configService, logger);
                });

                    // Phase executor factory for debug session orchestration (gap29_8_4)
            services.AddSingleton<PhaseExecutorFactory>(sp =>
            {
                var changeStackService = sp.GetRequiredService<IChangeStackService>();
                var strategyGenerator = sp.GetRequiredService<IDebugStrategyGeneratorService>();
                var instrumentationService = sp.GetRequiredService<IInstrumentationService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                var promptService = sp.GetRequiredService<IInteractivePromptService>();
                return new PhaseExecutorFactory(changeStackService, strategyGenerator, instrumentationService, logger, promptService);
            });

            // Interactive prompt service for user decision prompts in Debug mode (gap29_8_8)
            services.AddSingleton<IInteractivePromptService>(sp =>
            {
                var notificationService = sp.GetRequiredService<INotificationService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new InteractivePromptService(notificationService, logger);
            });

            // LLM question service for detecting and auto-answering LLM questions (gap29_8_9)
            services.AddSingleton<ILlmQuestionService>(sp =>
            {
                var promptService = sp.GetRequiredService<IInteractivePromptService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new LlmQuestionService(promptService, logger);
            });

            // Test plan execution repository for persistence and history tracking (gap29_8_10)
            services.AddSingleton<ITestPlanExecutionRepository>(sp =>
            {
                var configService = sp.GetRequiredService<IConfigService>();
                return new TestPlanExecutionRepository(configService);
            });

            // Error-driven instrumentation service for reactive exception-triggered suggestions (gap29_8_11)
            services.AddSingleton<IErrorDrivenInstrumentationService>(sp =>
            {
                var errorRepository = sp.GetRequiredService<IErrorRepository>();
                var strategyGenerator = sp.GetRequiredService<IDebugStrategyGeneratorService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new ErrorDrivenInstrumentationService(errorRepository, strategyGenerator, logger);
            });

            // Instruction processor for converting debug instructions to test plans (gap29_8_4)
            services.AddSingleton<IInstructionProcessorService>(sp =>
            {
                var llmService = sp.GetRequiredService<ILlmService>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new InstructionProcessorService(llmService, logger);
            });

            // Instruction executor service for orchestrating phase execution (gap29_8_4, gap45_2)
            services.AddSingleton<IInstructionExecutorService>(sp =>
            {
                var instructionProcessor = sp.GetRequiredService<IInstructionProcessorService>();
                var changeStackService = sp.GetRequiredService<IChangeStackService>();
                var executorFactory = sp.GetRequiredService<PhaseExecutorFactory>();
                var logger = sp.GetRequiredService<IBridgeLogger>();
                return new InstructionExecutorService(instructionProcessor, changeStackService, executorFactory, logger);
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
            // BP:sv-di-build — if execution reaches here, all registrations succeeded
            System.Diagnostics.Debug.WriteLine("[sv-di] All registrations complete — calling BuildServiceProvider()");
            var provider = services.BuildServiceProvider();
            System.Diagnostics.Debug.WriteLine("[sv-di] ✓ ServiceProvider built successfully");
            return provider;
        }
    }
}
