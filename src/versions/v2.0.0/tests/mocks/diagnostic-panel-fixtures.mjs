#!/usr/bin/env node

/**
 * Test fixtures for diagnostic panel handler tests (Step 102)
 *
 * Provides mock implementations of:
 * - ProfilerHandler with realistic metrics data
 * - HealthCheckService with various health states
 * - BridgeLogger with error events
 * - Valid/invalid request message examples
 *
 * @module diagnostic-panel-fixtures
 */

/**
 * Creates a mock ProfilerHandler with sample metrics.
 *
 * @param {Object} overrides - Override specific metrics
 * @returns {Object} Mock ProfilerHandler instance
 */
export function createMockProfilerHandler(overrides = {}) {
  const defaultHandlers = [
    {
      name: 'bridge:getEditorState',
      tier: 'core',
      latency: { p50: 10, p95: 25, p99: 45 },
      errorRate: 0.001,
      throughput: 120.5,
      requestCount: 1205,
      timeoutCount: 0,
      cacheHitRate: 0.92
    },
    {
      name: 'bridge:search',
      tier: 'core',
      latency: { p50: 15, p95: 35, p99: 75 },
      errorRate: 0.003,
      throughput: 85.2,
      requestCount: 852,
      timeoutCount: 2,
      cacheHitRate: 0.78
    },
    {
      name: 'bridge:goToDefinition',
      tier: 'core',
      latency: { p50: 20, p95: 50, p99: 120 },
      errorRate: 0.005,
      throughput: 42.1,
      requestCount: 421,
      timeoutCount: 1,
      cacheHitRate: 0.65
    },
    {
      name: 'bridge:codeCompletion',
      tier: 'utility',
      latency: { p50: 30, p95: 150, p99: 350 },
      errorRate: 0.01,
      throughput: 25.3,
      requestCount: 253,
      timeoutCount: 5,
      cacheHitRate: 0.45
    },
    {
      name: 'bridge:hoverInfo',
      tier: 'utility',
      latency: { p50: 5, p95: 15, p99: 30 },
      errorRate: 0.0,
      throughput: 200.1,
      requestCount: 2001,
      timeoutCount: 0,
      cacheHitRate: 0.95
    }
  ];

  const handlers = overrides.handlers || defaultHandlers;

  return {
    aggregateMetrics() {
      return {
        handlers: handlers,
        summary: {
          totalLatencyMs: handlers.reduce((sum, h) => sum + (h.latency?.p99 || 0), 0),
          avgErrorRate: handlers.reduce((sum, h) => sum + (h.errorRate || 0), 0) / handlers.length,
          avgThroughput: handlers.reduce((sum, h) => sum + (h.throughput || 0), 0) / handlers.length,
          totalRequests: handlers.reduce((sum, h) => sum + (h.requestCount || 0), 0),
          totalTimeouts: handlers.reduce((sum, h) => sum + (h.timeoutCount || 0), 0),
          uptime: '2h 30m 15s'
        }
      };
    }
  };
}

/**
 * Creates a mock HealthCheckService with configurable health state.
 *
 * @param {string} state - Health state: 'healthy', 'degraded', 'error'
 * @param {Object} overrides - Override specific properties
 * @returns {Object} Mock HealthCheckService instance
 */
export function createMockHealthCheckService(state = 'healthy', overrides = {}) {
  const stateMap = {
    healthy: { state: 'healthy', reason: 'All handlers responding normally' },
    degraded: { state: 'degraded', reason: 'Some handlers showing elevated latency' },
    error: { state: 'error', reason: 'Bridge process connection failed' }
  };

  const baseStatus = stateMap[state] || stateMap.healthy;

  return {
    getCurrentHealthStatus() {
      return {
        state: overrides.state || baseStatus.state,
        reason: overrides.reason || baseStatus.reason,
        timestamp: overrides.timestamp || new Date().toISOString(),
        uptime: overrides.uptime || '2h 30m 15s',
        lastCheckTime: overrides.lastCheckTime || new Date().toISOString(),
        consecutiveFailures: overrides.consecutiveFailures || 0
      };
    },

    getHealthStatus() {
      // Fallback method name
      return this.getCurrentHealthStatus();
    }
  };
}

/**
 * Creates a mock BridgeLogger with error events.
 *
 * @param {number} errorCount - Number of error events to generate
 * @param {Object} overrides - Override error patterns
 * @returns {Object} Mock BridgeLogger instance
 */
export function createMockBridgeLogger(errorCount = 20, overrides = {}) {
  const errors = [];
  const now = Date.now();

  // Generate sample error events
  for (let i = 0; i < errorCount; i++) {
    const severity = i < errorCount * 0.6 ? 'INFO' :
                    i < errorCount * 0.85 ? 'WARNING' : 'CRITICAL';
    const latencyMs = severity === 'CRITICAL' ? 550 + Math.random() * 100 :
                      severity === 'WARNING' ? 250 + Math.random() * 100 : 50 + Math.random() * 50;

    errors.push({
      timestamp: new Date(now - (errorCount - i) * 1000).toISOString(),
      severity,
      message: generateErrorMessage(severity, i),
      context: {
        requestId: `req-${i}`,
        duration: latencyMs.toFixed(2)
      },
      handlerName: ['bridge:search', 'bridge:codeCompletion', 'bridge:goToDefinition'][i % 3],
      latencyMs: latencyMs,
      code: severity === 'CRITICAL' ? 'TIMEOUT' : 'SLOW_RESPONSE'
    });
  }

  return {
    getRecentErrors() {
      return overrides.errors || errors;
    },

    recordError(message, context = {}) {
      errors.push({
        timestamp: new Date().toISOString(),
        severity: context.severity || 'WARNING',
        message,
        context,
        handlerName: context.handlerName || null,
        latencyMs: context.latencyMs || 0
      });

      // Keep only last 100
      if (errors.length > 100) {
        errors.shift();
      }
    }
  };
}

/**
 * Generates sample error messages based on severity.
 *
 * @param {string} severity - Error severity level
 * @param {number} index - Error index for variation
 * @returns {string} Error message
 */
function generateErrorMessage(severity, index) {
  const infoMessages = [
    'Handler response completed normally',
    'Request processed within SLA',
    'Cache hit for repeated query'
  ];

  const warningMessages = [
    'Handler latency approaching threshold (p99 > 200ms)',
    'Multiple consecutive timeouts detected',
    'Cache hit rate declining below 70%'
  ];

  const criticalMessages = [
    'Handler timeout after 5000ms',
    'Bridge process health check failed',
    'Critical error: handler crashed'
  ];

  const messageMap = {
    INFO: infoMessages,
    WARNING: warningMessages,
    CRITICAL: criticalMessages
  };

  const messages = messageMap[severity] || infoMessages;
  return messages[index % messages.length];
}

/**
 * Creates a valid request message for bridge:getDiagnosticPanel.
 *
 * @param {Object} overrides - Override message properties
 * @returns {Object} Request message
 */
export function createValidRequestMessage(overrides = {}) {
  return {
    messageType: 'bridge:getDiagnosticPanel',
    messageId: overrides.messageId || 'msg-' + Date.now(),
    data: {
      operation: overrides.operation || 'get-all',
      filter: overrides.filter || null,
      ...overrides.data
    }
  };
}

/**
 * Creates an invalid request message (missing required fields).
 *
 * @param {Object} overrides - Override message properties
 * @returns {Object} Invalid request message
 */
export function createInvalidRequestMessage(type = 'missing-data', overrides = {}) {
  const cases = {
    'missing-data': {
      messageType: 'bridge:getDiagnosticPanel',
      messageId: 'msg-' + Date.now()
      // Missing 'data' field
    },
    'missing-message-id': {
      messageType: 'bridge:getDiagnosticPanel',
      data: { operation: 'get-all' }
      // Missing 'messageId' field
    },
    'invalid-operation': {
      messageType: 'bridge:getDiagnosticPanel',
      messageId: 'msg-' + Date.now(),
      data: { operation: 'invalid-operation' }
    },
    'null-message': null
  };

  return {
    ...cases[type],
    ...overrides
  };
}

/**
 * Creates mock context for handler invocation.
 *
 * @param {Object} overrides - Override context properties
 * @returns {Object} Handler context
 */
export function createMockContext(overrides = {}) {
  return {
    requestId: overrides.requestId || 'ctx-' + Date.now(),
    userId: overrides.userId || 'test-user',
    timestamp: overrides.timestamp || new Date().toISOString(),
    ...overrides
  };
}

/**
 * Creates a complete test scenario with handler, logger, and health service.
 *
 * @param {Object} config - Configuration
 * @returns {Object} Test scenario with all mock dependencies
 */
export function createTestScenario(config = {}) {
  return {
    profilerHandler: createMockProfilerHandler(config.profilerOverrides),
    healthCheckService: createMockHealthCheckService(config.healthState, config.healthOverrides),
    bridgeLogger: createMockBridgeLogger(config.errorCount, config.loggerOverrides),
    message: createValidRequestMessage(config.messageOverrides),
    context: createMockContext(config.contextOverrides),
    // Helper methods
    getMetrics() {
      return this.profilerHandler.aggregateMetrics();
    },
    getHealth() {
      return this.healthCheckService.getCurrentHealthStatus();
    },
    getErrors() {
      return this.bridgeLogger.getRecentErrors();
    }
  };
}

export default {
  createMockProfilerHandler,
  createMockHealthCheckService,
  createMockBridgeLogger,
  createValidRequestMessage,
  createInvalidRequestMessage,
  createMockContext,
  createTestScenario
};
