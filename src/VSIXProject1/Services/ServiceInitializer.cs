using System;
using System.Threading.Tasks;
using ContinueVS.Services.Implementations;
using ContinueVS.Services.Interfaces;

namespace ContinueVS.Services
{
    /// <summary>
    /// Service initialization bootstrap for ContinueVS.
    /// Orchestrates asynchronous initialization of all services in dependency order.
    /// </summary>
    /// <remarks>
    /// CRITICAL SEQUENCING CONSTRAINT (Steps 34–36–37):
    /// 
    /// ServiceInitializer.InitializeAsync() MUST be called after ServiceBootstrapper.ConfigureServices() (step 33)
    /// and BEFORE the first message is dispatched to any handler (before message pump activation, before tool window creation).
    /// 
    /// Handlers depend on IConfigService via dependency injection (step 34 factory pattern).
    /// If service initialization is delayed or deferred, handlers will receive uninitialized config state,
    /// leading to runtime failures or undefined behavior in downstream tool/message processing.
    /// 
    /// Recommended call site: ContinueVSPackage.InitializeAsync() immediately after ServiceBootstrapper.ConfigureServices()
    /// (after line 174), before any handler is registered or message dispatch starts.
    /// 
    /// Call sequence:
    /// 1. ServiceBootstrapper.ConfigureServices() → IServiceProvider returned, stored in ContinueVSPackage.ServiceProvider
    /// 2. ServiceInitializer.InitializeAsync(ContinueVSPackage.ServiceProvider) → services initialized
    /// 3. Message dispatcher and handlers activated
    /// 
    /// If IConfigService initialization fails, the error is thrown (halts plugin startup) to prevent
    /// handlers from running with uninitialized config. Other service failures log errors but may not block.
    /// </remarks>
    public static class ServiceInitializer
    {
        /// <summary>
        /// Initializes all services asynchronously, starting with IConfigService and ISystemPromptService.
        /// </summary>
        /// <param name="serviceProvider">
        /// The DI service provider from step 33 (ContinueVSPackage.ServiceProvider).
        /// If null, initialization is skipped with a warning.
        /// </param>
        /// <returns>A task representing the asynchronous initialization operation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if IConfigService initialization fails (critical service).
        /// </exception>
        public static async Task InitializeAsync(IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null)
            {
                _ = LoggerService.Current.WriteDebugAsync("[ServiceInitializer] Warning: called with null serviceProvider; skipping initialization.");
                return;
            }

            IBridgeLogger? logger = null;
            try
            {
                logger = serviceProvider.GetService(typeof(IBridgeLogger)) as IBridgeLogger;
                await (logger?.WriteDebugAsync("[ServiceInitializer] Starting service initialization...") ?? Task.CompletedTask);

                // Initialize ISystemPromptService (loads prompts from config file)
                var systemPromptService = serviceProvider.GetService(typeof(ISystemPromptService)) as ISystemPromptService;
                if (systemPromptService != null)
                {
                    try
                    {
                        await (logger?.WriteDebugAsync("[ServiceInitializer] Initializing ISystemPromptService...") ?? Task.CompletedTask);
                        await systemPromptService.EnsureConfigFileExistsAsync();
                        await systemPromptService.LoadAsync();
                        await (logger?.WriteDebugAsync("[ServiceInitializer] ✓ ISystemPromptService initialized successfully.") ?? Task.CompletedTask);
                    }
                    catch (Exception ex)
                    {
                        await (logger?.WriteWarningAsync($"[ServiceInitializer] Warning: ISystemPromptService initialization failed; using defaults: {ex.Message}") ?? Task.CompletedTask);
                    }
                }
                else
                {
                    await (logger?.WriteWarningAsync("[ServiceInitializer] Warning: ISystemPromptService not resolved from serviceProvider; skipping initialization.") ?? Task.CompletedTask);
                }

                // Initialize IConfigService first (highest priority, no dependencies)
                var configService = serviceProvider.GetService(typeof(IConfigService)) as IConfigService;
                if (configService != null)
                {
                    try
                    {
                        await (logger?.WriteDebugAsync("[ServiceInitializer] Initializing IConfigService...") ?? Task.CompletedTask);
                        await configService.InitializeAsync();
                        await (logger?.WriteDebugAsync("[ServiceInitializer] ✓ IConfigService initialized successfully.") ?? Task.CompletedTask);
                    }
                    catch (Exception ex)
                    {
                        await (logger?.WriteErrorAsync($"[ServiceInitializer] ✗ IConfigService.InitializeAsync() failed: {ex.Message}", ex) ?? Task.CompletedTask);
                        throw new InvalidOperationException("Failed to initialize IConfigService. Handlers cannot operate with uninitialized config.", ex);
                    }
                }
                else
                {
                    await (logger?.WriteWarningAsync("[ServiceInitializer] Warning: IConfigService not resolved from serviceProvider; skipping initialization.") ?? Task.CompletedTask);
                }

                await (logger?.WriteDebugAsync("[ServiceInitializer] Service initialization complete.") ?? Task.CompletedTask);
            }
            catch (Exception ex)
            {
                await (logger?.WriteErrorAsync($"[ServiceInitializer] ✗ Fatal error during service initialization: {ex.Message}", ex) ?? Task.CompletedTask);
                throw;
            }
        }
    }
}
