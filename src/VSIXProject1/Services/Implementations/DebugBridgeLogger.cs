using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContinueVS.Services.Implementations
{
    // BP:sv-ibridgelogger — breakpoint here confirms IBridgeLogger is resolved and active
    /// <summary>
    /// Routes all IBridgeLogger calls to System.Diagnostics.Debug.WriteLine so they
    /// appear in the VS Output Window (Debug pane) alongside [CV-...] and [TRACE] entries.
    /// Consistent with the established codebase pattern in ExecutionTracer and ContinueVSPackage.
    /// Tag filter: [BL-debug], [BL-info], [BL-warn], [BL-error]
    /// </summary>
    public sealed class DebugBridgeLogger : IBridgeLogger
    {
        public void WriteDebug(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            System.Diagnostics.Debug.WriteLine($"[BL-debug] {message}{FormatMetadata(metadata)}");
        }

        public void WriteInfo(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            System.Diagnostics.Debug.WriteLine($"[BL-info] {message}{FormatMetadata(metadata)}");
        }

        public void WriteWarning(string message, IReadOnlyDictionary<string, object>? metadata = null)
        {
            System.Diagnostics.Debug.WriteLine($"[BL-warn] {message}{FormatMetadata(metadata)}");
        }

        public void WriteError(string message, Exception? exception = null, IReadOnlyDictionary<string, object>? metadata = null)
        {
            System.Diagnostics.Debug.WriteLine($"[BL-error] {message}{FormatMetadata(metadata)}");
            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"[BL-error] Exception: {exception.GetType().FullName}: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"[BL-error] StackTrace: {exception.StackTrace}");
                if (exception.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[BL-error] InnerException: {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
                }
            }
        }

        private static string FormatMetadata(IReadOnlyDictionary<string, object>? metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return string.Empty;

            var parts = new System.Text.StringBuilder(" | ");
            foreach (var kv in metadata)
            {
                parts.Append($"{kv.Key}={kv.Value} ");
            }
            return parts.ToString().TrimEnd();
        }
    }
}
