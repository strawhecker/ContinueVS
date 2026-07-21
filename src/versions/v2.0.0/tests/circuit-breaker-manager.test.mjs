#!/usr/bin/env node

/**
 * Circuit Breaker Manager Unit Tests (Step 108)
 *
 * Comprehensive test suite covering:
 * - State machine transitions (all 6 edges)
 * - Metrics aggregation and calculation
 * - Recovery workflows
 * - Event emissions
 * - Graceful degradation
 * - Performance gates
 *
 * @module src/versions/v2.0.0/tests/circuit-breaker-manager.test.mjs
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import {
  CircuitState,
  PerHandlerCircuit,
  createDefaultConfig,
  CircuitBreakerStateError,
  CircuitBreakerConfigError,
} from '../lib/circuit-breaker-state.mjs';
import {
  shouldOpenCircuit,
  shouldAttemptRecovery,
  shouldCloseCircuit,
  shouldReopenCircuit,
  evaluateNextTransition,
} from '../lib/circuit-breaker-transitions.mjs';
import { CircuitBreakerManager, createCircuitBreakerManager } from '../lib/circuit-breaker-manager.mjs';

// ============================================================================
// TEST SUITE 1: INITIALIZATION & CONFIGURATION (4 tests)
// ============================================================================

describe('CircuitBreakerManager - Initialization & Configuration', () => {
  it('should create manager with default config', () => {
    const manager = createCircuitBreakerManager();
    expect(manager).to.exist;
    expect(manager.config).to.exist;
    expect(manager.config.failureThreshold).to.equal(5);
    expect(manager.config.successThreshold).to.equal(2);
    expect(manager.isRunning).to.be.false;
  });

  it('should accept custom config', () => {
    const customConfig = createDefaultConfig();
    customConfig.failureThreshold = 10;
    const manager = createCircuitBreakerManager(customConfig);
    expect(manager.config.failureThreshold).to.equal(10);
  });

  it('should inject dependencies', () => {
    const mockLogger = { log: () => {} };
    const mockMetrics = { record: () => {} };
    const deps = { logger: mockLogger, metrics: mockMetrics };
    const manager = createCircuitBreakerManager(null, deps);
    expect(manager.logger).to.equal(mockLogger);
    expect(manager.metrics).to.equal(mockMetrics);
  });

  it('should throw on invalid config', () => {
    const invalidConfig = createDefaultConfig();
    invalidConfig.failureThreshold = -1;
    expect(() => createCircuitBreakerManager(invalidConfig)).to.throw();
  });
});

// ============================================================================
// TEST SUITE 2: STATE TRANSITIONS (12 tests)
// ============================================================================

describe('CircuitBreakerManager - State Transitions', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('CLOSED → OPEN: error count threshold', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);

    // Record 5 failures
    for (let i = 0; i < 5; i++) {
      manager.recordFailure('bridge:refactor');
    }

    // Manually trigger evaluation
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('CLOSED → OPEN: error rate threshold', () => {
    const circuit = manager.getCircuit('bridge:analyze');
    circuit.config.errorRateThreshold = 0.2; // 20%

    // Record 3 successes, 1 failure = 25% error rate
    manager.recordSuccess('bridge:analyze');
    manager.recordSuccess('bridge:analyze');
    manager.recordSuccess('bridge:analyze');
    manager.recordFailure('bridge:analyze');

    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('OPEN → HALF_OPEN: cooldown expired', (done) => {
    const circuit = manager.getCircuit('bridge:search');
    circuit.config.timeoutMs = 100; // 100ms cooldown for fast testing

    // Force to OPEN
    manager.recordFailure('bridge:search');
    manager.recordFailure('bridge:search');
    manager.recordFailure('bridge:search');
    manager.recordFailure('bridge:search');
    manager.recordFailure('bridge:search');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Wait for cooldown
    setTimeout(() => {
      manager._evaluateCircuit(circuit);
      expect(circuit.getState()).to.equal(CircuitState.HALF_OPEN);
      done();
    }, 150);
  });

  it('HALF_OPEN → CLOSED: success threshold', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    // Record 2 successes (threshold)
    manager.recordSuccess('bridge:refactor');
    manager.recordSuccess('bridge:refactor');

    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);
  });

  it('HALF_OPEN → OPEN: probe fails', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    // Record failure in HALF_OPEN
    manager.recordFailure('bridge:refactor');

    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('CLOSED → CLOSED: no transition', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    manager.recordSuccess('bridge:refactor');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);
  });

  it('OPEN → OPEN: cooldown not expired', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.config.timeoutMs = 10000; // Long cooldown

    manager.recordFailure('bridge:refactor');
    manager.recordFailure('bridge:refactor');
    manager.recordFailure('bridge:refactor');
    manager.recordFailure('bridge:refactor');
    manager.recordFailure('bridge:refactor');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // Immediately try transition
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('should emit stateChange event on transition', (done) => {
    const circuit = manager.getCircuit('bridge:refactor');
    let eventFired = false;

    manager.on('stateChange', (event) => {
      eventFired = true;
      expect(event.handler).to.equal('bridge:refactor');
      expect(event.state).to.equal(CircuitState.OPEN);
      expect(event.reason).to.exist;
    });

    for (let i = 0; i < 5; i++) {
      manager.recordFailure('bridge:refactor');
    }
    manager._evaluateCircuit(circuit);

    expect(eventFired).to.be.true;
    done();
  });

  it('should reject requests when OPEN', () => {
    const circuit = manager.getCircuit('bridge:refactor');

    for (let i = 0; i < 5; i++) {
      manager.recordFailure('bridge:refactor');
    }
    manager._evaluateCircuit(circuit);

    expect(manager.canAcceptRequest('bridge:refactor')).to.be.false;
  });

  it('should accept requests when CLOSED', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    expect(manager.canAcceptRequest('bridge:refactor')).to.be.true;
  });

  it('should handle HALF_OPEN probe state', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    expect(manager.canStartProbe('bridge:refactor')).to.be.true;
    expect(circuit.isProbeInProgress()).to.be.false;

    manager.canStartProbe('bridge:refactor');
    expect(circuit.isProbeInProgress()).to.be.true;

    manager.endProbe('bridge:refactor');
    expect(circuit.isProbeInProgress()).to.be.false;
  });
});

// ============================================================================
// TEST SUITE 3: METRICS AGGREGATION (6 tests)
// ============================================================================

describe('CircuitBreakerManager - Metrics Aggregation', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('should track total circuits', () => {
    manager.getCircuit('handler1');
    manager.getCircuit('handler2');
    manager.getCircuit('handler3');

    const agg = manager.getAggregateMetrics();
    expect(agg.totalCircuits).to.equal(3);
  });

  it('should count state distribution', () => {
    const c1 = manager.getCircuit('handler1');
    const c2 = manager.getCircuit('handler2');
    const c3 = manager.getCircuit('handler3');

    c2.transitionTo(CircuitState.OPEN, 'Test');
    c3.transitionTo(CircuitState.HALF_OPEN, 'Test');

    const agg = manager.getAggregateMetrics();
    expect(agg.closedCircuits).to.equal(1);
    expect(agg.openCircuits).to.equal(1);
    expect(agg.halfOpenCircuits).to.equal(1);
  });

  it('should track state transitions', () => {
    const circuit = manager.getCircuit('handler1');
    const initialCount = manager.aggregateMetrics.totalStateChanges;

    for (let i = 0; i < 5; i++) {
      manager.recordFailure('handler1');
    }
    manager._evaluateCircuit(circuit);

    expect(manager.aggregateMetrics.totalStateChanges).to.be.greaterThan(initialCount);
  });

  it('should provide circuit snapshots', () => {
    manager.getCircuit('handler1');
    manager.recordSuccess('handler1', 100);

    const state = manager.getCircuitState('handler1');
    expect(state).to.exist;
    expect(state.handlerType).to.equal('handler1');
    expect(state.state).to.equal(CircuitState.CLOSED);
    expect(state.metrics).to.exist;
  });

  it('should provide all circuit states', () => {
    manager.getCircuit('handler1');
    manager.getCircuit('handler2');
    manager.getCircuit('handler3');

    const states = manager.getAllCircuitStates();
    expect(Object.keys(states)).to.have.length(3);
  });

  it('should track error rates', () => {
    manager.recordSuccess('handler1', 50);
    manager.recordSuccess('handler1', 75);
    manager.recordFailure('handler1', 200);

    const state = manager.getCircuitState('handler1');
    expect(state.metrics.errorRate).to.be.approximately(0.333, 0.01);
  });
});

// ============================================================================
// TEST SUITE 4: ERROR RATE CALCULATION (5 tests)
// ============================================================================

describe('CircuitBreakerManager - Error Rate Calculation', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('should calculate 0% error rate on success', () => {
    for (let i = 0; i < 10; i++) {
      manager.recordSuccess('handler1');
    }

    const circuit = manager.getCircuit('handler1');
    expect(circuit.getErrorRate()).to.equal(0);
  });

  it('should calculate 100% error rate on failure', () => {
    for (let i = 0; i < 5; i++) {
      manager.recordFailure('handler1');
    }

    const circuit = manager.getCircuit('handler1');
    expect(circuit.getErrorRate()).to.equal(1);
  });

  it('should calculate 50% error rate on mixed', () => {
    manager.recordSuccess('handler1');
    manager.recordSuccess('handler1');
    manager.recordFailure('handler1');
    manager.recordFailure('handler1');

    const circuit = manager.getCircuit('handler1');
    expect(circuit.getErrorRate()).to.equal(0.5);
  });

  it('should handle zero requests', () => {
    const circuit = manager.getCircuit('handler1');
    expect(circuit.getErrorRate()).to.equal(0);
  });

  it('should trigger OPEN on error rate threshold', () => {
    const circuit = manager.getCircuit('handler1');
    circuit.config.errorRateThreshold = 0.5;

    // Record 6 failures out of 10 = 60% error rate
    for (let i = 0; i < 4; i++) {
      manager.recordSuccess('handler1');
    }
    for (let i = 0; i < 6; i++) {
      manager.recordFailure('handler1');
    }

    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });
});

// ============================================================================
// TEST SUITE 5: RECOVERY SCENARIOS (6 tests)
// ============================================================================

describe('CircuitBreakerManager - Recovery Scenarios', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('full recovery: CLOSED → OPEN → HALF_OPEN → CLOSED', (done) => {
    const circuit = manager.getCircuit('handler1');
    circuit.config.timeoutMs = 100;

    // CLOSED → OPEN
    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    // OPEN → HALF_OPEN
    setTimeout(() => {
      manager._evaluateCircuit(circuit);
      expect(circuit.getState()).to.equal(CircuitState.HALF_OPEN);

      // HALF_OPEN → CLOSED
      manager.recordSuccess('handler1');
      manager.recordSuccess('handler1');
      manager._evaluateCircuit(circuit);
      expect(circuit.getState()).to.equal(CircuitState.CLOSED);

      done();
    }, 150);
  });

  it('probe failure: HALF_OPEN → OPEN', () => {
    const circuit = manager.getCircuit('handler1');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);

    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });

  it('should reset metrics on transition to CLOSED', () => {
    const circuit = manager.getCircuit('handler1');

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);
    expect(circuit.getState()).to.equal(CircuitState.OPEN);

    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');
    manager.recordSuccess('handler1');
    manager.recordSuccess('handler1');
    manager._evaluateCircuit(circuit);

    expect(circuit.metrics.errorCount).to.equal(0);
    expect(circuit.metrics.consecutiveFailures).to.equal(0);
  });

  it('should track consecutive failures', () => {
    const circuit = manager.getCircuit('handler1');

    manager.recordFailure('handler1');
    manager.recordFailure('handler1');
    expect(circuit.metrics.consecutiveFailures).to.equal(2);

    manager.recordSuccess('handler1');
    expect(circuit.metrics.consecutiveFailures).to.equal(0);

    manager.recordFailure('handler1');
    expect(circuit.metrics.consecutiveFailures).to.equal(1);
  });

  it('should emit alert on circuit open', (done) => {
    const circuit = manager.getCircuit('handler1');
    let alertFired = false;

    manager.on('alert', (event) => {
      alertFired = true;
      expect(event.alertType).to.equal('CIRCUIT_OPEN');
    });

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(circuit);

    expect(alertFired).to.be.true;
    done();
  });

  it('should allow manual circuit state override', () => {
    const circuit = manager.getCircuit('handler1');
    expect(circuit.getState()).to.equal(CircuitState.CLOSED);

    manager.forceCircuitState('handler1', CircuitState.OPEN, 'Manual test');
    expect(circuit.getState()).to.equal(CircuitState.OPEN);
  });
});

// ============================================================================
// TEST SUITE 6: GRACEFUL DEGRADATION (5 tests)
// ============================================================================

describe('CircuitBreakerManager - Graceful Degradation', () => {
  it('should work without logger', () => {
    const manager = createCircuitBreakerManager(null, { logger: null });
    expect(() => {
      manager.recordFailure('handler1');
    }).to.not.throw();
  });

  it('should work without metrics', () => {
    const manager = createCircuitBreakerManager(null, { metrics: null });
    expect(() => {
      manager.recordFailure('handler1');
    }).to.not.throw();
  });

  it('should work without timeoutManager', () => {
    const manager = createCircuitBreakerManager(null, { timeoutManager: null });
    expect(() => {
      manager.recordFailure('handler1');
    }).to.not.throw();
  });

  it('should work without rateLimiter', () => {
    const manager = createCircuitBreakerManager(null, { rateLimiter: null });
    expect(() => {
      manager.canStartProbe('handler1');
    }).to.not.throw();
  });

  it('should handle all deps null', () => {
    const manager = createCircuitBreakerManager(null, {});
    expect(() => {
      manager.start();
      manager.recordFailure('handler1');
      manager.recordSuccess('handler1');
      manager.stop();
      manager.dispose();
    }).to.not.throw();
  });
});

// ============================================================================
// TEST SUITE 7: PERFORMANCE GATES (4 tests)
// ============================================================================

describe('CircuitBreakerManager - Performance Gates', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('canAcceptRequest should be <1ms', () => {
    const start = process.hrtime.bigint();
    manager.canAcceptRequest('handler1');
    const end = process.hrtime.bigint();
    const ms = Number(end - start) / 1_000_000;
    expect(ms).to.be.lessThan(1);
  });

  it('recordSuccess should be <1ms', () => {
    const start = process.hrtime.bigint();
    manager.recordSuccess('handler1', 100);
    const end = process.hrtime.bigint();
    const ms = Number(end - start) / 1_000_000;
    expect(ms).to.be.lessThan(1);
  });

  it('state transition should be <10ms', () => {
    const circuit = manager.getCircuit('handler1');
    const start = process.hrtime.bigint();
    circuit.transitionTo(CircuitState.OPEN, 'Test');
    const end = process.hrtime.bigint();
    const ms = Number(end - start) / 1_000_000;
    expect(ms).to.be.lessThan(10);
  });

  it('getAllCircuitStates should be <50ms for 100 circuits', () => {
    for (let i = 0; i < 100; i++) {
      manager.getCircuit(`handler${i}`);
    }

    const start = process.hrtime.bigint();
    manager.getAllCircuitStates();
    const end = process.hrtime.bigint();
    const ms = Number(end - start) / 1_000_000;
    expect(ms).to.be.lessThan(50);
  });
});

// ============================================================================
// TEST SUITE 8: EVENT EMISSION (4 tests)
// ============================================================================

describe('CircuitBreakerManager - Event Emission', () => {
  let manager;

  beforeEach(() => {
    manager = createCircuitBreakerManager();
  });

  it('should emit stateChange with correlation', (done) => {
    manager.on('stateChange', (event) => {
      expect(event).to.have.property('handler');
      expect(event).to.have.property('state');
      expect(event).to.have.property('reason');
      expect(event).to.have.property('timestamp');
      done();
    });

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(manager.getCircuit('handler1'));
  });

  it('should emit alert with details', (done) => {
    manager.on('alert', (event) => {
      expect(event).to.have.property('alertType');
      expect(event).to.have.property('details');
      done();
    });

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(manager.getCircuit('handler1'));
  });

  it('should allow multiple listeners', () => {
    let count = 0;
    manager.on('stateChange', () => count++);
    manager.on('stateChange', () => count++);

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(manager.getCircuit('handler1'));

    expect(count).to.equal(2);
  });

  it('should track total state changes', () => {
    const initial = manager.aggregateMetrics.totalStateChanges;

    for (let i = 0; i < 5; i++) manager.recordFailure('handler1');
    manager._evaluateCircuit(manager.getCircuit('handler1'));

    expect(manager.aggregateMetrics.totalStateChanges).to.be.greaterThan(initial);
  });
});

export default { describe, it, beforeEach, afterEach };
