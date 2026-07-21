#!/usr/bin/env node

/**
 * Circuit Breaker Manager Orchestrator (Step 108)
 *
 * Main orchestrator managing per-handler circuits across the entire bridge.
 * Coordinates state transitions, aggregates metrics, emits events, and
 * integrates with TimeoutManager, RateLimiter, and ErrorRecoveryMetrics.
 *
 * @module src/versions/v2.0.0/lib/circuit-breaker-manager.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 64: TimeoutManager (error rates, p99 latency)
 *   - Step 74: ErrorRecoveryMetrics (error classification)
 *   - Step 107: RateLimiter (token availability)
 *   - Step 108: CircuitBreakerMiddleware (pre/post-dispatch hooks)
 */

import { EventEmitter } from 'events';
import {
  CircuitState,
  PerHandlerCircuit,
  createDefaultConfig,
  validateConfig,
  CircuitBreakerError,
  CircuitBreakerStateError,
} from './circuit-breaker-state.mjs';
import {
  shouldOpenCircuit,
  shouldAttemptRecovery,
  shouldCloseCircuit,
  shouldReopenCircuit,
  evaluateNextTransition,
  analyzeTransitions,
} from './circuit-breaker-transitions.mjs';

// ============================================================================
// CIRCUIT BREAKER MANAGER
// ============================================================================

/**
 * Manages circuit-breaker state for all handlers in the bridge
 *
 * Features:
 *   - Per-handler independent circuits
 *   - Automatic state machine evaluation
 *   - Metrics aggregation from external sources (TimeoutManager, RateLimiter)
 *   - Event emission (stateChange, alert)
 *   - Graceful degradation if dependencies unavailable
 */
export class CircuitBreakerManager extends EventEmitter {
  /**
   * @param {CircuitBreakerConfig} config - Default config for all circuits
   * @param {Object} [deps={}] - External dependencies
   * @param {*} [deps.timeoutManager] - Step 64 TimeoutManager instance
   * @param {*} [deps.rateLimiter] - Step 107 RateLimiter instance
   * @param {*} [deps.errorRecoveryMetrics] - Step 74 ErrorRecoveryMetrics instance
   * @param {*} [deps.logger] - Logger for debug/warning messages
   * @param {*} [deps.metrics] - Metrics collector for observability
   */
  constructor(config = null, deps = {}) {
    super();

    // Configuration
    this.config = config || createDefaultConfig();
    validateConfig(this.config);

    // Dependencies
    this.timeoutManager = deps.timeoutManager;
    this.rateLimiter = deps.rateLimiter;
    this.errorRecoveryMetrics = deps.errorRecoveryMetrics;
    this.logger = deps.logger;
    this.metrics = deps.metrics;

    // Per-handler circuits
    this.circuits = new Map();

    // Manager state
    this.isRunning = false;
    this.evaluationIntervalMs = 1000; // Evaluate every 1s
    this.evaluationTimerId = null;

    // Aggregate metrics
    this.aggregateMetrics = {
      totalCircuits: 0,
      closedCircuits: 0,
      openCircuits: 0,
      halfOpenCircuits: 0,
      totalStateChanges: 0,
      totalAlerts: 0,
    };
  }

  /**
   * Start the manager (begins periodic evaluation)
   */
  start() {
    if (this.isRunning) {
      return;
    }

    this.isRunning = true;
    this._startEvaluation();
    this._log('info', 'CircuitBreakerManager started');
  }

  /**
   * Stop the manager (halts periodic evaluation)
   */
  stop() {
    if (!this.isRunning) {
      return;
    }

    this.isRunning = false;
    this._stopEvaluation();
    this._log('info', 'CircuitBreakerManager stopped');
  }

  /**
   * Get or create circuit for handler
   * @param {string} handlerType - Handler identifier
   * @param {CircuitBreakerConfig} [config] - Override default config
   * @returns {PerHandlerCircuit}
   */
  getCircuit(handlerType, config = null) {
    if (this.circuits.has(handlerType)) {
      return this.circuits.get(handlerType);
    }

    const circuitConfig = config || this.config;
    const circuit = new PerHandlerCircuit(handlerType, circuitConfig);
    this.circuits.set(handlerType, circuit);

    this.aggregateMetrics.totalCircuits = this.circuits.size;
    this._log('debug', `Created circuit for ${handlerType}`);

    return circuit;
  }

  /**
   * Check if request can be accepted for handler
   * @param {string} handlerType - Handler identifier
   * @returns {boolean} true if request should be accepted
   */
  canAcceptRequest(handlerType) {
    const circuit = this.getCircuit(handlerType);
    return !circuit.isOpen();
  }

  /**
   * Record successful request execution
   * @param {string} handlerType - Handler identifier
   * @param {number} latencyMs - Request latency
   */
  recordSuccess(handlerType, latencyMs = 0) {
    const circuit = this.getCircuit(handlerType);
    circuit.recordSuccess(latencyMs);
    this._evaluateCircuit(circuit);
  }

  /**
   * Record failed request execution
   * @param {string} handlerType - Handler identifier
   * @param {number} latencyMs - Request latency
   * @param {Error} [error] - Error object
   */
  recordFailure(handlerType, latencyMs = 0, error = null) {
    const circuit = this.getCircuit(handlerType);
    circuit.recordFailure(latencyMs);
    this._evaluateCircuit(circuit);

    if (error && this.logger) {
      this._log('debug', `Handler ${handlerType} failed: ${error.message}`);
    }
  }

  /**
   * Record request rejection (circuit was OPEN)
   * @param {string} handlerType - Handler identifier
   */
  recordRejection(handlerType) {
    const circuit = this.getCircuit(handlerType);
    circuit.recordRejection();
  }

  /**
   * Start probe request in HALF_OPEN state
   * @param {string} handlerType - Handler identifier
   * @returns {boolean} true if probe can be initiated
   */
  canStartProbe(handlerType) {
    const circuit = this.getCircuit(handlerType);
    if (!circuit.isHalfOpen() || circuit.isProbeInProgress()) {
      return false;
    }

    // Check if RateLimiter has tokens available for probe
    if (this.rateLimiter && !this.rateLimiter.canAcceptRequest(handlerType)) {
      this._log('debug', `Probe blocked for ${handlerType}: no tokens available`);
      return false;
    }

    circuit.startProbe();
    return true;
  }

  /**
   * End probe request
   * @param {string} handlerType - Handler identifier
   */
  endProbe(handlerType) {
    const circuit = this.getCircuit(handlerType);
    circuit.endProbe();
  }

  /**
   * Get state snapshot for handler
   * @param {string} handlerType - Handler identifier
   * @returns {Object}
   */
  getCircuitState(handlerType) {
    const circuit = this.getCircuit(handlerType);
    return circuit.getDetailedState();
  }

  /**
   * Get all circuit states
   * @returns {Object} Map of handlerType → state
   */
  getAllCircuitStates() {
    const states = {};
    for (const [handlerType, circuit] of this.circuits.entries()) {
      states[handlerType] = circuit.getDetailedState();
    }
    return states;
  }

  /**
   * Get aggregate metrics
   * @returns {Object}
   */
  getAggregateMetrics() {
    // Update state counts
    this.aggregateMetrics.closedCircuits = 0;
    this.aggregateMetrics.openCircuits = 0;
    this.aggregateMetrics.halfOpenCircuits = 0;

    for (const circuit of this.circuits.values()) {
      switch (circuit.getState()) {
        case CircuitState.CLOSED:
          this.aggregateMetrics.closedCircuits++;
          break;
        case CircuitState.OPEN:
          this.aggregateMetrics.openCircuits++;
          break;
        case CircuitState.HALF_OPEN:
          this.aggregateMetrics.halfOpenCircuits++;
          break;
      }
    }

    return { ...this.aggregateMetrics };
  }

  /**
   * Force circuit to specific state (for ops/debugging)
   * @param {string} handlerType - Handler identifier
   * @param {string} state - Target state
   * @param {string} reason - Reason for forced transition
   * @throws {CircuitBreakerStateError}
   */
  forceCircuitState(handlerType, state, reason = 'Manual override') {
    const circuit = this.getCircuit(handlerType);

    try {
      circuit.transitionTo(state, reason);
      this._emitStateChange(circuit, 'Manual override');
      this._log('warn', `Forced circuit transition: ${handlerType} → ${state}`);
    } catch (err) {
      this._log('error', `Failed to force state for ${handlerType}: ${err.message}`);
      throw err;
    }
  }

  /**
   * Reset circuit metrics (for testing/recovery)
   * @param {string} handlerType - Handler identifier
   */
  resetCircuit(handlerType) {
    const circuit = this.getCircuit(handlerType);
    circuit.resetMetrics();
    this._log('debug', `Reset metrics for ${handlerType}`);
  }

  // ========================================================================
  // PRIVATE METHODS
  // ========================================================================

  /**
   * Evaluate single circuit for state transitions
   * @private
   */
  _evaluateCircuit(circuit) {
    const nextState = evaluateNextTransition(circuit);
    if (!nextState || nextState === circuit.getState()) {
      return;
    }

    const reason = this._getTransitionReason(circuit, nextState);

    try {
      const oldState = circuit.getState();
      circuit.transitionTo(nextState, reason);
      this._emitStateChange(circuit, reason);
      this._log('info', `Circuit transition: ${circuit.handlerType} ${oldState} → ${nextState}`);

      // Check for alert conditions
      this._checkAlerts(circuit);
    } catch (err) {
      this._log('error', `Transition failed for ${circuit.handlerType}: ${err.message}`);
    }
  }

  /**
   * Determine reason for transition
   * @private
   */
  _getTransitionReason(circuit, nextState) {
    const current = circuit.getState();

    if (current === CircuitState.CLOSED && nextState === CircuitState.OPEN) {
      const { errorCount, errorRate } = circuit.getMetrics();
      const { failureThreshold, errorRateThreshold } = circuit.config;
      return `Error threshold: count=${errorCount}/${failureThreshold}, rate=${(errorRate * 100).toFixed(2)}%`;
    }

    if (current === CircuitState.OPEN && nextState === CircuitState.HALF_OPEN) {
      return 'Cooldown expired, attempting recovery';
    }

    if (current === CircuitState.HALF_OPEN && nextState === CircuitState.CLOSED) {
      const { successCount } = circuit.getMetrics();
      return `Recovery successful: ${successCount} consecutive successes`;
    }

    if (current === CircuitState.HALF_OPEN && nextState === CircuitState.OPEN) {
      return 'Probe failed or max attempts exceeded';
    }

    return 'State transition';
  }

  /**
   * Check alert conditions
   * @private
   */
  _checkAlerts(circuit) {
    const metrics = circuit.getMetrics();
    const { p99LatencyThreshold } = circuit.config;

    // Alert on high p99 latency
    if (metrics.p99Latency > p99LatencyThreshold) {
      this._emitAlert(circuit, 'HIGH_LATENCY', {
        p99Latency: metrics.p99Latency,
        threshold: p99LatencyThreshold,
      });
    }

    // Alert on high error rate
    if (metrics.errorRate > 0.1) {
      this._emitAlert(circuit, 'HIGH_ERROR_RATE', {
        errorRate: (metrics.errorRate * 100).toFixed(2) + '%',
      });
    }

    // Alert on circuit OPEN
    if (circuit.isOpen()) {
      this._emitAlert(circuit, 'CIRCUIT_OPEN', {
        errorCount: metrics.errorCount,
        errorRate: (metrics.errorRate * 100).toFixed(2) + '%',
      });
    }
  }

  /**
   * Emit state change event
   * @private
   */
  _emitStateChange(circuit, reason) {
    this.aggregateMetrics.totalStateChanges++;
    const event = {
      handler: circuit.handlerType,
      state: circuit.getState(),
      reason,
      metrics: circuit.getMetrics(),
      timestamp: Date.now(),
    };

    this.emit('stateChange', event);
    this._recordMetric('circuit_breaker.state_change', 1, {
      handler: circuit.handlerType,
      state: circuit.getState(),
    });
  }

  /**
   * Emit alert event
   * @private
   */
  _emitAlert(circuit, alertType, details) {
    this.aggregateMetrics.totalAlerts++;
    const event = {
      handler: circuit.handlerType,
      state: circuit.getState(),
      alertType,
      details,
      timestamp: Date.now(),
    };

    this.emit('alert', event);
    this._log('warn', `Alert for ${circuit.handlerType}: ${alertType}`);
    this._recordMetric('circuit_breaker.alert', 1, {
      handler: circuit.handlerType,
      alertType,
    });
  }

  /**
   * Start periodic evaluation loop
   * @private
   */
  _startEvaluation() {
    this.evaluationTimerId = setInterval(() => {
      for (const circuit of this.circuits.values()) {
        this._evaluateCircuit(circuit);
      }
    }, this.evaluationIntervalMs);
  }

  /**
   * Stop periodic evaluation loop
   * @private
   */
  _stopEvaluation() {
    if (this.evaluationTimerId) {
      clearInterval(this.evaluationTimerId);
      this.evaluationTimerId = null;
    }
  }

  /**
   * Log message (graceful degradation if logger unavailable)
   * @private
   */
  _log(level, message, data = null) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log({ level, message, data, source: 'CircuitBreakerManager' });
    }
  }

  /**
   * Record metric (graceful degradation if metrics unavailable)
   * @private
   */
  _recordMetric(metricName, value, tags = {}) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(metricName, value, tags);
    }
  }

  /**
   * Dispose manager resources
   */
  dispose() {
    this.stop();
    this.circuits.clear();
    this.removeAllListeners();
  }
}

// ============================================================================
// FACTORY FUNCTION
// ============================================================================

/**
 * Create circuit-breaker manager instance
 * @param {CircuitBreakerConfig} [config] - Configuration override
 * @param {Object} [deps] - External dependencies
 * @returns {CircuitBreakerManager}
 */
export function createCircuitBreakerManager(config = null, deps = {}) {
  return new CircuitBreakerManager(config, deps);
}

// ============================================================================
// EXPORTS
// ============================================================================

export default {
  CircuitBreakerManager,
  createCircuitBreakerManager,
};
