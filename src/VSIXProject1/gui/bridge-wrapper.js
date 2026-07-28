// Your Custom Bridge Wrapper - Communicates with C# backend via WebView2
// This is YOUR code that wraps Continue's bridge
(function() {
  'use strict';

  console.log('[VS-Bridge-Wrapper] Initializing VS extension bridge wrapper...');

  // Create your wrapper namespace
  window.continueVSBridge = {
    /**
     * Forward messages from React to C# via WebView2 postMessage
     */
    sendToExtension: function(messageType, data, messageId) {
      try {
        const id = messageId || ('vsext_' + Date.now() + '_' + Math.random());
        const envelope = {
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
          console.error('[VS-Bridge-Wrapper] ERROR: WebView2 postMessage not available');
          throw new Error('WebView2 bridge not available');
        }
      } catch (error) {
        console.error('[VS-Bridge-Wrapper] Error sending to extension:', error);
        throw error;
      }
    },

    /**
     * Receive messages from C# and inject into Continue's app
     */
    onMessageFromExtension: function(message) {
      try {
        if (typeof message === 'string') {
          message = JSON.parse(message);
        }

        console.log('[VS-Bridge-Wrapper] Received from C#:', message.messageType);

        // Forward to Continue's bridge if it exists
        if (window.continueVS && typeof window.continueVS.onMessage === 'function') {
          window.continueVS.onMessage(message);
        } else {
          console.warn('[VS-Bridge-Wrapper] Continue bridge not ready, queuing message');
          if (!window.continueVSBridge._messageQueue) {
            window.continueVSBridge._messageQueue = [];
          }
          window.continueVSBridge._messageQueue.push(message);
        }
      } catch (error) {
        console.error('[VS-Bridge-Wrapper] Error processing message from extension:', error);
      }
    },

    /**
     * Flush any queued messages once Continue is ready
     */
    flushQueuedMessages: function() {
      if (window.continueVSBridge._messageQueue && window.continueVSBridge._messageQueue.length > 0) {
        console.log('[VS-Bridge-Wrapper] Flushing', window.continueVSBridge._messageQueue.length, 'queued messages');

        while (window.continueVSBridge._messageQueue.length > 0) {
          const msg = window.continueVSBridge._messageQueue.shift();
          if (window.continueVS && typeof window.continueVS.onMessage === 'function') {
            window.continueVS.onMessage(msg);
          }
        }
      }
    },

    // Internal queue for messages received before Continue is ready
    _messageQueue: []
  };

  console.log('[VS-Bridge-Wrapper] Wrapper initialized. Waiting for Continue bridge...');
})();
