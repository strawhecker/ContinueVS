using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Services;

namespace ContinueVS.UI
{
    /// <summary>
    /// Extension methods for bridge teardown (b15).
    /// </summary>
    internal static class WebviewInjectorTeardownExtensions
    {
        private static readonly string TeardownScript = @"
(function() {
   'use strict';

   // [b15-TEARDOWN-SCRIPT] Cleanup and sanitize bridge state
   if (window.continueVS) {
     const bridge = window.continueVS;

     // Clear message queue
     if (bridge._messageQueue) {
       bridge._messageQueue = [];
     }

     // Clear handlers map
     if (bridge._handlers) {
       bridge._handlers.clear();
     }

     // Log cleanup
     if (typeof console !== 'undefined' && console.log) {
       console.log('[continueVS.teardown] Bridge cleanup completed');
     }
   }

   // Set window.continueVS to undefined
   const previousType = typeof window.continueVS;
   window.continueVS = undefined;

   // Return verification result
   return JSON.stringify({
     success: true,
     previousType: previousType,
     currentType: typeof window.continueVS,
     timestamp: Date.now()
   });
})();
";

        /// <summary>
        /// Injects the teardown script to clean up the continueVS bridge.
        /// </summary>
        public static async Task<string?> InjectTeardownScriptAsync(
            CoreWebView2 coreWebView2,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _ = LoggerService.Current.WriteDebugAsync("[b15-TEARDOWN-START] Bridge teardown starting");

                if (coreWebView2 == null)
                {
                    _ = LoggerService.Current.WriteDebugAsync("[b15-TEARDOWN-ERROR] CoreWebView2 is null");
                    return null;
                }

                _ = LoggerService.Current.WriteDebugAsync("[b15-SCRIPT-INJECT] Executing teardown script");
                var stopwatch = Stopwatch.StartNew();

                string result = await coreWebView2.ExecuteScriptAsync(TeardownScript);
                stopwatch.Stop();

                _ = LoggerService.Current.WriteDebugAsync($"[b15-SCRIPT-RESULT] Teardown script executed in {stopwatch.ElapsedMilliseconds}ms");
                _ = LoggerService.Current.WriteDebugAsync($"[b15-UNDEFINED-VERIFY] Result: {result}");

                return result;
            }
            catch (OperationCanceledException)
            {
                _ = LoggerService.Current.WriteDebugAsync("[b15-TEARDOWN-ERROR] Teardown was cancelled");
                return null;
            }
            catch (Exception ex)
            {
                _ = LoggerService.Current.WriteErrorAsync($"[b15-TEARDOWN-ERROR] Exception: {ex.GetType().Name} - {ex.Message}", ex);
                return null;
            }
            finally
            {
                _ = LoggerService.Current.WriteDebugAsync("[b15-COMPLETION] Bridge teardown operation completed");
            }
        }
    }
}
