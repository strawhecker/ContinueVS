#!/usr/bin/env node

/**
 * Metrics Stream Fixtures (Step 101)
 * 
 * Provides mock objects and test data for metrics stream handler testing:
 * - Valid/invalid subscription request messages
 * - Realistic metrics snapshots
 * - Mock metric collector instances
 * - Fixture helper functions
 * 
 * @module metrics-stream-fixtures
 */

/**
 * Valid subscription request messages for positive testing.
 * 
 * @returns {Array<Object>} Array of valid subscription messages
 */
export function getValidSubscriptionMessages() {
  return [
    {
      messageId: 'valid-1',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 1000,
        filters: null
      },
      description: 'Basic subscription with no filters'
    },
    {
      messageId: 'valid-2',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 500,
        filters: {
          handlers: ['refactor', 'search']
        }
      },
      description: 'Subscription with handler filter'
    },
    {
      messageId: 'valid-3',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 2000,
        filters: {
          metrics: ['latency', 'errorRate', 'throughput']
        }
      },
      description: 'Subscription with metric type filter'
    },
    {
      messageId: 'valid-4',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 5000,
        filters: {
          handlers: ['goToDefinition'],
          metrics: ['latency', 'status']
        }
      },
      description: 'Subscription with combined filters'
    },
    {
      messageId: 'valid-5',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 60000,
        filters: {}
      },
      description: 'Subscription at maximum interval'
    }
  ];
}

/**
 * Invalid subscription request messages for negative testing.
 * 
 * @returns {Array<Object>} Array of invalid subscription messages
 */
export function getInvalidSubscriptionMessages() {
  return [
    {
      messageId: 'invalid-1',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        filters: null
      },
      expectedError: 'Missing or invalid interval',
      description: 'Missing interval'
    },
    {
      messageId: 'invalid-2',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 100,
        filters: null
      },
      expectedError: 'Interval must be at least 500ms',
      description: 'Interval too low (100ms)'
    },
    {
      messageId: 'invalid-3',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 100000,
        filters: null
      },
      expectedError: 'Interval must not exceed 60000ms',
      description: 'Interval too high (100000ms)'
    },
    {
      messageId: 'invalid-4',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 1000,
        filters: 'not-an-object'
      },
      expectedError: 'Filters must be an object',
      description: 'Filters not an object'
    },
    {
      messageId: 'invalid-5',
      messageType: 'bridge:subscribeToMetrics',
      data: {
        interval: 1000,
        filters: {
          handlers: 'not-an-array'
        }
      },
      expectedError: 'filters.handlers must be an array',
      description: 'Handler filter not an array'
    }
  ];
}

/**
 * Realistic metrics snapshot examples for testing.
 * 
 * @returns {Array<Object>} Array of example metrics snapshots
 */
export function getMetricSnapshotExamples() {
  return [
    {
      name: 'healthy_system',
      snapshot: {
        handlers: [
          {
            name: 'refactor',
            latency: { p50: 45, p95: 110, p99: 240 },
            errorRate: 0.01,
            throughput: 18.5,
            requestCount: 5000,
            timeoutCount: 50,
            status: 'healthy'
          },
          {
            name: 'search',
            latency: { p50: 35, p95: 85, p99: 180 },
            errorRate: 0.008,
            throughput: 22.3,
            requestCount: 6000,
            timeoutCount: 48,
            status: 'healthy'
          },
          {
            name: 'goToDefinition',
            latency: { p50: 55, p95: 130, p99: 280 },
            errorRate: 0.009,
            throughput: 16.2,
            requestCount: 4400,
            timeoutCount: 39,
            status: 'healthy'
          }
        ],
        summary: {
          totalLatencyMs: 700,
          avgErrorRate: 0.0087,
          avgThroughput: 18.67,
          totalRequests: 15400,
          totalTimeouts: 137,
          generationTimeMs: 8
        },
        timestamp: '2024-01-20T10:30:45.123Z'
      },
      description: 'System with all handlers healthy, low error rates'
    },
    {
      name: 'degraded_system',
      snapshot: {
        handlers: [
          {
            name: 'refactor',
            latency: { p50: 120, p95: 580, p99: 1200 },
            errorRate: 0.08,
            throughput: 5.2,
            requestCount: 1400,
            timeoutCount: 112,
            status: 'degraded'
          },
          {
            name: 'search',
            latency: { p50: 45, p95: 115, p99: 250 },
            errorRate: 0.02,
            throughput: 20.1,
            requestCount: 5400,
            timeoutCount: 108,
            status: 'degraded'
          },
          {
            name: 'codeCompletion',
            latency: { p50: 30, p95: 75, p99: 155 },
            errorRate: 0.012,
            throughput: 25.3,
            requestCount: 6800,
            timeoutCount: 81,
            status: 'healthy'
          }
        ],
        summary: {
          totalLatencyMs: 1605,
          avgErrorRate: 0.0373,
          avgThroughput: 16.87,
          totalRequests: 13600,
          totalTimeouts: 301,
          generationTimeMs: 12
        },
        timestamp: '2024-01-20T10:31:00.456Z'
      },
      description: 'System with degraded refactor handler, elevated error rates'
    },
    {
      name: 'error_conditions',
      snapshot: {
        handlers: [
          {
            name: 'refactor',
            latency: { p50: 200, p95: 1500, p99: 5000 },
            errorRate: 0.25,
            throughput: 1.2,
            requestCount: 320,
            timeoutCount: 80,
            status: 'error'
          },
          {
            name: 'debugSession',
            latency: { p50: 300, p95: 2000, p99: 8000 },
            errorRate: 0.15,
            throughput: 0.8,
            requestCount: 215,
            timeoutCount: 32,
            status: 'error'
          }
        ],
        summary: {
          totalLatencyMs: 13000,
          avgErrorRate: 0.2,
          avgThroughput: 1.0,
          totalRequests: 535,
          totalTimeouts: 112,
          generationTimeMs: 18
        },
        timestamp: '2024-01-20T10:31:15.789Z'
      },
      description: 'System in error state with high latencies and error rates'
    }
  ];
}

/**
 * Creates a mock ProfilerHandler instance for testing.
 * 
 * @param {Object} [config={}] - Configuration
 * @param {Array} [config.handlers] - Handler metrics to return
 * @param {boolean} [config.throwError] - Whether to throw on aggregateMetrics()
 * @returns {Object} Mock ProfilerHandler with aggregateMetrics() method
 */
export function createMockProfilerHandler(config = {}) {
  return {
    aggregateMetrics: () => {
      if (config.throwError) {
        throw new Error('Profiler aggregation failed');
      }

      return {
        handlers: config.handlers || [
          {
            name: 'testHandler',
            latency: { p50: 50, p95: 100, p99: 200 },
            errorRate: 0.02,
            throughput: 15,
            requestCount: 1500,
            timeoutCount: 30,
            cacheHitRate: 0.75
          }
        ],
        summary: {}
      };
    },

    getMetrics: () => {
      if (config.throwError) {
        throw new Error('Profiler metrics failed');
      }

      return {
        pendingRequests: 5,
        completedRequests: 1500,
        totalTimeouts: 30,
        latencies: [10, 20, 30, 40, 50],
        averageLatencyMs: 30,
        p99LatencyMs: 200
      };
    }
  };
}

/**
 * Creates a mock MessageLogger instance for testing.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Mock MessageLogger with getStats() method
 */
export function createMockMessageLogger(config = {}) {
  return {
    getStats: () => {
      return {
        totalMessages: config.totalMessages || 5000,
        requestCount: config.requestCount || 2500,
        responseCount: config.responseCount || 2450,
        errorCount: config.errorCount || 50,
        averageLatency: config.averageLatency || 45
      };
    }
  };
}

/**
 * Creates a mock ErrorRecoveryMetrics instance for testing.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Mock ErrorRecoveryMetrics with getErrorRate() method
 */
export function createMockErrorRecoveryMetrics(config = {}) {
  return {
    getErrorRate: () => {
      return {
        errorCount: config.errorCount || 50,
        successCount: config.successCount || 2450,
        timeoutCount: config.timeoutCount || 30
      };
    }
  };
}

/**
 * Creates a mock TimeoutManager instance for testing.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Mock TimeoutManager with getMetrics() method
 */
export function createMockTimeoutManager(config = {}) {
  return {
    getMetrics: () => {
      return {
        pendingRequests: config.pendingRequests || 5,
        completedRequests: config.completedRequests || 1500,
        totalTimeouts: config.totalTimeouts || 30,
        latencies: config.latencies || [10, 20, 30, 40, 50, 100, 150, 200],
        averageLatencyMs: config.averageLatencyMs || 30,
        p99LatencyMs: config.p99LatencyMs || 200
      };
    }
  };
}

/**
 * Creates a mock SymbolExtractor instance for testing.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Mock SymbolExtractor with getCacheStats() method
 */
export function createMockSymbolExtractor(config = {}) {
  return {
    getCacheStats: () => {
      return {
        hitCount: config.hitCount || 3600,
        missCount: config.missCount || 1200,
        cacheSize: config.cacheSize || 512000
      };
    }
  };
}

/**
 * Creates a mock handler context for testing subscriptions.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Mock context with send and onCancel methods
 */
export function createMockContext(config = {}) {
  const context = {
    messages: [],
    cancelled: false,
    send: (msg) => {
      context.messages.push(msg);
      if (config.onSend) {
        config.onSend(msg);
      }
    },
    onCancel: (callback) => {
      context.cancelCallback = callback;
      if (config.onCancelRegistered) {
        config.onCancelRegistered(callback);
      }
    },
    cancel: () => {
      context.cancelled = true;
      if (context.cancelCallback) {
        context.cancelCallback();
      }
    }
  };

  return context;
}

/**
 * Creates a complete set of mock metric collectors.
 * 
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Complete mock environment with all collectors
 */
export function createCompleteMockEnvironment(config = {}) {
  return {
    profilerHandler: createMockProfilerHandler(config.profiler),
    messageLogger: createMockMessageLogger(config.messageLogger),
    errorRecoveryMetrics: createMockErrorRecoveryMetrics(config.errorRecoveryMetrics),
    timeoutManager: createMockTimeoutManager(config.timeoutManager),
    symbolExtractor: createMockSymbolExtractor(config.symbolExtractor),
    logger: config.logger || {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {}
    }
  };
}

/**
 * Creates subscription test scenarios with expected outcomes.
 * 
 * @returns {Array<Object>} Array of test scenarios
 */
export function getSubscriptionScenarios() {
  return [
    {
      name: 'basic_subscription',
      request: {
        messageId: 'scenario-1',
        messageType: 'bridge:subscribeToMetrics',
        data: { interval: 1000 }
      },
      expectedBehavior: 'Should emit update every 1000ms'
    },
    {
      name: 'filtered_by_handler',
      request: {
        messageId: 'scenario-2',
        messageType: 'bridge:subscribeToMetrics',
        data: {
          interval: 1000,
          filters: { handlers: ['refactor', 'search'] }
        }
      },
      expectedBehavior: 'Should only include refactor and search handlers'
    },
    {
      name: 'filtered_by_metrics',
      request: {
        messageId: 'scenario-3',
        messageType: 'bridge:subscribeToMetrics',
        data: {
          interval: 1000,
          filters: { metrics: ['latency', 'errorRate'] }
        }
      },
      expectedBehavior: 'Should only include latency and errorRate fields'
    },
    {
      name: 'rapid_updates',
      request: {
        messageId: 'scenario-4',
        messageType: 'bridge:subscribeToMetrics',
        data: { interval: 500 }
      },
      expectedBehavior: 'Should emit update every 500ms with <20ms generation time'
    },
    {
      name: 'slow_updates',
      request: {
        messageId: 'scenario-5',
        messageType: 'bridge:subscribeToMetrics',
        data: { interval: 10000 }
      },
      expectedBehavior: 'Should emit update every 10 seconds, minimal resource usage'
    }
  ];
}

/**
 * Helper to wait for condition with timeout.
 * 
 * @param {Function} condition - Function returning boolean
 * @param {number} timeout - Maximum wait time in milliseconds
 * @param {number} interval - Check interval in milliseconds
 * @returns {Promise<boolean>} True if condition met before timeout
 */
export async function waitFor(condition, timeout = 5000, interval = 100) {
  const start = Date.now();
  while (Date.now() - start < timeout) {
    if (condition()) {
      return true;
    }
    await new Promise(resolve => setTimeout(resolve, interval));
  }
  return false;
}
