using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Logger abstraction layer for bridge operations.
    /// 
    /// Unifies VS Output Window logging with npm bridge telemetry.
    /// All Write operations are async to avoid blocking the VS main thread.
    /// 
    /// Log levels follow the standard pyramid: Debug > Info > Warning > Error.
    /// Structured logging is supported via optional metadata key-value pairs.
    /// 
    /// The logger is designed to fail silently (swallow exceptions internally)
    /// to prevent logging errors from disrupting bridge operations.
    /// </summary>
    public interface IBridgeLogger
    {
        /// <summary>
        /// Writes a debug-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging (e.g., correlationId, component).</param>
        /// <returns>A task representing the async write operation.</returns>
        void WriteDebug(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes an info-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        void WriteInfo(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes a warning-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        void WriteWarning(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes an error-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log as part of the error context.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        void WriteError(string message, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null);
    }
}
