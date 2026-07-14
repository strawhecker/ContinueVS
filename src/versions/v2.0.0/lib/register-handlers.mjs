#!/usr/bin/env node

/**
 * Handler Registration Orchestrator (Step 71)
 *
 * Consumes the static HANDLER_REGISTRY and registers all handlers with the
 * bridge dispatcher during server initialization.
 *
 * **Lifecycle**:
 *   1. Import static HANDLER_REGISTRY from handler-registry.mjs
 *   2. Validate all handlers are callable and metadata is complete
 *   3. Instantiate factory handlers (check isFactory flag)
 *   4. Register each handler via server.dispatcher.register()
 *   5. Log registration at debug/info level
 *   6. Record telemetry (if metrics collector available)
 *   7. Return registration result with count, errors, duration
 *
 * **Usage**:
 *   ```javascript
 *   import { registerAllHandlersWithDispatcher } from './register-handlers.mjs';
 *   
 *   const result = await registerAllHandlersWithDispatcher(bridgeServer);
 *   console.log(`Registered ${result.count} handlers in ${result.duration}ms`);
 *   ```
 *
 * **Error Handling**:
 *   - Invalid server → HandlerRegistrationError (operation: 'validation')
 *   - Missing registry → HandlerRegistrationError (operation: 'registry_load')
 *   - Non-callable handler → HandlerRegistrationError (operation: 'validation', details)
 *   - Duplicate registration → Caught by dispatcher.register(), logged
 *   - Instantiation failure → HandlerRegistrationError (operation: 'instantiation')
 *
 * @module src/versions/v2.0.0/lib/register-handlers.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher.register method)
 *   - Step 45: bridge lifecycle manager (integration point: start())
 *   - Steps 50–61: Handler implementations (consumers of registry)
 *   - Step 66: handler-registry.mjs (HANDLER_REGISTRY source)
 *   - Steps 72–75: Middleware layers (depend on registered handlers)
 */

import { getAllHandlers, HandlerRegistryError } from './handler-registry.mjs';

// ============================================================================
// Error Types
// ============================================================================

/**
 * Thrown when handler registration fails.
 * Includes operation context (validation, instantiation, registration).
 */
export class HandlerRegistrationError extends Error {
  constructor(message, operation = 'unknown', details = null) {
    super(message);
    this.name = 'HandlerRegistrationError';
    this.operation = operation;
    this.details = details;
    this.timestamp = new Date().toISOString();
  }
}

// ============================================================================
// Registration Result Type
// ============================================================================

/**
 * @typedef {Object} RegistrationResult
 * @property {number} count - Number of handlers successfully registered
 * @property {boolean} success - Whether registration succeeded (all handlers registered)
 * @property {Error[]} errors - Errors encountered (may be non-fatal, logged only)
 * @property {number} duration - Time in milliseconds for entire registration
 * @property {Object} details - Per-handler registration results
 *   @property {string} messageType - Handler message type
 *   @property {boolean} registered - Whether this handler registered
 *   @property {string} error - Error message (if registered=false)
 *   @property {boolean} isFactory - Whether handler was instantiated
 */

// ============================================================================
// Registration Logic
// ============================================================================

/**
 * Validate handler registry structure.
 *
 * @param {Array} registry - HANDLER_REGISTRY from handler-registry.mjs
 * @throws {HandlerRegistrationError} If registry is invalid
 * @private
 */
function validateRegistry(registry) {
  if (!Array.isArray(registry)) {
    throw new HandlerRegistrationError(
      `Expected registry array, got ${typeof registry}`,
      'validation',
      { type: typeof registry }
    );
  }

  if (registry.length === 0) {
    throw new HandlerRegistrationError(
      'Registry is empty (expected at least 1 handler)',
      'validation',
      { registryLength: 0 }
    );
  }

  const messageTypes = new Set();
  for (let i = 0; i < registry.length; i++) {
    const entry = registry[i];

    // Validate structure
    if (!entry.messageType) {
      throw new HandlerRegistrationError(
        `Registry entry ${i} missing messageType`,
        'validation',
        { index: i, entry }
      );
    }

    if (!entry.handler) {
      throw new HandlerRegistrationError(
        `Handler for ${entry.messageType} is null/undefined`,
        'validation',
        { messageType: entry.messageType }
      );
    }

    if (typeof entry.handler !== 'function') {
      throw new HandlerRegistrationError(
        `Handler for ${entry.messageType} is not callable (type: ${typeof entry.handler})`,
        'validation',
        { messageType: entry.messageType, handlerType: typeof entry.handler }
      );
    }

    // Check for duplicates
    if (messageTypes.has(entry.messageType)) {
      throw new HandlerRegistrationError(
        `Duplicate messageType in registry: "${entry.messageType}"`,
        'validation',
        { messageType: entry.messageType }
      );
    }
    messageTypes.add(entry.messageType);
  }
}

/**
 * Validate server instance has required methods.
 *
 * @param {Object} server - BridgeServer instance
 * @throws {HandlerRegistrationError} If server is invalid
 * @private
 */
function validateServer(server) {
  if (!server || typeof server !== 'object') {
    throw new HandlerRegistrationError(
      `Expected server object, got ${typeof server}`,
      'validation',
      { serverType: typeof server }
    );
  }

  if (typeof server.registerHandler !== 'function') {
    throw new HandlerRegistrationError(
      'Server missing registerHandler method (BridgeServer expected)',
      'validation',
      { methods: Object.keys(server) }
    );
  }

  if (!server.logger) {
    throw new HandlerRegistrationError(
      'Server missing logger instance',
      'validation'
    );
  }
}

/**
 * Instantiate a factory handler if needed.
 *
 * Factory handlers are functions that return handler functions or handler objects.
 * They need to be called to get the actual handler.
 *
 * Handler patterns:
 *   1. Function handler: async (message, context) => response
 *   2. Class handler: { handle: async (message) => response }
 *
 * @param {Function} handler - Handler or factory function
 * @param {boolean} isFactory - Whether this is a factory
 * @param {string} messageType - Handler message type (for logging)
 * @param {Object} context - Optional context for factory instantiation
 * @returns {Function} The actual handler function (async)
 * @throws {HandlerRegistrationError} If instantiation fails
 * @private
 */
function instantiateHandler(handler, isFactory, messageType, context = {}) {
  if (!isFactory) {
    // Static handler, use as-is
    return handler;
  }

  try {
    // Call the factory to get the handler
    // Factories may require context (logger, dispatcher, collectors)
    let actualHandler;
    if (Object.keys(context).length > 0) {
      actualHandler = handler(context);
    } else {
      actualHandler = handler();
    }

    // Check if result is a function (function handler pattern)
    if (typeof actualHandler === 'function') {
      return actualHandler;
    }

    // Check if result is an object with a `handle` method (class handler pattern)
    if (
      actualHandler &&
      typeof actualHandler === 'object' &&
      typeof actualHandler.handle === 'function'
    ) {
      // Wrap the class handler's `handle` method as a function
      const classHandler = actualHandler;
      return async (message, context) => classHandler.handle(message, context);
    }

    throw new HandlerRegistrationError(
      `Factory for ${messageType} did not return a function or class handler (got ${typeof actualHandler})`,
      'instantiation',
      { messageType, returnType: typeof actualHandler, hasHandle: actualHandler?.handle ? true : false }
    );
  } catch (err) {
    if (err instanceof HandlerRegistrationError) {
      throw err;
    }

    throw new HandlerRegistrationError(
      `Failed to instantiate factory handler for ${messageType}: ${err.message}`,
      'instantiation',
      { messageType, originalError: err.message }
    );
  }
}

/**
 * Register all handlers with the dispatcher.
 *
 * Called during BridgeServer.start() after npm validation, before spawning Continue.
 *
 * **Process**:
 *   1. Validate registry and server
 *   2. Import HANDLER_REGISTRY
 *   3. Prepare context for factory handlers (logger, dispatcher, etc.)
 *   4. For each handler entry:
 *      a. Instantiate if factory (passing context)
 *      b. Call server.registerHandler(messageType, handler)
 *      c. Log at debug level
 *      d. Track success/error
 *   5. Log final result at info level
 *   6. Record metrics (if available)
 *   7. Return result
 *
 * @param {Object} server - BridgeServer instance (with registerHandler, logger)
 * @param {Object} options - Optional configuration
 *   @param {boolean} options.throwOnError - If true, throw on first registration error (default: false)
 *   @param {boolean} options.silent - If true, suppress logging (default: false)
 * @returns {Promise<RegistrationResult>} Registration result
 * @throws {HandlerRegistrationError} If server invalid (or throwOnError=true and registration fails)
 *
 * @example
 * // During BridgeServer.start()
 * const result = await registerAllHandlersWithDispatcher(this);
 * if (!result.success) {
 *   this.logger.warn('Some handlers failed to register', result.errors);
 * }
 * this.logger.info(`Bridge initialized with ${result.count} handlers`);
 */
export async function registerAllHandlersWithDispatcher(
  server,
  options = {}
) {
  const startTime = Date.now();
  const { throwOnError = false, silent = false } = options;

  const result = {
    count: 0,
    success: true,
    errors: [],
    duration: 0,
    details: [],
  };

  try {
    // Validate inputs
    validateServer(server);

    // Get registry
    let registry;
    try {
      registry = getAllHandlers();
      validateRegistry(registry);
    } catch (err) {
      if (err instanceof HandlerRegistryError) {
        throw new HandlerRegistrationError(
          `Failed to load registry: ${err.message}`,
          'registry_load',
          { originalError: err }
        );
      }
      throw err;
    }

    if (!silent) {
      server.logger.debug(`Starting handler registration (${registry.length} handlers)`);
    }

    // Prepare context for factory handlers
    // Factories may need access to logger, dispatcher, collectors, etc.
    const factoryContext = {
      logger: server.logger,
      dispatcher: server.dispatcher,
      // If server has a method to provide additional context, use it
      ...(typeof server._getFactoryContext === 'function' ? server._getFactoryContext() : {}),
    };

    // Register each handler
    for (const entry of registry) {
      const detail = {
        messageType: entry.messageType,
        registered: false,
        error: null,
        isFactory: entry.isFactory || false,
      };

      try {
        // Instantiate if factory
        const handler = instantiateHandler(
          entry.handler,
          entry.isFactory,
          entry.messageType,
          factoryContext
        );

        // Register with dispatcher
        server.registerHandler(entry.messageType, handler);

        detail.registered = true;
        result.count++;

        if (!silent) {
          server.logger.debug(`Registered handler: ${entry.messageType}`, {
            stabilityTier: entry.stabilityTier,
            timeoutPolicy: entry.timeoutPolicy,
            isFactory: entry.isFactory,
          });
        }
      } catch (err) {
        result.success = false;
        detail.error = err.message;
        result.errors.push(err);

        if (!silent) {
          server.logger.warn(
            `Failed to register handler ${entry.messageType}: ${err.message}`,
            {
              operation: err.operation || 'unknown',
              details: err.details,
            }
          );
        }

        // Throw on first error if requested
        if (throwOnError) {
          throw err;
        }
      }

      result.details.push(detail);
    }

    // Record final result
    result.duration = Date.now() - startTime;

    if (!silent) {
      server.logger.info(
        `Handler registration complete: ${result.count}/${registry.length} handlers registered in ${result.duration}ms`,
        {
          success: result.success,
          errorCount: result.errors.length,
        }
      );
    }

    // Record metrics if available
    if (server.metrics && typeof server.metrics.record === 'function') {
      try {
        server.metrics.record('handler_registration_count', result.count);
        server.metrics.record('handler_registration_duration', result.duration);
        server.metrics.record('handler_registration_errors', result.errors.length);
      } catch (err) {
        // Silently fail metrics recording; don't block registration
        if (!silent) {
          server.logger.debug('Failed to record handler registration metrics', {
            error: err.message,
          });
        }
      }
    }

    return result;
  } catch (err) {
    result.duration = Date.now() - startTime;
    result.success = false;

    if (!silent) {
      if (err instanceof HandlerRegistrationError) {
        server.logger.error(
          `Handler registration failed (${err.operation}): ${err.message}`,
          {
            operation: err.operation,
            details: err.details,
          }
        );
      } else {
        server.logger.error(`Handler registration failed: ${err.message}`, {
          errorType: err.constructor.name,
        });
      }
    }

    // Only throw errors from validation/load phase; registration errors are logged but not thrown
    if (
      err instanceof HandlerRegistrationError &&
      (err.operation === 'validation' || err.operation === 'registry_load')
    ) {
      throw err;
    }

    return result;
  }
}

/**
 * Get handler diagnostics from the dispatcher.
 *
 * Useful for testing and debugging.
 *
 * @param {Object} server - BridgeServer instance
 * @returns {Object} Diagnostics: { handlerCount, registeredTypes, ... }
 */
export function getHandlerDiagnostics(server) {
  if (!server || typeof server.getDispatcherDiagnostics !== 'function') {
    return { error: 'Invalid server or dispatcher not available' };
  }

  try {
    return server.getDispatcherDiagnostics();
  } catch (err) {
    return { error: err.message };
  }
}

export default {
  registerAllHandlersWithDispatcher,
  getHandlerDiagnostics,
  HandlerRegistrationError,
};
