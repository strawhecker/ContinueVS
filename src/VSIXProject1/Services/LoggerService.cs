using System;
using ContinueVS.Services.Implementations;

namespace ContinueVS.Services
{
    /// <summary>
    /// Static accessor for the global FileLogger instance.
    /// Enables early startup code to log before DI is configured.
    /// </summary>
    public static class LoggerService
    {
        private static readonly Lazy<FileLogger> _instance = new Lazy<FileLogger>(() => new FileLogger());

        /// <summary>
        /// Gets the global FileLogger instance (lazily instantiated on first access).
        /// The background writer thread starts immediately when the instance is created.
        /// Safe to call from any thread; all enqueues are lock-free.
        /// </summary>
        public static FileLogger Current => _instance.Value;
    }
}
