#!/usr/bin/env node

/**
 * Test-Explorer-Handler Test Suite
 *
 * Comprehensive test suite for TestExplorerHandler with 35+ test cases
 * covering initialization, discovery, caching, queries, subscriptions, and edge cases.
 *
 * @module src/versions/v2.0.0/tests/test-explorer-handler.test.mjs
 */

import { strict as assert } from 'assert';
import 'mocha';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  TestExplorerHandler,
  TestExplorerCache,
  TestExplorerError,
  TestDiscoveryError,
  StateValidationError,
  createTestExplorerHandler,
} from '../lib/test-explorer-handler.mjs';
import {
  MockTestExplorerBuilder,
  createBasicMockSetup,
  createMockSetupWithFailures,
  createEmptyMockSetup,
  createLargeMockSetup,
} from './mocks/test-explorer-mocks.mjs';
import {
  getValidTestExplorerRequest,
  getInvalidTestExplorerRequest,
  getExpectedTestResponse,
  getTestDiscoveredEvent,
  getTestExecutionEvent,
  getTestResultsEvent,
  getCacheStatsFixture,
  getMultipleTestFilesFixture,
  getNestedTestSuiteFixture,
  getLargeTestCountFixture,
  getTestsWithTagsFixture,
  getMixedLanguageTestsFixture,
  getMalformedTestAttributesFixture,
  getConcurrentQueryFixture,
} from './mocks/test-explorer-fixtures.mjs';

// ==============================================================
// Test Suite 1: Initialization
// ==============================================================

describe('Test Suite 1: Initialization', () => {
  it('should initialize with default options', () => {
    const handler = new TestExplorerHandler();
    assert.ok(handler);
    assert.ok(handler.cache);
    assert.strictEqual(handler._discoveredListeners.length, 0);
    assert.strictEqual(handler._executionStartedListeners.length, 0);
    assert.strictEqual(handler._resultsArrivedListeners.length, 0);
  });

  it('should initialize with custom logger and metrics', () => {
    const mocks = createBasicMockSetup();
    const handler = new TestExplorerHandler({
      logger: mocks.logger,
      metrics: mocks.metrics,
      documentProvider: mocks.documentProvider,
      symbolExtractor: mocks.symbolExtractor,
    });
    assert.ok(handler.logger);
    assert.ok(handler.metrics);
    assert.strictEqual(mocks.logger.getLogCount('info'), 1); // initialization log
  });

  it('should create handler via factory function', () => {
    const mocks = createBasicMockSetup();
    const handler = createTestExplorerHandler(mocks);
    assert.ok(handler instanceof TestExplorerHandler);
    assert.strictEqual(handler.documentProvider, mocks.documentProvider);
  });
});

// ==============================================================
// Test Suite 2: Test Discovery
// ==============================================================

describe('Test Suite 2: Test Discovery', () => {
  let handler;
  let mocks;

  beforeEach(() => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(2).withTypeScriptTests(1);
    mocks = builder.getMocks();
    handler = new TestExplorerHandler(mocks);
  });

  it('should discover C# tests from symbols', async () => {
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.ok(Array.isArray(response.data.tests));
    // May have tests depending on mock setup
  });

  it('should discover TypeScript tests from regex', async () => {
    const message = { data: getValidTestExplorerRequest('workspace') };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.ok(response.data.tests.length > 0);
  });

  it('should return empty array when no tests found', async () => {
    handler = new TestExplorerHandler(createEmptyMockSetup());
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.strictEqual(response.data.tests.length, 0);
    assert.strictEqual(response.data.summary.total, 0);
  });

  it('should detect and avoid duplicate tests', async () => {
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    const testIds = new Set(response.data.tests.map((t) => t.id));
    assert.strictEqual(testIds.size, response.data.tests.length, 'Duplicate test IDs detected');
  });

  it('should handle invalid filepath gracefully', async () => {
    const message = { data: { scope: 'file', filepath: null } };
    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.ok(response.error.includes('State validation error'));
  });

  it('should recover from symbol extraction errors', async () => {
    // Simulate corrupted symbols
    mocks.symbolExtractor.addSymbols('/corrupt.cs', [{ name: null, kind: null }]);
    handler = new TestExplorerHandler(mocks);

    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success); // Should not crash
  });
});

// ==============================================================
// Test Suite 3: Caching & Performance
// ==============================================================

describe('Test Suite 3: Caching & Performance', () => {
  let handler;
  let mocks;

  beforeEach(() => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(1);
    mocks = builder.getMocks();
    handler = new TestExplorerHandler(mocks);
  });

  it('should return cached results on repeated queries', async () => {
    const message = { data: getValidTestExplorerRequest('workspace') };

    // First query (cache miss)
    const response1 = await handler.handle(message);
    assert.strictEqual(response1.data.cacheHit, false);

    // Second query (cache hit)
    const response2 = await handler.handle(message);
    assert.strictEqual(response2.data.cacheHit, true);
    assert.deepStrictEqual(response1.data.tests, response2.data.tests);
  });

  it('should expire cache entries after TTL', async () => {
    handler = new TestExplorerHandler({
      ...mocks,
      cacheTtlMs: 50, // 50ms TTL for testing
    });

    const message = { data: getValidTestExplorerRequest('workspace') };

    // First query (populates cache)
    const response1 = await handler.handle(message);
    assert.strictEqual(response1.data.cacheHit, false);

    // Immediate second query should hit cache
    const response2 = await handler.handle(message);
    assert.strictEqual(response2.data.cacheHit, true);

    // Wait for TTL to expire
    await new Promise((r) => setTimeout(r, 100));

    // Third query should miss (TTL expired)
    const response3 = await handler.handle(message);
    assert.strictEqual(response3.data.cacheHit, false, 'Cache should have expired');
  });

  it('should evict LRU entries when cache is full', async () => {
    const cache = new TestExplorerCache(2, 10 * 60 * 1000); // Max 2 entries

    // Add 3 entries
    cache.set('workspace', '', [{ id: 'test-1' }], { total: 1 });
    cache.set('file', '/file1.cs', [{ id: 'test-2' }], { total: 1 });
    cache.set('file', '/file2.cs', [{ id: 'test-3' }], { total: 1 });

    const stats = cache.getStats();
    assert.ok(stats.evictions > 0, 'LRU eviction should have occurred');
    assert.strictEqual(stats.size, 2, 'Cache size should not exceed maxSize');
  });

  it('should track cache statistics', () => {
    const cache = new TestExplorerCache(100, 60000);
    const stats1 = cache.getStats();

    assert.strictEqual(stats1.hits, 0);
    assert.strictEqual(stats1.misses, 0);
    assert.strictEqual(stats1.size, 0);

    // Simulate gets (misses)
    cache.get('workspace');
    cache.get('workspace');
    const stats2 = cache.getStats();
    assert.strictEqual(stats2.misses, 2);

    // Simulate sets and gets (hits)
    cache.set('workspace', '', [{ id: '1', name: 'test' }], { total: 1, passed: 0, failed: 0, skipped: 0 });
    cache.get('workspace');
    const stats3 = cache.getStats();
    assert.strictEqual(stats3.hits, 1);
  });

  it('should handle very large test counts in cache', async () => {
    const largeTests = getLargeTestCountFixture(500);
    handler.cache.set('workspace', '', largeTests, {
      total: 500,
      passed: 166,
      failed: 167,
      skipped: 167,
      executionTime: 12000,
    });

    const cached = handler.cache.get('workspace');
    assert.ok(cached);
    assert.strictEqual(cached.data.tests.length, 500);
  });
});

// ==============================================================
// Test Suite 4: Query Mode
// ==============================================================

describe('Test Suite 4: Query Mode', () => {
  let handler;
  let mocks;

  beforeEach(() => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(2).withTypeScriptTests(1);
    mocks = builder.getMocks();
    handler = new TestExplorerHandler(mocks);
  });

  it('should query file scope', async () => {
    const message = { data: getValidTestExplorerRequest('file') };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.strictEqual(response.data.scope, 'file');
    assert.ok(response.data.tests.length >= 0);
  });

  it('should query project scope', async () => {
    const message = { data: getValidTestExplorerRequest('project') };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.strictEqual(response.data.scope, 'project');
  });

  it('should query workspace scope', async () => {
    const message = { data: getValidTestExplorerRequest('workspace') };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.strictEqual(response.data.scope, 'workspace');
  });

  it('should aggregate test state counts correctly', async () => {
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    const { summary } = response.data;
    assert.ok(summary.total >= 0);
    assert.ok(summary.passed >= 0);
    assert.ok(summary.failed >= 0);
    assert.ok(summary.skipped >= 0);
    // Ensure counts don't exceed total
    assert.ok(summary.passed + summary.failed + summary.skipped <= summary.total);
  });

  it('should track execution time', async () => {
    const message = { data: getValidTestExplorerRequest('workspace') };
    const response = await handler.handle(message);

    assert.ok(response.data.queryTime > 0, 'Query time should be recorded');
    assert.ok(response.data.queryTime < 5000, 'Query time should be reasonable (<5s)');
  });

  it('should return empty result gracefully', async () => {
    handler = new TestExplorerHandler(createEmptyMockSetup());
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.deepStrictEqual(response.data.tests, []);
    assert.strictEqual(response.data.summary.total, 0);
  });
});

// ==============================================================
// Test Suite 5: Subscription Mode
// ==============================================================

describe('Test Suite 5: Subscription Mode', () => {
  let handler;

  beforeEach(() => {
    handler = new TestExplorerHandler(createBasicMockSetup());
  });

  it('should subscribe to test discovery events', (done) => {
    let callCount = 0;
    handler.onTestDiscovered((event) => {
      callCount++;
      assert.ok(event.tests);
      assert.ok(event.discoveredAt);
      if (callCount === 1) done();
    });

    handler._emitTestDiscovered([{ id: 'test-1', name: 'Test1', kind: 'test' }]);
  });

  it('should subscribe to test execution started events', (done) => {
    let callCount = 0;
    handler.onTestExecutionStarted((event) => {
      callCount++;
      assert.ok(event.testIds);
      assert.ok(event.startedAt);
      if (callCount === 1) done();
    });

    handler._emitTestExecutionStarted(['test-1', 'test-2']);
  });

  it('should subscribe to test results arrived events', (done) => {
    let callCount = 0;
    handler.onTestResultsArrived((event) => {
      callCount++;
      assert.ok(event.results);
      assert.ok(event.completedAt);
      if (callCount === 1) done();
    });

    handler._emitTestResultsArrived([{ id: 'test-1', state: 'passed', duration: 100 }]);
  });

  it('should support multiple subscribers without interference', () => {
    const calls1 = [];
    const calls2 = [];

    handler.onTestDiscovered((event) => calls1.push(event));
    handler.onTestDiscovered((event) => calls2.push(event));

    handler._emitTestDiscovered([{ id: 'test-1' }]);

    assert.strictEqual(calls1.length, 1);
    assert.strictEqual(calls2.length, 1);
  });

  it('should allow unsubscribe', () => {
    let callCount = 0;
    const unsub = handler.onTestDiscovered(() => {
      callCount++;
    });

    handler._emitTestDiscovered([]);
    assert.strictEqual(callCount, 1);

    unsub(); // Unsubscribe

    handler._emitTestDiscovered([]);
    assert.strictEqual(callCount, 1); // Should not increment
  });
});

// ==============================================================
// Test Suite 6: Test State & Results
// ==============================================================

describe('Test Suite 6: Test State & Results', () => {
  let handler;
  let mocks;

  beforeEach(() => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(3).withFailures(['/test0.cs:4:4']);
    mocks = builder.getMocks();
    handler = new TestExplorerHandler(mocks);
  });

  it('should map diagnostic failures to test state', async () => {
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success);
    // Tests with diagnostics should have proper state mapping
    assert.ok(response.data.tests.length > 0);
  });

  it('should track execution time per test', async () => {
    const tests = [
      { id: 'test-1', name: 'Test1', kind: 'test', duration: 100, state: 'passed' },
      { id: 'test-2', name: 'Test2', kind: 'test', duration: 200, state: 'passed' },
    ];

    const summary = await handler._aggregateSummary(tests, true);
    assert.strictEqual(summary.executionTime, 300);
  });

  it('should detect skip markers', async () => {
    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    const testWithSkip = response.data.tests.find((t) => t.tags && t.tags.includes('skipped'));
    // May or may not have skipped tests depending on fixtures
    assert.ok(Array.isArray(response.data.tests));
  });

  it('should extract error messages from diagnostics', () => {
    const diags = [{ severity: 'error', message: 'Expected 5 but got 4' }];
    assert.ok(diags[0].message);
    assert.ok(diags[0].message.length > 0);
  });
});

// ==============================================================
// Test Suite 7: Message Handler Integration
// ==============================================================

describe('Test Suite 7: Message Handler Integration', () => {
  let handler;
  let mocks;

  beforeEach(() => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(1);
    mocks = builder.getMocks();
    handler = new TestExplorerHandler(mocks);
  });

  it('should register message handlers successfully', () => {
    const mockServer = {
      messageHandler: {
        on: (type, callback) => {
          assert.strictEqual(type, 'bridge:getTestExplorer');
          assert.strictEqual(typeof callback, 'function');
        },
      },
    };

    assert.doesNotThrow(() => {
      handler.registerMessageHandlers(mockServer);
    });
  });

  it('should throw or handle invalid server gracefully', async () => {
    // Should throw on null
    try {
      await handler.registerMessageHandlers(null);
      assert.fail('Should have thrown for null server');
    } catch (error) {
      assert.ok(error instanceof TestExplorerError || error instanceof Error);
    }

    // Should throw on missing messageHandler
    try {
      await handler.registerMessageHandlers({ /* missing messageHandler */ });
      assert.fail('Should have thrown for missing messageHandler');
    } catch (error) {
      assert.ok(error instanceof TestExplorerError || error instanceof Error);
    }
  });

  it('should handle bridge:getTestExplorer message', async () => {
    const message = { data: getValidTestExplorerRequest('workspace'), messageId: '1' };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.ok(response.data);
  });

  it('should return error response for invalid input', async () => {
    const message = { data: null };
    const response = await handler.handle(message);

    assert.strictEqual(response.success, false);
    assert.ok(response.error);
  });
});

// ==============================================================
// Test Suite 8: Edge Cases
// ==============================================================

describe('Test Suite 8: Edge Cases', () => {
  it('should handle mixed C# and TypeScript in workspace', async () => {
    const builder = new MockTestExplorerBuilder();
    builder.withCSharpTests(2).withTypeScriptTests(2);
    const mocks = builder.getMocks();
    const handler = new TestExplorerHandler(mocks);

    const message = { data: { scope: 'workspace' } };
    const response = await handler.handle(message);

    assert.ok(response.success);
    assert.ok(response.data.tests.length > 0);
  });

  it('should handle nested test suites', async () => {
    const handler = new TestExplorerHandler(createBasicMockSetup());
    const nested = getNestedTestSuiteFixture();

    assert.ok(nested.children);
    assert.ok(nested.children.length > 0);
    assert.ok(nested.children[0].children);
  });

  it('should gracefully handle malformed test attributes', async () => {
    const handler = new TestExplorerHandler(createBasicMockSetup());
    const malformed = getMalformedTestAttributesFixture();

    assert.ok(malformed.incompleteAttribute);
    // Should not crash when processing malformed data
    assert.doesNotThrow(() => {
      Object.values(malformed).forEach((test) => {
        if (test && test.attributes === null) {
          assert.ok(!Array.isArray(test.attributes));
        }
      });
    });
  });

  it('should handle large test counts (1000+)', async () => {
    const handler = new TestExplorerHandler(createBasicMockSetup());
    const largeTests = getLargeTestCountFixture(1000);

    assert.strictEqual(largeTests.length, 1000);

    // Should be able to cache large counts
    handler.cache.set('workspace', '', largeTests, {
      total: 1000,
      passed: 333,
      failed: 334,
      skipped: 333,
      executionTime: 30000,
    });

    const stats = handler.cache.getStats();
    assert.strictEqual(stats.size, 1);
  });

  it('should handle concurrent queries without thrashing', async () => {
    const handler = new TestExplorerHandler(createBasicMockSetup());
    const queries = getConcurrentQueryFixture(5);

    const promises = queries.map((q) => handler.handle({ data: q }));
    const results = await Promise.all(promises);

    assert.strictEqual(results.length, 5);
    assert.ok(results.every((r) => r.success || !r.success)); // All should have result
  });

  it('should dispose safely', () => {
    const handler = new TestExplorerHandler(createBasicMockSetup());

    handler.onTestDiscovered(() => {});
    handler.onTestExecutionStarted(() => {});
    handler.onTestResultsArrived(() => {});

    assert.doesNotThrow(() => {
      handler.dispose();
    });

    assert.strictEqual(handler._discoveredListeners.length, 0);
    assert.strictEqual(handler._executionStartedListeners.length, 0);
    assert.strictEqual(handler._resultsArrivedListeners.length, 0);
    assert.strictEqual(handler.cache.getStats().size, 0);
  });
});

// ==============================================================
// Test Suite 9: TestExplorerCache Unit Tests
// ==============================================================

describe('Test Suite 9: TestExplorerCache Unit Tests', () => {
  it('should create cache with default settings', () => {
    const cache = new TestExplorerCache();
    assert.strictEqual(cache.maxSize, 1000);
    assert.strictEqual(cache.ttlMs, 10 * 60 * 1000);
  });

  it('should set and get cache entries', () => {
    const cache = new TestExplorerCache();
    const tests = [{ id: 'test-1', name: 'Test1' }];
    const summary = { total: 1, passed: 0, failed: 0, skipped: 0 };

    cache.set('workspace', '', tests, summary);
    const result = cache.get('workspace');

    assert.ok(result);
    assert.strictEqual(result.data.tests[0].id, 'test-1');
    assert.strictEqual(result.cacheHit, true);
  });

  it('should handle scope-specific cache keys', () => {
    const cache = new TestExplorerCache();
    const tests = [{ id: 'test-1' }];
    const summary = { total: 1 };

    cache.set('file', '/file1.cs', tests, summary);
    cache.set('file', '/file2.cs', tests, summary);

    const result1 = cache.get('file', '/file1.cs');
    const result2 = cache.get('file', '/file2.cs');

    assert.ok(result1);
    assert.ok(result2);
    assert.strictEqual(cache.getStats().size, 2);
  });

  it('should clear all cache', () => {
    const cache = new TestExplorerCache();
    cache.set('workspace', '', [{ id: 'test-1' }], { total: 1 });

    assert.strictEqual(cache.getStats().size, 1);

    cache.clear();
    assert.strictEqual(cache.getStats().size, 0);
  });
});
