// VS Extension Bridge - Communicates with C# backend via WebView2
// Provides the `vscode` shim that Continue's React GUI expects (acquireVsCodeApi pattern)
// Also creates window.continueVS (architecture contract) and window.continueVSBridge (compat)
(function() {
  'use strict';

  console.log('[VS-Bridge] Initializing VS extension bridge...');

  // =========================================================================
  // MESSAGE LISTENER INTERCEPT: Capture all window 'message' event handlers
  // so we can invoke them directly when C# posts a message.
  // window.dispatchEvent(new MessageEvent(...)) does NOT reliably trigger
  // handlers added via window.addEventListener('message', ...) in Chromium.
  // =========================================================================
  var _messageListeners = [];
  var _origAddEventListener = window.addEventListener.bind(window);
  var _origRemoveEventListener = window.removeEventListener.bind(window);

  window.addEventListener = function(type, handler, options) {
    if (type === 'message' && typeof handler === 'function') {
      _messageListeners.push(handler);
    }
    return _origAddEventListener(type, handler, options);
  };

  window.removeEventListener = function(type, handler, options) {
    if (type === 'message' && typeof handler === 'function') {
      var idx = _messageListeners.indexOf(handler);
      if (idx !== -1) _messageListeners.splice(idx, 1);
    }
    return _origRemoveEventListener(type, handler, options);
  };

  // Helper: invoke all captured 'message' listeners with a synthetic event
  function _dispatchToMessageListeners(data) {
    var fakeEvent = { data: data, origin: window.location.origin, source: window, ports: [] };
    console.log('[VS-Bridge] _dispatch: messageType=' + (data && data.messageType) + ', messageId=' + (data && data.messageId) + ', hasData=' + !!(data && data.data) + ', listenerCount=' + _messageListeners.length);
    for (var i = 0; i < _messageListeners.length; i++) {
      try { _messageListeners[i](fakeEvent); } catch (e) { console.error('[VS-Bridge] listener error:', e); }
    }
  }

  // Expose listener count for diagnostics
  Object.defineProperty(window, '__bridgeListenerCount', { get: function() { return _messageListeners.length; } });

  // Expose dispatch function so C# can call it via ExecuteScriptAsync
  window._dispatchToMessageListeners = _dispatchToMessageListeners;

  console.log('[VS-Bridge] Message listener intercept installed');

  // =========================================================================
  // CRITICAL: `vscode` shim — Continue's GUI calls vscode.postMessage(msg)
  // to send messages to the host. We map this to WebView2's postMessage.
  // =========================================================================
  if (typeof vscode === 'undefined') {
    window.vscode = {
      postMessage: function(msg) {
        console.log('[VS-Bridge] GUI->C#: messageType=' + (msg && msg.messageType) + ', messageId=' + (msg && msg.messageId));
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
          window.chrome.webview.postMessage(msg);
        } else {
          console.error('[VS-Bridge] WebView2 postMessage not available in vscode shim');
        }
      },
      getState: function() { return window.__vscodePersistState || {}; },
      setState: function(s) { window.__vscodePersistState = s; return s; }
    };
    // Also expose acquireVsCodeApi for any code that calls it
    window.acquireVsCodeApi = function() { return window.vscode; };
    console.log('[VS-Bridge] vscode shim installed');
  }

  // =========================================================================
  // PRIMARY BRIDGE: window.continueVS (required by architecture)
  // C# delivers messages by calling: window.continueVS.onMessage(json)
  // GUI sends messages via: window.chrome.webview.postMessage(json)
  // =========================================================================
  var _handlers = {};

  window.continueVS = {
    /**
     * Called by C# via ExecuteScriptAsync to deliver messages to the GUI.
     * Dispatches as a MessageEvent so React's message listeners receive it.
     */
    onMessage: function(jsonStr) {
      try {
        var msg = typeof jsonStr === 'string' ? JSON.parse(jsonStr) : jsonStr;
        console.log('[VS-Bridge] onMessage received:', msg.messageType || 'unknown');

        // Dispatch to all window "message" listeners by manually invoking them.
        // We cannot use window.postMessage because WebView2 intercepts it and
        // sends it back to C# (causing an infinite loop).
        // We cannot use window.dispatchEvent(new MessageEvent(...)) because in
        // some contexts it doesn't trigger addEventListener('message', ...).
        // Solution: create a MessageEvent and dispatch it on window.
        var event = new MessageEvent('message', { data: msg, origin: window.location.origin, source: window });
        window.dispatchEvent(event);

        // Also fire any directly registered handlers
        var type = msg.messageType || msg.type;
        if (type && _handlers[type]) {
          _handlers[type].forEach(function(handler) {
            try { handler(msg); } catch (e) { console.error('[VS-Bridge] Handler error:', e); }
          });
        }
      } catch (e) {
        console.error('[VS-Bridge] onMessage error:', e);
      }
    },

    /**
     * Send a message from GUI to C# backend
     */
    sendMessage: function(data) {
      try {
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
          window.chrome.webview.postMessage(data);
        } else {
          console.error('[VS-Bridge] WebView2 postMessage not available');
        }
      } catch (e) {
        console.error('[VS-Bridge] sendMessage error:', e);
      }
    },

    /**
     * Register a handler for a specific message type
     */
    on: function(messageType, handler) {
      if (!_handlers[messageType]) {
        _handlers[messageType] = [];
      }
      _handlers[messageType].push(handler);
    }
  };

  // =========================================================================
  // SECONDARY BRIDGE: window.continueVSBridge (wrapper for compatibility)
  // =========================================================================
  window.continueVSBridge = {
    sendToExtension: function(messageType, data, messageId) {
      try {
        var id = messageId || ('vsext_' + Date.now() + '_' + Math.random());
        var envelope = {
          messageType: messageType,
          data: data || {},
          messageId: id,
          source: 'continue-react',
          timestamp: new Date().toISOString()
        };
        console.log('[VS-Bridge-Wrapper] Forwarding to C#:', messageType, 'ID:', id);
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
          window.chrome.webview.postMessage(envelope);
          return id;
        } else {
          throw new Error('WebView2 bridge not available');
        }
      } catch (error) {
        console.error('[VS-Bridge-Wrapper] Error:', error);
        throw error;
      }
    },

    onMessageFromExtension: function(message) {
      try {
        if (typeof message === 'string') {
          message = JSON.parse(message);
        }
        // Delegate to the primary bridge
        window.continueVS.onMessage(message);
      } catch (error) {
        console.error('[VS-Bridge-Wrapper] Error:', error);
      }
    },

    _messageQueue: []
  };

  // =========================================================================
  // MESSAGE RELAY: C# PostWebMessageAsJson -> GUI message listeners
  // WebView2's PostWebMessageAsJson fires on window.chrome.webview 'message'.
  // We relay to the captured window 'message' listeners directly.
  // =========================================================================
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function(event) {
      _dispatchToMessageListeners(event.data);
    });
    console.log('[VS-Bridge] WebView2 -> window message relay installed');
  }

  console.log('[VS-Bridge] Bridge initialized. window.continueVS ready.');
})();
