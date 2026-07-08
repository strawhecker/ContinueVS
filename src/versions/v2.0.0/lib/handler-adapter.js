#!/usr/bin/env node

/**
 * IDE State Handler Adapter
 *
 * Provides a high-level facade over HandlerDispatcher for creating type-safe
 * handlers that interact with IDE state. Simplifies error handling, validation,
 * and metrics collection for all bridge message handlers.
 *
 * @module src/versions/v2.0.0/lib/handler-adapter.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (wrapped service)
 *   - Step 50: getEditorState handler (uses createHandler)
 *   - Step 51: onEditorStateChange handler (uses createHandler)
 *   - Steps 52–61: Other handlers (use createHandler factory)
 *   - Step 67: Handler tests (editor context) — validates this adapter
 *   - Step 71: Handler registration — uses adapter-created handlers
 */

import { HandlerDispatcher } from './handler-dispatcher.js';

/**
 * IDE State Handler Adapter
 *
 * Wraps HandlerDispatcher with convenience methods for creating and managing
 * bridge message handlers. Provides:
 * - Automatic error wrapping and response formatting
 * - Editor state validation (EditorState schema)
 * - Handler execution tracking (invocation count, latency, error rate)
 * - Optional debug logging for handler activity
 *
 * @example
 * const dispatcher = new HandlerDispatcher({ logger, metrics, server });
 * const adapter = new IDEStateAdapter(dispatcher);
 *
 * // Create a typed handler for getEditorState (Step 50)
 * const getEditorStateHandler = adapter.createHandler(
 *   'bridge:getEditorState',
 *   async (data, context) => {
 *     // User function receives typed inputs: data + context
 *     // Returns Promise<{activeFile, cursorLine, ...}>
 *     const editorState = await collectEditorState(context);
 *     return editorState; // Adapter wraps as {success: true, data: editorState}
 *   }
 * );
 *
 * dispatcher.register('bridge:getEditorState', getEditorStateHandler);
 */
export class IDEStateAdapter {
  /**
   * Create an IDE State Handler Adapter
   *
   * @param {HandlerDispatcher} dispatcher - The underlying dispatcher instance
   * @param {Object} [options={}] - Configuration options
   * @param {Object} [options.logger=null] - Logger instance (optional)
   * @param {Object} [options.metrics=null] - Metrics collector (optional)
   * @param {boolean} [options.enableLogging=false] - Enable debug logging for handlers
   * @throws {Error} If dispatcher is not a HandlerDispatcher instance
   */
  constructor(dispatcher, options = {}) {
    if (!(dispatcher instanceof HandlerDispatcher)) {
      throw new Error(
        'IDEStateAdapter requires a HandlerDispatcher instance as first argument'
      );
    }

    this.dispatcher = dispatcher;
    this.logger = options.logger || this._createMockLogger();
    this.metrics = options.metrics || this._createMockMetrics();
    this.enableLogging = options.enableLogging || false;

    /** @type {Map<string, {invocations: number, errors: number, totalLatency: number}>} */
    this.handlerStats = new Map();

    this.logger.debug('IDEStateAdapter initialized');
  }

  /**
   * Get the wrapped HandlerDispatcher
   *
   * @returns {HandlerDispatcher} The underlying dispatcher
   */
  getDispatcher() {
    return this.dispatcher;
  }

  /**
   * Factory method to create a type-safe handler with automatic error wrapping
   *
   * Converts a user-provided async function into a handler compatible with
   * HandlerDispatcher. The user function receives typed inputs (data, context)
   * and returns the response payload directly. This adapter wraps the result
   * in a HandlerResponse shape (success/error) and handles all exceptions.
   *
   * Handler signature for user function:
   *   async (data, context) => Promise<any>
   *   - data: message.data (handler-specific payload)
   *   - context: {logger, metrics, server} (from dispatcher)
   *   Returns: Response payload (auto-wrapped as {success: true, data: ...})
   *
   * @param {string} messageType - Message type for this handler (e.g., "bridge:getEditorState")
   * @param {Function} fn - User handler function with signature (data, context) => Promise<any>
   * @returns {Function} Dispatcher-compatible handler: (message, context) => Promise<HandlerResponse>
   * @throws {Error} If messageType is empty or fn is not a function
   *
   * @example
   * const handler = adapter.createHandler('bridge:getEditorState', async (data, context) => {
   *   // Validate input
   *   if (!data || typeof data !== 'object') {
   *     throw new Error('Invalid request data');
   *   }
   *
   *   // User code returns the payload directly
   *   const result = await context.server.getEditorState();
   *   return result; // {activeFile, cursorLine, ...}
   * });
   *
   * // Adapter automatically wraps as:
   * // {success: true, data: {activeFile, cursorLine, ...}}
   * // On error: {success: false, error: 'Error message'}
   */
  createHandler(messageType, fn) {
    if (!messageType || typeof messageType !== 'string') {
      throw new Error(`Invalid messageType: ${messageType}`);
    }

    if (typeof fn !== 'function') {
      throw new Error(`Handler for "${messageType}" is not a function`);
    }

    // Initialize stats for this handler
    if (!this.handlerStats.has(messageType)) {
      this.handlerStats.set(messageType, {
        invocations: 0,
        errors: 0,
        totalLatency: 0,
      });
    }

    this.logger.debug(`createHandler: "${messageType}"`);

    /**
     * Dispatcher-compatible handler wrapper
     * @param {Object} message - {messageType, messageId, data}
     * @param {Object} context - {logger, metrics, server}
     * @returns {Promise<HandlerResponse>}
     */
    const wrappedHandler = async (message, context) => {
      const { messageId, data } = message;
      const startTime = Date.now();

      try {
        // Update stats
        const stats = this.handlerStats.get(messageType);
        stats.invocations += 1;

        if (this.enableLogging) {
          context.logger.debug(
            `[Handler] Executing "${messageType}"`,
            { messageId, dataKeys: data ? Object.keys(data) : [] }
          );
        }

        // Call user function
        const result = await fn(data, context);

        // Record latency
        const latency = Date.now() - startTime;
        stats.totalLatency += latency;

        if (this.enableLogging) {
          context.logger.debug(
            `[Handler] "${messageType}" completed successfully`,
            { messageId, latency }
          );
        }

        // Return wrapped response
        return {
          success: true,
          data: result,
        };
      } catch (error) {
        // Update error stats
        const stats = this.handlerStats.get(messageType);
        stats.errors += 1;

        const latency = Date.now() - startTime;
        stats.totalLatency += latency;

        const errorMsg = IDEStateAdapter.wrapHandlerError(error, messageId);

        if (this.enableLogging) {
          context.logger.warn(
            `[Handler] "${messageType}" failed`,
            { messageId, error: errorMsg.error, latency }
          );
        }

        // Record error metric
        context.metrics.recordHandlerError?.(messageType, error);

        return errorMsg;
      }
    };

    return wrappedHandler;
  }

  /**
   * Get execution statistics for a handler
   *
   * @param {string} messageType - Message type to get stats for
   * @returns {Object|null} Stats object {invocations, errors, totalLatency, avgLatency} or null if not found
   */
  getHandlerStats(messageType) {
    const stats = this.handlerStats.get(messageType);
    if (!stats) {
      return null;
    }

    return {
      ...stats,
      avgLatency: stats.invocations > 0 ? stats.totalLatency / stats.invocations : 0,
    };
  }

  /**
   * Reset statistics for a handler (useful for testing)
   *
   * @param {string} messageType - Message type to reset stats for
   * @returns {boolean} True if stats were reset, false if handler not found
   */
  resetHandlerStats(messageType) {
    if (!this.handlerStats.has(messageType)) {
      return false;
    }

    this.handlerStats.set(messageType, {
      invocations: 0,
      errors: 0,
      totalLatency: 0,
    });

    return true;
  }

  /**
   * Validate an editor state object against the EditorState schema
   *
   * Static method that can be used independently or via adapter instance.
   * Validates required fields from the EditorState typedef:
   * - activeFile (string)
   * - cursorLine (number)
   * - cursorColumn (number)
   * - fileContent (string)
   * - language (string)
   * - projectPath (string)
   *
   * Optional fields (if present, must match type):
   * - selectedText (string)
   * - selectionStart (number)
   * - selectionEnd (number)
   * - diagnosticsCount (number)
   *
   * @param {*} state - Object to validate
   * @returns {{valid: boolean, errors: string[]}} Validation result with list of errors
   *
   * @example
   * const validation = IDEStateAdapter.validateEditorState(editorState);
   * if (!validation.valid) {
   *   console.error('Invalid editor state:', validation.errors);
   * }
   */
  static validateEditorState(state) {
    const errors = [];

    // Check if state is an object
    if (!state || typeof state !== 'object') {
      return {
        valid: false,
        errors: [`EditorState must be an object, got ${typeof state}`],
      };
    }

    // Required fields with type checks
    const requiredFields = {
      activeFile: 'string',
      cursorLine: 'number',
      cursorColumn: 'number',
      fileContent: 'string',
      language: 'string',
      projectPath: 'string',
    };

    for (const [field, expectedType] of Object.entries(requiredFields)) {
      if (!(field in state)) {
        errors.push(`Missing required field: "${field}"`);
      } else if (typeof state[field] !== expectedType) {
        errors.push(
          `Field "${field}" must be ${expectedType}, ` +
          `got ${typeof state[field]}`
        );
      }
    }

    // Optional fields (if present, validate type)
    const optionalFields = {
      selectedText: 'string',
      selectionStart: 'number',
      selectionEnd: 'number',
      diagnosticsCount: 'number',
    };

    for (const [field, expectedType] of Object.entries(optionalFields)) {
      if (field in state && typeof state[field] !== expectedType) {
        errors.push(
          `Optional field "${field}" must be ${expectedType}, ` +
          `got ${typeof state[field]}`
        );
      }
    }

    return {
      valid: errors.length === 0,
      errors,
    };
  }

  /**
   * Format an error into a consistent HandlerResponse error object
   *
   * Static method that handles:
   * - Error objects: extracts message and stack
   * - Strings: uses as-is
   * - Unknown types: converts to string
   *
   * Returns HandlerResponse shape compatible with dispatcher:
   * {success: false, error: string}
   *
   * @param {*} error - Error object, string, or unknown type
   * @param {string} [messageId] - Optional messageId for correlation
   * @returns {{success: boolean, error: string}} Error response object
   *
   * @example
   * try {
   *   // ...
   * } catch (err) {
   *   return IDEStateAdapter.wrapHandlerError(err, message.messageId);
   * }
   * // Returns: {success: false, error: "Error message"}
   */
  static wrapHandlerError(error, messageId) {
    let errorMsg = 'Unknown error';

    if (error instanceof Error) {
      errorMsg = error.message || 'Unknown Error';
    } else if (typeof error === 'string') {
      errorMsg = error;
    } else if (error !== null && typeof error === 'object') {
      errorMsg = error.toString();
    } else {
      errorMsg = String(error);
    }

    return {
      success: false,
      error: errorMsg,
    };
  }

  // ============================================================================
  // Mock implementations (used when real logger/metrics not provided)
  // ============================================================================

  /**
   * @private
   * Create a mock logger for when no real logger is provided
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
   * @private
   * Create a mock metrics collector for when no real metrics are provided
   */
  _createMockMetrics() {
    return {
      recordHandlerExecution: () => {},
      recordHandlerError: () => {},
      recordHandlerMissing: () => {},
    };
  }
}

/**
 * Factory function to create a handler using the adapter
 *
 * Convenience function for Steps 50–61 handler creation.
 * Equivalent to `adapter.createHandler(messageType, fn)`.
 *
 * @param {IDEStateAdapter} adapter - Adapter instance
 * @param {string} messageType - Message type (e.g., "bridge:getEditorState")
 * @param {Function} fn - Handler function with signature (data, context) => Promise<any>
 * @returns {Function} Dispatcher-compatible handler
 *
 * @example
 * import { IDEStateAdapter, createHandler } from './handler-adapter.js';
 *
 * const handler = createHandler(adapter, 'bridge:getEditorState', async (data, ctx) => {
 *   return await ctx.server.getEditorState();
 * });
 */
export function createHandler(adapter, messageType, fn) {
  if (!(adapter instanceof IDEStateAdapter)) {
    throw new Error('First argument must be an IDEStateAdapter instance');
  }
  return adapter.createHandler(messageType, fn);
}

/**
 * Validate editor state — exported as named export for convenience
 *
 * @param {*} state - State object to validate
 * @returns {{valid: boolean, errors: string[]}} Validation result
 */
export function validateEditorState(state) {
  return IDEStateAdapter.validateEditorState(state);
}

/**
 * Wrap handler error — exported as named export for convenience
 *
 * @param {*} error - Error to wrap
 * @param {string} [messageId] - Optional correlation ID
 * @returns {{success: boolean, error: string}} Error response
 */
export function wrapHandlerError(error, messageId) {
  return IDEStateAdapter.wrapHandlerError(error, messageId);
}

export default IDEStateAdapter;
