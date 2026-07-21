#!/usr/bin/env node

/**
 * Circuit Breaker Integration Adapter (Step 108)
 *
 * Bridges CircuitBreakerManager with external dependencies:
 * - Step 64: TimeoutManager (error rates, p99 latency)
 * - Step 74: ErrorRecoveryMetrics (error classification, retry outcomes)
 * - Step 107: RateLimiter (token availability for HALF_OPEN probes)
 * - Step 25: Logger (debug/warning logs)
 * - Step 26: Metrics (observability collection)
 *
 * Provides metric consumption patterns and event coordination.
 *
 * @module src/versions/v2.0.0/lib/circuit-breaker-integration.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

// ============================================================================
// DEPENDENCY INTEGRATION PATTERNS
// ============================================================================

/**
 * Integration wrapper for TimeoutManager (Step 64)
 *
 * Consumes:
 *   - Error rate metrics per handler
 *   - p99 latency per handler
 *   - Timeout event counts
 */
export class TimeoutManagerIntegration {
  constructor(timeoutManager) {
    this.timeoutManager = timeoutManager;
  }

  /**
   * Get error rate for handler from TimeoutManager
   * @param {string} handlerType - Handler identifier
   * @returns {number} Error rate 0.0-1.0
   */
  getErrorRate(handlerType) {
    if (!this.timeoutManager) return 0;

    // Consume from TimeoutManager metrics if available
    if (typeof this.timeoutManager.getErrorRate === 'function') {
      return this.timeoutManager.getErrorRate(handlerType);
    }

    return 0;
  }

  /**
   * Get p99 latency for handler
   * @param {string} handlerType - Handler identifier
   * @returns {number} p99 latency in ms
   */
  getP99Latency(handlerType) {
    if (!this.timeoutManager) return 0;

    if (typeof this.timeoutManager.getP99Latency === 'function') {
      return this.timeoutManager.getP99Latency(handlerType);
    }

    return 0;
  }

  /**
   * Get timeout count for handler
   * @param {string} handlerType - Handler identifier
   * @returns {number}
   */
  getTimeoutCount(handlerType) {
    if (!this.timeoutManager) return 0;

    if (typeof this.timeoutManager.getTimeoutCount === 'function') {
      return this.timeoutManager.getTimeoutCount(handlerType);
    }

    return 0;
  }
}

/**
 * Integration wrapper for RateLimiter (Step 107)
 *
 * Integrations:
 *   - Check token availability before HALF_OPEN probes
 *   - Consume tokens for probe requests
 *   - Track rate-limited rejection count
 */
export class RateLimiterIntegration {
  constructor(rateLimiter) {
    this.rateLimiter = rateLimiter;
  }

  /**
   * Check if handler has tokens available for probe
   * @param {string} handlerType - Handler identifier
   * @returns {boolean}
   */
  canAcceptProbeRequest(handlerType) {
    if (!this.rateLimiter) return true;

    if (typeof this.rateLimiter.canAcceptRequest === 'function') {
      return this.rateLimiter.canAcceptRequest(handlerType);
    }

    return true;
  }

  /**
   * Consume token for probe request
   * @param {string} handlerType - Handler identifier
   * @param {number} [amount=1] - Tokens to consume
   * @returns {boolean} true if tokens consumed
   */
  consumeProbeTokens(handlerType, amount = 1) {
    if (!this.rateLimiter) return true;

    if (typeof this.rateLimiter.consumeTokens === 'function') {
      const result = this.rateLimiter.consumeTokens(handlerType, amount);
      return result.allowed;
    }

    return true;
  }

  /**
   * Get metrics from rate limiter
   * @returns {Object}
   */
  getMetrics() {
    if (!this.rateLimiter || typeof this.rateLimiter.getMetrics !== 'function') {
      return {};
    }

    return this.rateLimiter.getMetrics();
  }
}

/**
 * Integration wrapper for ErrorRecoveryMetrics (Step 74)
 *
 * Integrations:
 *   - Get error classification (transient vs permanent)
 *   - Get retry outcome statistics
 *   - Track recovery success rate
 */
export class ErrorRecoveryIntegration {
  constructor(errorRecoveryMetrics) {
    this.errorRecoveryMetrics = errorRecoveryMetrics;
  }

  /**
   * Check if error is transient (retry-able)
   * @param {Error} error - Error to classify
   * @returns {boolean}
   */
  isTransientError(error) {
    if (!this.errorRecoveryMetrics) return false;

    if (typeof this.errorRecoveryMetrics.isTransientError === 'function') {
      return this.errorRecoveryMetrics.isTransientError(error);
    }

    return false;
  }

  /**
   * Get retry success rate for handler
   * @param {string} handlerType - Handler identifier
   * @returns {number} Success rate 0.0-1.0
   */
  getRetrySuccessRate(handlerType) {
    if (!this.errorRecoveryMetrics) return 0;

    if (typeof this.errorRecoveryMetrics.getRetrySuccessRate === 'function') {
      return this.errorRecoveryMetrics.getRetrySuccessRate(handlerType);
    }

    return 0;
  }

  /**
   * Get recovery metrics
   * @returns {Object}
   */
  getMetrics() {
    if (!this.errorRecoveryMetrics || typeof this.errorRecoveryMetrics.getMetrics !== 'function') {
      return {};
    }

    return this.errorRecoveryMetrics.getMetrics();
  }
}

/**
 * Unified integration context
 * Aggregates all dependency integrations
 */
export class CircuitBreakerIntegrationContext {
  constructor(deps = {}) {
    this.timeoutManager = new TimeoutManagerIntegration(deps.timeoutManager);
    this.rateLimiter = new RateLimiterIntegration(deps.rateLimiter);
    this.errorRecovery = new ErrorRecoveryIntegration(deps.errorRecoveryMetrics);
    this.logger = deps.logger;
    this.metrics = deps.metrics;
  }

  /**
   * Get comprehensive health view for circuit decision-making
   * @param {string} handlerType - Handler identifier
   * @returns {Object} Health metrics
   */
  getHandlerHealth(handlerType) {
    return {
      errorRate: this.timeoutManager.getErrorRate(handlerType),
      p99Latency: this.timeoutManager.getP99Latency(handlerType),
      timeoutCount: this.timeoutManager.getTimeoutCount(handlerType),
      retrySuccessRate: this.errorRecovery.getRetrySuccessRate(handlerType),
      canAcceptProbe: this.rateLimiter.canAcceptProbeRequest(handlerType),
    };
  }

  /**
   * Log message through integrated logger
   * @param {string} level - Log level
   * @param {string} message - Log message
   * @param {Object} [data] - Additional data
   */
  log(level, message, data = null) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log({ level, message, data, source: 'CircuitBreakerIntegration' });
    }
  }

  /**
   * Record metric through integrated metrics collector
   * @param {string} metricName - Metric name
   * @param {number} value - Metric value
   * @param {Object} [tags] - Metric tags
   */
  recordMetric(metricName, value, tags = {}) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(metricName, value, tags);
    }
  }
}

// ============================================================================
// METRIC ENRICHMENT HELPERS
// ============================================================================

/**
 * Enrich circuit metrics with external dependency data
 * @param {PerHandlerCircuit} circuit - Circuit to enrich
 * @param {CircuitBreakerIntegrationContext} context - Integration context
 * @returns {Object} Enriched metrics
 */
export function enrichCircuitMetrics(circuit, context) {
  const baseMetrics = circuit.getMetrics();
  const externalHealth = context.getHandlerHealth(circuit.handlerType);

  return {
    ...baseMetrics,
    external: {
      errorRate: externalHealth.errorRate,
      p99Latency: externalHealth.p99Latency,
      timeoutCount: externalHealth.timeoutCount,
      retrySuccessRate: externalHealth.retrySuccessRate,
      canAcceptProbe: externalHealth.canAcceptProbe,
    },
    enrichedAt: Date.now(),
  };
}

/**
 * Determine if circuit should be opened based on external metrics
 * @param {PerHandlerCircuit} circuit - Circuit to evaluate
 * @param {CircuitBreakerIntegrationContext} context - Integration context
 * @returns {boolean} true if should open
 */
export function shouldOpenBasedOnExternalMetrics(circuit, context) {
  if (circuit.getState() !== 'CLOSED') {
    return false;
  }

  const health = context.getHandlerHealth(circuit.handlerType);
  const config = circuit.config;

  // External error rate exceeds threshold
  if (health.errorRate > config.errorRateThreshold) {
    return true;
  }

  // p99 latency spiked (indicates stress)
  if (health.p99Latency > config.p99LatencyThreshold * 2) {
    return true;
  }

  // Many timeouts recorded
  if (health.timeoutCount > config.failureThreshold) {
    return true;
  }

  return false;
}

// ============================================================================
// FACTORY FUNCTIONS
// ============================================================================

/**
 * Create integration context from dependencies
 * @param {Object} deps - Dependencies
 * @returns {CircuitBreakerIntegrationContext}
 */
export function createIntegrationContext(deps = {}) {
  return new CircuitBreakerIntegrationContext(deps);
}

// ============================================================================
// EXPORTS
// ============================================================================

export default {
  TimeoutManagerIntegration,
  RateLimiterIntegration,
  ErrorRecoveryIntegration,
  CircuitBreakerIntegrationContext,
  enrichCircuitMetrics,
  shouldOpenBasedOnExternalMetrics,
  createIntegrationContext,
};
