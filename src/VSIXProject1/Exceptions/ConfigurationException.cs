using System;
using System.Collections.Generic;

namespace ContinueVS.Exceptions
{
    /// <summary>
    /// Exception thrown when bridge configuration is invalid or incomplete.
    /// 
    /// Covers failures including:
    /// - Missing or invalid npm executable path
    /// - Missing or inaccessible working directory
    /// - Invalid bridge version format
    /// - Missing Continue npm package files
    /// - Invalid timeout values
    /// - Null or empty configuration parameters
    /// 
    /// Used by BridgeConfiguration (Step 18) to validate settings
    /// before transport initialization (Steps 19–21).
    /// </summary>
    internal sealed class ConfigurationException : BridgeException
    {
        /// <summary>
        /// Well-known error codes for configuration validation failures.
        /// </summary>
        public static class ErrorCodes
        {
            /// <summary>Bridge version format is invalid.</summary>
            public const string InvalidVersionFormat = "CONFIG_INVALID_VERSION";

            /// <summary>Npm executable path is missing or invalid.</summary>
            public const string InvalidNpmPath = "CONFIG_INVALID_NPM_PATH";

            /// <summary>Working directory is missing or invalid.</summary>
            public const string InvalidWorkingDirectory = "CONFIG_INVALID_WORKING_DIR";

            /// <summary>Continue npm package not found at expected path.</summary>
            public const string PackageNotFound = "CONFIG_PACKAGE_NOT_FOUND";

            /// <summary>Timeout value is invalid (negative, zero, or exceeds maximum).</summary>
            public const string InvalidTimeout = "CONFIG_INVALID_TIMEOUT";

            /// <summary>Required configuration parameter is null or empty.</summary>
            public const string MissingParameter = "CONFIG_MISSING_PARAMETER";

            /// <summary>Npm package version is incompatible with this extension.</summary>
            public const string IncompatibleVersion = "CONFIG_INCOMPATIBLE_VERSION";

            /// <summary>Npm package integrity check failed (checksum mismatch).</summary>
            public const string IntegrityCheckFailed = "CONFIG_INTEGRITY_CHECK_FAILED";
        }

        /// <summary>
        /// Initializes a new instance of ConfigurationException with a message and error code.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code (e.g., InvalidVersionFormat).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ConfigurationException(string message, string errorCode)
            : base(message, errorCode)
        {
        }

        /// <summary>
        /// Initializes a new instance of ConfigurationException with a message, error code, and inner exception.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception (e.g., FileNotFoundException, FormatException).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ConfigurationException(string message, string errorCode, Exception? innerException)
            : base(message, errorCode, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of ConfigurationException with a message, error code, and context dictionary.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="context">Debugging context (e.g., version, npmPath, workingDirectory, expectedPath).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ConfigurationException(string message, string errorCode, Dictionary<string, string>? context)
            : base(message, errorCode, context)
        {
        }

        /// <summary>
        /// Initializes a new instance of ConfigurationException with all parameters.
        /// </summary>
        /// <param name="message">Human-readable error message.</param>
        /// <param name="errorCode">Machine-readable error code.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        /// <param name="context">Debugging context (e.g., version, npmPath, workingDirectory, expectedPath).</param>
        /// <exception cref="ArgumentNullException">Thrown if message or errorCode is null.</exception>
        public ConfigurationException(string message, string errorCode, Exception? innerException, Dictionary<string, string>? context)
            : base(message, errorCode, innerException, context)
        {
        }
    }
}
