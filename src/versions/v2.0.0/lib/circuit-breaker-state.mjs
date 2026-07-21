#!/usr/bin/env node

/**
 * Circuit Breaker State Management (Step 108)
 *
 * Defines the state model for the three-state circuit-breaker pattern:
 * - CLOSED: Normal operation, requests flow through
 * - OPEN: Handler failing; requests rejected immediately
 * - HALF_OPEN: Probing recovery; 1 request allowed to test
 *
 * Each handler maintains independent circuit state with metrics tracking.
 *
 * @module src/versions/v2.0.0/lib/circuit-breaker-state.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 64: TimeoutManager (error rate metrics)
 *   - Step 74: ErrorRecoveryMetrics (error classification)
 *   - Step 107: RateLimiter (token availability for probes)
 *   - Step 108: CircuitBreakerManager (orchestrator)
 */

// ============================================================================
// CIRCUIT STATE ENUM
// ============================================================================

/**
 * Enumeration of valid circuit states
 * @enum {string}
 */
export const CircuitState = Object.freeze({
  CLOSED: 'CLOSED',
  OPEN: 'OPEN',
  HALF_OPEN: 'HALF_OPEN',
});

/**
 * Check if a state is valid
 * @param {string} state
 * @returns {boolean}
 */
export function isValidCircuitState(state) {
  return Object.values(CircuitState).includes(state);
}

// ============================================================================
// ERROR CLASSES
// ============================================================================

/**
 * Base error for circuit-breaker operations
 */
export class CircuitBreakerError extends Error {
  constructor(message, code = 'CIRCUIT_BREAKER_ERROR', details = {}) {
    super(message);
    this.name = 'CircuitBreakerError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Thrown when invalid state transition attempted
 */
export class CircuitBreakerStateError extends CircuitBreakerError {
  constructor(message, details = {}) {
    super(message, 'INVALID_STATE_TRANSITION', details);
    this.name = 'CircuitBreakerStateError';
  }
}

/**
 * Thrown when configuration is invalid
 */
export class CircuitBreakerConfigError extends CircuitBreakerError {
  constructor(message, details = {}) {
    super(message, 'INVALID_CONFIG', details);
    this.name = 'CircuitBreakerConfigError';
  }
}

// ============================================================================
// CIRCUIT BREAKER CONFIG
// ============================================================================

/**
 * Configuration model for circuit-breaker behavior
 * @typedef {Object} CircuitBreakerConfig
 * @property {number} failureThreshold - Error count to trigger OPEN (default 5)
 * @property {number} successThreshold - Consecutive successes to trigger CLOSED (default 2)
 * @property {number} errorRateThreshold - Error rate to trigger OPEN (default 0.05 = 5%)
 * @property {number} timeoutMs - Cooldown before HALF_OPEN probe (default 30000 = 30s)
 * @property {number} cooldownMs - Initial cooldown duration (default 5000 = 5s)
 * @property {number} maxRetries - Max probe attempts before staying OPEN (default 3)
 * @property {number} windowSizeMs - Metrics window for error rate calculation (default 60000 = 60s)
 * @property {number} p99LatencyThreshold - p99 latency alert threshold in ms (default 500)
 */

/**
 * Create default CircuitBreakerConfig
 * @returns {CircuitBreakerConfig}
 */
export function createDefaultConfig() {
  return {
    failureThreshold: 5,
    successThreshold: 2,
    errorRateThreshold: 0.05,
    timeoutMs: 30000,
    cooldownMs: 5000,
    maxRetries: 3,
    windowSizeMs: 60000,
    p99LatencyThreshold: 500,
  };
}

/**
 * Validate configuration values
 * @param {CircuitBreakerConfig} config
 * @throws {CircuitBreakerConfigError} if config invalid
 */
export function validateConfig(config) {
  if (!config || typeof config !== 'object') {
    throw new CircuitBreakerConfigError('Config must be an object', { config });
  }

  const { failureThreshold, successThreshold, timeoutMs, cooldownMs, errorRateThreshold } = config;

  if (!Number.isInteger(failureThreshold) || failureThreshold < 1) {
    throw new CircuitBreakerConfigError('failureThreshold must be positive integer', { failureThreshold });
  }

  if (!Number.isInteger(successThreshold) || successThreshold < 1) {
    throw new CircuitBreakerConfigError('successThreshold must be positive integer', { successThreshold });
  }

  if (timeoutMs < 1000 || timeoutMs > 600000) {
    throw new CircuitBreakerConfigError('timeoutMs must be between 1s and 600s', { timeoutMs });
  }

  if (cooldownMs < 100 || cooldownMs > 60000) {
    throw new CircuitBreakerConfigError('cooldownMs must be between 100ms and 60s', { cooldownMs });
  }

  if (errorRateThreshold < 0 || errorRateThreshold > 1) {
    throw new CircuitBreakerConfigError('errorRateThreshold must be between 0 and 1', { errorRateThreshold });
  }
}

// ============================================================================
// PER-HANDLER CIRCUIT STATE
// ============================================================================

/**
 * Metrics tracked per handler circuit
 * @typedef {Object} CircuitMetrics
 * @property {number} errorCount - Total errors in current window
 * @property {number} successCount - Total successes in current window
 * @property {number} consecutiveFailures - Failures without success
 * @property {number} probeAttempts - Probe attempts in HALF_OPEN
 * @property {number} lastStateChange - Timestamp of last state transition
 * @property {number} windowStartTime - Start of metrics window
 * @property {number} totalRequests - Total requests processed
 * @property {number} p99Latency - p99 latency in ms
 */

/**
 * Per-handler circuit state container
 * Maintains circuit state, metrics, and configuration for one handler
 */
export class PerHandlerCircuit {
  /**
   * @param {string} handlerType - Handler identifier (e.g., 'bridge:refactor')
   * @param {CircuitBreakerConfig} config - Configuration for this circuit
   */
  constructor(handlerType, config) {
    if (!handlerType || typeof handlerType !== 'string') {
      throw new CircuitBreakerConfigError('handlerType must be non-empty string', { handlerType });
    }

    validateConfig(config);

    this.handlerType = handlerType;
    this.config = config;
    this.state = CircuitState.CLOSED;
    this.lastStateChange = Date.now();
    this.stateChangeReason = 'Initialized';

    // Metrics
    this.metrics = {
      errorCount: 0,
      successCount: 0,
      consecutiveFailures: 0,
      probeAttempts: 0,
      windowStartTime: Date.now(),
      totalRequests: 0,
      p99Latency: 0,
    };

    // Probe tracking
    this.probeInProgress = false;
    this.lastProbeTime = null;
  }

  /**
   * Get current state
   * @returns {string}
   */
  getState() {
    return this.state;
  }

  /**
   * Check if circuit is OPEN (rejecting requests)
   * @returns {boolean}
   */
  isOpen() {
    return this.state === CircuitState.OPEN;
  }

  /**
   * Check if circuit is CLOSED (accepting requests)
   * @returns {boolean}
   */
  isClosed() {
    return this.state === CircuitState.CLOSED;
  }

  /**
   * Check if circuit is HALF_OPEN (probing)
   * @returns {boolean}
   */
  isHalfOpen() {
    return this.state === CircuitState.HALF_OPEN;
  }

  /**
   * Transition to new state with reason
   * @param {string} newState - Target state
   * @param {string} reason - Transition reason
   * @throws {CircuitBreakerStateError} if invalid transition
   */
  transitionTo(newState, reason = '') {
    if (!isValidCircuitState(newState)) {
      throw new CircuitBreakerStateError('Invalid target state', { newState });
    }

    const oldState = this.state;

    // Validate state machine edges
    const validTransitions = {
      [CircuitState.CLOSED]: [CircuitState.OPEN],
      [CircuitState.OPEN]: [CircuitState.HALF_OPEN],
      [CircuitState.HALF_OPEN]: [CircuitState.CLOSED, CircuitState.OPEN],
    };

    if (!validTransitions[oldState].includes(newState)) {
      throw new CircuitBreakerStateError('Invalid state transition', {
        from: oldState,
        to: newState,
        reason,
      });
    }

    this.state = newState;
    this.lastStateChange = Date.now();
    this.stateChangeReason = reason;

    // Reset probe when leaving HALF_OPEN
    if (oldState === CircuitState.HALF_OPEN) {
      this.probeInProgress = false;
      this.lastProbeTime = null;
    }

    // Reset error count when transitioning to CLOSED
    if (newState === CircuitState.CLOSED) {
      this.metrics.errorCount = 0;
      this.metrics.consecutiveFailures = 0;
      this.metrics.probeAttempts = 0;
    }
  }

  /**
   * Record successful request
   * @param {number} latencyMs - Request latency
   */
  recordSuccess(latencyMs = 0) {
    this.metrics.successCount++;
    this.metrics.totalRequests++;
    this.metrics.consecutiveFailures = 0;
    if (latencyMs > 0) {
      this.metrics.p99Latency = Math.max(this.metrics.p99Latency, latencyMs);
    }
  }

  /**
   * Record failed request
   * @param {number} latencyMs - Request latency
   */
  recordFailure(latencyMs = 0) {
    this.metrics.errorCount++;
    this.metrics.totalRequests++;
    this.metrics.consecutiveFailures++;
    if (latencyMs > 0) {
      this.metrics.p99Latency = Math.max(this.metrics.p99Latency, latencyMs);
    }
  }

  /**
   * Record request rejection (when circuit OPEN)
   */
  recordRejection() {
    this.metrics.totalRequests++;
    this.metrics.errorCount++;
  }

  /**
   * Get error rate (errors / total requests)
   * @returns {number} Error rate 0.0-1.0
   */
  getErrorRate() {
    if (this.metrics.totalRequests === 0) return 0;
    return this.metrics.errorCount / this.metrics.totalRequests;
  }

  /**
   * Get metrics snapshot
   * @returns {Object}
   */
  getMetrics() {
    return {
      handlerType: this.handlerType,
      state: this.state,
      lastStateChange: this.lastStateChange,
      stateChangeReason: this.stateChangeReason,
      errorCount: this.metrics.errorCount,
      successCount: this.metrics.successCount,
      consecutiveFailures: this.metrics.consecutiveFailures,
      totalRequests: this.metrics.totalRequests,
      errorRate: this.getErrorRate(),
      p99Latency: this.metrics.p99Latency,
      probeAttempts: this.metrics.probeAttempts,
    };
  }

  /**
   * Reset metrics for new window
   */
  resetMetrics() {
    this.metrics.errorCount = 0;
    this.metrics.successCount = 0;
    this.metrics.windowStartTime = Date.now();
  }

  /**
   * Check if circuit should be reset (time-based recovery attempt)
   * @returns {boolean}
   */
  shouldAttemptRecovery() {
    if (this.state !== CircuitState.OPEN) return false;
    const timeSinceOpen = Date.now() - this.lastStateChange;
    return timeSinceOpen >= this.config.timeoutMs;
  }

  /**
   * Mark probe as in-progress
   */
  startProbe() {
    this.probeInProgress = true;
    this.lastProbeTime = Date.now();
    this.metrics.probeAttempts++;
  }

  /**
   * Clear probe state
   */
  endProbe() {
    this.probeInProgress = false;
  }

  /**
   * Check if probe is currently in-progress
   * @returns {boolean}
   */
  isProbeInProgress() {
    return this.probeInProgress;
  }

  /**
   * Get current state with detailed info
   * @returns {Object}
   */
  getDetailedState() {
    return {
      handlerType: this.handlerType,
      state: this.state,
      isClosed: this.isClosed(),
      isOpen: this.isOpen(),
      isHalfOpen: this.isHalfOpen(),
      metrics: this.getMetrics(),
      config: this.config,
      timeSinceStateChange: Date.now() - this.lastStateChange,
      canAttemptRecovery: this.shouldAttemptRecovery(),
    };
  }
}

// ============================================================================
// EXPORTS
// ============================================================================

export default {
  CircuitState,
  isValidCircuitState,
  CircuitBreakerError,
  CircuitBreakerStateError,
  CircuitBreakerConfigError,
  createDefaultConfig,
  validateConfig,
  PerHandlerCircuit,
};
