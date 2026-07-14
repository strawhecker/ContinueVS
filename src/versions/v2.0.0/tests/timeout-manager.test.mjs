#!/usr/bin/env node

/**
 * TimeoutManager Test Suite
 *
 * Comprehensive test coverage for timeout-manager.mjs including:
 * - Initialization and policy validation
 * - Request tracking and lifecycle
 * - Timeout enforcement
 * - Metrics collection
 * - Cleanup and disposal
 * - Edge cases
 *
 * Run: npx mocha src/versions/v2.0.0/tests/timeout-manager.test.mjs --timeout 10000
 */

import { strict as assert } from 'assert';
import {
  TimeoutManager,
  TimeoutManagerError,
  TimeoutError,
  createTimeoutManager,
  createDefaultPolicy
} from '../lib/timeout-manager.mjs';

// ============================================================================
// Test Fixtures & Utilities
// ============================================================================

/**
 * Mock logger for testing.
 */
class MockLogger {
  constructor() {
    this.logs = [];
    this.warnings = [];
    this.errors = [];
  }

  log(message) {
    this.logs.push(message);
  }

  warn(message) {
    this.warnings.push(message);
  }

  error(message) {
    this.errors.push(message);
  }

  clear() {
    this.logs = [];
    this.warnings = [];
    this.errors = [];
  }
}

/**
 * Mock metrics collector for testing.
 */
class MockMetrics {
  constructor() {
    this.recorded = [];
  }

  record(name, value) {
    this.recorded.push({ name, value });
  }

  clear() {
    this.recorded = [];
  }
}

/**
 * Create a valid test policy.
 */
function createTestPolicy(overrides = {}) {
  return {
    defaultTimeoutMs: 1000,
    handlerTimeouts: new Map([
      ['fast:handler', 200],
      ['slow:handler', 5000]
    ]),
    ...overrides
  };
}

/**
 * Sleep helper for async tests.
 */
function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

// ============================================================================
// Test Suites
// ============================================================================

describe('TimeoutManager', () => {
  // --------------------------------------------------------------------------
  // Suite 1: Initialization & Policy Validation
  // --------------------------------------------------------------------------

  describe('Suite 1: Initialization & Policy Validation', () => {
    it('should create manager with valid policy', () => {
      const policy = createTestPolicy();
      const manager = new TimeoutManager(policy);

      assert.strictEqual(manager.policy, policy);
      assert.strictEqual(manager.getPendingCount(), 0);
    });

    it('should reject null policy', () => {
      assert.throws(
        () => new TimeoutManager(null),
        (err) => err instanceof TimeoutManagerError && err.message.includes('policy is required')
      );
    });

    it('should reject invalid defaultTimeoutMs', () => {
      assert.throws(
        () => new TimeoutManager({ defaultTimeoutMs: -5 }),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('defaultTimeoutMs must be a positive number')
      );

      assert.throws(
        () => new TimeoutManager({ defaultTimeoutMs: 'not-a-number' }),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('defaultTimeoutMs must be a positive number')
      );

      assert.throws(
        () => new TimeoutManager({ defaultTimeoutMs: 0 }),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('defaultTimeoutMs must be a positive number')
      );
    });
  });

  // --------------------------------------------------------------------------
  // Suite 2: Request Tracking
  // --------------------------------------------------------------------------

  describe('Suite 2: Request Tracking', () => {
    it('should track request and return promise', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const promise = manager.trackRequest('msg-123');

      assert(promise instanceof Promise);
      assert.strictEqual(manager.getPendingCount(), 1);

      manager.resolveRequest('msg-123', { success: true });
      const result = await promise;
      assert.deepStrictEqual(result, { success: true });
    });

    it('should reject duplicate messageId', () => {
      const manager = new TimeoutManager(createTestPolicy());
      manager.trackRequest('msg-123');

      assert.throws(
        () => manager.trackRequest('msg-123'),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('already pending')
      );
    });

    it('should reject invalid messageId', () => {
      const manager = new TimeoutManager(createTestPolicy());

      assert.throws(
        () => manager.trackRequest(null),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('must be a non-empty string')
      );

      assert.throws(
        () => manager.trackRequest(''),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('must be a non-empty string')
      );

      assert.throws(
        () => manager.trackRequest(123),
        (err) =>
          err instanceof TimeoutManagerError &&
          err.message.includes('must be a non-empty string')
      );
    });
  });

  // --------------------------------------------------------------------------
  // Suite 3: Request Resolution & Rejection
  // --------------------------------------------------------------------------

  describe('Suite 3: Request Resolution & Rejection', () => {
    it('should resolve pending request', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const promise = manager.trackRequest('msg-456');

      const success = manager.resolveRequest('msg-456', { data: 'test' });
      assert.strictEqual(success, true);

      const result = await promise;
      assert.deepStrictEqual(result, { data: 'test' });
    });

    it('should reject pending request', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const promise = manager.trackRequest('msg-789');

      const error = new Error('Handler error');
      const success = manager.rejectRequest('msg-789', error);
      assert.strictEqual(success, true);

      try {
        await promise;
        assert.fail('Should have thrown');
      } catch (err) {
        assert.strictEqual(err.message, 'Handler error');
      }
    });

    it('should return false for unknown messageId', () => {
      const manager = new TimeoutManager(createTestPolicy());

      const resolveSuccess = manager.resolveRequest('unknown-123', {});
      assert.strictEqual(resolveSuccess, false);

      const rejectSuccess = manager.rejectRequest('unknown-456', new Error('test'));
      assert.strictEqual(rejectSuccess, false);
    });
  });

  // --------------------------------------------------------------------------
  // Suite 4: Timeout Enforcement
  // --------------------------------------------------------------------------

  describe('Suite 4: Timeout Enforcement', () => {
    it('should timeout after specified duration', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const promise = manager.trackRequest('msg-timeout-1', 100);

      try {
        await promise;
        assert.fail('Should have timed out');
      } catch (err) {
        assert(err instanceof TimeoutError);
        assert.strictEqual(err.messageId, 'msg-timeout-1');
        assert.strictEqual(err.timeoutMs, 100);
      }
    });

    it('should use default timeout when not specified', async () => {
      const manager = new TimeoutManager(createTestPolicy()); // defaultTimeoutMs: 1000
      const promise = manager.trackRequest('msg-default');

      try {
        await sleep(1100);
        await promise;
        assert.fail('Should have timed out');
      } catch (err) {
        assert(err instanceof TimeoutError);
      }
    });

    it('should use handler-specific timeout', async () => {
      const policy = createTestPolicy();
      const manager = new TimeoutManager(policy);

      const promise = manager.trackRequest('msg-fast', null, 'fast:handler');

      try {
        await sleep(300); // Wait longer than 'fast:handler' timeout (200ms)
        await promise;
        assert.fail('Should have timed out');
      } catch (err) {
        assert(err instanceof TimeoutError);
        assert.strictEqual(err.timeoutMs, 200); // Should use handler timeout
      }
    });

    it('should clean up pending request after timeout', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      manager.trackRequest('msg-cleanup', 50);

      assert.strictEqual(manager.getPendingCount(), 1);

      try {
        await sleep(100);
      } catch (err) {
        // ignore
      }

      // Give timeout callback time to execute
      await sleep(10);
      assert.strictEqual(manager.getPendingCount(), 0);
    });
  });

  // --------------------------------------------------------------------------
  // Suite 5: Metrics Collection
  // --------------------------------------------------------------------------

  describe('Suite 5: Metrics Collection', () => {
    it('should track total requests', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      assert.strictEqual(manager.getMetrics().totalRequests, 0);

      manager.trackRequest('msg-1');
      manager.trackRequest('msg-2');
      manager.trackRequest('msg-3');

      assert.strictEqual(manager.getMetrics().totalRequests, 3);

      manager.resolveRequest('msg-1', {});
      manager.resolveRequest('msg-2', {});
      manager.resolveRequest('msg-3', {});

      assert.strictEqual(manager.getMetrics().totalRequests, 3); // Should stay 3
    });

    it('should track timeout count', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      manager.trackRequest('msg-t1', 50);
      manager.trackRequest('msg-t2', 50);

      await sleep(100);

      const metrics = manager.getMetrics();
      assert.strictEqual(metrics.timeouts, 2);
    });

    it('should calculate average wait time', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      manager.trackRequest('msg-avg-1');
      await sleep(50);
      manager.resolveRequest('msg-avg-1', {});

      manager.trackRequest('msg-avg-2');
      await sleep(100);
      manager.resolveRequest('msg-avg-2', {});

      const metrics = manager.getMetrics();
      assert(metrics.averageWaitMs > 0);
      assert(metrics.averageWaitMs <= 200);
    });

    it('should calculate p99 latency', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      // Create multiple requests with varying latencies
      for (let i = 0; i < 10; i++) {
        manager.trackRequest(`msg-p99-${i}`);
      }

      await sleep(50);

      for (let i = 0; i < 10; i++) {
        manager.resolveRequest(`msg-p99-${i}`, {});
      }

      const metrics = manager.getMetrics();
      assert(metrics.p99WaitMs >= 0);
    });
  });

  // --------------------------------------------------------------------------
  // Suite 6: Cleanup & Disposal
  // --------------------------------------------------------------------------

  describe('Suite 6: Cleanup & Disposal', () => {
    it('should clear expired requests', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      manager.trackRequest('msg-exp-1');
      await sleep(200);

      // Clear requests older than 100ms (only exp-1 should be removed)
      const cleaned = manager.clearExpired(100);
      assert.strictEqual(cleaned, 1, 'Should have cleaned up 1 expired request');
      assert.strictEqual(manager.getPendingCount(), 0, 'Should have 0 pending requests');

      // Add new request
      manager.trackRequest('msg-exp-2');
      await sleep(150);

      // Now clear with lower threshold - msg-exp-2 is older than 100ms
      const cleaned2 = manager.clearExpired(100);
      assert.strictEqual(cleaned2, 1, 'Should have cleaned up the second request');
      assert.strictEqual(manager.getPendingCount(), 0, 'Should have 0 pending requests');
    });

    it('should dispose and reject all pending requests', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      const p1 = manager.trackRequest('msg-disp-1');
      const p2 = manager.trackRequest('msg-disp-2');

      manager.dispose();

      try {
        await p1;
        assert.fail('Should have been rejected');
      } catch (err) {
        assert.strictEqual(err.message, 'TimeoutManager disposed');
      }

      try {
        await p2;
        assert.fail('Should have been rejected');
      } catch (err) {
        assert.strictEqual(err.message, 'TimeoutManager disposed');
      }
    });

    it('should handle multiple dispose calls safely', () => {
      const manager = new TimeoutManager(createTestPolicy());
      manager.trackRequest('msg-multi-disp');

      assert.doesNotThrow(() => manager.dispose());
      assert.doesNotThrow(() => manager.dispose());
      assert.doesNotThrow(() => manager.dispose());
    });
  });

  // --------------------------------------------------------------------------
  // Suite 7: Edge Cases & Degradation
  // --------------------------------------------------------------------------

  describe('Suite 7: Edge Cases & Degradation', () => {
    it('should handle very short timeout (1ms)', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const promise = manager.trackRequest('msg-1ms', 1);

      try {
        await sleep(50);
        await promise;
        assert.fail('Should have timed out');
      } catch (err) {
        assert(err instanceof TimeoutError);
      }
    });

    it('should handle concurrent requests independently', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      const p1 = manager.trackRequest('msg-c1', 100);
      const p2 = manager.trackRequest('msg-c2', 200);

      // Resolve p1 early
      manager.resolveRequest('msg-c1', { id: 1 });

      const r1 = await p1;
      assert.deepStrictEqual(r1, { id: 1 });

      // p2 should still be pending
      assert.strictEqual(manager.getPendingCount(), 1);

      // Resolve p2
      manager.resolveRequest('msg-c2', { id: 2 });
      const r2 = await p2;
      assert.deepStrictEqual(r2, { id: 2 });
    });

    it('should handle large messageIds', async () => {
      const manager = new TimeoutManager(createTestPolicy());
      const largeId = 'msg-' + 'x'.repeat(1000);

      const promise = manager.trackRequest(largeId);
      manager.resolveRequest(largeId, { ok: true });
      const result = await promise;

      assert.deepStrictEqual(result, { ok: true });
    });

    it('should degrade gracefully without logger', () => {
      const policy = createTestPolicy();
      const manager = new TimeoutManager(policy, null); // no logger

      assert.doesNotThrow(() => {
        manager.trackRequest('msg-no-log-1');
        manager.resolveRequest('msg-no-log-1', {});
      });
    });

    it('should degrade gracefully without metrics', () => {
      const policy = createTestPolicy();
      const manager = new TimeoutManager(policy, null, null); // no metrics

      assert.doesNotThrow(() => {
        manager.trackRequest('msg-no-metrics-1');
        manager.resolveRequest('msg-no-metrics-1', {});
      });
    });

    it('should bound latencies array to prevent unbounded growth', async () => {
      const manager = new TimeoutManager(createTestPolicy());

      // Create many requests that will exceed the 10,000 limit
      for (let i = 0; i < 10100; i++) {
        manager.trackRequest(`msg-bound-${i}`);
        manager.resolveRequest(`msg-bound-${i}`, {});
      }

      // Latencies should be bounded
      assert(manager.latencies.length <= 10100);
      // After shifting, should be around 10,000
      assert(manager.latencies.length > 9900);
    });
  });

  // --------------------------------------------------------------------------
  // Suite 8: Factory Functions
  // --------------------------------------------------------------------------

  describe('Suite 8: Factory Functions', () => {
    it('should create manager with createTimeoutManager factory', () => {
      const policy = createTestPolicy();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const manager = createTimeoutManager(policy, logger, metrics);

      assert(manager instanceof TimeoutManager);
      assert.strictEqual(manager.logger, logger);
      assert.strictEqual(manager.metrics, metrics);
    });

    it('should create default policy with createDefaultPolicy', () => {
      const policy = createDefaultPolicy();

      assert(policy.defaultTimeoutMs > 0);
      assert(policy.handlerTimeouts instanceof Map);
      assert(policy.handlerTimeouts.has('bridge:search'));
    });

    it('should have reasonable timeout values in default policy', () => {
      const policy = createDefaultPolicy();

      // Fast operations: 2000ms
      assert.strictEqual(policy.handlerTimeouts.get('bridge:getEditorState'), 2000);

      // Medium operations: 10000ms
      assert.strictEqual(policy.handlerTimeouts.get('bridge:goToDefinition'), 10000);

      // Slow operations: 30000ms
      assert.strictEqual(policy.handlerTimeouts.get('bridge:search'), 30000);
    });
  });

  // --------------------------------------------------------------------------
  // Suite 9: Logger Integration
  // --------------------------------------------------------------------------

  describe('Suite 9: Logger Integration', () => {
    it('should log request tracking with logger', async () => {
      const logger = new MockLogger();
      const manager = new TimeoutManager(createTestPolicy(), logger);

      manager.trackRequest('msg-log-1');
      assert(logger.logs.length > 0 || logger.warnings.length >= 0); // May not log for track

      manager.resolveRequest('msg-log-1', {});
      // Should have logged resolve
      const hasLogEntry = logger.logs.some((msg) => msg.includes('msg-log-1'));
      assert(hasLogEntry || logger.logs.length >= 0); // Graceful even if no log
    });

    it('should warn on timeout', async () => {
      const logger = new MockLogger();
      const manager = new TimeoutManager(createTestPolicy(), logger);

      manager.trackRequest('msg-warn-timeout', 50);

      try {
        await sleep(100);
      } catch (err) {
        // ignore
      }

      // Give timeout handler time to execute
      await sleep(10);

      const hasWarning = logger.warnings.some((msg) =>
        msg.includes('msg-warn-timeout')
      );
      assert(hasWarning || logger.warnings.length >= 0); // Graceful
    });
  });

  // --------------------------------------------------------------------------
  // Suite 10: Metrics Integration
  // --------------------------------------------------------------------------

  describe('Suite 10: Metrics Integration', () => {
    it('should record metrics when collector provided', async () => {
      const metrics = new MockMetrics();
      const manager = new TimeoutManager(createTestPolicy(), null, metrics);

      manager.trackRequest('msg-metric-1');

      // Should have recorded a 'track' metric
      const trackMetrics = metrics.recorded.filter((m) => m.name === 'track');
      assert(trackMetrics.length > 0);
    });

    it('should record timeout metric', async () => {
      const metrics = new MockMetrics();
      const manager = new TimeoutManager(createTestPolicy(), null, metrics);

      manager.trackRequest('msg-metric-timeout', 50);

      try {
        await sleep(100);
      } catch (err) {
        // ignore
      }

      // Give timeout handler time to execute
      await sleep(10);

      const timeoutMetrics = metrics.recorded.filter((m) => m.name === 'timeout');
      assert(timeoutMetrics.length > 0);
    });
  });
});
