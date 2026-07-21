#!/usr/bin/env node

/**
 * Circuit Breaker Middleware for Message Chain (Step 108)
 *
 * Integrates with Step 47 MiddlewareChain to provide pre/post-dispatch hooks:
 * - Pre-dispatch: canAcceptRequest() → reject if circuit OPEN
 * - Post-execution: recordResult() → record success/failure, trigger transitions
 *
 * Implements middleware signature compatible with MiddlewareChain.
 *
 * @module src/versions/v2.0.0/lib/circuit-breaker-middleware.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Integration Point: Step 47 MiddlewareChain
 * Consumers: Step 71 HandlerRegistry, Step 108 CircuitBreakerManager
 */

import { CircuitBreakerError } from './circuit-breaker-state.mjs';

// ============================================================================
// CIRCUIT BREAKER MIDDLEWARE
// ============================================================================

/**
 * Middleware for circuit-breaker request filtering
 *
 * Features:
 *   - Pre-dispatch: Fast-fail if circuit OPEN
 *   - Post-execution: Record result + evaluate transitions
 *   - Handles HALF_OPEN probe requests with RateLimiter coordination
 *   - Emits metrics and logs for observability
 *   - Graceful degradation if manager unavailable
 *
 * Middleware Signature (Step 47 compatible):
 *   async execute(message, next, context)
 *     - message: { messageType, messageId, data, ... }
 *     - next: () → Promise (calls next middleware or dispatcher)
 *     - context: { logger?, metrics?, server? }
 *     - returns: { handled, shouldRelay, response }
 */
export class CircuitBreakerMiddleware {
  /**
   * @param {CircuitBreakerManager} manager - Circuit-breaker manager instance
   * @param {Object} [config={}] - Middleware configuration
   * @param {*} [config.logger] - Logger instance
   * @param {*} [config.metrics] - Metrics collector
   * @param {boolean} [config.enableBlockingOnOpen=true] - Block requests when OPEN
   * @param {boolean} [config.recordMetrics=true] - Record metrics
   */
  constructor(manager, config = {}) {
    this.manager = manager;
    this.config = {
      enableBlockingOnOpen: true,
      recordMetrics: true,
      ...config,
    };
    this.logger = config.logger;
    this.metrics = config.metrics;
  }

  /**
   * Execute middleware hook (Step 47 chain signature)
   * @param {Object} message - Message object
   * @param {string} message.messageType - Handler type (e.g., 'bridge:refactor')
   * @param {string} message.messageId - Unique message ID
   * @param {*} message.data - Message payload
   * @param {Function} next - Next middleware function
   * @param {Object} context - Execution context
   * @returns {Promise<Object>} { handled, shouldRelay, response }
   */
  async execute(message, next, context = {}) {
    if (!this.manager) {
      // Manager unavailable, pass through
      return next ? await next(message) : { handled: false };
    }

    const { messageType, messageId, data } = message;
    const startTime = Date.now();

    try {
      // PRE-DISPATCH: Check circuit state
      if (!this.manager.canAcceptRequest(messageType)) {
        this._log('debug', `Circuit OPEN for ${messageType}, rejecting request ${messageId}`);
        this.manager.recordRejection(messageType);
        this._recordMetric('circuit_breaker.request_rejected', 1, { handler: messageType });

        return {
          handled: true,
          shouldRelay: true,
          response: this._buildErrorResponse(
            messageId,
            messageType,
            'Circuit breaker OPEN - handler unavailable'
          ),
        };
      }

      // Check if probe needed
      const isProbeRequest = this._shouldInitiateProbe(messageType);
      if (isProbeRequest && !this.manager.canStartProbe(messageType)) {
        this._log('debug', `Cannot start probe for ${messageType}: tokens unavailable or probe in progress`);
        return {
          handled: true,
          shouldRelay: true,
          response: this._buildErrorResponse(
            messageId,
            messageType,
            'Circuit breaker probing - temporarily unavailable'
          ),
        };
      }

      // Mark probe started
      if (isProbeRequest) {
        this._log('debug', `Starting circuit probe for ${messageType}`);
      }

      // PASS THROUGH: Call next middleware/dispatcher
      let response;
      let error = null;
      let success = false;

      try {
        response = await (next ? next(message) : { handled: false });
        success = !response.error && response.success !== false;
      } catch (err) {
        error = err;
        response = this._buildErrorResponse(
          messageId,
          messageType,
          error.message,
          error
        );
      }

      // POST-EXECUTION: Record result
      const latency = Date.now() - startTime;

      if (success) {
        this.manager.recordSuccess(messageType, latency);
        this._log('debug', `Handler ${messageType} succeeded (${latency}ms)`);
        this._recordMetric('circuit_breaker.request_success', 1, { handler: messageType });
      } else {
        this.manager.recordFailure(messageType, latency, error);
        this._log('debug', `Handler ${messageType} failed (${latency}ms): ${error?.message || 'unknown error'}`);
        this._recordMetric('circuit_breaker.request_failed', 1, { handler: messageType });
      }

      // End probe if started
      if (isProbeRequest) {
        this.manager.endProbe(messageType);
        this._log('debug', `Ended circuit probe for ${messageType}, result: ${success ? 'success' : 'failure'}`);
      }

      return response;

    } catch (err) {
      this._log('error', `CircuitBreakerMiddleware error: ${err.message}`);
      this._recordMetric('circuit_breaker.middleware_error', 1, { handler: messageType });

      // Return error response
      return {
        handled: true,
        shouldRelay: true,
        response: this._buildErrorResponse(messageId, messageType, err.message, err),
      };
    }
  }

  /**
   * Check if this message should initiate a HALF_OPEN probe
   * @private
   */
  _shouldInitiateProbe(messageType) {
    const circuit = this.manager.getCircuit(messageType);
    return circuit.isHalfOpen() && !circuit.isProbeInProgress();
  }

  /**
   * Build JSON-RPC error response
   * @private
   */
  _buildErrorResponse(messageId, handlerType, message, error = null) {
    return {
      handled: true,
      shouldRelay: true,
      response: {
        jsonrpc: '2.0',
        id: messageId,
        error: {
          code: -32000, // Server error (reserved for implementation-defined errors)
          message: message,
          data: {
            handler: handlerType,
            timestamp: Date.now(),
            ...this._errorDetails(error),
          },
        },
      },
    };
  }

  /**
   * Extract error details
   * @private
   */
  _errorDetails(error) {
    if (!error) return {};

    return {
      errorCode: error.code || 'UNKNOWN',
      errorName: error.name || 'Error',
    };
  }

  /**
   * Log message (graceful degradation)
   * @private
   */
  _log(level, message, data = null) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log({ level, message, data, source: 'CircuitBreakerMiddleware' });
    }
  }

  /**
   * Record metric (graceful degradation)
   * @private
   */
  _recordMetric(metricName, value, tags = {}) {
    if (this.config.recordMetrics && this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(metricName, value, tags);
    }
  }

  /**
   * Get middleware state (for testing/debugging)
   * @returns {Object}
   */
  getState() {
    return {
      enabled: !!this.manager,
      config: this.config,
    };
  }
}

// ============================================================================
// PRE-DISPATCH HOOK (Standalone)
// ============================================================================

/**
 * Pre-dispatch hook factory (for Step 47 registration)
 * Can be registered separately if needed
 * @param {CircuitBreakerManager} manager
 * @returns {Function}
 */
export function createPreDispatchHook(manager) {
  return async (message, next, context) => {
    if (!manager) {
      return next ? await next(message) : { handled: false };
    }

    const { messageType, messageId } = message;

    if (!manager.canAcceptRequest(messageType)) {
      manager.recordRejection(messageType);
      return {
        handled: true,
        shouldRelay: true,
        response: {
          jsonrpc: '2.0',
          id: messageId,
          error: {
            code: -32000,
            message: 'Circuit breaker OPEN - handler unavailable',
            data: { handler: messageType },
          },
        },
      };
    }

    return next ? await next(message) : { handled: false };
  };
}

/**
 * Post-dispatch hook factory (for Step 47 registration)
 * Records result and evaluates transitions
 * @param {CircuitBreakerManager} manager
 * @returns {Function}
 */
export function createPostDispatchHook(manager) {
  return async (message, response, context = {}) => {
    if (!manager) {
      return response;
    }

    const { messageType } = message;
    const startTime = context.startTime || Date.now();
    const latency = Date.now() - startTime;
    const success = !response.error && response.success !== false;

    if (success) {
      manager.recordSuccess(messageType, latency);
    } else {
      const error = response.error?.message ? new Error(response.error.message) : null;
      manager.recordFailure(messageType, latency, error);
    }

    return response;
  };
}

// ============================================================================
// FACTORY FUNCTION
// ============================================================================

/**
 * Create circuit-breaker middleware instance
 * @param {CircuitBreakerManager} manager - Manager instance
 * @param {Object} [config] - Configuration
 * @returns {CircuitBreakerMiddleware}
 */
export function createCircuitBreakerMiddleware(manager, config = {}) {
  return new CircuitBreakerMiddleware(manager, config);
}

// ============================================================================
// EXPORTS
// ============================================================================

export default {
  CircuitBreakerMiddleware,
  createCircuitBreakerMiddleware,
  createPreDispatchHook,
  createPostDispatchHook,
};
