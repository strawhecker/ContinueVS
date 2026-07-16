#!/usr/bin/env node

/**
 * Manual test runner for context-window-handler
 * Executes all 22 tests and reports results
 */

import assert from 'assert';
import {
  createContextWindowHandler,
  ContextWindowError,
  TokenCalculationError,
} from '../lib/context-window-handler.mjs';

let testsPassed = 0;
let testsFailed = 0;
let testCount = 0;

// Helper to run tests
async function runTest(suiteName, testName, testFn) {
  testCount++;
  try {
    await testFn();
    console.log(`✓ ${suiteName} → ${testName}`);
    testsPassed++;
  } catch (error) {
    console.error(`✗ ${suiteName} → ${testName}`);
    console.error(`  ${error.message}`);
    testsFailed++;
  }
}

// Mock utilities
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

// ============================================================================
// Test Suites
// ============================================================================

console.log('\n=== Context-Window Handler Test Suite ===\n');

// Suite 1: Initialization & Dependency Injection
console.log('Suite 1: Initialization & Dependency Injection');
await runTest('Suite 1', 'Create handler with all options', () => {
  const logger = createMockLogger();
  const metrics = createMockMetrics();
  const collector = createMockCollector();
  const handler = createContextWindowHandler({ logger, metrics, collectorInstance: collector });
  assert.strictEqual(typeof handler, 'function');
});

await runTest('Suite 1', 'Create handler with minimal options', () => {
  const handler = createContextWindowHandler({});
  assert.strictEqual(typeof handler, 'function');
});

await runTest('Suite 1', 'Create handler with null collector', () => {
  const handler = createContextWindowHandler({ collectorInstance: null });
  assert.strictEqual(typeof handler, 'function');
});

// Suite 2: Happy Path Scenarios
console.log('\nSuite 2: Happy Path Scenarios');
await runTest('Suite 2', 'Normal context window (50% utilization)', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 2000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-1' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.utilization, 0.5);
});

await runTest('Suite 2', 'Nearly full context (90% utilization)', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 3600 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-2' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.utilization, 0.9);
});

await runTest('Suite 2', 'Empty context (0% utilization)', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 0 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-3' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.utilization, 0);
});

await runTest('Suite 2', 'Max tokens reached (100% utilization)', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 4000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-4' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.utilization, 1.0);
});

// Suite 3: Recommendations Engine
console.log('\nSuite 3: Recommendations Engine');
await runTest('Suite 3', 'No recommendations at < 70%', async () => {
  const collector = createMockCollector({ maxTokens: 10000, usedTokens: 6000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-5' }, {});
  assert.strictEqual(response.data.recommendations.length, 0);
});

await runTest('Suite 3', 'Recommendations at 75%', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 3000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-6' }, {});
  assert(response.data.recommendations.length > 0);
});

await runTest('Suite 3', 'Critical warnings at 95%', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 3800 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-7' }, {});
  const critical = response.data.recommendations.filter((r) => r.toUpperCase().includes('CRITICAL'));
  assert(critical.length > 0);
});

// Suite 4: Error Handling
console.log('\nSuite 4: Error Handling');
await runTest('Suite 4', 'Reject if collector not initialized', async () => {
  const handler = createContextWindowHandler({ collectorInstance: null });
  const response = await handler({ messageId: 'msg-8' }, {});
  assert.strictEqual(response.success, false);
  assert.strictEqual(response.error.code, 'COLLECTOR_NOT_INITIALIZED');
});

await runTest('Suite 4', 'Reject if collector returns invalid data', async () => {
  const badCollector = { GetContextWindowAsync: async () => ({ maxTokens: 'invalid' }) };
  const handler = createContextWindowHandler({ collectorInstance: badCollector });
  const response = await handler({ messageId: 'msg-9' }, {});
  assert.strictEqual(response.success, false);
});

await runTest('Suite 4', 'Reject if token calculations overflow', async () => {
  const badCollector = { GetContextWindowAsync: async () => ({ maxTokens: -100, usedTokens: 50 }) };
  const handler = createContextWindowHandler({ collectorInstance: badCollector });
  const response = await handler({ messageId: 'msg-10' }, {});
  assert.strictEqual(response.success, false);
});

await runTest('Suite 4', 'Gracefully handle missing optional fields', async () => {
  const collector = { GetContextWindowAsync: async () => ({ maxTokens: 4000, usedTokens: 2000 }) };
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-11' }, {});
  assert.strictEqual(response.success, true);
  assert.deepStrictEqual(response.data.estimatedTokens, {});
});

// Suite 5: Metrics & Logging
console.log('\nSuite 5: Metrics & Logging');
await runTest('Suite 5', 'Record token usage metrics', async () => {
  const metrics = createMockMetrics();
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 2000 });
  const handler = createContextWindowHandler({ collectorInstance: collector, metrics });
  await handler({ messageId: 'msg-12' }, {});
  const events = metrics.getEvents();
  assert(events.length > 0);
});

await runTest('Suite 5', 'Record utilization percentage', async () => {
  const metrics = createMockMetrics();
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 3600 });
  const handler = createContextWindowHandler({ collectorInstance: collector, metrics });
  await handler({ messageId: 'msg-13' }, {});
  const latencies = metrics.getLatencies();
  assert(latencies.length > 0);
});

await runTest('Suite 5', 'Log high-utilization warnings', async () => {
  const logger = createMockLogger();
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 3600 });
  const handler = createContextWindowHandler({ collectorInstance: collector, logger });
  await handler({ messageId: 'msg-14' }, {});
  const logs = logger.getLogs();
  assert(logs.length > 0);
});

await runTest('Suite 5', 'Track request latency', async () => {
  const metrics = createMockMetrics();
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 2000 });
  const handler = createContextWindowHandler({ collectorInstance: collector, metrics });
  await handler({ messageId: 'msg-15' }, {});
  const latencies = metrics.getLatencies();
  assert(latencies.length > 0);
});

// Suite 6: Edge Cases & Performance
console.log('\nSuite 6: Edge Cases & Performance');
await runTest('Suite 6', 'Handle very large token counts (>100K)', async () => {
  const collector = createMockCollector({ maxTokens: 100000, usedTokens: 75000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-16' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.maxTokens, 100000);
});

await runTest('Suite 6', 'Handle rapid successive requests', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 2000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const r1 = await handler({ messageId: 'msg-17a' }, {});
  const r2 = await handler({ messageId: 'msg-17b' }, {});
  const r3 = await handler({ messageId: 'msg-17c' }, {});
  assert.strictEqual(r1.success, true);
  assert.strictEqual(r2.success, true);
  assert.strictEqual(r3.success, true);
});

await runTest('Suite 6', 'Estimate tokens from empty editor state', async () => {
  const collector = createMockCollector({
    maxTokens: 4000,
    usedTokens: 0,
    estimatedTokens: { editorContent: 0, selectedText: 0, recentFiles: 0, conversationHistory: 0 },
  });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const response = await handler({ messageId: 'msg-18' }, {});
  assert.strictEqual(response.success, true);
  assert.strictEqual(response.data.usedTokens, 0);
});

await runTest('Suite 6', 'Performance gate: response < 100ms', async () => {
  const collector = createMockCollector({ maxTokens: 4000, usedTokens: 2000 });
  const handler = createContextWindowHandler({ collectorInstance: collector });
  const start = Date.now();
  await handler({ messageId: 'msg-19' }, {});
  const elapsed = Date.now() - start;
  assert(elapsed < 100, `Response took ${elapsed}ms, should be < 100ms`);
});

// Summary
console.log(`\n=== Test Summary ===`);
console.log(`Total: ${testCount}`);
console.log(`✓ Passed: ${testsPassed}`);
console.log(`✗ Failed: ${testsFailed}`);

if (testsFailed === 0) {
  console.log('\n✅ ALL TESTS PASSED');
  process.exit(0);
} else {
  console.log('\n❌ SOME TESTS FAILED');
  process.exit(1);
}
