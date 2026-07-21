#!/usr/bin/env node

/**
 * Rate Limiter Middleware (Step 107)
 *
 * Provides a pre-dispatch middleware hook that enforces rate limiting on all RPC requests
 * before they reach handlers. Integrates with MiddlewareChain (Step 47) to throttle
 * incoming traffic according to per-handler policies and global ceiling.
 *
 * Responsibilities:
 * 1. **Pre-Dispatch Throttle**: Check rate limits before handler execution
 * 2. **Graceful Rejection**: Return JSON-RPC -32603 error if rate exceeded
 * 3. **Error Response**: Include detailed data (handler, tokens, refillsInMs, availableAt)
 * 4. **Metrics Recording**: Track allowed/rejected/queued requests
 * 5. **Graceful Degradation**: Handle null logger/metrics safely
 * 6. **MiddlewareChain Compatibility**: Follow middleware signature and lifecycle
 *
 * Architecture:
 * ```
 * MiddlewareChain
 *   ├─ RateLimiterMiddleware (Step 107)
 *   ├─ MessageLoggingMiddleware (Step 72)
 *   ├─ RequestValidationMiddleware (Step 73)
 *   └─ ErrorRecoveryMiddleware (Step 74)
 *
 * Request Flow:
 *   incoming message
 *     ↓
 *   RateLimiterMiddleware.canAcceptRequest(messageType)
 *     ├─ true  → next middleware
 *     └─ false → reject with -32603 ResourceExhausted
 * ```
 *
 * @module src/versions/v2.0.0/lib/rate-limiter-middleware.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 47: MiddlewareChain (middleware framework)
 *   - Step 72: MessageLoggingMiddleware (logs alongside rate limiting)
 *   - Step 73: RequestValidationMiddleware (validates after rate limiting)
 *   - Step 74: ErrorRecoveryMiddleware (recovers from rate limit errors)
 *   - Step 107: RateLimiter (core token bucket implementation)
 */

// ===== MIDDLEWARE FACTORY =====

/**
 * Create a rate limiter middleware hook for MiddlewareChain
 *
 * @param {RateLimiter} rateLimiter - RateLimiter instance from Step 107
 * @param {Object} options - Configuration options
 * @param {boolean} options.includeDetailsInError - Include token/timing details in error (default true)
 * @param {boolean} options.recordMetrics - Record rejection metrics (default true)
 * @returns {Function} Middleware hook function
 *
 * @example
 * const rateLimiter = createRateLimiter();
 * const middleware = createRateLimiterMiddleware(rateLimiter);
 * middlewareChain.use(middleware);
 */
export function createRateLimiterMiddleware(rateLimiter, options = {}) {
  const {
    includeDetailsInError = true,
    recordMetrics = true,
  } = options;

  /**
   * Middleware hook function
   * Signature: (message, next) → Promise<result>
   *
   * @param {Object} message - WebView message
   * @param {string} message.messageType - Handler type (e.g., 'bridge:complete')
   * @param {string} message.messageId - Unique request ID
   * @param {Function} next - Next middleware in chain
   * @returns {Promise<Object>} Handler result or error response
   */
  async function rateLimiterMiddleware(message, next) {
    if (!message || !message.messageType) {
      return next(message);
    }

    const { messageType, messageId } = message;

    // Check rate limit
    if (!rateLimiter.canAcceptRequest(messageType, 1)) {
      // Rate limit exceeded - return JSON-RPC error
      const result = consumeAndBuildError(messageType, messageId, includeDetailsInError);

      if (recordMetrics && rateLimiter.metrics) {
        rateLimiter.metrics.recordRejected?.(messageType);
      }

      if (rateLimiter.logger) {
        rateLimiter.logger.warn?.(
          `Rate limit middleware rejected: ${messageType} (${messageId})`
        );
      }

      return result;
    }

    // Rate limit OK - consume token and proceed
    const consumption = rateLimiter.consumeTokens(messageType, 1);

    if (recordMetrics && rateLimiter.metrics) {
      rateLimiter.metrics.recordAllowed?.(messageType);
    }

    if (rateLimiter.logger) {
      rateLimiter.logger.debug?.(
        `Rate limit middleware allowed: ${messageType} (${messageId}), tokens: ${consumption.tokens.toFixed(2)}`
      );
    }

    // Proceed to next middleware
    try {
      return await next(message);
    } catch (error) {
      // If next middleware throws, still mark metrics
      if (recordMetrics && rateLimiter.metrics) {
        rateLimiter.metrics.recordError?.(messageType);
      }
      throw error;
    }
  }

  /**
   * Consume token and build error response
   * @private
   */
  function consumeAndBuildError(messageType, messageId, includeDetails) {
    const consumption = rateLimiter.consumeTokens(messageType, 1);

    const errorData = {
      success: false,
      error: {
        code: -32603,
        message: `Rate limit exceeded for handler: ${messageType}`,
      },
    };

    if (includeDetails && consumption.error?.details) {
      const details = consumption.error.details;
      errorData.error.data = {
        handler: details.handler,
        currentTokens: details.currentTokens,
        requiredTokens: details.requiredTokens,
        refillsInMs: details.refillsInMs,
        availableAt: details.availableAt,
        globalCeiling: rateLimiter.policy?.globalCeilingPerSecond,
      };
    }

    return {
      messageId,
      messageType: 'error',
      data: errorData,
    };
  }

  // Attach limiter reference to middleware for introspection
  rateLimiterMiddleware.rateLimiter = rateLimiter;
  rateLimiterMiddleware.name = 'RateLimiterMiddleware';

  return rateLimiterMiddleware;
}

export default createRateLimiterMiddleware;
