using System;

namespace ContinueVS.Services.Exceptions
{
    /// <summary>
    /// Exception thrown when configuration file cannot be loaded, parsed, or validated.
    /// </summary>
    public class ConfigLoadException : Exception
    {
        /// <summary>
        /// Gets the path to the configuration file that failed to load.
        /// </summary>
        public string? ConfigPath { get; }

        /// <summary>
        /// Initializes a new instance of the ConfigLoadException class.
        /// </summary>
        public ConfigLoadException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConfigLoadException class with a config path.
        /// </summary>
        public ConfigLoadException(string message, string? configPath)
            : base(message)
        {
            ConfigPath = configPath;
        }

        /// <summary>
        /// Initializes a new instance of the ConfigLoadException class with an inner exception.
        /// </summary>
        public ConfigLoadException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConfigLoadException class with config path and inner exception.
        /// </summary>
        public ConfigLoadException(string message, string? configPath, Exception innerException)
            : base(message, innerException)
        {
            ConfigPath = configPath;
        }
    }
}
