#!/usr/bin/env node

/**
 * Rate Limiter Test Fixtures (Step 107)
 * Provides reusable test fixtures, factories, and mocks for rate limiter testing
 */

export function createTestPolicy(overrides = {}) {
  return {
    globalCeilingPerSecond: 500,
    handlerPolicies: new Map([
      ['bridge:complete', { tokensPerSecond: 100, burst: 5 }],
      ['bridge:analyze', { tokensPerSecond: 50, burst: 3 }],
      ['bridge:refactor', { tokensPerSecond: 10, burst: 2 }],
    ]),
    defaultTokensPerSecond: 20,
    defaultBurstMultiplier: 2,
    refillIntervalMs: 100,
    ...overrides,
  };
}

export function createAggressivePolicy() {
  return createTestPolicy({
    globalCeilingPerSecond: 10000,
    defaultTokensPerSecond: 1000,
  });
}

export function createLenientPolicy() {
  return createTestPolicy({
    globalCeilingPerSecond: 100,
    defaultTokensPerSecond: 5,
  });
}

export function createRestrictivePolicy() {
  return createTestPolicy({
    globalCeilingPerSecond: 10,
    handlerPolicies: new Map([
      ['bridge:complete', { tokensPerSecond: 2, burst: 1 }],
      ['bridge:analyze', { tokensPerSecond: 1, burst: 1 }],
    ]),
    defaultTokensPerSecond: 1,
  });
}

export function createMockLogger() {
  const logs = {
    debug: [],
    info: [],
    warn: [],
    error: [],
  };

  return {
    debug: (msg) => logs.debug.push(msg),
    info: (msg) => logs.info.push(msg),
    warn: (msg) => logs.warn.push(msg),
    error: (msg) => logs.error.push(msg),
    getLogs: () => logs,
    clear: () => {
      logs.debug = [];
      logs.info = [];
      logs.warn = [];
      logs.error = [];
    },
  };
}

export function createMockMetrics() {
  const metrics = {
    allowed: 0,
    rejected: 0,
    errors: 0,
    queued: 0,
    byHandler: {},
  };

  return {
    recordAllowed: (handler) => {
      metrics.allowed++;
      metrics.byHandler[handler] = (metrics.byHandler[handler] || 0) + 1;
    },
    recordRejected: (handler) => {
      metrics.rejected++;
      metrics.byHandler[handler] = (metrics.byHandler[handler] || -1) - 1;
    },
    recordError: (handler) => {
      metrics.errors++;
    },
    recordQueued: (handler) => {
      metrics.queued++;
    },
    getMetrics: () => ({ ...metrics }),
    reset: () => {
      metrics.allowed = 0;
      metrics.rejected = 0;
      metrics.errors = 0;
      metrics.queued = 0;
      metrics.byHandler = {};
    },
  };
}

export function createTestMessage(overrides = {}) {
  return {
    messageId: `msg-${Math.random().toString(36).substr(2, 9)}`,
    messageType: 'bridge:complete',
    data: {},
    ...overrides,
  };
}

export function createCompletionMessage() {
  return createTestMessage({
    messageType: 'bridge:complete',
    data: { prefix: 'const x = ' },
  });
}

export function createAnalysisMessage() {
  return createTestMessage({
    messageType: 'bridge:analyze',
    data: { filePath: '/src/index.js' },
  });
}

export function createRefactorMessage() {
  return createTestMessage({
    messageType: 'bridge:refactor',
    data: { action: 'extract-variable' },
  });
}

export function createBulkMessages(count, handler = 'bridge:complete') {
  const messages = [];
  for (let i = 0; i < count; i++) {
    messages.push(createTestMessage({ messageType: handler }));
  }
  return messages;
}

export function createSimulatedLoad(handlers = ['bridge:complete', 'bridge:analyze', 'bridge:refactor'], requestsPerHandler = 100) {
  const messages = [];
  for (const handler of handlers) {
    for (let i = 0; i < requestsPerHandler; i++) {
      messages.push(createTestMessage({ messageType: handler }));
    }
  }
  return messages;
}

export async function simulateMiddlewareExecution(middleware, message, shouldSucceed = true) {
  let executed = false;
  let response = null;

  const next = async (msg) => {
    executed = true;
    return { success: true, data: 'handler response' };
  };

  try {
    response = await middleware(message, next);
  } catch (error) {
    response = { error: error.message };
  }

  return {
    executed,
    response,
    passed: shouldSucceed ? response.data : response.data?.error,
  };
}

export function createTestScenario(name, config = {}) {
  return {
    name,
    policy: config.policy || createTestPolicy(),
    messages: config.messages || createBulkMessages(10),
    expectedAllowed: config.expectedAllowed || 10,
    expectedRejected: config.expectedRejected || 0,
    description: config.description || '',
  };
}

export const commonScenarios = [
  createTestScenario('Normal Load', {
    policy: createTestPolicy(),
    messages: createBulkMessages(100, 'bridge:complete'),
    expectedAllowed: 100,
  }),
  createTestScenario('Rate Exceeded', {
    policy: createRestrictivePolicy(),
    messages: createBulkMessages(100, 'bridge:complete'),
    expectedRejected: 98,
  }),
  createTestScenario('Multi-Handler', {
    policy: createTestPolicy(),
    messages: createSimulatedLoad(),
    expectedAllowed: 300,
  }),
];

export function assertMetrics(actual, expected) {
  const errors = [];

  if (expected.allowed !== undefined && actual.allowed !== expected.allowed) {
    errors.push(`Expected ${expected.allowed} allowed, got ${actual.allowed}`);
  }
  if (expected.rejected !== undefined && actual.rejected !== expected.rejected) {
    errors.push(`Expected ${expected.rejected} rejected, got ${actual.rejected}`);
  }
  if (expected.totalRequests !== undefined && actual.totalRequests !== expected.totalRequests) {
    errors.push(`Expected ${expected.totalRequests} total, got ${actual.totalRequests}`);
  }

  return errors;
}

export default {
  createTestPolicy,
  createAggressivePolicy,
  createLenientPolicy,
  createRestrictivePolicy,
  createMockLogger,
  createMockMetrics,
  createTestMessage,
  createCompletionMessage,
  createAnalysisMessage,
  createRefactorMessage,
  createBulkMessages,
  createSimulatedLoad,
  simulateMiddlewareExecution,
  createTestScenario,
  commonScenarios,
  assertMetrics,
};
