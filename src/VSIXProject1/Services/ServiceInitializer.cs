using System;
using System.Threading.Tasks;
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
                System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Warning: called with null serviceProvider; skipping initialization.");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Starting service initialization...");

                // Initialize ISystemPromptService (loads prompts from config file)
                var systemPromptService = serviceProvider.GetService(typeof(ISystemPromptService)) as ISystemPromptService;
                if (systemPromptService != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Initializing ISystemPromptService...");
                        await systemPromptService.EnsureConfigFileExistsAsync();
                        await systemPromptService.LoadAsync();
                        System.Diagnostics.Debug.WriteLine("[ServiceInitializer] ✓ ISystemPromptService initialized successfully.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ServiceInitializer] Warning: ISystemPromptService initialization failed; using defaults: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Warning: ISystemPromptService not resolved from serviceProvider; skipping initialization.");
                }

                // Initialize IConfigService first (highest priority, no dependencies)
                var configService = serviceProvider.GetService(typeof(IConfigService)) as IConfigService;
                if (configService != null)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Initializing IConfigService...");
                        await configService.InitializeAsync();
                        System.Diagnostics.Debug.WriteLine("[ServiceInitializer] ✓ IConfigService initialized successfully.");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ServiceInitializer] ✗ IConfigService.InitializeAsync() failed: {ex.Message}");
                        throw new InvalidOperationException("Failed to initialize IConfigService. Handlers cannot operate with uninitialized config.", ex);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Warning: IConfigService not resolved from serviceProvider; skipping initialization.");
                }

                System.Diagnostics.Debug.WriteLine("[ServiceInitializer] Service initialization complete.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServiceInitializer] ✗ Fatal error during service initialization: {ex.Message}");
                throw;
            }
        }
    }
}
