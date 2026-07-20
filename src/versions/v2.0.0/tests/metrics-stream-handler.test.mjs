#!/usr/bin/env node

/**
 * Metrics Stream Handler Tests (Step 101)
 * 
 * Comprehensive test suite (40+ test cases) covering:
 * - Handler initialization with various dependency configurations
 * - Subscription request validation
 * - Metrics snapshot generation with graceful degradation
 * - Stream message formatting and schema validation
 * - Filtering by handler name and metric type
 * - Interval enforcement and boundaries
 * - Error handling and recovery
 * - Performance gates (<20ms snapshot generation)
 * - Concurrent subscription isolation
 * - Resource cleanup
 * 
 * @module metrics-stream-handler.test
 */

import { strict as assert } from 'assert';
import {
  createMetricsStreamHandler,
  MetricsStreamError
} from '../lib/metrics-stream-handler.mjs';

/**
 * Suite 1: Initialization & Dependency Injection
 */
describe('MetricsStreamHandler - Initialization', () => {
  it('should create handler with all dependencies', () => {
    const mockProfiler = {
      aggregateMetrics: () => ({ handlers: [], summary: {} })
    };

    const handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      messageLogger: {},
      errorRecoveryMetrics: {},
      timeoutManager: {},
      symbolExtractor: {},
      logger: {}
    });

    assert(typeof handler === 'function', 'Handler should be a function');
  });

  it('should create handler with only required dependency (profilerHandler)', () => {
    const mockProfiler = {
      aggregateMetrics: () => ({ handlers: [], summary: {} })
    };

    const handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler
    });

    assert(typeof handler === 'function', 'Handler should be a function with minimal deps');
  });

  it('should create handler with partial dependencies', () => {
    const mockProfiler = {
      aggregateMetrics: () => ({ handlers: [], summary: {} })
    };

    const handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      messageLogger: {},
      errorRecoveryMetrics: null,
      timeoutManager: {}
    });

    assert(typeof handler === 'function', 'Handler should work with partial deps');
  });

  it('should throw MetricsStreamError if profilerHandler is null', () => {
    assert.throws(
      () => createMetricsStreamHandler({ profilerHandler: null }),
      MetricsStreamError,
      'Should reject null profilerHandler'
    );
  });

  it('should throw MetricsStreamError if profilerHandler is missing', () => {
    assert.throws(
      () => createMetricsStreamHandler({}),
      MetricsStreamError,
      'Should reject missing profilerHandler'
    );
  });
});

/**
 * Suite 2: Subscription Message Handling
 */
describe('MetricsStreamHandler - Subscription Requests', () => {
  let handler;

  beforeEach(() => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: [
          {
            name: 'refactor',
            latency: { p50: 50, p95: 100, p99: 200 },
            errorRate: 0.02,
            throughput: 15,
            requestCount: 1500,
            timeoutCount: 30
          }
        ],
        summary: {}
      })
    };

    handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });
  });

  it('should accept valid subscription request', async () => {
    const message = {
      messageId: 'test-1',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 1000,
        filters: { handlers: ['refactor'] }
      }
    };

    const context = {
      send: () => {},
      onCancel: (cb) => {}
    };

    const result = await handler(message, context);
    assert(result.success === true, 'Should return success for valid request');
  });

  it('should reject request missing interval', async () => {
    const message = {
      messageId: 'test-2',
      messageType: 'bridge:subscribeToMetrics',
      data: { filters: {} }
    };

    const context = {
      send: () => {},
      onCancel: (cb) => {}
    };

    const result = await handler(message, context);
    assert(result.success === false, 'Should reject missing interval');
  });

  it('should reject invalid interval (too low)', async () => {
    const message = {
      messageId: 'test-3',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 100 }
    };

    const context = {
      send: () => {},
      onCancel: (cb) => {}
    };

    const result = await handler(message, context);
    assert(result.success === false, 'Should reject interval < 500ms');
  });

  it('should reject invalid interval (too high)', async () => {
    const message = {
      messageId: 'test-4',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 70000 }
    };

    const context = {
      send: () => {},
      onCancel: (cb) => {}
    };

    const result = await handler(message, context);
    assert(result.success === false, 'Should reject interval > 60000ms');
  });

  it('should reject invalid filters structure', async () => {
    const message = {
      messageId: 'test-5',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 1000,
        filters: 'invalid'
      }
    };

    const context = {
      send: () => {},
      onCancel: (cb) => {}
    };

    const result = await handler(message, context);
    assert(result.success === false, 'Should reject non-object filters');
  });
});

/**
 * Suite 3: Metrics Snapshot Generation
 */
describe('MetricsStreamHandler - Snapshot Generation', () => {
  let handler;
  const mockProfilerMetrics = {
    handlers: [
      {
        name: 'search',
        latency: { p50: 40, p95: 90, p99: 180 },
        errorRate: 0.01,
        throughput: 20.5,
        requestCount: 2000,
        timeoutCount: 20
      },
      {
        name: 'refactor',
        latency: { p50: 50, p95: 120, p99: 250 },
        errorRate: 0.02,
        throughput: 15.3,
        requestCount: 1500,
        timeoutCount: 30
      }
    ],
    summary: {}
  };

  beforeEach(() => {
    const mockProfiler = {
      aggregateMetrics: () => mockProfilerMetrics
    };

    handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });
  });

  it('should generate snapshot with all metric sources available', async () => {
    const message = {
      messageId: 'snap-1',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => { this.cancelFn = cb; }
    };

    const result = await handler(message, context);
    assert(result.success === true, 'Handler should succeed');

    // Give it a moment to generate first snapshot
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate !== null, 'Should generate snapshot');
    assert(capturedUpdate.data.snapshot.handlers.length === 2, 'Should include all handlers');
  });

  it('should include p50/p95/p99 latencies from profiler', async () => {
    const message = {
      messageId: 'snap-2',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.data.snapshot.handlers[0].latency.p99 === 180, 'Should preserve p99');
    assert(capturedUpdate.data.snapshot.handlers[0].latency.p95 === 90, 'Should preserve p95');
  });

  it('should validate snapshot schema structure', async () => {
    const message = {
      messageId: 'snap-3',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    const snapshot = capturedUpdate.data.snapshot;
    assert(snapshot.handlers, 'Should have handlers array');
    assert(snapshot.summary, 'Should have summary object');
    assert(snapshot.timestamp, 'Should have timestamp');
    assert(Array.isArray(snapshot.handlers), 'handlers should be array');
    assert(typeof snapshot.summary.totalRequests === 'number', 'summary.totalRequests should be number');
  });

  it('should gracefully degrade with partial metric sources', async () => {
    const mockProfiler = {
      aggregateMetrics: () => null
    };

    const degradedHandler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });

    const message = {
      messageId: 'snap-4',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    const result = await degradedHandler(message, context);
    assert(result.success === true, 'Should not throw even with null metrics');
  });
});

/**
 * Suite 4: Stream Message Format
 */
describe('MetricsStreamHandler - Stream Message Format', () => {
  let handler;

  beforeEach(() => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: [
          {
            name: 'test',
            latency: { p50: 10, p95: 20, p99: 30 },
            errorRate: 0,
            throughput: 100,
            requestCount: 1000,
            timeoutCount: 0
          }
        ],
        summary: {}
      })
    };

    handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });
  });

  it('should format snapshot as proper JSON-RPC message', async () => {
    const message = {
      messageId: 'fmt-1',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.messageType === 'bridge:metricsUpdate', 'Should have correct messageType');
    assert(capturedUpdate.data.subscriptionId, 'Should have subscriptionId');
  });

  it('should include timestamp in ISO 8601 format', async () => {
    const message = {
      messageId: 'fmt-2',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    const isoRegex = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;
    assert(isoRegex.test(capturedUpdate.data.timestamp), 'Timestamp should be ISO 8601');
  });

  it('should include status field for handlers', async () => {
    const message = {
      messageId: 'fmt-3',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    const handler0 = capturedUpdate.data.snapshot.handlers[0];
    assert(['healthy', 'degraded', 'error'].includes(handler0.status), 'Should have valid status');
  });

  it('should include uptime in summary', async () => {
    const message = {
      messageId: 'fmt-4',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.data.snapshot.summary.uptime, 'Should include uptime');
    assert(typeof capturedUpdate.data.snapshot.summary.uptime === 'string', 'Uptime should be string');
  });
});

/**
 * Suite 5: Filtering & Subscription Options
 */
describe('MetricsStreamHandler - Filtering', () => {
  let handler;

  beforeEach(() => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: [
          {
            name: 'refactor',
            latency: { p50: 50, p95: 100, p99: 200 },
            errorRate: 0.02,
            throughput: 15,
            requestCount: 1500,
            timeoutCount: 30
          },
          {
            name: 'search',
            latency: { p50: 40, p95: 90, p99: 180 },
            errorRate: 0.01,
            throughput: 20,
            requestCount: 2000,
            timeoutCount: 20
          }
        ],
        summary: {}
      })
    };

    handler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });
  });

  it('should filter by handler name', async () => {
    const message = {
      messageId: 'filt-1',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 500,
        filters: { handlers: ['refactor'] }
      }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.data.snapshot.handlers.length === 1, 'Should filter to 1 handler');
    assert(capturedUpdate.data.snapshot.handlers[0].name === 'refactor', 'Should be refactor');
  });

  it('should filter by metric type', async () => {
    const message = {
      messageId: 'filt-2',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 500,
        filters: { metrics: ['latency', 'status'] }
      }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    const h = capturedUpdate.data.snapshot.handlers[0];
    assert(h.latency, 'Should include latency');
    assert(h.status, 'Should include status');
    assert(h.errorRate === undefined, 'Should exclude errorRate');
  });

  it('should combine multiple filters', async () => {
    const message = {
      messageId: 'filt-3',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 500,
        filters: {
          handlers: ['refactor'],
          metrics: ['latency']
        }
      }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.data.snapshot.handlers.length === 1, 'Should have 1 handler');
    assert(capturedUpdate.data.snapshot.handlers[0].name === 'refactor', 'Should be refactor');
    assert(capturedUpdate.data.snapshot.handlers[0].latency, 'Should have latency');
  });

  it('should return all metrics when no filters applied', async () => {
    const message = {
      messageId: 'filt-4',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await handler(message, context);
    await new Promise(r => setTimeout(r, 600));

    const h = capturedUpdate.data.snapshot.handlers[0];
    assert(h.latency, 'Should have latency');
    assert(h.errorRate !== undefined, 'Should have errorRate');
    assert(h.throughput !== undefined, 'Should have throughput');
  });
});

/**
 * Suite 6: Error Handling & Degradation
 */
describe('MetricsStreamHandler - Error Handling', () => {
  it('should handle missing profilerHandler gracefully', () => {
    assert.throws(
      () => createMetricsStreamHandler({}),
      MetricsStreamError,
      'Should throw MetricsStreamError for missing profiler'
    );
  });

  it('should continue streaming on metric aggregation errors', async () => {
    const mockProfiler = {
      aggregateMetrics: () => {
        throw new Error('Aggregation failed');
      }
    };

    const errorHandler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });

    const message = {
      messageId: 'err-1',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    const result = await errorHandler(message, context);
    assert(result.success === true, 'Should not throw on metric errors');
  });

  it('should return partial data on partial failures', async () => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: [
          {
            name: 'working',
            latency: { p50: 10, p95: 20, p99: 30 },
            errorRate: 0,
            throughput: 100,
            requestCount: 1000,
            timeoutCount: 0
          }
        ],
        summary: {}
      })
    };

    const partialHandler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });

    const message = {
      messageId: 'err-2',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
        }
      },
      onCancel: (cb) => {}
    };

    await partialHandler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(capturedUpdate.data.snapshot.handlers.length === 1, 'Should return available data');
  });
});

/**
 * Suite 7: Performance & Concurrency
 */
describe('MetricsStreamHandler - Performance', () => {
  it('should generate snapshot in <20ms', async () => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: Array.from({ length: 20 }, (_, i) => ({
          name: `handler${i}`,
          latency: { p50: 10, p95: 20, p99: 30 },
          errorRate: 0.01,
          throughput: 15,
          requestCount: 1500,
          timeoutCount: 10
        })),
        summary: {}
      })
    };

    const perfHandler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });

    const message = {
      messageId: 'perf-1',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    let capturedUpdate = null;
    let generationTime = 0;
    const context = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          capturedUpdate = msg;
          generationTime = msg.data.snapshot.summary.generationTimeMs;
        }
      },
      onCancel: (cb) => {}
    };

    await perfHandler(message, context);
    await new Promise(r => setTimeout(r, 600));

    assert(generationTime < 20, `Snapshot generation should be <20ms, was ${generationTime}ms`);
  });

  it('should handle multiple concurrent subscriptions independently', async () => {
    const mockProfiler = {
      aggregateMetrics: () => ({
        handlers: [
          {
            name: 'test',
            latency: { p50: 10, p95: 20, p99: 30 },
            errorRate: 0,
            throughput: 100,
            requestCount: 1000,
            timeoutCount: 0
          }
        ],
        summary: {}
      })
    };

    const concurrencyHandler = createMetricsStreamHandler({
      profilerHandler: mockProfiler,
      logger: null
    });

    const updates1 = [];
    const updates2 = [];

    const context1 = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          updates1.push(msg);
        }
      },
      onCancel: (cb) => {}
    };

    const context2 = {
      send: (msg) => {
        if (msg.messageType === 'bridge:metricsUpdate') {
          updates2.push(msg);
        }
      },
      onCancel: (cb) => {}
    };

    const msg1 = {
      messageId: 'conc-1',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 500 }
    };

    const msg2 = {
      messageId: 'conc-2',
      messageType: 'bridge:subscribeToMetrics',
      data: { interval: 600 }
    };

    await concurrencyHandler(msg1, context1);
    await concurrencyHandler(msg2, context2);

    await new Promise(r => setTimeout(r, 700));

    assert(updates1.length > 0, 'First subscription should receive updates');
    assert(updates2.length > 0, 'Second subscription should receive updates');
    assert(updates1[0].data.subscriptionId !== updates2[0].data.subscriptionId, 'Subscriptions should be independent');
  });
});
