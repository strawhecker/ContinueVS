#!/usr/bin/env node

/**
 * Test Suite for Message Logging Middleware
 *
 * Comprehensive tests for MessageLoggingMiddleware, including initialization,
 * inbound/outbound logging, latency tracking, error handling, and metrics aggregation.
 *
 * Test Coverage:
 *   - Suite 1: Initialization (3 tests)
 *   - Suite 2: Inbound Logging (4 tests)
 *   - Suite 3: Outbound Logging (4 tests)
 *   - Suite 4: Latency Tracking (4 tests)
 *   - Suite 5: Error Logging (4 tests)
 *   - Suite 6: Metrics Aggregation & Cleanup (3 tests)
 *   Total: 22 tests
 *
 * @module src/versions/v2.0.0/tests/message-logging-middleware.test.mjs
 */

import assert from 'assert';
import {
  MessageLoggingMiddleware,
  LoggingMiddlewareError,
  createMessageLoggingMiddleware,
} from '../lib/message-logging-middleware.mjs';

// ============================================================================
// Mock Logger
// ============================================================================

class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(msg, meta = {}) {
    this.logs.push({ level: 'debug', msg, meta });
  }

  info(msg, meta = {}) {
    this.logs.push({ level: 'info', msg, meta });
  }

  warn(msg, meta = {}) {
    this.logs.push({ level: 'warn', msg, meta });
  }

  error(msg, meta = {}) {
    this.logs.push({ level: 'error', msg, meta });
  }

  clear() {
    this.logs = [];
  }

  getLogsByLevel(level) {
    return this.logs.filter((l) => l.level === level);
  }

  getLast() {
    return this.logs[this.logs.length - 1];
  }
}

// ============================================================================
// Mock Metrics Collector
// ============================================================================

class MockMetrics {
  constructor() {
    this.records = [];
  }

  recordMetric(name, value, tags = {}) {
    this.records.push({ name, value, tags });
  }

  recordError(type) {
    this.records.push({ error: type });
  }

  clear() {
    this.records = [];
  }

  getRecordsByName(name) {
    return this.records.filter((r) => r.name === name);
  }
}

// ============================================================================
// Mock MiddlewareChain
// ============================================================================

class MockMiddlewareChain {
  constructor(config = {}) {
    this.executeCount = 0;
    this.shouldFail = config.shouldFail || false;
    this.failureError = config.failureError || new Error('Chain execution failed');
    this.latencyMs = config.latencyMs || 100;
  }

  async execute(message, dispatcher, context) {
    this.executeCount += 1;

    // Simulate latency
    await new Promise((r) => setTimeout(r, this.latencyMs));

    if (this.shouldFail) {
      throw this.failureError;
    }

    return {
      handled: true,
      shouldRelay: false,
      response: {
        messageType: message.messageType,
        messageId: message.messageId,
        success: true,
        data: { result: 'success' },
      },
    };
  }
}

// ============================================================================
// Test Helpers
// ============================================================================

function createTestMessage(messageType = 'test:message', data = {}) {
  return {
    messageType,
    messageId: `msg-${Date.now()}-${Math.random()}`,
    data,
  };
}

// ============================================================================
// Test Suites
// ============================================================================

describe('MessageLoggingMiddleware', () => {
  // ========================================================================
  // Suite 1: Initialization (3 tests)
  // ========================================================================

  describe('Suite 1: Initialization', () => {
    it('should initialize with required parameters', () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      assert.strictEqual(middleware.middlewareChain, chain);
      assert.strictEqual(middleware.logger, logger);
      assert.strictEqual(middleware.metrics, metrics);
    });

    it('should use default logger and metrics if not provided', () => {
      const chain = new MockMiddlewareChain();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
      });

      assert(middleware.logger);
      assert(middleware.metrics);
      assert.strictEqual(typeof middleware.logger.debug, 'function');
      assert.strictEqual(typeof middleware.metrics.recordMetric, 'function');
    });

    it('should throw if middlewareChain is missing', () => {
      assert.throws(
        () => new MessageLoggingMiddleware({}),
        /middlewareChain is required/
      );
    });
  });

  // ========================================================================
  // Suite 2: Inbound Logging (4 tests)
  // ========================================================================

  describe('Suite 2: Inbound Logging', () => {
    it('should log inbound message metadata', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('completion', { query: 'test' });
      await middleware.executeWithLogging(message, null, {});

      const inboundLogs = logger.getLogsByLevel('info');
      assert(inboundLogs.some((log) => log.msg === 'Inbound message'));
      assert(inboundLogs.some((log) => log.meta.messageType === 'completion'));
    });

    it('should increment inbound message count', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const msg1 = createTestMessage('msg1');
      const msg2 = createTestMessage('msg2');

      await middleware.executeWithLogging(msg1, null, {});
      await middleware.executeWithLogging(msg2, null, {});

      const metrics1 = middleware.getMetrics();
      assert.strictEqual(metrics1.inbound.total, 2);
      assert.strictEqual(metrics1.inbound.byType.msg1, 1);
      assert.strictEqual(metrics1.inbound.byType.msg2, 1);
    });

    it('should gracefully degrade when logger is null', async () => {
      const chain = new MockMiddlewareChain();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger: null,
        metrics: null,
      });

      const message = createTestMessage('test');
      const result = await middleware.executeWithLogging(message, null, {});

      assert(result);
      assert(result.response.success);
    });

    it('should respect sample rate configuration', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      // 100% sample rate
      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
        config: { sampleRate: 1.0 },
      });

      const message = createTestMessage('test');
      await middleware.executeWithLogging(message, null, {});

      const allLogs = logger.logs;
      assert(allLogs.length > 0);
    });
  });

  // ========================================================================
  // Suite 3: Outbound Logging (4 tests)
  // ========================================================================

  describe('Suite 3: Outbound Logging', () => {
    it('should log outbound message on success', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('test');
      await middleware.executeWithLogging(message, null, {});

      const outboundLogs = logger.getLogsByLevel('info');
      assert(outboundLogs.some((log) => log.msg === 'Outbound message'));
      assert(outboundLogs.some((log) => log.meta.status === 'success'));
    });

    it('should log outbound message on error', async () => {
      const chain = new MockMiddlewareChain({ shouldFail: true });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('test');

      try {
        await middleware.executeWithLogging(message, null, {});
      } catch (err) {
        // Expected
      }

      const errorLogs = logger.getLogsByLevel('error');
      assert(errorLogs.some((log) => log.msg === 'Handler execution error'));
    });

    it('should calculate and log latency', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 150 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('test');
      await middleware.executeWithLogging(message, null, {});

      const outboundLogs = logger.getLogsByLevel('info');
      const outboundLog = outboundLogs.find((log) => log.msg === 'Outbound message');
      assert(outboundLog);
      assert(outboundLog.meta.latencyMs >= 150);
    });

    it('should include response data in detailed logging', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
        config: {
          enableDetailedLogging: true,
          includePayloads: true,
        },
      });

      const message = createTestMessage('test', { foo: 'bar' });
      await middleware.executeWithLogging(message, null, {});

      const outboundLogs = logger.getLogsByLevel('info');
      const outboundLog = outboundLogs.find((log) => log.msg === 'Outbound message');
      assert(outboundLog);
      assert(outboundLog.meta.response);
    });
  });

  // ========================================================================
  // Suite 4: Latency Tracking (4 tests)
  // ========================================================================

  describe('Suite 4: Latency Tracking', () => {
    it('should categorize messages as fast (<50ms)', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 25 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('fast');
      await middleware.executeWithLogging(message, null, {});

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.latency.fast, 1);
      assert.strictEqual(metricsSnapshot.latency.normal, 0);
      assert.strictEqual(metricsSnapshot.latency.slow, 0);
    });

    it('should categorize messages as normal (50-500ms)', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 200 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('normal');
      await middleware.executeWithLogging(message, null, {});

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.latency.fast, 0);
      assert.strictEqual(metricsSnapshot.latency.normal, 1);
      assert.strictEqual(metricsSnapshot.latency.slow, 0);
    });

    it('should categorize messages as slow (>500ms)', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 600 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('slow');
      await middleware.executeWithLogging(message, null, {});

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.latency.fast, 0);
      assert.strictEqual(metricsSnapshot.latency.normal, 0);
      assert.strictEqual(metricsSnapshot.latency.slow, 1);
    });

    it('should aggregate latency histogram across multiple messages', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 100 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      for (let i = 0; i < 5; i++) {
        await middleware.executeWithLogging(createTestMessage(`msg${i}`), null, {});
      }

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.latency.normal, 5);
    });
  });

  // ========================================================================
  // Suite 5: Error Logging (4 tests)
  // ========================================================================

  describe('Suite 5: Error Logging', () => {
    it('should log handler errors separately', async () => {
      const testError = new Error('Handler failed');
      const chain = new MockMiddlewareChain({
        shouldFail: true,
        failureError: testError,
      });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('test');

      try {
        await middleware.executeWithLogging(message, null, {});
      } catch (err) {
        // Expected
      }

      const errorLogs = logger.getLogsByLevel('error');
      assert(errorLogs.some((log) => log.msg === 'Handler execution error'));
      assert(errorLogs.some((log) => log.meta.errorMessage === 'Handler failed'));
    });

    it('should track error rate in metrics', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      // Successful message
      await middleware.executeWithLogging(createTestMessage('success'), null, {});

      // Failed message (will throw, caught by executeWithLogging)
      const failChain = new MockMiddlewareChain({
        shouldFail: true,
      });
      middleware.middlewareChain = failChain;

      try {
        await middleware.executeWithLogging(createTestMessage('error'), null, {});
      } catch (err) {
        // Expected
      }

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.outbound.successCount, 1);
      assert.strictEqual(metricsSnapshot.outbound.errorCount, 1);
      assert.strictEqual(metricsSnapshot.errors.total, 1);
    });

    it('should categorize errors by type', async () => {
      const typeError = new TypeError('Type mismatch');
      const chain = new MockMiddlewareChain({
        shouldFail: true,
        failureError: typeError,
      });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      try {
        await middleware.executeWithLogging(createTestMessage('test'), null, {});
      } catch (err) {
        // Expected
      }

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.errors.byType.TypeError, 1);
    });

    it('should calculate error rate percentage', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      // 4 successful, 1 error = 20% error rate
      for (let i = 0; i < 4; i++) {
        await middleware.executeWithLogging(createTestMessage(`success${i}`), null, {});
      }

      const failChain = new MockMiddlewareChain({ shouldFail: true });
      middleware.middlewareChain = failChain;

      try {
        await middleware.executeWithLogging(createTestMessage('error'), null, {});
      } catch (err) {
        // Expected
      }

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.summary.errorRate, 20);
    });
  });

  // ========================================================================
  // Suite 6: Metrics Aggregation & Cleanup (3 tests)
  // ========================================================================

  describe('Suite 6: Metrics Aggregation & Cleanup', () => {
    it('should return accurate metrics aggregation', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      for (let i = 0; i < 3; i++) {
        await middleware.executeWithLogging(createTestMessage('test'), null, {});
      }

      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.inbound.total, 3);
      assert.strictEqual(metricsSnapshot.outbound.successCount, 3);
      assert(metricsSnapshot.outbound.averageLatency > 0);
    });

    it('should calculate percentile latencies (p95, p99)', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      // Generate multiple messages with consistent latency
      for (let i = 0; i < 10; i++) {
        await middleware.executeWithLogging(createTestMessage(`test${i}`), null, {});
      }

      const metricsSnapshot = middleware.getMetrics();
      assert(metricsSnapshot.outbound.p95Latency > 0);
      assert(metricsSnapshot.outbound.p99Latency > 0);
      assert(metricsSnapshot.outbound.p99Latency >= metricsSnapshot.outbound.p95Latency);
    });

    it('should reset metrics to initial state', async () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      // Log some messages
      for (let i = 0; i < 5; i++) {
        await middleware.executeWithLogging(createTestMessage(`test${i}`), null, {});
      }

      let metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.inbound.total, 5);

      // Reset
      middleware.resetMetrics();

      metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.inbound.total, 0);
      assert.strictEqual(metricsSnapshot.outbound.successCount, 0);
      assert.strictEqual(metricsSnapshot.outbound.errorCount, 0);
    });
  });

  // ========================================================================
  // Suite 7: Factory Function & E2E (2 tests)
  // ========================================================================

  describe('Suite 7: Factory Function & E2E', () => {
    it('should create middleware via factory function', () => {
      const chain = new MockMiddlewareChain();
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = createMessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      assert(middleware instanceof MessageLoggingMiddleware);
    });

    it('should handle full message lifecycle', async () => {
      const chain = new MockMiddlewareChain({ latencyMs: 150 });
      const logger = new MockLogger();
      const metrics = new MockMetrics();

      const middleware = new MessageLoggingMiddleware({
        middlewareChain: chain,
        logger,
        metrics,
      });

      const message = createTestMessage('lifecycle:test', { query: 'data' });
      const result = await middleware.executeWithLogging(message, null, {});

      // Verify result
      assert(result.response.success);
      assert.strictEqual(result.response.messageType, 'lifecycle:test');

      // Verify logging
      const allLogs = logger.logs;
      assert(allLogs.length > 0);
      assert(allLogs.some((log) => log.msg === 'Inbound message'));
      assert(allLogs.some((log) => log.msg === 'Outbound message'));

      // Verify metrics
      const metricsSnapshot = middleware.getMetrics();
      assert.strictEqual(metricsSnapshot.inbound.total, 1);
      assert.strictEqual(metricsSnapshot.outbound.successCount, 1);
    });
  });
});
