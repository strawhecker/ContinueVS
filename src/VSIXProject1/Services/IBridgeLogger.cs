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
        Task WriteDebugAsync(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes an info-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        Task WriteInfoAsync(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes a warning-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        Task WriteWarningAsync(string message, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Writes an error-level log message.
        /// </summary>
        /// <param name="message">The log message.</param>
        /// <param name="exception">Optional exception to log as part of the error context.</param>
        /// <param name="metadata">Optional key-value pairs for structured logging.</param>
        /// <returns>A task representing the async write operation.</returns>
        Task WriteErrorAsync(string message, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null);

        /// <summary>
        /// Flushes any pending log messages.
        /// Called during shutdown to ensure messages are not lost.
        /// </summary>
        /// <returns>A task representing the flush operation.</returns>
        Task FlushAsync();
    }
}
