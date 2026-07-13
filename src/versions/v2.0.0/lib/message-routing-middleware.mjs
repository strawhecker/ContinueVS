#!/usr/bin/env node

/**
 * Message Routing Middleware for Bridge Message Chain
 *
 * Provides a composable middleware chain for routing messages between core-server.js
 * and the handler dispatcher. Enables pre-dispatch and post-dispatch hooks for
 * logging, validation, and error recovery (Steps 72, 73, 74).
 *
 * @module src/versions/v2.0.0/lib/message-routing-middleware.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Architecture:
 *   Message Flow: IDE → core-server.js → MiddlewareChain → Dispatcher → Handler | Relay
 *
 * Middleware Signature:
 *   async function middleware(message, next, context) {
 *     // Pre-dispatch phase: inspect/modify message
 *     const result = await next(message);
 *     // Post-dispatch phase: inspect/modify response
 *     return result;
 *   }
 *
 * Related Steps:
 *   - Step 14: HandlerDispatcher (wrapped by middleware)
 *   - Step 71: Register all handlers (benefits from middleware validation)
 *   - Step 72: Message logging middleware (plugins into loggingHook)
 *   - Step 73: Request/response validation (plugins into validationHook)
 *   - Step 74: Error recovery middleware (plugins into errorRecoveryHook)
 */

/**
 * Middleware function type definition.
 *
 * @typedef {Function} MiddlewareFunction
 * @param {Object} message - Message object
 * @param {string} message.messageType - Message type (e.g., "bridge:getEditorState")
 * @param {string} message.messageId - Correlation UUID
 * @param {*} message.data - Message payload
 * @param {Function} next - Next middleware in chain (or dispatcher.dispatch if last)
 * @param {Object} context - Middleware context
 * @param {*} context.logger - Logger instance
 * @param {*} context.metrics - Metrics collector
 * @param {*} context.server - CoreServer instance
 * @returns {Promise<Object>} DispatchResult from dispatcher:
 *   {
 *     handled: boolean,
 *     shouldRelay: boolean,
 *     response: { messageType, messageId, success, data?, error? }
 *   }
 */

/**
 * Error class for middleware execution failures.
 */
export class MiddlewareExecutionError extends Error {
  constructor(operation, message, originalError = null) {
    super(`Middleware error [${operation}]: ${message}`);
    this.name = 'MiddlewareExecutionError';
    this.operation = operation;
    this.originalError = originalError;
  }
}

/**
 * Composable middleware chain for message routing.
 *
 * Manages registration and execution of middleware functions that wrap
 * the handler dispatcher. Supports pre-dispatch and post-dispatch phases.
 */
export class MiddlewareChain {
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

    /** @type {MiddlewareFunction[]} */
    this.middlewares = [];

    /** @type {Object<string, MiddlewareFunction>} Hook registry for Steps 72-74 */
    this.hooks = {
      validationHook: null,
      loggingHook: null,
      errorRecoveryHook: null,
    };

    this.logger.debug('MiddlewareChain initialized');
  }

  /**
   * Register a middleware function to the chain.
   *
   * Middleware are executed in FIFO order (first registered, first executed).
   * Each middleware must call next() to pass control to the next middleware
   * or to the dispatcher if this is the last middleware.
   *
   * @param {MiddlewareFunction} middleware - Async middleware function
   * @throws {Error} If middleware is not a function
   */
  use(middleware) {
    if (typeof middleware !== 'function') {
      throw new Error(
        `Invalid middleware: expected function, got ${typeof middleware}`
      );
    }

    this.middlewares.push(middleware);
    this.logger.debug(`Middleware registered [total: ${this.middlewares.length}]`);
  }

  /**
   * Register a hook for optional injection by Steps 72-74.
   *
   * @param {string} hookName - Hook identifier ('validationHook', 'loggingHook', 'errorRecoveryHook')
   * @param {MiddlewareFunction} hookFn - Middleware function
   * @throws {Error} If hookName not recognized
   */
  registerHook(hookName, hookFn) {
    if (!this.hooks.hasOwnProperty(hookName)) {
      throw new Error(
        `Unknown hook: "${hookName}". Available: ${Object.keys(this.hooks).join(', ')}`
      );
    }

    if (typeof hookFn !== 'function') {
      throw new Error(`Hook "${hookName}" must be a function, got ${typeof hookFn}`);
    }

    this.hooks[hookName] = hookFn;
    this.logger.debug(`Hook registered: ${hookName}`);
  }

  /**
   * List all registered hooks for introspection.
   *
   * @returns {Object} { hookName: isRegistered, ... }
   */
  listHooks() {
    const result = {};
    for (const [name, fn] of Object.entries(this.hooks)) {
      result[name] = fn !== null;
    }
    return result;
  }

  /**
   * Compose middleware chain with hooks and return a wrapper function.
   *
   * Returns an async function that can be called with (message, dispatcher, context)
   * to execute the full chain.
   *
   * @returns {Function} Async chain executor
   */
  compose() {
    // Build final middleware array: built-in hooks + registered middleware
    const allMiddleware = [];

    // Step 1: Validation hook (Step 73 will inject here)
    if (this.hooks.validationHook) {
      allMiddleware.push(this.hooks.validationHook);
    }

    // Step 2: User-registered middleware
    allMiddleware.push(...this.middlewares);

    // Step 3: Logging hook (Step 72 will inject here)
    if (this.hooks.loggingHook) {
      allMiddleware.push(this.hooks.loggingHook);
    }

    // Return the chain executor
    return async (message, dispatcher, context) => {
      return this._executeChain(allMiddleware, message, dispatcher, context);
    };
  }

  /**
   * Execute a message through the middleware chain and dispatcher.
   *
   * This is a convenience method; typically you'd call compose() once and reuse
   * the returned function. This method is useful for one-off executions.
   *
   * @param {Object} message - Message to dispatch
   * @param {*} dispatcher - HandlerDispatcher instance
   * @param {Object} context - Execution context (logger, metrics, server)
   * @returns {Promise<Object>} DispatchResult
   */
  async execute(message, dispatcher, context = {}) {
    const mergedContext = {
      logger: context.logger || this.logger,
      metrics: context.metrics || this.metrics,
      server: context.server || this.server,
    };

    // Build full middleware array including hooks
    const allMiddleware = [];

    // Step 1: Validation hook (Step 73 will inject here)
    if (this.hooks.validationHook) {
      allMiddleware.push(this.hooks.validationHook);
    }

    // Step 2: User-registered middleware
    allMiddleware.push(...this.middlewares);

    // Step 3: Logging hook (Step 72 will inject here)
    if (this.hooks.loggingHook) {
      allMiddleware.push(this.hooks.loggingHook);
    }

    try {
      return await this._executeChain(
        allMiddleware,
        message,
        dispatcher,
        mergedContext
      );
    } catch (err) {
      this.logger.error('Middleware chain execution failed', {
        messageId: message?.messageId,
        error: err.message,
        stack: err.stack,
      });
      throw err;
    }
  }

  /**
   * Recursively execute middleware chain.
   *
   * @private
   * @param {MiddlewareFunction[]} middlewares - Middleware to execute
   * @param {Object} message - Message to dispatch
   * @param {*} dispatcher - HandlerDispatcher instance
   * @param {Object} context - Execution context
   * @returns {Promise<Object>} DispatchResult
   */
  async _executeChain(middlewares, message, dispatcher, context) {
    let index = -1;

    // Create the chain of next() functions
    const dispatch = async () => {
      // Final next() calls the dispatcher
      return dispatcher.dispatch(message, context);
    };

    // Build next() for each middleware position
    const chain = middlewares.map((fn, i) => async () => {
      if (i <= index) {
        throw new MiddlewareExecutionError(
          'chain_reentry',
          'Middleware called next() multiple times'
        );
      }
      index = i;

      try {
        return await fn(message, async () => {
          if (i === middlewares.length - 1) {
            // Last middleware: call dispatcher
            return dispatch();
          }
          // Call next middleware
          return chain[i + 1]();
        }, context);
      } catch (err) {
        throw new MiddlewareExecutionError(
          `middleware[${i}]`,
          err.message,
          err
        );
      }
    });

    if (chain.length === 0) {
      // No middleware: call dispatcher directly
      return dispatch();
    }

    try {
      return await chain[0]();
    } catch (err) {
      if (err instanceof MiddlewareExecutionError) {
        throw err;
      }
      throw new MiddlewareExecutionError('chain_execution', err.message, err);
    }
  }

  /**
   * Built-in validation hook scaffold (Step 73 will implement).
   *
   * @private
   */
  static get validationHookScaffold() {
    return async (message, next, context) => {
      // Pre-dispatch validation (Step 73 implementation)
      // For now: pass through
      const result = await next();
      // Post-dispatch validation if needed
      return result;
    };
  }

  /**
   * Built-in logging hook scaffold (Step 72 will implement).
   *
   * @private
   */
  static get loggingHookScaffold() {
    return async (message, next, context) => {
      // Pre-dispatch logging (Step 72 implementation)
      // For now: pass through
      const result = await next();
      // Post-dispatch logging if needed
      return result;
    };
  }

  /**
   * Built-in error recovery hook scaffold (Step 74 will implement).
   *
   * @private
   */
  static get errorRecoveryHookScaffold() {
    return async (message, next, context) => {
      try {
        return await next();
      } catch (err) {
        // Error recovery implementation (Step 74)
        // For now: re-throw
        throw err;
      }
    };
  }

  /**
   * Create a mock logger for standalone usage (testing, etc).
   *
   * @private
   */
  _createMockLogger() {
    return {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
    };
  }

  /**
   * Create a mock metrics collector for standalone usage.
   *
   * @private
   */
  _createMockMetrics() {
    return {
      recordMiddlewareExecution: () => {},
      recordError: () => {},
    };
  }
}

/**
 * Factory function to create a pre-configured middleware chain.
 *
 * Useful for standard setup in core-server.js and tests.
 *
 * @param {Object} config - Configuration (logger, metrics, server)
 * @returns {MiddlewareChain}
 */
export function createMiddlewareChain(config) {
  return new MiddlewareChain(config);
}

/**
 * Utility to wrap a dispatcher with a middleware chain.
 *
 * Usage:
 *   const chain = new MiddlewareChain({ logger, metrics, server });
 *   const wrapped = wrapDispatcher(chain, dispatcher);
 *   const result = await wrapped.dispatch(message, context);
 *
 * @param {MiddlewareChain} chain - Middleware chain instance
 * @param {*} dispatcher - HandlerDispatcher instance
 * @returns {Object} Wrapped dispatcher with middleware
 */
export function wrapDispatcher(chain, dispatcher) {
  const executor = chain.compose();

  return {
    dispatch: async (message, context = {}) => {
      return executor(message, dispatcher, context);
    },
    // Expose chain for introspection
    chain,
    // Expose original dispatcher for direct access if needed
    originalDispatcher: dispatcher,
  };
}

export default {
  MiddlewareChain,
  MiddlewareExecutionError,
  createMiddlewareChain,
  wrapDispatcher,
};
