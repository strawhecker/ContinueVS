using System;
using Microsoft.Extensions.DependencyInjection;
using ContinueVS.Services;

namespace ContinueVS.ViewModels
{
    /// <summary>
    /// Static facade for providing ViewModel instances to XAML views via data binding.
    /// 
    /// This locator bridges the gap between XAML static binding and the DI container,
    /// enabling patterns like {Binding Source={StaticResource Locator}, Path=MainViewModel}.
    /// 
    /// ViewModel instances are created on-demand via factory delegates registered in
    /// ServiceBootstrapper.ConfigureServices() (see Step 61).
    /// </summary>
    /// <remarks>
    /// Usage in XAML:
    /// 1. Register in App.xaml as a static resource:
    ///    &lt;Application.Resources&gt;
    ///        &lt;local:ViewModelLocator x:Key="Locator" /&gt;
    ///    &lt;/Application.Resources&gt;
    /// 
    /// 2. In a View's code-behind (OnStartup or Window.Loaded):
    ///    ViewModelLocator.ServiceProvider = serviceProvider;
    /// 
    /// 3. In XAML, bind:
    ///    &lt;Window DataContext="{Binding Source={StaticResource Locator}, Path=MainViewModel}" /&gt;
    /// </remarks>
    public class ViewModelLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets or sets the global IServiceProvider instance.
        /// Must be set once during application startup before ViewModels are accessed.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if value is null.</exception>
        public static IServiceProvider? ServiceProvider
        {
            get => _serviceProvider;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "ServiceProvider cannot be null.");
                _serviceProvider = value;
            }
        }

        /// <summary>
        /// Gets or creates the MainViewModel instance.
        /// Creates a new instance on each access via the factory delegate.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if ServiceProvider is not yet initialized.</exception>
        public MainViewModel MainViewModel
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException(
                        "ServiceProvider must be initialized via ViewModelLocator.ServiceProvider = ... before accessing ViewModels.");

                try
                {
                    var factory = _serviceProvider.GetRequiredService<Func<MainViewModel>>();
                    return factory();
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        "MainViewModel factory is not registered in ServiceBootstrapper. " +
                        "Ensure ServiceBootstrapper.ConfigureServices() registers Func<MainViewModel> (Step 61).",
                        ex);
                }
            }
        }

        /// <summary>
        /// Gets or creates the ChatPageViewModel instance.
        /// Creates a new instance on each access via the factory delegate.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if ServiceProvider is not yet initialized.</exception>
        public ChatPageViewModel ChatPageViewModel
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException(
                        "ServiceProvider must be initialized via ViewModelLocator.ServiceProvider = ... before accessing ViewModels.");

                try
                {
                    var factory = _serviceProvider.GetRequiredService<Func<ChatPageViewModel>>();
                    return factory();
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        "ChatPageViewModel factory is not registered in ServiceBootstrapper. " +
                        "Ensure ServiceBootstrapper.ConfigureServices() registers Func<ChatPageViewModel> (Step 61).",
                        ex);
                }
            }
        }

        /// <summary>
        /// Gets or creates the ConfigPageViewModel instance.
        /// Creates a new instance on each access via the factory delegate.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if ServiceProvider is not yet initialized.</exception>
        public ConfigPageViewModel ConfigPageViewModel
        {
            get
            {
                if (_serviceProvider == null)
                    throw new InvalidOperationException(
                        "ServiceProvider must be initialized via ViewModelLocator.ServiceProvider = ... before accessing ViewModels.");

                try
                {
                    var factory = _serviceProvider.GetRequiredService<Func<ConfigPageViewModel>>();
                    return factory();
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException(
                        "ConfigPageViewModel factory is not registered in ServiceBootstrapper. " +
                        "Ensure ServiceBootstrapper.ConfigureServices() registers Func<ConfigPageViewModel> (Step 61).",
                        ex);
                }
            }
        }
    }
}
