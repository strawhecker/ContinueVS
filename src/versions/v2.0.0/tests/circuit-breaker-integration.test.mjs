#!/usr/bin/env node

/**
 * Circuit Breaker Integration Tests (Step 108)
 *
 * Tests circuit-breaker middleware integration with handler pipeline:
 * - Pre-dispatch blocking when OPEN
 * - Post-dispatch result recording
 * - Multi-handler isolation
 * - HALF_OPEN probe coordination
 * - Concurrent request handling
 *
 * @module src/versions/v2.0.0/tests/circuit-breaker-integration.test.mjs
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import { expect } from 'chai';
import { CircuitState } from '../lib/circuit-breaker-state.mjs';
import { CircuitBreakerManager } from '../lib/circuit-breaker-manager.mjs';
import { CircuitBreakerMiddleware } from '../lib/circuit-breaker-middleware.mjs';

// ============================================================================
// MOCK HELPERS
// ============================================================================

/**
 * Create mock message
 */
function createMessage(messageType, messageId = 'msg-1', data = {}) {
  return { messageType, messageId, data };
}

/**
 * Create mock next middleware
 */
function createNextMiddleware(shouldSucceed = true, latency = 10) {
  return async (message) => {
    await new Promise((resolve) => setTimeout(resolve, latency));
    return {
      handled: true,
      shouldRelay: true,
      response: {
        jsonrpc: '2.0',
        id: message.messageId,
        ...(shouldSucceed
          ? { result: { success: true, data: 'mock result' } }
          : { error: { code: -32001, message: 'Mock error' } }),
      },
    };
  };
}

/**
 * Create mock logger
 */
function createMockLogger() {
  return {
    logs: [],
    log: function (entry) {
      this.logs.push(entry);
    },
  };
}

/**
 * Create mock metrics collector
 */
function createMockMetrics() {
  return {
    records: [],
    record: function (name, value, tags) {
      this.records.push({ name, value, tags });
    },
  };
}

// ============================================================================
// TEST SUITE 1: PRE-DISPATCH BLOCKING (4 tests)
// ============================================================================

describe('CircuitBreakerMiddleware - Pre-Dispatch Blocking', () => {
  let manager;
  let middleware;
  let logger;
  let metrics;

  beforeEach(() => {
    logger = createMockLogger();
    metrics = createMockMetrics();
    manager = new CircuitBreakerManager(null, { logger, metrics });
    middleware = new CircuitBreakerMiddleware(manager, { logger, metrics });
  });

  it('should block request when circuit OPEN', async () => {
    const circuit = manager.getCircuit('bridge:refactor');

    // Force to OPEN
    for (let i = 0; i < 5; i++) manager.recordFailure('bridge:refactor');
    manager._evaluateCircuit(circuit);

    // Execute middleware
    const msg = createMessage('bridge:refactor', 'msg-1');
    const response = await middleware.execute(msg, null, {});

    expect(response.handled).to.be.true;
    expect(response.response.error).to.exist;
    expect(response.response.error.code).to.equal(-32000);
  });

  it('should allow request when circuit CLOSED', async () => {
    const msg = createMessage('bridge:refactor', 'msg-1');
    const next = createNextMiddleware(true);

    const response = await middleware.execute(msg, next, {});

    expect(response.response.result).to.exist;
  });

  it('should record rejection when OPEN', async () => {
    const circuit = manager.getCircuit('bridge:refactor');

    for (let i = 0; i < 5; i++) manager.recordFailure('bridge:refactor');
    manager._evaluateCircuit(circuit);

    const msg = createMessage('bridge:refactor', 'msg-1');
    await middleware.execute(msg, null, {});

    const rejectionMetric = metrics.records.find((r) => r.name === 'circuit_breaker.request_rejected');
    expect(rejectionMetric).to.exist;
  });

  it('should include error context in response', async () => {
    const circuit = manager.getCircuit('bridge:analyze');

    for (let i = 0; i < 5; i++) manager.recordFailure('bridge:analyze');
    manager._evaluateCircuit(circuit);

    const msg = createMessage('bridge:analyze', 'msg-1');
    const response = await middleware.execute(msg, null, {});

    expect(response.response.error.data).to.exist;
    expect(response.response.error.data.handler).to.equal('bridge:analyze');
    expect(response.response.error.data.timestamp).to.exist;
  });
});

// ============================================================================
// TEST SUITE 2: POST-DISPATCH RESULT RECORDING (4 tests)
// ============================================================================

describe('CircuitBreakerMiddleware - Post-Dispatch Result Recording', () => {
  let manager;
  let middleware;
  let logger;
  let metrics;

  beforeEach(() => {
    logger = createMockLogger();
    metrics = createMockMetrics();
    manager = new CircuitBreakerManager(null, { logger, metrics });
    middleware = new CircuitBreakerMiddleware(manager, { logger, metrics });
  });

  it('should record success', async () => {
    const circuit = manager.getCircuit('bridge:complete');
    const msg = createMessage('bridge:complete', 'msg-1');
    const next = createNextMiddleware(true);

    await middleware.execute(msg, next, {});

    expect(circuit.metrics.successCount).to.equal(1);
    expect(circuit.metrics.errorCount).to.equal(0);
  });

  it('should record failure', async () => {
    const circuit = manager.getCircuit('bridge:complete');
    const msg = createMessage('bridge:complete', 'msg-1');
    const next = createNextMiddleware(false);

    await middleware.execute(msg, next, {});

    expect(circuit.metrics.errorCount).to.equal(1);
  });

  it('should record latency', async () => {
    const circuit = manager.getCircuit('bridge:search');
    const msg = createMessage('bridge:search', 'msg-1');
    const next = createNextMiddleware(true, 50);

    await middleware.execute(msg, next, {});

    // Latency should be at least 50ms
    expect(circuit.metrics.p99Latency).to.be.greaterThanOrEqual(40);
  });

  it('should emit success metric', async () => {
    const msg = createMessage('bridge:analyze', 'msg-1');
    const next = createNextMiddleware(true);

    await middleware.execute(msg, next, {});

    const successMetric = metrics.records.find((r) => r.name === 'circuit_breaker.request_success');
    expect(successMetric).to.exist;
  });
});

// ============================================================================
// TEST SUITE 3: MULTI-HANDLER ISOLATION (4 tests)
// ============================================================================

describe('CircuitBreakerMiddleware - Multi-Handler Isolation', () => {
  let manager;
  let middleware;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
    middleware = new CircuitBreakerMiddleware(manager);
  });

  it('handler A failure should not affect handler B', async () => {
    const circuitA = manager.getCircuit('handler_a');
    const circuitB = manager.getCircuit('handler_b');

    // Fail handler_a
    for (let i = 0; i < 5; i++) manager.recordFailure('handler_a');
    manager._evaluateCircuit(circuitA);

    // handler_b should still accept requests
    expect(manager.canAcceptRequest('handler_a')).to.be.false;
    expect(manager.canAcceptRequest('handler_b')).to.be.true;
  });

  it('should track per-handler metrics independently', async () => {
    manager.recordSuccess('handler_x', 100);
    manager.recordSuccess('handler_x', 150);
    manager.recordFailure('handler_y', 200);

    const stateX = manager.getCircuitState('handler_x');
    const stateY = manager.getCircuitState('handler_y');

    expect(stateX.metrics.successCount).to.equal(2);
    expect(stateX.metrics.errorCount).to.equal(0);
    expect(stateY.metrics.successCount).to.equal(0);
    expect(stateY.metrics.errorCount).to.equal(1);
  });

  it('should handle 20 handlers independently', () => {
    const handlers = [];
    for (let i = 1; i <= 20; i++) {
      handlers.push(`handler${i}`);
      manager.getCircuit(`handler${i}`);
    }

    // Fail half of them
    for (let i = 1; i <= 10; i++) {
      for (let j = 0; j < 5; j++) {
        manager.recordFailure(`handler${i}`);
      }
      manager._evaluateCircuit(manager.getCircuit(`handler${i}`));
    }

    const agg = manager.getAggregateMetrics();
    expect(agg.openCircuits).to.equal(10);
    expect(agg.closedCircuits).to.equal(10);
  });

  it('should transition handlers independently', (done) => {
    const c1 = manager.getCircuit('h1');
    const c2 = manager.getCircuit('h2');

    c1.config.timeoutMs = 100;
    c2.config.timeoutMs = 200;

    // Open both
    for (let i = 0; i < 5; i++) {
      manager.recordFailure('h1');
      manager.recordFailure('h2');
    }
    manager._evaluateCircuit(c1);
    manager._evaluateCircuit(c2);

    // After 150ms, only h1 should be HALF_OPEN
    setTimeout(() => {
      manager._evaluateCircuit(c1);
      manager._evaluateCircuit(c2);

      expect(c1.getState()).to.equal(CircuitState.HALF_OPEN);
      expect(c2.getState()).to.equal(CircuitState.OPEN);

      done();
    }, 150);
  });
});

// ============================================================================
// TEST SUITE 4: HALF_OPEN PROBE COORDINATION (2 tests)
// ============================================================================

describe('CircuitBreakerMiddleware - HALF_OPEN Probe Coordination', () => {
  let manager;
  let middleware;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
    middleware = new CircuitBreakerMiddleware(manager);
  });

  it('should allow single probe request in HALF_OPEN', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    expect(manager.canStartProbe('bridge:refactor')).to.be.true;
    manager.canStartProbe('bridge:refactor');
    expect(circuit.isProbeInProgress()).to.be.true;
  });

  it('should block additional probes while one in progress', () => {
    const circuit = manager.getCircuit('bridge:refactor');
    circuit.transitionTo(CircuitState.HALF_OPEN, 'Test');

    manager.canStartProbe('bridge:refactor');
    expect(manager.canStartProbe('bridge:refactor')).to.be.false;
  });
});

// ============================================================================
// TEST SUITE 5: CONCURRENT REQUEST HANDLING (2 tests)
// ============================================================================

describe('CircuitBreakerMiddleware - Concurrent Requests', () => {
  let manager;
  let middleware;

  beforeEach(() => {
    manager = new CircuitBreakerManager();
    middleware = new CircuitBreakerMiddleware(manager);
  });

  it('should handle concurrent requests safely', async () => {
    const msg1 = createMessage('bridge:search', 'msg-1');
    const msg2 = createMessage('bridge:search', 'msg-2');
    const msg3 = createMessage('bridge:search', 'msg-3');

    const next = createNextMiddleware(true);

    const results = await Promise.all([
      middleware.execute(msg1, next, {}),
      middleware.execute(msg2, next, {}),
      middleware.execute(msg3, next, {}),
    ]);

    const circuit = manager.getCircuit('bridge:search');
    expect(circuit.metrics.successCount).to.equal(3);
    expect(results.every((r) => r.response.result)).to.be.true;
  });

  it('should handle concurrent failures safely', async () => {
    const messages = [];
    const nexts = [];

    for (let i = 0; i < 5; i++) {
      messages.push(createMessage('bridge:analyze', `msg-${i}`));
      nexts.push(createNextMiddleware(false));
    }

    await Promise.all(
      messages.map((msg, idx) => middleware.execute(msg, nexts[idx], {}))
    );

    const circuit = manager.getCircuit('bridge:analyze');
    expect(circuit.metrics.errorCount).to.equal(5);
  });
});

// ============================================================================
// TEST SUITE 6: ERROR RESPONSE FORMAT (1 test)
// ============================================================================

describe('CircuitBreakerMiddleware - Error Response Format', () => {
  let middleware;

  beforeEach(() => {
    const manager = new CircuitBreakerManager();
    middleware = new CircuitBreakerMiddleware(manager);

    // Force circuit OPEN
    for (let i = 0; i < 5; i++) manager.recordFailure('bridge:refactor');
    manager._evaluateCircuit(manager.getCircuit('bridge:refactor'));
  });

  it('should return valid JSON-RPC error response', async () => {
    const msg = createMessage('bridge:refactor', 'msg-123');
    const response = await middleware.execute(msg, null, {});

    expect(response.response.jsonrpc).to.equal('2.0');
    expect(response.response.id).to.equal('msg-123');
    expect(response.response.error).to.exist;
    expect(response.response.error.code).to.equal(-32000);
    expect(response.response.error.message).to.be.a('string');
    expect(response.response.error.data).to.exist;
  });
});

export default { describe, it, beforeEach, afterEach };
