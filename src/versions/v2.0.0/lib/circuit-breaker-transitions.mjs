#!/usr/bin/env node

/**
 * Circuit Breaker State Transition Predicates (Step 108)
 *
 * Implements guards and decision logic for state machine transitions:
 * - CLOSED → OPEN: errorCount ≥ threshold OR errorRate > threshold
 * - OPEN → HALF_OPEN: cooldown expired
 * - HALF_OPEN → CLOSED: successThreshold consecutive successes
 * - HALF_OPEN → OPEN: probe fails
 *
 * @module src/versions/v2.0.0/lib/circuit-breaker-transitions.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import { CircuitState, CircuitBreakerStateError } from './circuit-breaker-state.mjs';

// ============================================================================
// STATE TRANSITION PREDICATES
// ============================================================================

/**
 * Check if CLOSED circuit should transition to OPEN
 * Criteria:
 *   1. Error count ≥ failureThreshold, OR
 *   2. Error rate > errorRateThreshold, OR
 *   3. Consecutive failures ≥ failureThreshold
 *
 * @param {PerHandlerCircuit} circuit - Circuit to evaluate
 * @returns {boolean}
 */
export function shouldOpenCircuit(circuit) {
  if (circuit.getState() !== CircuitState.CLOSED) {
    return false;
  }

  const { errorCount, consecutiveFailures } = circuit.metrics;
  const { failureThreshold, errorRateThreshold } = circuit.config;
  const errorRate = circuit.getErrorRate();

  // Criterion 1: Error count threshold
  if (errorCount >= failureThreshold) {
    return true;
  }

  // Criterion 2: Error rate threshold (if totalRequests > 0)
  if (circuit.metrics.totalRequests > 0 && errorRate > errorRateThreshold) {
    return true;
  }

  // Criterion 3: Consecutive failures threshold
  if (consecutiveFailures >= failureThreshold) {
    return true;
  }

  return false;
}

/**
 * Check if OPEN circuit should transition to HALF_OPEN
 * Criteria:
 *   1. Cooldown period has elapsed (timeoutMs)
 *
 * @param {PerHandlerCircuit} circuit - Circuit to evaluate
 * @returns {boolean}
 */
export function shouldAttemptRecovery(circuit) {
  if (circuit.getState() !== CircuitState.OPEN) {
    return false;
  }

  const timeSinceOpen = Date.now() - circuit.lastStateChange;
  return timeSinceOpen >= circuit.config.timeoutMs;
}

/**
 * Check if HALF_OPEN circuit should transition to CLOSED
 * Criteria:
 *   1. Consecutive successes ≥ successThreshold
 *   2. Not already at max probe attempts
 *
 * @param {PerHandlerCircuit} circuit - Circuit to evaluate
 * @returns {boolean}
 */
export function shouldCloseCircuit(circuit) {
  if (circuit.getState() !== CircuitState.HALF_OPEN) {
    return false;
  }

  const { successCount } = circuit.metrics;
  const { successThreshold, maxRetries } = circuit.config;

  // Check if probe attempts exceeded
  if (circuit.metrics.probeAttempts > maxRetries) {
    return false;
  }

  return successCount >= successThreshold;
}

/**
 * Check if HALF_OPEN circuit should transition back to OPEN
 * Criteria:
 *   1. Any failure in HALF_OPEN state, OR
 *   2. Max probe attempts reached without enough successes
 *
 * @param {PerHandlerCircuit} circuit - Circuit to evaluate
 * @returns {boolean}
 */
export function shouldReopenCircuit(circuit) {
  if (circuit.getState() !== CircuitState.HALF_OPEN) {
    return false;
  }

  const { errorCount, probeAttempts } = circuit.metrics;
  const { maxRetries } = circuit.config;

  // Criterion 1: Any failure in probe phase
  if (errorCount > 0) {
    return true;
  }

  // Criterion 2: Max probe attempts reached
  if (probeAttempts >= maxRetries) {
    return true;
  }

  return false;
}

// ============================================================================
// TRANSITION EXECUTION HELPERS
// ============================================================================

/**
 * Execute CLOSED → OPEN transition
 * @param {PerHandlerCircuit} circuit
 * @param {string} reason - Why circuit opened
 * @throws {CircuitBreakerStateError}
 */
export function transitionToOpen(circuit, reason = 'Error threshold exceeded') {
  if (!shouldOpenCircuit(circuit)) {
    throw new CircuitBreakerStateError(
      `Cannot transition to OPEN: conditions not met`,
      {
        currentState: circuit.getState(),
        reason,
        metrics: circuit.getMetrics(),
      }
    );
  }

  circuit.transitionTo(CircuitState.OPEN, reason);
}

/**
 * Execute OPEN → HALF_OPEN transition
 * @param {PerHandlerCircuit} circuit
 * @throws {CircuitBreakerStateError}
 */
export function transitionToHalfOpen(circuit) {
  if (!shouldAttemptRecovery(circuit)) {
    throw new CircuitBreakerStateError(
      `Cannot transition to HALF_OPEN: cooldown not expired`,
      {
        currentState: circuit.getState(),
        timeSinceOpen: Date.now() - circuit.lastStateChange,
        requiredCooldown: circuit.config.timeoutMs,
      }
    );
  }

  circuit.transitionTo(CircuitState.HALF_OPEN, 'Cooldown expired, attempting recovery');
}

/**
 * Execute HALF_OPEN → CLOSED transition
 * @param {PerHandlerCircuit} circuit
 * @throws {CircuitBreakerStateError}
 */
export function transitionToClosed(circuit) {
  if (!shouldCloseCircuit(circuit)) {
    throw new CircuitBreakerStateError(
      `Cannot transition to CLOSED: recovery not successful`,
      {
        currentState: circuit.getState(),
        successCount: circuit.metrics.successCount,
        requiredSuccesses: circuit.config.successThreshold,
        probeAttempts: circuit.metrics.probeAttempts,
        maxRetries: circuit.config.maxRetries,
      }
    );
  }

  circuit.transitionTo(CircuitState.CLOSED, 'Recovery successful');
}

/**
 * Execute HALF_OPEN → OPEN transition
 * @param {PerHandlerCircuit} circuit
 * @throws {CircuitBreakerStateError}
 */
export function transitionBackToOpen(circuit) {
  if (!shouldReopenCircuit(circuit)) {
    throw new CircuitBreakerStateError(
      `Cannot transition back to OPEN: recovery still in progress`,
      {
        currentState: circuit.getState(),
        metrics: circuit.getMetrics(),
      }
    );
  }

  let reason = 'Recovery probe failed';
  if (circuit.metrics.probeAttempts >= circuit.config.maxRetries) {
    reason = `Max probe attempts (${circuit.config.maxRetries}) exceeded`;
  }

  circuit.transitionTo(CircuitState.OPEN, reason);
}

// ============================================================================
// TRANSITION EVALUATION HELPERS
// ============================================================================

/**
 * Evaluate all possible transitions and return next state
 * Returns null if no transition should occur
 *
 * @param {PerHandlerCircuit} circuit
 * @returns {string|null} Next state or null
 */
export function evaluateNextTransition(circuit) {
  const currentState = circuit.getState();

  switch (currentState) {
    case CircuitState.CLOSED:
      return shouldOpenCircuit(circuit) ? CircuitState.OPEN : null;

    case CircuitState.OPEN:
      return shouldAttemptRecovery(circuit) ? CircuitState.HALF_OPEN : null;

    case CircuitState.HALF_OPEN:
      if (shouldCloseCircuit(circuit)) {
        return CircuitState.CLOSED;
      }
      if (shouldReopenCircuit(circuit)) {
        return CircuitState.OPEN;
      }
      return null;

    default:
      return null;
  }
}

/**
 * Get detailed transition analysis
 * @param {PerHandlerCircuit} circuit
 * @returns {Object} Analysis with possible transitions and reasons
 */
export function analyzeTransitions(circuit) {
  const state = circuit.getState();
  const metrics = circuit.getMetrics();
  const config = circuit.config;

  const analysis = {
    currentState: state,
    possibleTransitions: [],
    metrics,
    config,
  };

  if (state === CircuitState.CLOSED) {
    if (shouldOpenCircuit(circuit)) {
      analysis.possibleTransitions.push({
        to: CircuitState.OPEN,
        reason: 'Error threshold exceeded',
        conditions: {
          errorCount: `${metrics.errorCount} >= ${config.failureThreshold}`,
          errorRate: `${(metrics.errorRate * 100).toFixed(2)}% > ${(config.errorRateThreshold * 100).toFixed(2)}%`,
          consecutiveFailures: `${metrics.consecutiveFailures} >= ${config.failureThreshold}`,
        },
      });
    }
  } else if (state === CircuitState.OPEN) {
    if (shouldAttemptRecovery(circuit)) {
      const timeSinceOpen = Date.now() - circuit.lastStateChange;
      analysis.possibleTransitions.push({
        to: CircuitState.HALF_OPEN,
        reason: 'Cooldown expired, initiating recovery probe',
        conditions: {
          timeSinceOpen: `${timeSinceOpen}ms >= ${config.timeoutMs}ms`,
        },
      });
    } else {
      const timeSinceOpen = Date.now() - circuit.lastStateChange;
      analysis.nextTransitionIn = config.timeoutMs - timeSinceOpen;
    }
  } else if (state === CircuitState.HALF_OPEN) {
    if (shouldCloseCircuit(circuit)) {
      analysis.possibleTransitions.push({
        to: CircuitState.CLOSED,
        reason: 'Recovery successful',
        conditions: {
          successCount: `${metrics.successCount} >= ${config.successThreshold}`,
          probeAttempts: `${metrics.probeAttempts} <= ${config.maxRetries}`,
        },
      });
    }
    if (shouldReopenCircuit(circuit)) {
      analysis.possibleTransitions.push({
        to: CircuitState.OPEN,
        reason: 'Recovery failed',
        conditions: {
          errorCount: `${metrics.errorCount} > 0`,
          probeAttempts: `${metrics.probeAttempts} >= ${config.maxRetries}`,
        },
      });
    }
  }

  return analysis;
}

// ============================================================================
// EXPORTS
// ============================================================================

export default {
  shouldOpenCircuit,
  shouldAttemptRecovery,
  shouldCloseCircuit,
  shouldReopenCircuit,
  transitionToOpen,
  transitionToHalfOpen,
  transitionToClosed,
  transitionBackToOpen,
  evaluateNextTransition,
  analyzeTransitions,
};
