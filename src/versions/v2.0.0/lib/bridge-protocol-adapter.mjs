#!/usr/bin/env node

/**
 * Bridge Protocol Adapter
 *
 * Translation layer between low-level transport messages (C# JSON-RPC Message format)
 * and high-level handler contracts (Node BridgeMessage typedef + HandlerContext).
 *
 * Responsibilities:
 * 1. **Inbound Translation**: Message (C# JSON) → BridgeMessage (Node typedef)
 * 2. **Outbound Translation**: HandlerResponse (Node) → Message (C# JSON)
 * 3. **RPC Correlation**: Track pending requests by messageId, enforce timeouts
 * 4. **Middleware Integration**: Hooks for logging, validation, error recovery
 * 5. **Error Handling**: Structured exceptions with operation context
 *
 * Architecture:
 * ```
 * [Transport.SendMessage(Message)]
 *   ↓
 * [ProtocolAdapter.translateInbound(Message)]
 *   ↓
 * [BridgeMessage + HandlerContext] → Handler
 *   ↓
 * [Handler returns HandlerResponse]
 *   ↓
 * [ProtocolAdapter.translateOutbound(HandlerResponse)]
 *   ↓
 * [Message envelope] → Transport
 * ```
 *
 * @module src/versions/v2.0.0/lib/bridge-protocol-adapter.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: HandlerDispatcher (receives normalized messages)
 *   - Step 47: MessageRoutingMiddleware (integrates with adapter)
 *   - Step 50–61: Handler implementations (consume normalized BridgeMessage)
 *   - Step 71: Handler registration (uses adapter output)
 *   - Step 72–74: Middleware hooks (logging, validation, recovery)
 */

/**
 * Error thrown by BridgeProtocolAdapter during message translation or correlation.
 * @class ProtocolAdapterError
 * @extends {Error}
 */
export class ProtocolAdapterError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType='unknown'] - Type of operation ('translate', 'correlate', 'timeout', 'validation')
   * @param {string} [messageId=null] - Optional messageId for correlation
   * @param {Error} [originalError=null] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', messageId = null, originalError = null) {
    super(message);
    this.name = 'ProtocolAdapterError';
    this.operationType = operationType;
    this.messageId = messageId;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when RPC call timeout expires.
 * @class TimeoutError
 * @extends {ProtocolAdapterError}
 */
export class TimeoutError extends ProtocolAdapterError {
  /**
   * @param {string} messageId - Correlation message ID
   * @param {number} timeoutMs - Timeout window in milliseconds
   */
  constructor(messageId, timeoutMs) {
    super(
      `RPC call timeout (${timeoutMs}ms): messageId=${messageId}`,
      'timeout',
      messageId
    );
    this.name = 'TimeoutError';
    this.timeoutMs = timeoutMs;
  }
}

/**
 * Error thrown when message validation fails.
 * @class ValidationError
 * @extends {ProtocolAdapterError}
 */
export class ValidationError extends ProtocolAdapterError {
  /**
   * @param {string} fieldName - Name of field that failed validation
   * @param {*} value - The invalid value
   * @param {string} reason - Why validation failed
   * @param {string} [messageId=null] - Optional messageId for correlation
   */
  constructor(fieldName, value, reason, messageId = null) {
    super(
      `Validation failed: ${fieldName}=${JSON.stringify(value)} (${reason})`,
      'validation',
      messageId
    );
    this.name = 'ValidationError';
    this.fieldName = fieldName;
    this.value = value;
    this.reason = reason;
  }
}

/**
 * Pending RPC request tracker.
 * @private
 */
class RPCPendingRequest {
  /**
   * @param {string} messageId - Correlation ID
   * @param {AbortController} abortController - Timeout cancellation
   * @param {Function} resolve - Resolve callback
   * @param {Function} reject - Reject callback
   */
  constructor(messageId, abortController, resolve, reject) {
    this.messageId = messageId;
    this.abortController = abortController;
    this.resolve = resolve;
    this.reject = reject;
    this.startTime = Date.now();
  }

  isExpired(timeoutMs) {
    return Date.now() - this.startTime > timeoutMs;
  }
}

/**
 * Bridge Protocol Adapter
 *
 * Translates between C# transport Message format and Node handler contract.
 * Tracks RPC correlations by messageId, enforces timeouts, and provides
 * middleware integration hooks for logging, validation, and error recovery.
 *
 * @class BridgeProtocolAdapter
 */
export class BridgeProtocolAdapter {
  /**
   * Create a Bridge Protocol Adapter
   *
   * @param {Object} [config={}] - Configuration options
   * @param {*} [config.logger=null] - Logger instance (optional)
   * @param {*} [config.metrics=null] - Metrics collector (optional)
   * @param {number} [config.defaultTimeoutMs=30000] - Default RPC timeout
   * @param {boolean} [config.enableTracing=false] - Enable detailed tracing
   */
  constructor(config = {}) {
    this.logger = config.logger || this._createMockLogger();
    this.metrics = config.metrics || this._createMockMetrics();
    this.defaultTimeoutMs = config.defaultTimeoutMs || 30000;
    this.enableTracing = config.enableTracing || false;

    /**
     * @type {Map<string, RPCPendingRequest>}
     * Tracks pending RPC calls by messageId for correlation and timeout enforcement
     */
    this.pendingRequests = new Map();

    /**
     * @type {Map<string, Function>}
     * Middleware hooks: 'pre-translate' | 'post-translate' | 'pre-handler' | 'post-handler'
     */
    this.middlewareHooks = new Map();

    this.logger.debug('BridgeProtocolAdapter initialized');
  }

  /**
   * Translate inbound C# Message to Node BridgeMessage + HandlerContext
   *
   * @param {Object} message - C# Message envelope {messageType, messageId, data}
   * @param {Object} [handlerContext={}] - Optional handler context overrides {logger, metrics, server}
   * @returns {Promise<Object>} - {bridgeMessage, handlerContext}
   * @throws {ProtocolAdapterError} If message validation fails
   * @throws {ValidationError} If required fields missing
   *
   * @example
   * const { bridgeMessage, handlerContext } = await adapter.translateInbound({
   *   messageType: 'bridge:getEditorState',
   *   messageId: 'uuid-1234',
   *   data: {}
   * });
   */
  async translateInbound(message, handlerContext = {}) {
    try {
      // Invoke pre-translate hook
      await this._invokeHook('pre-translate', message);

      // Validate inbound message
      this._validateMessage(message);

      // Construct BridgeMessage typedef
      const bridgeMessage = {
        messageType: message.messageType,
        messageId: message.messageId,
        data: message.data || {}
      };

      // Assemble HandlerContext
      const context = this._createHandlerContext(message, handlerContext);

      if (this.enableTracing) {
        this.logger.debug(
          `[translateInbound] ${message.messageType}: ${message.messageId}`,
          { data: bridgeMessage.data }
        );
      }

      // Invoke post-translate hook
      await this._invokeHook('post-translate', { bridgeMessage, context });

      return { bridgeMessage, handlerContext: context };
    } catch (error) {
      if (error instanceof ProtocolAdapterError || error instanceof ValidationError) {
        throw error;
      }
      throw new ProtocolAdapterError(
        `Failed to translate inbound message: ${error.message}`,
        'translate',
        message?.messageId,
        error
      );
    }
  }

  /**
   * Translate outbound HandlerResponse to C# Message envelope
   *
   * @param {Object} response - HandlerResponse {success, data?, error?}
   * @param {string} messageId - Correlation messageId
   * @param {string} messageType - Echo of original messageType
   * @returns {Promise<Object>} - C# Message envelope {messageType, messageId, data}
   * @throws {ProtocolAdapterError} If response wrapping fails
   *
   * @example
   * const message = await adapter.translateOutbound(
   *   { success: true, data: { activeFile: '/path/to/file.cs' } },
   *   'uuid-1234',
   *   'bridge:getEditorState'
   * );
   */
  async translateOutbound(response, messageId, messageType) {
    try {
      // Invoke pre-handler hook (response transformation)
      await this._invokeHook('pre-handler-response', response);

      // Wrap response in Message envelope
      const message = {
        messageType,
        messageId,
        data: response
      };

      if (this.enableTracing) {
        this.logger.debug(
          `[translateOutbound] ${messageType}: ${messageId}`,
          { success: response.success, hasError: !!response.error }
        );
      }

      // Invoke post-handler hook
      await this._invokeHook('post-handler-response', message);

      return message;
    } catch (error) {
      if (error instanceof ProtocolAdapterError) {
        throw error;
      }
      throw new ProtocolAdapterError(
        `Failed to translate outbound response: ${error.message}`,
        'translate',
        messageId,
        error
      );
    }
  }

  /**
   * Track a pending RPC call and await response with timeout
   *
   * @param {string} messageId - Correlation ID
   * @param {number} [timeoutMs] - Timeout in milliseconds (uses default if not specified)
   * @returns {Promise<Object>} - Response object or timeout rejection
   *
   * @example
   * const pendingResponse = adapter.trackPendingRequest('uuid-1234', 5000);
   * // Later, when response arrives:
   * adapter.resolvePendingRequest('uuid-1234', { success: true, data: {...} });
   */
  trackPendingRequest(messageId, timeoutMs = null) {
    if (!messageId) {
      throw new ValidationError('messageId', messageId, 'messageId must be non-empty');
    }

    const timeout = timeoutMs || this.defaultTimeoutMs;
    const abortController = new AbortController();

    return new Promise((resolve, reject) => {
      const pendingRequest = new RPCPendingRequest(
        messageId,
        abortController,
        resolve,
        reject
      );

      this.pendingRequests.set(messageId, pendingRequest);

      // Set timeout
      const timeoutHandle = setTimeout(() => {
        this.pendingRequests.delete(messageId);
        const error = new TimeoutError(messageId, timeout);
        this.logger.warn(`RPC timeout: ${messageId} (${timeout}ms)`);
        reject(error);
      }, timeout);

      // Clean up timeout if request resolves early
      const originalResolve = resolve;
      const wrappedResolve = (value) => {
        clearTimeout(timeoutHandle);
        this.pendingRequests.delete(messageId);
        originalResolve(value);
      };

      const wrappedReject = (error) => {
        clearTimeout(timeoutHandle);
        this.pendingRequests.delete(messageId);
        reject(error);
      };

      // Update the pending request callbacks
      pendingRequest.resolve = wrappedResolve;
      pendingRequest.reject = wrappedReject;
    });
  }

  /**
   * Resolve a pending RPC request with response
   *
   * @param {string} messageId - Correlation ID
   * @param {Object} response - Response object
   * @returns {boolean} - True if request was pending and resolved, false otherwise
   */
  resolvePendingRequest(messageId, response) {
    const pending = this.pendingRequests.get(messageId);
    if (!pending) {
      this.logger.warn(`resolvePendingRequest: no pending request for ${messageId}`);
      return false;
    }

    pending.resolve(response);
    return true;
  }

  /**
   * Reject a pending RPC request with error
   *
   * @param {string} messageId - Correlation ID
   * @param {Error} error - Error object
   * @returns {boolean} - True if request was pending and rejected, false otherwise
   */
  rejectPendingRequest(messageId, error) {
    const pending = this.pendingRequests.get(messageId);
    if (!pending) {
      this.logger.warn(`rejectPendingRequest: no pending request for ${messageId}`);
      return false;
    }

    pending.reject(error);
    return true;
  }

  /**
   * Clear expired pending requests
   *
   * @param {number} [maxAgeMs=60000] - Maximum age before cleanup
   * @returns {number} - Number of requests cleaned up
   */
  clearExpiredRequests(maxAgeMs = 60000) {
    let count = 0;
    for (const [messageId, pending] of this.pendingRequests.entries()) {
      if (pending.isExpired(maxAgeMs)) {
        this.pendingRequests.delete(messageId);
        count++;
      }
    }
    if (count > 0) {
      this.logger.debug(`Cleared ${count} expired pending requests`);
    }
    return count;
  }

  /**
   * Register a middleware hook
   *
   * Available hooks:
   * - 'pre-translate': invoked before message translation
   * - 'post-translate': invoked after message translation
   * - 'pre-handler-response': invoked before response wrapping
   * - 'post-handler-response': invoked after response wrapping
   *
   * @param {string} hookName - Hook name
   * @param {Function} handler - Async hook handler (args) => Promise<void>
   * @throws {Error} If hook name invalid
   */
  registerHook(hookName, handler) {
    const validHooks = [
      'pre-translate',
      'post-translate',
      'pre-handler-response',
      'post-handler-response'
    ];
    if (!validHooks.includes(hookName)) {
      throw new Error(`Invalid hook name: ${hookName}. Valid: ${validHooks.join(', ')}`);
    }
    if (typeof handler !== 'function') {
      throw new Error('Hook handler must be a function');
    }
    this.middlewareHooks.set(hookName, handler);
  }

  /**
   * Invoke a middleware hook (internal)
   *
   * @private
   * @param {string} hookName - Hook name
   * @param {*} args - Arguments to pass to hook
   */
  async _invokeHook(hookName, args) {
    const handler = this.middlewareHooks.get(hookName);
    if (!handler) {
      return; // Hook not registered
    }
    try {
      await handler(args);
    } catch (error) {
      this.logger.error(`Middleware hook '${hookName}' failed: ${error.message}`, error.stack);
      throw new ProtocolAdapterError(
        `Middleware hook error: ${error.message}`,
        'middleware',
        null,
        error
      );
    }
  }

  /**
   * Validate required fields in inbound message
   *
   * @private
   * @param {Object} message - Message to validate
   * @throws {ValidationError} If validation fails
   */
  _validateMessage(message) {
    if (!message) {
      throw new ValidationError('message', message, 'Message must be non-null');
    }
    if (typeof message !== 'object') {
      throw new ValidationError('message', message, 'Message must be an object');
    }
    if (!message.messageType || typeof message.messageType !== 'string') {
      throw new ValidationError('messageType', message.messageType, 'Must be non-empty string');
    }
    if (!message.messageId || typeof message.messageId !== 'string') {
      throw new ValidationError('messageId', message.messageId, 'Must be non-empty string');
    }
  }

  /**
   * Assemble HandlerContext from message and overrides
   *
   * @private
   * @param {Object} message - Inbound message
   * @param {Object} overrides - Context overrides
   * @returns {Object} - HandlerContext {logger, metrics, server, message}
   */
  _createHandlerContext(message, overrides = {}) {
    return {
      logger: overrides.logger || this.logger,
      metrics: overrides.metrics || this.metrics,
      server: overrides.server || null,
      message: {
        messageType: message.messageType,
        messageId: message.messageId
      }
    };
  }

  /**
   * Create mock logger for when none provided
   *
   * @private
   */
  _createMockLogger() {
    return {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {}
    };
  }

  /**
   * Create mock metrics collector for when none provided
   *
   * @private
   */
  _createMockMetrics() {
    return {
      recordMetric: () => {},
      recordError: () => {}
    };
  }
}

/**
 * Factory function to create a BridgeProtocolAdapter
 *
 * @param {Object} [config={}] - Configuration options
 * @returns {BridgeProtocolAdapter} - Adapter instance
 *
 * @example
 * const adapter = createBridgeProtocolAdapter({
 *   logger,
 *   metrics,
 *   defaultTimeoutMs: 5000,
 *   enableTracing: true
 * });
 */
export function createBridgeProtocolAdapter(config = {}) {
  return new BridgeProtocolAdapter(config);
}
