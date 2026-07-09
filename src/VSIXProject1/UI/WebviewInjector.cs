using Microsoft.Web.WebView2.Core;
using System;
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
(function() {
  'use strict';

  // Polyfill for older browsers (though WebView2 supports this natively)
  if (!window.chrome) {
    window.chrome = {};
  }
  if (!window.chrome.webview) {
    window.chrome.webview = {};
  }

  // Initialize the continueVS bridge object
  if (!window.continueVS) {
    window.continueVS = {};
  }

  const bridge = window.continueVS;

  // State tracking for debugging
  bridge._initialized = true;
  bridge._version = '2.0.0';
  bridge._bridgeReady = true;
  bridge._messageQueue = [];
  bridge._handlers = new Map();
  bridge._nextMessageId = 0;

  /**
   * Called by C# handlers to send messages into the React application.
   * 
   * Example from C#:
   *   SendToGui('configUpdate', { result: {...}, profiles: [...] })
   * 
   * This function is invoked via:
   *   CoreWebView2.ExecuteScriptAsync(
   *       ""window.continueVS && window.continueVS.onMessage({...})"");
   * 
   * @param {Object} message - The message from C# handlers
   * @param {string} message.messageType - The type/event name
   * @param {string} message.messageId - Unique message identifier
   * @param {Object} message.data - The payload
   */
  bridge.onMessage = function(message) {
    try {
      // Log message arrival for debugging (optional)
      if (typeof console !== 'undefined' && console.debug) {
        console.debug('[continueVS.onMessage]', message.messageType, message);
      }

      // Queue the message for React processing
      bridge._messageQueue.push(message);

      // Fire a custom event so React components can subscribe
      // Example: window.addEventListener('continueVSMessage', handler)
      try {
        const event = new CustomEvent('continueVSMessage', {
          detail: message,
          bubbles: false,
          cancelable: true
        });
        window.dispatchEvent(event);
      } catch (e) {
        if (typeof console !== 'undefined' && console.warn) {
          console.warn('[continueVS] Failed to dispatch continueVSMessage event', e);
        }
      }

      // Invoke any registered handlers for this message type
      if (bridge._handlers.has(message.messageType)) {
        const handler = bridge._handlers.get(message.messageType);
        try {
          handler(message);
        } catch (e) {
          if (typeof console !== 'undefined' && console.error) {
            console.error('[continueVS] Handler error for', message.messageType, e);
          }
        }
      }
    } catch (error) {
      if (typeof console !== 'undefined' && console.error) {
        console.error('[continueVS.onMessage] Unhandled error', error);
      }
    }
  };

  /**
   * Called by React components to send messages back to the C# handlers.
   * 
   * Example from React:
   *   window.continueVS.sendMessage('getEditorState', {})
   * 
   * This uses the native WebView2 postMessage API to communicate with C#.
   * 
   * @param {string} messageType - The handler type to invoke
   * @param {Object} data - The message payload
   * @param {string} [messageId] - Optional message ID for request/reply correlation
   */
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

      // Log message sent for debugging (optional)
      if (typeof console !== 'undefined' && console.debug) {
        console.debug('[continueVS.sendMessage]', messageType, message);
      }

      // Post the message to C# via the WebView2 native API
      if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
        window.chrome.webview.postMessage(message);
      } else {
        throw new Error('WebView2 postMessage API not available');
      }

      return id;
    } catch (error) {
      if (typeof console !== 'undefined' && console.error) {
        console.error('[continueVS.sendMessage] Failed to send message', error);
      }
      throw error;
    }
  };

  /**
   * Registers a handler for a specific message type coming from C#.
   * 
   * Example:
   *   window.continueVS.on('configUpdate', (msg) => {
   *     console.log('Config updated', msg.data);
   *   });
   * 
   * @param {string} messageType - The message type to listen for
   * @param {Function} handler - Callback function(message) { ... }
   */
  bridge.on = function(messageType, handler) {
    if (typeof handler === 'function') {
      bridge._handlers.set(messageType, handler);
    }
  };

  /**
   * Unregisters a message handler.
   * 
   * @param {string} messageType - The message type to stop listening for
   */
  bridge.off = function(messageType) {
    bridge._handlers.delete(messageType);
  };

  /**
   * Gets the current state of the bridge.
   * Useful for debugging and diagnostic purposes.
   * 
   * @returns {Object} State information
   */
  bridge.getState = function() {
    return {
      initialized: bridge._initialized,
      version: bridge._version,
      bridgeReady: bridge._bridgeReady,
      messageCount: bridge._messageQueue.length,
      handlers: Array.from(bridge._handlers.keys()),
      nextMessageId: bridge._nextMessageId
    };
  };

  /**
   * Clears the message queue. Useful for cleanup.
   */
  bridge.clearQueue = function() {
    bridge._messageQueue = [];
  };

  // Fire the 'continueVSBridgeReady' event to notify listeners that the bridge is initialized
  try {
    const readyEvent = new CustomEvent('continueVSBridgeReady', {
      detail: {
        bridge: bridge,
        version: bridge._version,
        timestamp: Date.now()
      },
      bubbles: false,
      cancelable: false
    });
    window.dispatchEvent(readyEvent);

    // Also log for debugging
    if (typeof console !== 'undefined' && console.log) {
      console.log('[continueVS] Bridge initialized successfully', bridge.getState());
    }
  } catch (e) {
    if (typeof console !== 'undefined' && console.warn) {
      console.warn('[continueVS] Failed to fire readyEvent', e);
    }
  }
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
                // Validate input
                if (coreWebView2 == null)
                {
                    const string msg = "CoreWebView2 is null; cannot inject bridge.";
                    return WebviewInjectionResult.CreateFailure(msg, _injectionScript);
                }

                // Check if WebView is ready
                if (coreWebView2 == null)
                {
                    const string msg = "CoreWebView2 is not initialized; cannot inject bridge.";
                    return WebviewInjectionResult.CreateFailure(msg, _injectionScript);
                }

                // Execute the injection script
                // ExecuteScriptAsync returns the result of the last statement in the script.
                // Our IIFE (Immediately Invoked Function Expression) returns undefined,
                // so we expect an empty or undefined result.
                await coreWebView2.ExecuteScriptAsync(_injectionScript);

                // If we reach here, injection succeeded
                return WebviewInjectionResult.CreateSuccess(_injectionScript);
            }
            catch (OperationCanceledException)
            {
                const string msg = "Bridge injection was cancelled.";
                return WebviewInjectionResult.CreateFailure(msg, _injectionScript);
            }
            catch (Exception ex)
            {
                string msg = $"Bridge injection failed: {ex.GetType().Name} — {ex.Message}";
                return WebviewInjectionResult.CreateFailure(
                    msg,
                    _injectionScript,
                    ex);
            }
        }
    }
}
