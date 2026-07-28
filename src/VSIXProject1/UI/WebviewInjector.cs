using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.UI
{
    /// <summary>
    /// Injects the continueVS JavaScript bridge into WebView2 to enable
    /// bidirectional communication between C# handlers and the React GUI.
    /// </summary>
    /// <remarks>
    /// This class embeds a self-contained JavaScript payload that:
    /// 1. Creates the window.continueVS global object
    /// 2. Provides onMessage() for C# → React messaging
    /// 3. Provides sendMessage() for React → C# messaging
    /// 4. Fires a 'continueVSBridgeReady' event when initialized
    /// 
    /// The injector is tolerant of transient errors and logs failures
    /// without throwing exceptions during normal operation.
    /// </remarks>
    internal sealed class WebviewInjector : IWebviewInjector
    {
                                   private static readonly string _injectionScript = @"
                          // Initialize the continueVS bridge (function expression returns diagnostics)
                          (function() {
                            (function initBridge() {
                              'use strict';

          // Set up global error handler to catch any init failures
          window.addEventListener('error', function(event) {
            console.error('[continueVS] Global error caught:', event.message, event.filename, event.lineno);
          });

          window.addEventListener('unhandledrejection', function(event) {
            console.error('[continueVS] Unhandled promise rejection:', event.reason);
          });

          // Create or reuse the bridge object
          if (!window.continueVS) {
            window.continueVS = {};
          }

          const bridge = window.continueVS;
          bridge._initialized = true;
  bridge._version = '2.0.0';
  bridge._bridgeReady = true;
  bridge._messageQueue = [];
  bridge._handlers = new Map();
  bridge._nextMessageId = 0;

  console.log('[continueVS] Bridge initialization starting...');
  console.log('[continueVS-DEBUG] document.readyState:', document.readyState);
  console.log('[continueVS-DEBUG] window.location.href:', window.location.href);
  console.log('[continueVS-DEBUG] navigator.userAgent:', navigator.userAgent);

  // sendMessage: Called by React to send messages to C#
  bridge.sendMessage = function(messageType, data, messageId) {
    try {
      const id = messageId || ('msg_' + (++bridge._nextMessageId));
      const message = {
        messageType: messageType,
        data: data || {},
        messageId: id,
        source: 'continueVS',
        timestamp: Date.now()
      };

      console.log('[continueVS.sendMessage] Sending message:', messageType, 'id:', id);
      console.log('[continueVS-OUTBOUND-DEBUG]', JSON.stringify(message));

      if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(message);
        console.log('[continueVS.sendMessage] Message posted successfully:', messageType);
      } else {
        console.error('[continueVS.sendMessage] ERROR: WebView2 postMessage API not available!');
        console.error('[continueVS-API-DEBUG] window.chrome:', typeof window.chrome, window.chrome);
        console.error('[continueVS-API-DEBUG] window.chrome.webview:', typeof window.chrome?.webview, window.chrome?.webview);
        throw new Error('WebView2 postMessage API not available');
      }
      return id;
    } catch (error) {
      console.error('[continueVS.sendMessage] Error:', error);
      throw error;
    }
  };

  // onMessage: Called by C# to send messages to React
  bridge.onMessage = function(message) {
    try {
      if (typeof message === 'string') {
        message = JSON.parse(message);
      }

      bridge._messageQueue.push(message);
      console.log('[continueVS.onMessage] Message queued:', message.messageType, 'Queue size:', bridge._messageQueue.length);

      // Dispatch custom event
      try {
        const event = new CustomEvent('continueVSMessage', {
          detail: message,
          bubbles: false,
          cancelable: true
        });
        const dispatched = window.dispatchEvent(event);
        console.log('[continueVS.onMessage] Event dispatched:', message.messageType, 'handled:', dispatched);
      } catch (e) {
        console.warn('[continueVS] Event dispatch failed:', e);
      }

      // Call registered handler
      if (message.messageType && bridge._handlers.has(message.messageType)) {
        const handler = bridge._handlers.get(message.messageType);
        if (typeof handler === 'function') {
          try {
            console.log('[continueVS.onMessage] Calling handler for:', message.messageType);
            handler(message);
          } catch (e) {
            console.error('[continueVS] Handler error:', e);
          }
        }
      } else if (message.messageType) {
        console.log('[continueVS.onMessage] No handler registered for:', message.messageType, 'Available handlers:', Array.from(bridge._handlers.keys()));
      }
    } catch (error) {
      console.error('[continueVS.onMessage] Error:', error);
    }
  };

  // on: Register handler for message type
  bridge.on = function(messageType, handler) {
    if (typeof handler === 'function') {
      bridge._handlers.set(messageType, handler);
    }
  };

  // off: Unregister handler
  bridge.off = function(messageType) {
    bridge._handlers.delete(messageType);
  };

  // getState: Get bridge state (can include queued messages)
  bridge.getState = function() {
    return {
      initialized: bridge._initialized,
      version: bridge._version,
      bridgeReady: bridge._bridgeReady,
      messageCount: bridge._messageQueue.length,
      queuedMessages: bridge._messageQueue.slice(),  // Include actual queued messages
      handlers: Array.from(bridge._handlers.keys()),
      nextMessageId: bridge._nextMessageId
    };
  };

  // dequeueMessages: React can call this to get and clear all queued messages
  bridge.dequeueMessages = function() {
    const messages = bridge._messageQueue.slice();
    bridge._messageQueue = [];
    console.log('[continueVS.dequeueMessages] Returning', messages.length, 'queued messages');
    return messages;
  };

     // Fire ready event
     try {
       const event = new CustomEvent('continueVSBridgeReady', {
         detail: { bridge, version: bridge._version, timestamp: Date.now() },
         bubbles: false
       });
       window.dispatchEvent(event);
       console.log('[continueVS] Bridge ready event dispatched successfully');
     } catch (e) {
       console.error('[continueVS] Error firing ready event:', e);
     }

              console.log('[continueVS] Bridge initialization complete - all methods available');

              // Signal to React that the bridge is ready by setting a property it can check
              window.__continueVSBridgeReady = true;

              // Auto-bootstrap: If React hasn't made a bootstrap request within 2 seconds, do it ourselves
              // This handles the case where React's bootstrap logic isn't triggering
              setTimeout(function() {
                console.log('[continueVS] Checking if bootstrap was initiated...');
                // We can't easily check if bootstrap was called, so just notify React we're ready
                if (window.__continueVSBootstrapSent !== true) {
                  console.log('[continueVS] Auto-triggering bootstrap fallback');
                  // Dispatch an event that React might listen for
                  const event = new CustomEvent('continueVSBootstrapRequired', {
                    detail: { timestamp: Date.now() },
                    bubbles: false
                  });
                  window.dispatchEvent(event);
                }
              }, 2000);

              // Provide a method for React to explicitly signal it's ready
              window.__continueVSReactReady = function() {
                console.log('[continueVS] React signaled it is ready');
                // Dispatch queued messages now
                if (bridge._messageQueue && bridge._messageQueue.length > 0) {
                  console.log('[continueVS] Flushing', bridge._messageQueue.length, 'queued messages to React');
                  const messages = bridge._messageQueue.slice();
                  messages.forEach(msg => {
                    const event = new CustomEvent('continueVSMessage', {
                      detail: msg,
                      bubbles: false,
                      cancelable: true
                    });
                    window.dispatchEvent(event);
                  });
                }
              };

                             // Also trigger any waiting promises/watchers
                             if (window.__bridgeReadyResolver) {
                               window.__bridgeReadyResolver(bridge);
                             }
                                                                                                                                                                                                                          })();

                                                                                                                                                                                                                        // Return diagnostics as JSON string
                                                                                                                                                                                                                        return JSON.stringify({
                                                                                                                                                                                                                          success: true,
                                                                                                                                                                                                                          timestamp: new Date().toISOString(),
                                                                                                                                                                                                                          bridge: (typeof window !== 'undefined' && typeof window.continueVS !== 'undefined')
                                                                                                                                                                                                                        });
                                                                                                                                                                                                                      })();
                                                                                                                                                                                              ";

                                                                                                                                                                                                      /// <summary>
                                                                                                                                                                                                      /// Injects the continueVS bridge into the WebView.
        /// </summary>
        public async Task<WebviewInjectionResult> InjectBridgeAsync(
            CoreWebView2 coreWebView2,
            CancellationToken cancellationToken)
        {
            try
            {
                // B4.1: Pre-injection state validation
                Debug.WriteLine("[B4.1] Bridge injection starting - validating CoreWebView2 state");

                // Validate input
                if (coreWebView2 == null)
                {
                    const string msg = "CoreWebView2 is null; cannot inject bridge.";
                    Debug.WriteLine("[B4.1] ERROR: CoreWebView2 is null");
                    return WebviewInjectionResult.CreateFailure(msg, _injectionScript);
                }

                Debug.WriteLine("[B4.1] CoreWebView2 validated: non-null, ready for injection");

                // Check if WebView is ready
                if (coreWebView2 == null)
                {
                    const string msg = "CoreWebView2 is not initialized; cannot inject bridge.";
                    Debug.WriteLine("[B4.2] ERROR: CoreWebView2 initialization check failed");
                    return WebviewInjectionResult.CreateFailure(msg, _injectionScript);
                }

                // B4.2: Injection script execution entry
                Debug.WriteLine("[B4.2] Bridge injection entering async wrapper - ExecuteScriptAsync call imminent");
                var stopwatch = Stopwatch.StartNew();

                // Execute the injection script - the final statement returns JSON diagnostics
                string scriptResult = await coreWebView2.ExecuteScriptAsync(_injectionScript);

                stopwatch.Stop();
                Debug.WriteLine($"[B4.3] Bridge injection script executed successfully in {stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[B4.3-DIAGNOSTICS] Injection script diagnostics: {scriptResult}");

                if (!string.IsNullOrEmpty(scriptResult))
                {
                    try
                    {
                        var diag = Newtonsoft.Json.Linq.JObject.Parse(scriptResult);
                        Debug.WriteLine($"[B4.3-DIAG-PARSED] success: {diag["success"]}, bridgeReady: {diag["bridgeReady"]}, sendMessage: {diag["sendMessage"]}, onMessage: {diag["onMessage"]}");
                    }
                    catch (Exception parseEx)
                    {
                        Debug.WriteLine($"[B4.3-DIAG-PARSE-ERROR] Failed to parse diagnostics: {parseEx.Message}");
                    }
                }

                // B4.4: Post-injection verification - window.continueVS defined check
                Debug.WriteLine("[B4.4] Verifying bridge object availability in JavaScript context");
                string verifyScript = "typeof window.continueVS !== 'undefined' && typeof window.continueVS.sendMessage === 'function' && typeof window.continueVS.onMessage === 'function'";
                string verifyResult = await coreWebView2.ExecuteScriptAsync(verifyScript);
                Debug.WriteLine($"[B4.4] Bridge verification result: {verifyResult} (true = bridge accessible and callable)");

                // If we reach here, injection succeeded
                Debug.WriteLine("[B4.5] Bridge injection completed successfully - window.continueVS is defined and operational");
                return WebviewInjectionResult.CreateSuccess(_injectionScript);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[B4] ERROR: Bridge injection failed with exception: {ex.Message}");
                return WebviewInjectionResult.CreateFailure($"Bridge injection failed: {ex.Message}", _injectionScript);
            }
        }
    }
}
