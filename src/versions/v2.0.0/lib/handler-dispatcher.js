#!/usr/bin/env node

/**
 * Handler Dispatcher for Bridge Message Routing
 *
 * Routes bridge-specific messages (prefixed with "bridge:") to registered
 * handler functions. Non-bridge messages pass through unchanged to the
 * Continue process relay.
 *
 * @module src/versions/v2.0.0/lib/handler-dispatcher.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 13: core-server.js (integration point)
 *   - Step 15: handler-adapter.js (creates handlers)
 *   - Step 47: message-routing-middleware.js (uses dispatcher)
 *   - Steps 50–61: Individual handler implementations (register here)
 *   - Step 71: Register all handlers with dispatcher
 *
 * Message Flow:
 *   IDE (C#) → core-server.js → dispatcher.dispatch() → handler | relay
 */

/**
 * Handler function type definition (JSDoc for IDE support).
 * 
 * @typedef {Function} HandlerFunction
 * @param {Object} message - Message object
 * @param {string} message.messageType - e.g., "bridge:getEditorState"
 * @param {string} message.messageId - Correlation UUID
 * @param {*} message.data - Payload (handler-specific)
 * @param {Object} context - Dispatch context (logger, metrics, server)
 * @returns {Promise<{success: boolean, data?: *, error?: string}>}
 *   Handler must return response object with success flag and optional data/error
 */

/**
 * Dispatcher result structure.
 * @typedef {Object} DispatchResult
 * @property {boolean} handled - Whether a bridge handler was invoked
 * @property {boolean} shouldRelay - Whether message should relay to Continue
 * @property {Object} response - Response from handler (if handled=true)
 * @property {string} response.messageType - Echo of input messageType
 * @property {string} response.messageId - Echo of input messageId
 * @property {boolean} response.success - Handler success flag
 * @property {*} response.data - Handler response data (if success=true)
 * @property {string} response.error - Error message (if success=false)
 */

/**
 * Handler Dispatcher
 * 
 * Maps message types to handler functions. Intercepts bridge-prefixed
 * messages and routes them to registered handlers.
 */
export class HandlerDispatcher {
  /**
   * @param {Object} config - Configuration
   * @param {*} config.logger - Logger instance (Step 25, or mock)
   * @param {*} config.metrics - Metrics collector (Step 26, or mock)
   * @param {*} config.server - CoreServer instance for context
   */
  constructor({ logger = null, metrics = null, server = null } = {}) {
    this.logger = logger || this._createMockLogger();
    this.metrics = metrics || this._createMockMetrics();
    this.server = server;

    /** @type {Map<string, HandlerFunction>} */
    this.handlers = new Map();

    this.logger.debug('HandlerDispatcher initialized');
  }

  /**
   * Register a handler for a message type.
   * 
   * @param {string} messageType - Message type (e.g., "bridge:getEditorState")
   * @param {HandlerFunction} handler - Async handler function
   * @throws {Error} If handler already registered for this type
   */
  register(messageType, handler) {
    if (!messageType || typeof handler !== 'function') {
      throw new Error(
        `Invalid handler registration: messageType=${messageType}, ` +
        `handler type=${typeof handler}`
      );
    }

    if (this.handlers.has(messageType)) {
      throw new Error(
        `Handler already registered for message type "${messageType}"`
      );
    }

    this.handlers.set(messageType, handler);
    this.logger.debug(`Registered handler for "${messageType}"`);
  }

  /**
   * Get handler for a message type.
   * 
   * @param {string} messageType - Message type to lookup
   * @returns {HandlerFunction|null} Handler function or null if not found
   */
  getHandler(messageType) {
    return this.handlers.get(messageType) || null;
  }

  /**
   * Check if a handler is registered for a message type.
   * 
   * @param {string} messageType - Message type to check
   * @returns {boolean} True if handler exists
   */
  hasHandler(messageType) {
    return this.handlers.has(messageType);
  }

  /**
   * Dispatch a message through the handler registry.
   * 
   * Bridge messages are routed to handlers. Non-bridge messages
   * return shouldRelay=true to pass through to Continue.
   * 
   * @param {Object} message - Message to dispatch
   * @param {string} message.messageType - Message type
   * @param {string} message.messageId - Correlation ID
   * @param {*} message.data - Message payload
   * @returns {Promise<DispatchResult>} Dispatch result with routing decision
   */
  async dispatch(message) {
    // Validate message shape
    if (!message || typeof message !== 'object') {
      this.logger.warn('Dispatcher received invalid message', {
        type: typeof message,
      });
      return { handled: false, shouldRelay: false };
    }

    const { messageType, messageId, data } = message;

    if (!messageType) {
      this.logger.warn('Message missing messageType field', { messageId });
      return { handled: false, shouldRelay: false };
    }

    // Check if this is a bridge message
    const isBridgeMessage = messageType.startsWith('bridge:');

    if (!isBridgeMessage) {
      // Non-bridge message: pass through to Continue
      return {
        handled: false,
        shouldRelay: true,
        response: null,
      };
    }

    // Bridge message: attempt handler lookup
    const handler = this.getHandler(messageType);

    if (!handler) {
      this.logger.warn(`No handler registered for "${messageType}"`, {
        messageId,
      });
      this.metrics.recordHandlerMissing?.(messageType);

      // Bridge message without handler: drop it (don't relay)
      return {
        handled: false,
        shouldRelay: false,
        response: {
          messageType,
          messageId,
          success: false,
          error: `No handler for message type "${messageType}"`,
        },
      };
    }

    // Execute handler with error handling
    const startTime = Date.now();
    try {
      this.logger.debug(`Dispatching to handler for "${messageType}"`, {
        messageId,
      });

      const result = await handler(message, {
        logger: this.logger,
        metrics: this.metrics,
        server: this.server,
      });

      const duration = Date.now() - startTime;

      // Record metrics
      this.metrics.recordHandlerExecution?.(messageType, duration, true);

      // Validate handler response shape
      const response = this._buildResponse(messageType, messageId, result);

      this.logger.debug(`Handler succeeded for "${messageType}"`, {
        messageId,
        duration,
        success: response.success,
      });

      return {
        handled: true,
        shouldRelay: false,
        response,
      };
    } catch (err) {
      const duration = Date.now() - startTime;

      this.logger.error(`Handler failed for "${messageType}"`, {
        messageId,
        error: err.message,
        stack: err.stack,
        duration,
      });

      this.metrics.recordHandlerExecution?.(messageType, duration, false);
      this.metrics.recordError?.('handler_exception');

      return {
        handled: true,
        shouldRelay: false,
        response: {
          messageType,
          messageId,
          success: false,
          error: `Handler error: ${err.message}`,
        },
      };
    }
  }

  /**
   * List all registered handlers (for debugging/diagnostics).
   * 
   * @returns {string[]} Array of registered message types
   */
  listHandlers() {
    return Array.from(this.handlers.keys());
  }

  /**
   * Get handler registry state (for diagnostics).
   * 
   * @returns {Object} Diagnostics object
   */
  getDiagnostics() {
    return {
      handlerCount: this.handlers.size,
      handlers: this.listHandlers(),
      timestamp: new Date().toISOString(),
    };
  }

  /**
   * Validate and build response message from handler result.
   * 
   * @private
   * @param {string} messageType - Original message type
   * @param {string} messageId - Original message ID
   * @param {*} result - Handler result (should be object with success flag)
   * @returns {Object} Normalized response message
   */
  _buildResponse(messageType, messageId, result) {
    // Ensure result is an object
    if (!result || typeof result !== 'object') {
      return {
        messageType,
        messageId,
        success: false,
        error: 'Handler returned invalid response type',
      };
    }

    // Extract fields from handler result
    const { success = false, data = null, error = null } = result;

    const response = {
      messageType,
      messageId,
      success: Boolean(success),
    };

    if (success && data !== null && data !== undefined) {
      response.data = data;
    }

    if (!success && error) {
      response.error = String(error);
    }

    return response;
  }

  /**
   * Create mock logger for standalone operation (Step 25 not ready).
   * @private
   */
  _createMockLogger() {
    return {
      debug: (msg, data) => console.error(`[DEBUG] ${msg}`, data || ''),
      info: (msg, data) => console.error(`[INFO] ${msg}`, data || ''),
      warn: (msg, data) => console.error(`[WARN] ${msg}`, data || ''),
      error: (msg, data) => console.error(`[ERROR] ${msg}`, data || ''),
    };
  }

  /**
   * Create mock metrics collector for standalone operation (Step 26 not ready).
   * @private
   */
  _createMockMetrics() {
    return {
      recordHandlerExecution: () => {},
      recordHandlerMissing: () => {},
      recordError: () => {},
    };
  }
}

export default HandlerDispatcher;
