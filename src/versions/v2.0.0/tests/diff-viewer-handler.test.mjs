#!/usr/bin/env node

/**
 * Test Suite for Diff-Viewer Handler (Step 92)
 *
 * Comprehensive tests covering initialization, validation, diff generation,
 * hunk application, error handling, caching, performance, and integration.
 *
 * Run: node --test src/versions/v2.0.0/tests/diff-viewer-handler.test.mjs
 *
 * @module src/versions/v2.0.0/tests/diff-viewer-handler.test.mjs
 */

import test from 'node:test';
import assert from 'node:assert/strict';
import createDiffViewerHandler, {
  DiffViewerError,
  DiffValidationError,
  DiffGenerationError,
  HunkApplicationError,
  DiffViewerOperationType,
} from '../lib/diff-viewer-handler.mjs';
import {
  SIMPLE_ORIGINAL,
  SIMPLE_MODIFIED,
  COMPLEX_ORIGINAL,
  COMPLEX_MODIFIED,
  IDENTICAL_ORIGINAL,
  IDENTICAL_MODIFIED,
  LARGE_ORIGINAL,
  LARGE_MODIFIED,
  UNICODE_ORIGINAL,
  UNICODE_MODIFIED,
  createMockDocumentProvider,
  createMockLogger,
  createMockMetrics,
  verifyHunkCount,
  verifyUnifiedDiffFormat,
  verifyHunkStructure,
  verifyEditStructure,
} from './mocks/diff-viewer-fixtures.mjs';

// ============================================================================
// SUITE 1: INITIALIZATION & DEPENDENCY INJECTION
// ============================================================================

test('Suite 1: Initialization & DI', async (t) => {
  await t.test(
    'should throw when DocumentProvider is missing',
    async () => {
      try {
        await createDiffViewerHandler({});
        assert.fail('Expected DiffViewerError');
      } catch (err) {
        assert(err instanceof DiffViewerError);
        assert.strictEqual(err.errorCode, 'MISSING_DEPENDENCY');
      }
    }
  );

  await t.test('should create handler with required DocumentProvider', async () => {
    const provider = createMockDocumentProvider();
    const handler = await createDiffViewerHandler({ documentProvider: provider });
    assert(typeof handler === 'function');
  });

  await t.test(
    'should accept optional logger and metrics',
    async () => {
      const provider = createMockDocumentProvider();
      const logger = createMockLogger();
      const metrics = createMockMetrics();
      const handler = await createDiffViewerHandler({
        documentProvider: provider,
        logger,
        metrics,
      });
      assert(typeof handler === 'function');
    }
  );
});

// ============================================================================
// SUITE 2: INPUT VALIDATION
// ============================================================================

test('Suite 2: Input Validation', async (t) => {
  const provider = createMockDocumentProvider({
    'source.js': { text: SIMPLE_ORIGINAL },
    'target.js': { text: SIMPLE_MODIFIED },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  await t.test('should reject getDiff without filePath', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'test-1',
        data: { targetPath: 'target.js' },
      },
      {}
    );
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'DIFF_VALIDATION_ERROR');
  });

  await t.test('should reject getDiff without targetPath or targetContent', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'test-2',
        data: { filePath: 'source.js' },
      },
      {}
    );
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'DIFF_VALIDATION_ERROR');
  });

  await t.test('should reject applyDiff without filePath', async () => {
    const response = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'test-3',
        data: { hunks: [] },
      },
      {}
    );
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'DIFF_VALIDATION_ERROR');
  });

  await t.test('should reject unknown message type', async () => {
    const response = await handler(
      {
        messageType: 'bridge:unknownType',
        messageId: 'test-4',
        data: {},
      },
      {}
    );
    assert.strictEqual(response.success, false);
  });
});

// ============================================================================
// SUITE 3: DIFF GENERATION
// ============================================================================

test('Suite 3: Diff Generation', async (t) => {
  const provider = createMockDocumentProvider({
    'simple-src.js': { text: SIMPLE_ORIGINAL },
    'simple-tgt.js': { text: SIMPLE_MODIFIED },
    'complex-src.js': { text: COMPLEX_ORIGINAL },
    'complex-tgt.js': { text: COMPLEX_MODIFIED },
    'identical-src.js': { text: IDENTICAL_ORIGINAL },
    'identical-tgt.js': { text: IDENTICAL_MODIFIED },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  await t.test(
    'should generate diff for simple single-line change',
    async () => {
      const response = await handler(
        {
          messageType: 'bridge:getDiff',
          messageId: 'test-5',
          data: {
            filePath: 'simple-src.js',
            targetPath: 'simple-tgt.js',
          },
        },
        {}
      );
      assert.strictEqual(response.success, true);
      assert(Array.isArray(response.data.hunks));
      assert(response.data.hunks.length > 0);
      assert.strictEqual(response.data.stats.linesAdded, 1);
      assert.strictEqual(response.data.stats.linesRemoved, 0);
      assert(verifyUnifiedDiffFormat(response.data.diff));
      assert(verifyHunkStructure(response.data.hunks));
    }
  );

  await t.test('should generate diff for complex multi-hunk change', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'test-6',
        data: {
          filePath: 'complex-src.js',
          targetPath: 'complex-tgt.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert(response.data.hunks.length >= 1);
    assert(response.data.stats.linesAdded > 0);
  });

  await t.test('should return empty diff for identical documents', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'test-7',
        data: {
          filePath: 'identical-src.js',
          targetPath: 'identical-tgt.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.hunks.length, 0);
    assert.strictEqual(response.data.stats.linesAdded, 0);
    assert.strictEqual(response.data.stats.linesRemoved, 0);
  });

  await t.test('should support inline targetContent instead of targetPath', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'test-8',
        data: {
          filePath: 'simple-src.js',
          targetContent: SIMPLE_MODIFIED,
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert(response.data.hunks.length > 0);
  });
});

// ============================================================================
// SUITE 4: HUNK APPLICATION
// ============================================================================

test('Suite 4: Hunk Application', async (t) => {
  const provider = createMockDocumentProvider({
    'source.js': { text: SIMPLE_ORIGINAL },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  // First generate a diff
  const diffResponse = await handler(
    {
      messageType: 'bridge:getDiff',
      messageId: 'diff',
      data: {
        filePath: 'source.js',
        targetContent: SIMPLE_MODIFIED,
      },
    },
    {}
  );

  assert.strictEqual(diffResponse.success, true);
  const { hunks } = diffResponse.data;

  await t.test('should apply all hunks when no indices specified', async () => {
    const response = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'apply-1',
        data: {
          filePath: 'source.js',
          hunks,
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.applied, true);
    assert(response.data.edits.length > 0);
    assert(verifyEditStructure(response.data.edits));
  });

  await t.test('should apply specific hunks via indices', async () => {
    const response = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'apply-2',
        data: {
          filePath: 'source.js',
          hunks,
          hunkIndices: [0],
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.applied, true);
  });

  await t.test('should handle empty hunk selection', async () => {
    const response = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'apply-3',
        data: {
          filePath: 'source.js',
          hunks,
          hunkIndices: [],
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert.strictEqual(response.data.applied, false);
  });
});

// ============================================================================
// SUITE 5: ERROR HANDLING
// ============================================================================

test('Suite 5: Error Handling', async (t) => {
  const provider = createMockDocumentProvider({
    'exists.js': { text: SIMPLE_ORIGINAL },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  await t.test('should handle file not found gracefully', async () => {
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'error-1',
        data: {
          filePath: '/nonexistent/file.js',
          targetPath: 'exists.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'DIFF_GENERATION_ERROR');
  });

  await t.test('should handle DocumentProvider errors', async () => {
    const badProvider = {
      async queryDocument() {
        throw new Error('Provider error');
      },
    };
    const badHandler = await createDiffViewerHandler({
      documentProvider: badProvider,
    });
    const response = await badHandler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'error-2',
        data: {
          filePath: 'any.js',
          targetPath: 'other.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, false);
  });

  await t.test('should reject applyDiff without hunks array', async () => {
    const response = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'error-3',
        data: {
          filePath: 'exists.js',
          hunks: null,
        },
      },
      {}
    );
    assert.strictEqual(response.success, false);
    assert.strictEqual(response.error.code, 'DIFF_VALIDATION_ERROR');
  });
});

// ============================================================================
// SUITE 6: CACHING & TTL
// ============================================================================

test('Suite 6: Caching & TTL', async (t) => {
  const provider = createMockDocumentProvider({
    'cache-test.js': { text: SIMPLE_ORIGINAL },
    'target.js': { text: SIMPLE_MODIFIED },
  });
  const logger = createMockLogger();
  const handler = await createDiffViewerHandler({
    documentProvider: provider,
    logger,
    cacheTtlMs: 100, // Short TTL for testing
  });

  await t.test('should cache diff results on repeated calls', async () => {
    // First call - not cached
    const response1 = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'cache-1',
        data: {
          filePath: 'cache-test.js',
          targetPath: 'target.js',
        },
      },
      {}
    );
    assert.strictEqual(response1.success, true);

    // Second call - should be cached (same response)
    const response2 = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'cache-2',
        data: {
          filePath: 'cache-test.js',
          targetPath: 'target.js',
        },
      },
      {}
    );
    assert.strictEqual(response2.success, true);
    assert.deepStrictEqual(response1.data.diff, response2.data.diff);
  });

  await t.test('should invalidate cache after TTL expires', async (t) => {
    // Generate initial diff
    await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'ttl-1',
        data: {
          filePath: 'cache-test.js',
          targetPath: 'target.js',
        },
      },
      {}
    );

    // Wait for cache to expire
    await new Promise(r => setTimeout(r, 150));

    // Next call should regenerate
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'ttl-2',
        data: {
          filePath: 'cache-test.js',
          targetPath: 'target.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
  });
});

// ============================================================================
// SUITE 7: PERFORMANCE GATES
// ============================================================================

test('Suite 7: Performance Gates', async (t) => {
  const provider = createMockDocumentProvider({
    'large-src.js': { text: LARGE_ORIGINAL },
    'large-tgt.js': { text: LARGE_MODIFIED },
    'simple-src.js': { text: SIMPLE_ORIGINAL },
    'simple-tgt.js': { text: SIMPLE_MODIFIED },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  await t.test('should generate diff under 50ms for typical files', async () => {
    const start = Date.now();
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'perf-1',
        data: {
          filePath: 'simple-src.js',
          targetPath: 'simple-tgt.js',
        },
      },
      {}
    );
    const duration = Date.now() - start;
    assert(duration < 50, `Diff took ${duration}ms, expected < 50ms`);
    assert.strictEqual(response.success, true);
  });

  await t.test('should generate large diff under 200ms', async () => {
    const start = Date.now();
    const response = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'perf-2',
        data: {
          filePath: 'large-src.js',
          targetPath: 'large-tgt.js',
        },
      },
      {}
    );
    const duration = Date.now() - start;
    assert(duration < 200, `Large diff took ${duration}ms, expected < 200ms`);
    assert.strictEqual(response.success, true);
  });
});

// ============================================================================
// SUITE 8: INTEGRATION WITH APPLY-EDIT
// ============================================================================

test('Suite 8: Integration with Apply-Edit', async (t) => {
  const provider = createMockDocumentProvider({
    'integration-src.js': { text: COMPLEX_ORIGINAL },
    'integration-tgt.js': { text: COMPLEX_MODIFIED },
  });
  const handler = await createDiffViewerHandler({ documentProvider: provider });

  await t.test('should generate hunks compatible with apply-edit', async () => {
    // Get diff
    const diffResponse = await handler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'int-1',
        data: {
          filePath: 'integration-src.js',
          targetPath: 'integration-tgt.js',
        },
      },
      {}
    );
    assert.strictEqual(diffResponse.success, true);

    // Apply diff
    const applyResponse = await handler(
      {
        messageType: 'bridge:applyDiff',
        messageId: 'int-2',
        data: {
          filePath: 'integration-src.js',
          hunks: diffResponse.data.hunks,
        },
      },
      {}
    );
    assert.strictEqual(applyResponse.success, true);

    // Verify edit structure is apply-edit-handler compatible
    const edits = applyResponse.data.edits;
    assert(Array.isArray(edits));
    assert(edits.length > 0);
    assert(verifyEditStructure(edits));
  });

  await t.test('should handle unicode content correctly', async () => {
    const unicodeProvider = createMockDocumentProvider({
      'unicode-src.js': { text: UNICODE_ORIGINAL },
      'unicode-tgt.js': { text: UNICODE_MODIFIED },
    });
    const unicodeHandler = await createDiffViewerHandler({
      documentProvider: unicodeProvider,
    });

    const response = await unicodeHandler(
      {
        messageType: 'bridge:getDiff',
        messageId: 'int-3',
        data: {
          filePath: 'unicode-src.js',
          targetPath: 'unicode-tgt.js',
        },
      },
      {}
    );
    assert.strictEqual(response.success, true);
    assert(response.data.hunks.length > 0);
  });
});

console.log('✅ All diff-viewer-handler tests completed');
