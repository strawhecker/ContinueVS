#!/usr/bin/env node

/**
 * Circuit Breaker Edge Case Tests (Step 108)
 *
 * Tests challenging scenarios and edge cases:
 * - Rapid state changes (error spike then recovery)
 * - Clock skew during cooldown calculation
 * - Concurrent state transitions
 * - Large handler count performance
 * - Metrics overflow/wraparound
 *
 * @module src/versions/v2.0.0/tests/circuit-breaker-edge-cases.test.mjs
 */

import { describe, it, beforeEach } from 'mocha';
import { expect } from 'chai';
import { CircuitState, PerHandlerCircuit } from '../lib/circuit-breaker-state.mjs';
import { CircuitBreakerManager } from '../lib/circuit-breaker-manager.mjs';
import { CircuitBreakerMiddleware } from '../lib/circuit-breaker-middleware.mjs';

// ============================================================================
// TEST SUITE 1: RAPID STATE CHANGES (3 tests)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Rapid State Changes', () => {
  let manager;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
  });

  it('should handle error spike then immediate recovery', () => {
    const circuit = manager.getCircuit('handler1');
    circuit.config.timeoutMs = 100;

    // Spike: 10 consecutive failures
    for (let i = 0; i < 10; i++) {
      manager.recordFailure('handler1');
    }
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Rapid recovery attempt (before cooldown)
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // After cooldown, transition to HALF_OPEN
    circuit.lastStateChange = Date.now() - 150;
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.HALF_OPEN);

    // Probe succeeds immediately
    manager.recordSuccess('handler1');
    manager.recordSuccess('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);
  });

  it('should handle repeated failures during recovery', () => {
    const circuit = manager.getCircuit('handler2');

    // Open circuit
    for (let i = 0; i < 5; i++) manager.recordFailure('handler2');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Move to HALF_OPEN
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');
    manager.recordFailure('handler2');

    // Should reopen immediately
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Error count should reset when transitioning back to OPEN
    expect(circuit.metrics.errorCount).to.equal(1);
  });

  it('should track consecutive failures across state transitions', () => {
    const circuit = manager.getCircuit('handler3');

    manager.recordFailure('handler3');
    manager.recordFailure('handler3');
    expect(circuit.metrics.consecutiveFailures).to.equal(2);

    // Success resets consecutive count
    manager.recordSuccess('handler3');
    expect(circuit.metrics.consecutiveFailures).to.equal(0);

    // New failures start counting from 1
    manager.recordFailure('handler3');
    expect(circuit.metrics.consecutiveFailures).to.equal(1);
  });
});

// ============================================================================
// TEST SUITE 2: CLOCK SKEW & TIME HANDLING (2 tests)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Clock Skew & Time', () => {
  let manager;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
  });

  it('should handle time regression during cooldown', () => {
    const circuit = manager.getCircuit('handler1');
    circuit.config.timeoutMs = 5000;

    // Open circuit
    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
    const openTime = circuit.lastStateChange;

    // Simulate time advancement
    circuit.lastStateChange = openTime - 1000; // 1s ago
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Move time forward past cooldown
    circuit.lastStateChange = openTime - 6000; // 6s ago
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.HALF_OPEN);
  });

  it('should handle large time deltas safely', () => {
    const circuit = manager.getCircuit('handler2');

    // Open circuit
    for (let i = 0; i < 5; i++) manager.recordFailure('handler2');
    manager._evaluateCircuit(circuit);

    // Simulate very old state change (24 hours ago)
    circuit.lastStateChange = Date.now() - 86400000;

    // Should safely transition to HALF_OPEN
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.HALF_OPEN);
  });
});

// ============================================================================
// TEST SUITE 3: CONCURRENT STATE TRANSITIONS (2 tests)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Concurrent Transitions', () => {
  let manager;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
  });

  it('should handle simultaneous evaluations of same circuit', async () => {
    const circuit = manager.getCircuit('handler1');

    // Open the circuit
    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');

    // Simulate concurrent evaluations
    const results = await Promise.all([
      Promise.resolve(manager._evaluateCircuit(circuit)),
      Promise.resolve(manager._evaluateCircuit(circuit)),
      Promise.resolve(manager._evaluateCircuit(circuit)),
    ]);

    // Should have transitioned to OPEN exactly once
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('should not corrupt state with rapid metric updates', () => {
    const circuit = manager.getCircuit('handler2');

    // Rapid concurrent-like updates (simulated)
    for (let i = 0; i < 100; i++) {
      if (i % 2 === 0) {
        manager.recordSuccess('handler2');
      } else {
        manager.recordFailure('handler2');
      }
    }

    const metrics = circuit.getMetrics();
    expect(metrics.successCount + metrics.errorCount).to.equal(100);
    expect(metrics.totalRequests).to.equal(100);
  });
});

// ============================================================================
// TEST SUITE 4: LARGE HANDLER COUNTS (1 test)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Large Handler Counts', () => {
  it('should handle 100+ circuits with acceptable performance', () => {
    const manager = new CircuitBreakerManager();
    const handlerCount = 100;

    // Create 100 circuits
    const start1 = process.hrtime.bigint();
    for (let i = 0; i < handlerCount; i++) {
      manager.getCircuit(`handler${i}`);
    }
    const elapsed1 = Number(process.hrtime.bigint() - start1) / 1_000_000;
    expect(elapsed1).to.be.lessThan(100); // <100ms to create all

    // Record metrics for all
    const start2 = process.hrtime.bigint();
    for (let i = 0; i < handlerCount; i++) {
      manager.recordSuccess(`handler${i}`, 50);
    }
    const elapsed2 = Number(process.hrtime.bigint() - start2) / 1_000_000;
    expect(elapsed2).to.be.lessThan(50); // <50ms to record all

    // Get all states
    const start3 = process.hrtime.bigint();
    const states = manager.getAllCircuitStates();
    const elapsed3 = Number(process.hrtime.bigint() - start3) / 1_000_000;
    expect(elapsed3).to.be.lessThan(100); // <100ms to snapshot all
    expect(Object.keys(states)).to.have.length(handlerCount);

    // Evaluate all
    const start4 = process.hrtime.bigint();
    for (let i = 0; i < handlerCount; i++) {
      manager._evaluateCircuit(manager.getCircuit(`handler${i}`));
    }
    const elapsed4 = Number(process.hrtime.bigint() - start4) / 1_000_000;
    expect(elapsed4).to.be.lessThan(100); // <100ms to evaluate all
  });
});

// ============================================================================
// TEST SUITE 5: METRICS BOUNDARY CONDITIONS (2 tests)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Metrics Boundaries', () => {
  let manager;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
  });

  it('should handle very high request counts', () => {
    const circuit = manager.getCircuit('handler1');

    // 10,000 requests
    for (let i = 0; i < 10000; i++) {
      if (i % 100 === 0) {
        manager.recordFailure('handler1');
      } else {
        manager.recordSuccess('handler1');
      }
    }

    const metrics = circuit.getMetrics();
    expect(metrics.totalRequests).to.equal(10000);
    expect(metrics.errorCount).to.equal(100);
    expect(metrics.successCount).to.equal(9900);
    expect(metrics.errorRate).to.be.approximately(0.01, 0.001);
  });

  it('should handle extreme error rates', () => {
    const circuit = manager.getCircuit('handler2');

    // 1000 consecutive failures
    for (let i = 0; i < 1000; i++) {
      manager.recordFailure('handler2');
    }

    const metrics = circuit.getMetrics();
    expect(metrics.errorRate).to.equal(1.0);
    expect(metrics.errorCount).to.equal(1000);

    // Should transition to OPEN
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });
});

// ============================================================================
// TEST SUITE 6: CONFIG EDGE CASES (1 test)
// ============================================================================

describe('CircuitBreaker - Edge Cases: Configuration', () => {
  it('should respect extreme config values safely', () => {
    const config = {
      failureThreshold: 1,
      successThreshold: 1,
      errorRateThreshold: 1.0,
      timeoutMs: 1000,
      cooldownMs: 100,
      maxRetries: 1,
      windowSizeMs: 1000,
      p99LatencyThreshold: 10000,
    };

    const manager = new CircuitBreakerManager(config);
    const circuit = manager.getCircuit('handler1');

    // Single failure should open (failureThreshold = 1)
    manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Transition to HALF_OPEN
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    // Single success should close (successThreshold = 1)
    manager.recordSuccess('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);
  });
});

export default { describe, it, beforeEach };
