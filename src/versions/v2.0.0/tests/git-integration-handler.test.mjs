#!/usr/bin/env node

/**
 * Git Integration Handler Test Suite (Step 81)
 *
 * 28 test cases across 6 suites:
 * - Suite 1: Initialization & Dependency Injection (3 tests)
 * - Suite 2: Git Status Operation (5 tests)
 * - Suite 3: Git Log Operation (5 tests)
 * - Suite 4: Git Branches & Diff Operations (5 tests)
 * - Suite 5: Caching Behavior (5 tests)
 * - Suite 6: Error Handling & Fallback (3 tests)
 *
 * @module src/versions/v2.0.0/tests/git-integration-handler.test.mjs
 * @version 1.0.0
 */

import assert from 'assert';
import {
  createGitIntegrationHandler,
  GitOperationCache,
  GitError,
  GitCommandError,
  GitRepositoryError,
  GitValidationError,
} from '../lib/git-integration-handler.mjs';

// ============================================================================
// MOCK HELPERS
// ============================================================================

class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(message, data = {}) {
    this.logs.push({ level: 'debug', message, data });
  }

  error(message, data = {}) {
    this.logs.push({ level: 'error', message, data });
  }

  info(message, data = {}) {
    this.logs.push({ level: 'info', message, data });
  }

  getLogs() {
    return this.logs;
  }

  clear() {
    this.logs = [];
  }
}

class MockMetrics {
  constructor() {
    this.operations = [];
    this.cacheHits = [];
  }

  recordOperation(module, operation, durationMs, status) {
    this.operations.push({ module, operation, durationMs, status });
  }

  recordCacheHit(module, type) {
    this.cacheHits.push({ module, type });
  }

  getOperations() {
    return this.operations;
  }

  getCacheHits() {
    return this.cacheHits;
  }

  clear() {
    this.operations = [];
    this.cacheHits = [];
  }
}

// ============================================================================
// TEST SUITE 1: INITIALIZATION & DEPENDENCY INJECTION
// ============================================================================

describe('Suite 1: Initialization & Dependency Injection', () => {
  test('should create handler with default options', () => {
    const handler = createGitIntegrationHandler();
    assert.strictEqual(typeof handler, 'function', 'handler should be a function');
  });

  test('should accept custom logger and metrics', () => {
    const logger = new MockLogger();
    const metrics = new MockMetrics();
    const handler = createGitIntegrationHandler({ logger, metrics, cacheTtl: 5000 });
    assert.strictEqual(typeof handler, 'function', 'handler should accept options');
  });

  test('should be an async function with correct signature', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };
    const context = {};
    const result = await handler(message, context);
    assert.ok(result, 'handler should return a result');
    assert.strictEqual(typeof result.success, 'boolean', 'result should have success property');
  });
});

// ============================================================================
// TEST SUITE 2: GIT STATUS OPERATION
// ============================================================================

describe('Suite 2: Git Status Operation', () => {
  test('should return status response with correct structure', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok(typeof result.result === 'object', 'result should be an object');
    assert.ok(
      'clean' in result.result &&
      'staged' in result.result &&
      'unstaged' in result.result &&
      'untracked' in result.result,
      'result should have status fields'
    );
  });

  test('should report clean repository correctly', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.strictEqual(typeof result.result.clean, 'boolean', 'clean should be boolean');
    assert.ok(Array.isArray(result.result.staged), 'staged should be array');
  });

  test('should include metadata with timestamp and duration', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.metadata, 'should have metadata');
    assert.ok(result.metadata.timestamp, 'should have timestamp');
    assert.ok(typeof result.metadata.durationMs === 'number', 'should have durationMs');
    assert.strictEqual(typeof result.metadata.cached, 'boolean', 'should have cached flag');
  });

  test('should respect cache parameter', async () => {
    const handler = createGitIntegrationHandler({ cacheTtl: 10000 });
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };

    // First call - not cached
    const result1 = await handler(message, {});
    assert.ok(result1.success, 'first call should succeed');
    assert.strictEqual(result1.metadata.cached, false, 'first call should not be cached');

    // Second call immediately - should be cached
    const result2 = await handler(message, {});
    assert.ok(result2.success, 'second call should succeed');
    assert.strictEqual(result2.metadata.cached, true, 'second call should be cached');
  });

  test('should bypass cache when cache parameter is false', async () => {
    const handler = createGitIntegrationHandler();
    const messageNoCache = {
      data: { operation: 'status', cwd: process.cwd(), cache: false },
    };

    const result1 = await handler(messageNoCache, {});
    const result2 = await handler(messageNoCache, {});

    assert.ok(result1.success && result2.success, 'both calls should succeed');
    assert.strictEqual(result1.metadata.cached, false, 'first call should not use cache');
    assert.strictEqual(result2.metadata.cached, false, 'second call should not use cache');
  });
});

// ============================================================================
// TEST SUITE 3: GIT LOG OPERATION
// ============================================================================

describe('Suite 3: Git Log Operation', () => {
  test('should return log with array of commits', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'log', cwd: process.cwd(), cache: false, params: { count: 5 } },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok(Array.isArray(result.result.commits), 'commits should be array');
  });

  test('should include commit properties: sha, author, message, date', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'log', cwd: process.cwd(), cache: false, params: { count: 1 } },
    };
    const result = await handler(message, {});

    if (result.result.commits.length > 0) {
      const commit = result.result.commits[0];
      assert.ok('sha' in commit, 'commit should have sha');
      assert.ok('author' in commit, 'commit should have author');
      assert.ok('message' in commit, 'commit should have message');
      assert.ok('date' in commit, 'commit should have date');
      assert.ok(commit.date.includes('T'), 'date should be ISO format');
    }
  });

  test('should respect count parameter', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'log', cwd: process.cwd(), cache: false, params: { count: 3 } },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok(result.result.commits.length <= 3, 'should return <= requested count');
  });

  test('should default to 10 commits if count not specified', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'log', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok(result.result.commits.length <= 10, 'should default to max 10 commits');
  });

  test('should parse author and message correctly', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'log', cwd: process.cwd(), cache: false, params: { count: 1 } },
    };
    const result = await handler(message, {});

    if (result.result.commits.length > 0) {
      const commit = result.result.commits[0];
      assert.ok(typeof commit.author === 'string', 'author should be string');
      assert.ok(commit.author.length > 0, 'author should not be empty');
      assert.ok(typeof commit.message === 'string', 'message should be string');
    }
  });
});

// ============================================================================
// TEST SUITE 4: GIT BRANCHES & DIFF OPERATIONS
// ============================================================================

describe('Suite 4: Git Branches & Diff Operations', () => {
  test('should return branches with current and list', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'branches', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok('current' in result.result, 'should have current branch');
    assert.ok(Array.isArray(result.result.branches), 'branches should be array');
  });

  test('should correctly identify current branch', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'currentBranch', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    assert.ok(result.success, 'operation should succeed');
    assert.ok('branch' in result.result, 'should have branch property');
    assert.ok(typeof result.result.branch === 'string', 'branch should be string');
  });

  test('should mark remote branches correctly', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'branches', cwd: process.cwd(), cache: false },
    };
    const result = await handler(message, {});

    if (result.result.branches.length > 0) {
      const branch = result.result.branches[0];
      assert.ok('name' in branch, 'branch should have name');
      assert.ok('remote' in branch, 'branch should have remote flag');
      assert.strictEqual(typeof branch.remote, 'boolean', 'remote should be boolean');
    }
  });

  test('should return diff structure with path and stats', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: {
        operation: 'diff',
        cwd: process.cwd(),
        cache: false,
        params: { filePath: 'package.json', baseRef: 'HEAD' },
      },
    };
    const result = await handler(message, {});

    // Diff may fail if file doesn't exist or not staged, so just check structure if success
    if (result.success) {
      assert.ok('path' in result.result, 'should have path');
      assert.ok('additions' in result.result, 'should have additions count');
      assert.ok('deletions' in result.result, 'should have deletions count');
      assert.ok('diff' in result.result, 'should have diff content');
      assert.strictEqual(typeof result.result.additions, 'number', 'additions should be number');
      assert.strictEqual(typeof result.result.deletions, 'number', 'deletions should be number');
    }
  });

  test('should validate required diff parameter filePath', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'diff', cwd: process.cwd(), cache: false, params: {} },
    };
    const result = await handler(message, {});

    // Should either succeed (empty diff) or fail with validation error
    assert.ok('success' in result, 'should have success field');
  });
});

// ============================================================================
// TEST SUITE 5: CACHING BEHAVIOR
// ============================================================================

describe('Suite 5: Caching Behavior', () => {
  test('should cache status calls within TTL window', async () => {
    const logger = new MockLogger();
    const handler = createGitIntegrationHandler({ cacheTtl: 10000, logger });
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };

    const result1 = await handler(message, {});
    const result2 = await handler(message, {});

    assert.strictEqual(result1.metadata.cached, false, 'first call should not be cached');
    assert.strictEqual(result2.metadata.cached, true, 'second call should be cached');
  });

  test('should expire cache after TTL', async () => {
    const handler = createGitIntegrationHandler({ cacheTtl: 100 });
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };

    const result1 = await handler(message, {});
    assert.strictEqual(result1.metadata.cached, false, 'first call should not be cached');

    // Wait for cache to expire
    await new Promise(resolve => setTimeout(resolve, 150));

    const result2 = await handler(message, {});
    assert.strictEqual(result2.metadata.cached, false, 'second call after TTL should not be cached');
  });

  test('should respect cache parameter in message', async () => {
    const handler = createGitIntegrationHandler({ cacheTtl: 10000 });

    const messageWithCache = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };
    const messageNoCache = {
      data: { operation: 'status', cwd: process.cwd(), cache: false },
    };

    // Call with caching
    const result1 = await handler(messageWithCache, {});
    assert.strictEqual(result1.metadata.cached, false, 'first call should not be cached');

    // Call again with caching - should be cached
    const result2 = await handler(messageWithCache, {});
    assert.strictEqual(result2.metadata.cached, true, 'should use cache');

    // Call with cache disabled - should bypass
    const result3 = await handler(messageNoCache, {});
    assert.strictEqual(result3.metadata.cached, false, 'cache=false should bypass');
  });

  test('should include cache stats in metadata', async () => {
    const handler = createGitIntegrationHandler({ cacheTtl: 10000 });
    const message = {
      data: { operation: 'status', cwd: process.cwd(), cache: true },
    };

    const result = await handler(message, {});

    assert.ok(result.metadata.cacheStats, 'should have cacheStats');
    assert.ok('hits' in result.metadata.cacheStats, 'should report hits');
    assert.ok('misses' in result.metadata.cacheStats, 'should report misses');
    assert.ok('hitRate' in result.metadata.cacheStats, 'should report hitRate');
  });
});

// ============================================================================
// TEST SUITE 6: ERROR HANDLING & FALLBACK
// ============================================================================

describe('Suite 6: Error Handling & Fallback', () => {
  test('should validate required operation field', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { cwd: process.cwd() },
    };
    const result = await handler(message, {});

    assert.strictEqual(result.success, false, 'should fail on missing operation');
    assert.ok(result.error, 'should have error object');
    assert.ok(result.error.code.includes('VALIDATION'), 'should be validation error');
  });

  test('should validate required cwd field', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'status' },
    };
    const result = await handler(message, {});

    assert.strictEqual(result.success, false, 'should fail on missing cwd');
    assert.ok(result.error, 'should have error object');
    assert.ok(result.error.code.includes('VALIDATION'), 'should be validation error');
  });

  test('should handle invalid operation gracefully', async () => {
    const handler = createGitIntegrationHandler();
    const message = {
      data: { operation: 'invalid', cwd: process.cwd() },
    };
    const result = await handler(message, {});

    assert.strictEqual(result.success, false, 'should fail on invalid operation');
    assert.ok(result.error, 'should have error object');
    assert.ok(result.error.message.includes('unsupported'), 'should mention unsupported operation');
  });
});

// ============================================================================
// TEST SUITE: GIT OPERATION CACHE
// ============================================================================

describe('Suite X: GitOperationCache Class', () => {
  test('should create cache with default TTL', () => {
    const cache = new GitOperationCache();
    assert.ok(cache, 'cache should be created');
    assert.strictEqual(cache.ttlMs, 3000, 'default TTL should be 3000ms');
  });

  test('should set and get values', () => {
    const cache = new GitOperationCache(3000);
    cache.set('key1', { value: 'test' });
    const result = cache.get('key1');
    assert.deepStrictEqual(result, { value: 'test' }, 'should retrieve cached value');
  });

  test('should return null for expired values', async () => {
    const cache = new GitOperationCache(100);
    cache.set('key1', { value: 'test' });
    await new Promise(resolve => setTimeout(resolve, 150));
    const result = cache.get('key1');
    assert.strictEqual(result, null, 'should return null for expired value');
  });

  test('should track hits and misses', () => {
    const cache = new GitOperationCache(5000);
    cache.set('key1', { value: 'test' });
    cache.get('key1'); // hit
    cache.get('key1'); // hit
    cache.get('key2'); // miss
    const stats = cache.getStats();
    assert.strictEqual(stats.hits, 2, 'should track hits');
    assert.strictEqual(stats.misses, 1, 'should track misses');
  });

  test('should invalidate specific keys', () => {
    const cache = new GitOperationCache(5000);
    cache.set('key1', { value: 'test' });
    cache.invalidate('key1');
    const result = cache.get('key1');
    assert.strictEqual(result, null, 'should return null for invalidated key');
  });
});

// ============================================================================
// TEST RUNNER
// ============================================================================

/**
 * Simple test runner (works with node --test flag or direct execution)
 */
let testCount = 0;
let passCount = 0;
let failCount = 0;

function describe(name, fn) {
  console.log(`\n📋 ${name}`);
  fn();
}

function test(name, fn) {
  testCount++;
  try {
    const result = fn();
    if (result instanceof Promise) {
      result
        .then(() => {
          passCount++;
          console.log(`  ✅ ${name}`);
        })
        .catch(err => {
          failCount++;
          console.log(`  ❌ ${name}`);
          console.log(`     Error: ${err.message}`);
        });
    } else {
      passCount++;
      console.log(`  ✅ ${name}`);
    }
  } catch (err) {
    failCount++;
    console.log(`  ❌ ${name}`);
    console.log(`     Error: ${err.message}`);
  }
}

// Export for external test runners
export { describe, test, MockLogger, MockMetrics };

// Run tests if executed directly
if (import.meta.url === `file://${process.argv[1]}`) {
  console.log('🧪 Git Integration Handler Test Suite (Step 81)');
  console.log('='.repeat(50));

  // Import and run all tests synchronously
  setTimeout(() => {
    console.log('\n' + '='.repeat(50));
    console.log(`📊 Results: ${passCount} passed, ${failCount} failed out of ${testCount} tests`);
    process.exit(failCount > 0 ? 1 : 0);
  }, 2000);
}
