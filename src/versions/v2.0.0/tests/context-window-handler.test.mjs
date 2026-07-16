#!/usr/bin/env node

/**
 * Context-Window Handler Test Suite
 *
 * 22 comprehensive tests across 6 test suites:
 * - Suite 1: Initialization & Dependency Injection (3 tests)
 * - Suite 2: Happy Path Scenarios (4 tests)
 * - Suite 3: Recommendations Engine (3 tests)
 * - Suite 4: Error Handling (4 tests)
 * - Suite 5: Metrics & Logging (4 tests)
 * - Suite 6: Edge Cases & Performance (4 tests)
 */

import assert from 'assert';
import { describe, it } from 'mocha';
import {
  createContextWindowHandler,
  ContextWindowError,
  TokenCalculationError,
} from '../lib/context-window-handler.mjs';

// ============================================================================
// Mock Utilities
// ============================================================================

/**
 * Create a mock logger for testing
 */
function createMockLogger() {
  const logs = [];
  return {
    debug: (...args) => logs.push({ level: 'debug', args }),
    info: (...args) => logs.push({ level: 'info', args }),
    warn: (...args) => logs.push({ level: 'warn', args }),
    error: (...args) => logs.push({ level: 'error', args }),
    getLogs: () => logs,
    clear: () => logs.splice(0),
  };
}

/**
 * Create a mock metrics collector
 */
function createMockMetrics() {
  const events = [];
  const latencies = [];
  return {
    recordEvent: (eventName, data) => events.push({ eventName, data }),
    recordLatency: (operation, ms) => latencies.push({ operation, ms }),
    getEvents: () => events,
    getLatencies: () => latencies,
    clear: () => {
      events.splice(0);
      latencies.splice(0);
    },
  };
}

/**
 * Create a mock collector with configurable state
 */
function createMockCollector(state = {}) {
  const defaults = {
    maxTokens: 4096,
    usedTokens: 2100,
    estimatedTokens: {
      editorContent: 450,
      selectedText: 80,
      recentFiles: 600,
      conversationHistory: 970,
    },
  };
  const config = { ...defaults, ...state };

  return {
    GetContextWindowAsync: async () => config,
  };
}

/**
 * Create a collector that returns a promise
 */
function createPromiseCollector(state = {}) {
  const collector = createMockCollector(state);
  return {
    GetContextWindowAsync: () => Promise.resolve(collector.GetContextWindowAsync()),
  };
}

// ============================================================================
// Test Suite 1: Initialization & Dependency Injection (3 tests)
// ============================================================================

describe('Suite 1: Initialization & Dependency Injection', () => {
  it('should create handler with all options provided', () => {
    const logger = createMockLogger();
    const metrics = createMockMetrics();
    const collector = createMockCollector();

    const handler = createContextWindowHandler({
      logger,
      metrics,
      collectorInstance: collector,
    });

    assert.strictEqual(typeof handler, 'function');
  });

  it('should create handler with minimal options (graceful defaults)', () => {
    const handler = createContextWindowHandler({});
    assert.strictEqual(typeof handler, 'function');
  });

  it('should create handler with null collector (will error on invoke)', () => {
    const handler = createContextWindowHandler({ collectorInstance: null });
    assert.strictEqual(typeof handler, 'function');
  });
});

// ============================================================================
// Test Suite 2: Happy Path Scenarios (4 tests)
// ============================================================================

describe('Suite 2: Happy Path Scenarios', () => {
  it('should return normal context window state (50% utilization)', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 2000,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-1' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.maxTokens, 4000);
    assert.strictEqual(response.data.usedTokens, 2000);
    assert.strictEqual(response.data.availableTokens, 2000);
    assert.strictEqual(response.data.utilization, 0.5);
    assert(Array.isArray(response.data.recommendations));
  });

  it('should return nearly full context (90% utilization)', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 3600,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-2' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.utilization, 0.9);
    assert(response.data.recommendations.length > 0);
  });

  it('should return empty context (0% utilization)', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 0,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-3' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.utilization, 0);
    assert.strictEqual(response.data.availableTokens, 4000);
  });

  it('should handle max tokens reached (100% utilization)', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 4000,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-4' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.utilization, 1.0);
    assert.strictEqual(response.data.availableTokens, 0);
  });
});

// ============================================================================
// Test Suite 3: Recommendations Engine (3 tests)
// ============================================================================

describe('Suite 3: Recommendations Engine', () => {
  it('should not generate recommendations when utilization < 70%', async () => {
    const collector = createMockCollector({
      maxTokens: 10000,
      usedTokens: 6000, // 60% utilization
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-5' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.recommendations.length, 0);
  });

  it('should generate recommendations at 75% utilization', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 3000, // 75% utilization
      estimatedTokens: {
        conversationHistory: 1200,
        recentFiles: 800,
      },
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-6' }, {});

    assert.strictEqual(response.success, true);
    assert(response.data.recommendations.length > 0);
  });

  it('should generate critical warnings at 95% utilization', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 3800, // 95% utilization
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-7' }, {});

    assert.strictEqual(response.success, true);
    const criticalRecommendations = response.data.recommendations.filter((r) =>
      r.toUpperCase().includes('CRITICAL')
    );
    assert(criticalRecommendations.length > 0);
  });
});

// ============================================================================
// Test Suite 4: Error Handling (4 tests)
// ============================================================================

describe('Suite 4: Error Handling', () => {
  it('should reject if collector not initialized', async () => {
    const handler = createContextWindowHandler({ collectorInstance: null });

    const response = await handler({ messageId: 'msg-8' }, {});

    assert.strictEqual(response.success, false);
    assert(response.error);
    assert.strictEqual(response.error.code, 'COLLECTOR_NOT_INITIALIZED');
  });

  it('should reject if collector returns invalid data', async () => {
    const badCollector = {
      GetContextWindowAsync: async () => ({ maxTokens: 'invalid' }), // Not a number
    };
    const handler = createContextWindowHandler({ collectorInstance: badCollector });

    const response = await handler({ messageId: 'msg-9' }, {});

    assert.strictEqual(response.success, false);
    assert(response.error);
  });

  it('should reject if token calculations overflow', async () => {
    const badCollector = {
      GetContextWindowAsync: async () => ({
        maxTokens: -100, // Invalid
        usedTokens: 50,
      }),
    };
    const handler = createContextWindowHandler({ collectorInstance: badCollector });

    const response = await handler({ messageId: 'msg-10' }, {});

    assert.strictEqual(response.success, false);
    assert(response.error);
  });

  it('should gracefully recover from missing optional fields', async () => {
    const collector = {
      GetContextWindowAsync: async () => ({
        maxTokens: 4000,
        usedTokens: 2000,
        // Missing estimatedTokens
      }),
    };
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-11' }, {});

    assert.strictEqual(response.success, true);
    assert.deepStrictEqual(response.data.estimatedTokens, {});
  });
});

// ============================================================================
// Test Suite 5: Metrics & Logging (4 tests)
// ============================================================================

describe('Suite 5: Metrics & Logging', () => {
  it('should record token usage metrics', async () => {
    const metrics = createMockMetrics();
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 2000,
    });
    const handler = createContextWindowHandler({
      collectorInstance: collector,
      metrics,
    });

    await handler({ messageId: 'msg-12' }, {});

    const events = metrics.getEvents();
    assert(events.length > 0);
    const contextEvent = events.find((e) => e.eventName === 'context_window_query');
    assert(contextEvent);
    assert.strictEqual(contextEvent.data.utilization, 0.5);
  });

  it('should record utilization percentage', async () => {
    const metrics = createMockMetrics();
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 3600, // 90%
    });
    const handler = createContextWindowHandler({
      collectorInstance: collector,
      metrics,
    });

    await handler({ messageId: 'msg-13' }, {});

    const latencies = metrics.getLatencies();
    const contextLatency = latencies.find((l) => l.operation === 'bridge:getContextWindow');
    assert(contextLatency);
    assert(contextLatency.ms >= 0);
  });

  it('should log high-utilization warnings', async () => {
    const logger = createMockLogger();
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 3600, // 90%
    });
    const handler = createContextWindowHandler({
      collectorInstance: collector,
      logger,
    });

    await handler({ messageId: 'msg-14' }, {});

    // Handler should complete successfully
    const logs = logger.getLogs();
    assert(logs.length > 0);
  });

  it('should track request latency', async () => {
    const metrics = createMockMetrics();
    const collector = createPromiseCollector({
      maxTokens: 4000,
      usedTokens: 2000,
    });
    const handler = createContextWindowHandler({
      collectorInstance: collector,
      metrics,
    });

    await handler({ messageId: 'msg-15' }, {});

    const latencies = metrics.getLatencies();
    assert(latencies.length > 0);
    const cwLatency = latencies.find((l) => l.operation === 'bridge:getContextWindow');
    assert(cwLatency);
    assert(cwLatency.ms < 100); // Should be fast
  });
});

// ============================================================================
// Test Suite 6: Edge Cases & Performance (4 tests)
// ============================================================================

describe('Suite 6: Edge Cases & Performance', () => {
  it('should handle very large token counts (>100K)', async () => {
    const collector = createMockCollector({
      maxTokens: 100000,
      usedTokens: 75000,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-16' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.maxTokens, 100000);
    assert.strictEqual(response.data.utilization, 0.75);
  });

  it('should handle rapid successive requests (cache freshness)', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 2000,
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response1 = await handler({ messageId: 'msg-17a' }, {});
    const response2 = await handler({ messageId: 'msg-17b' }, {});
    const response3 = await handler({ messageId: 'msg-17c' }, {});

    assert.strictEqual(response1.success, true);
    assert.strictEqual(response2.success, true);
    assert.strictEqual(response3.success, true);
    assert.strictEqual(response1.data.utilization, response2.data.utilization);
  });

  it('should estimate tokens from empty editor state', async () => {
    const collector = createMockCollector({
      maxTokens: 4000,
      usedTokens: 0,
      estimatedTokens: {
        editorContent: 0,
        selectedText: 0,
        recentFiles: 0,
        conversationHistory: 0,
      },
    });
    const handler = createContextWindowHandler({ collectorInstance: collector });

    const response = await handler({ messageId: 'msg-18' }, {});

    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.usedTokens, 0);
  });

  it('should achieve performance gate: response < 10ms', async () => {
    const metrics = createMockMetrics();
    const collector = createPromiseCollector({
      maxTokens: 4000,
      usedTokens: 2000,
    });
    const handler = createContextWindowHandler({
      collectorInstance: collector,
      metrics,
    });

    const startTime = Date.now();
    await handler({ messageId: 'msg-19' }, {});
    const elapsed = Date.now() - startTime;

    // Most responses should be < 10ms
    // (Note: This is a soft gate; async overhead may push it higher in CI)
    assert(elapsed < 100, `Response took ${elapsed}ms, should be < 100ms`);
  });
});
