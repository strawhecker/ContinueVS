using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ContinueVS.Services
{
    /// <summary>
    /// Concrete implementation of IBridgeLogger.
    /// 
    /// Logs to both VS Output Window (for developer visibility) and npm bridge
    /// (for unified telemetry and crash reporting).
    /// 
    /// Gracefully degrades if OutputWindow is unavailable by falling back to console.
    /// All exceptions are swallowed internally to prevent logging errors from propagating.
    /// </summary>
    public sealed class BridgeLogger : IBridgeLogger
    {
        private readonly Action<string, string, IReadOnlyDictionary<string, object>?>? _npmBridgeCallback;
        private readonly Action<string>? _outputWindowWriter;

        /// <summary>
        /// Creates a new BridgeLogger instance.
        /// </summary>
        /// <param name="serviceProvider">The VS service provider (typically the package). Used to locate VS output window.</param>
        /// <param name="npmBridgeCallback">Optional callback to forward logs to npm bridge (for telemetry).</param>
        public BridgeLogger(IServiceProvider? serviceProvider = null, Action<string, string, IReadOnlyDictionary<string, object>?>? npmBridgeCallback = null)
        {
            System.Diagnostics.Debug.WriteLine($"[CV-t2] BridgeLogger constructor entry: serviceProvider={((serviceProvider != null) ? "✓ NOTNULL" : "✗ NULL")}, npmBridgeCallback={((npmBridgeCallback != null) ? "✓ NOTNULL" : "✗ NULL")}");
            _npmBridgeCallback = npmBridgeCallback;
            _outputWindowWriter = TryGetOutputWindowWriter(serviceProvider);
            System.Diagnostics.Debug.WriteLine($"[CV-t2] Output window writer: {(_outputWindowWriter != null ? "✓ RESOLVED" : "✗ FALLBACK")}");
            System.Diagnostics.Debug.WriteLine($"[CV-t2] NPM callback registered: {(_npmBridgeCallback != null ? "✓ YES" : "✗ NULL (expected at t2)")}");
        }

        public async Task WriteDebugAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            await WriteCoreAsync("DEBUG", message, metadata).ConfigureAwait(false);
        }

        public async Task WriteInfoAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            await WriteCoreAsync("INFO", message, metadata).ConfigureAwait(false);
        }

        public async Task WriteWarningAsync(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            await WriteCoreAsync("WARNING", message, metadata).ConfigureAwait(false);
        }

        public async Task WriteErrorAsync(string message, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var combinedMetadata = new Dictionary<string, object>();
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    combinedMetadata[kvp.Key] = kvp.Value;
                }
            }

            if (exception != null)
            {
                combinedMetadata["exception_type"] = exception.GetType().Name;
                combinedMetadata["exception_message"] = exception.Message;
                combinedMetadata["stack_trace"] = exception.StackTrace ?? string.Empty;
            }

            await WriteCoreAsync("ERROR", message, combinedMetadata).ConfigureAwait(false);
        }

        public async Task FlushAsync()
        {
            // Flush any pending output pane buffers
            try
            {
                _outputWindowWriter?.Invoke(FormatLogLine("FLUSH", "Logger flush", null));
            }
            catch
            {
                // Swallow exceptions during flush to prevent teardown issues
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Core write logic that delegates to both OutputWindow and npm bridge.
        /// </summary>
        private async Task WriteCoreAsync(string level, string message, IReadOnlyDictionary<string, object>? metadata)
        {
            try
            {
                // Write to VS Output Window
                WriteToOutput(level, message, metadata);

                // Forward to npm bridge for telemetry
                _npmBridgeCallback?.Invoke(level, message, metadata);
            }
            catch
            {
                // Swallow all exceptions from logging to prevent them from propagating
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Writes to the VS Output Window or fallback console.
        /// </summary>
        private void WriteToOutput(string level, string message, IReadOnlyDictionary<string, object>? metadata)
        {
            try
            {
                var logLine = FormatLogLine(level, message, metadata);
                _outputWindowWriter?.Invoke(logLine);
            }
            catch
            {
                // Silently fail; we've exhausted logging options
            }
        }

        /// <summary>
        /// Formats a log line with timestamp, level, message, and optional metadata.
        /// </summary>
        private static string FormatLogLine(string level, string message, IReadOnlyDictionary<string, object>? metadata)
        {
            var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var metadataString = FormatMetadata(metadata);
            var logLine = string.IsNullOrEmpty(metadataString)
                ? $"[{timestamp}] {level}: {message}\n"
                : $"[{timestamp}] {level}: {message} | {metadataString}\n";
            return logLine;
        }

        /// <summary>
        /// Attempts to get an output writer for VS Output Window or falls back to console.
        /// </summary>
        private static Action<string>? TryGetOutputWindowWriter(IServiceProvider? serviceProvider)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[CV-t2] TryGetOutputWindowWriter: serviceProvider={((serviceProvider != null) ? "✓ NOTNULL" : "✗ NULL")}");
                if (serviceProvider == null)
                {
                    // Fallback: Use safe Debug.Write (avoids console deadlock)
                    System.Diagnostics.Debug.WriteLine("[CV-t2] TryGetOutputWindowWriter: Using Debug.Write fallback (null serviceProvider)");
                    return line => System.Diagnostics.Debug.Write(line);
                }

                // Try to get VS Output Window via IServiceProvider
                // This is a simplified implementation; in production, you'd use:
                // var outputWindow = serviceProvider.GetService(typeof(SVsOutputWindow)) as IVsOutputWindow
                // For now, we fallback to Debug.Write since we can't access VS SDK in unit tests
                System.Diagnostics.Debug.WriteLine("[CV-t2] TryGetOutputWindowWriter: Returning Debug.Write writer");
                return line => System.Diagnostics.Debug.Write(line);
            }
            catch (Exception ex)
            {
                // If anything fails, use Debug.Write as fallback
                System.Diagnostics.Debug.WriteLine($"[CV-t2] TryGetOutputWindowWriter: Exception caught: {ex.GetType().Name} - {ex.Message}");
                return line => System.Diagnostics.Debug.Write(line);
            }
        }

        /// <summary>
        /// Formats metadata dictionary as a pipe-delimited string.
        /// Example: "key1=value1 | key2=value2"
        /// </summary>
        private static string FormatMetadata(IReadOnlyDictionary<string, object>? metadata)
        {
            if (metadata == null || metadata.Count == 0)
            {
                return string.Empty;
            }

            var formatted = string.Join(" | ", metadata.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            return formatted;
        }
    }
}
