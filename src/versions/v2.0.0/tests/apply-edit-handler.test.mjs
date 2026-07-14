#!/usr/bin/env node

/**
 * Apply-Edit Handler Tests (Step 78)
 *
 * Comprehensive test suite for the apply-edit handler with 22 tests across 6 suites:
 * 1. Initialization & Validation (3 tests)
 * 2. Single Edit Operations (4 tests)
 * 3. Multiple Edits (4 tests)
 * 4. Edge Cases (4 tests)
 * 5. Error Recovery (4 tests)
 * 6. Metadata & Tracking (3 tests)
 *
 * @module src/versions/v2.0.0/tests/apply-edit-handler.test.mjs
 * @version 1.0.0
 */

import assert from 'assert';
import { test, describe, beforeEach, afterEach } from 'node:test';
import createApplyEditHandler, {
  ApplyEditError,
  ApplyEditValidationError,
  ApplyEditRangeError,
  ApplyEditIOError
} from '../lib/apply-edit-handler.mjs';

// ============================================================================
// TEST FIXTURES & MOCKS
// ============================================================================

/**
 * Sample documents for testing
 */
const SAMPLE_DOCS = {
  '/test/simple.js': 'function hello() {\n  console.log("world");\n}',
  '/test/medium.js': `const x = 1;
const y = 2;
const z = 3;

function test() {
  return x + y + z;
}

module.exports = test;`,
  '/test/large.js': Array(100)
    .fill(null)
    .map((_, i) => `// Line ${i + 1}`)
    .join('\n'),
  '/test/unicode.js': 'const emoji = "😀🎉✨";\nconst chinese = "你好世界";'
};

/**
 * Mock DocumentProvider factory
 */
function createMockDocumentProvider(docs = SAMPLE_DOCS) {
  return {
    async getDocument(path) {
      if (!path) return null;
      if (path in docs) {
        return { path, text: docs[path] };
      }
      return null;
    }
  };
}

/**
 * Mock Logger factory
 */
function createMockLogger() {
  const logs = {
    debug: [],
    error: [],
    warn: []
  };
  return {
    debug(msg, ctx) {
      logs.debug.push({ msg, ctx });
    },
    error(msg, ctx) {
      logs.error.push({ msg, ctx });
    },
    warn(msg, ctx) {
      logs.warn.push({ msg, ctx });
    },
    getLogs() {
      return logs;
    }
  };
}

/**
 * Mock Metrics factory
 */
function createMockMetrics() {
  const operations = [];
  const errors = [];
  return {
    recordOperation(type, data) {
      operations.push({ type, data });
    },
    recordError(type, data) {
      errors.push({ type, data });
    },
    getOperations() {
      return operations;
    },
    getErrors() {
      return errors;
    }
  };
}

/**
 * Generate unique message ID
 */
function generateId() {
  return `msg-${Date.now()}-${Math.random().toString(36).slice(2)}`;
}

/**
 * Create edit request helper
 */
function createEditRequest(filePath, edits) {
  return {
    messageType: 'bridge:applyEdit',
    messageId: generateId(),
    data: { filePath, edits }
  };
}

// ============================================================================
// TESTS
// ============================================================================

describe('Apply-Edit Handler Tests', () => {
  let mockDocProvider;
  let mockLogger;
  let mockMetrics;

  beforeEach(() => {
    mockDocProvider = createMockDocumentProvider(SAMPLE_DOCS);
    mockLogger = createMockLogger();
    mockMetrics = createMockMetrics();
  });

  // ==========================================================================
  // SUITE 1: Initialization & Validation
  // ==========================================================================

  describe('Suite 1: Initialization & Validation (3 tests)', () => {
    test('should create handler with required dependencies', async () => {
      const handler = await createApplyEditHandler({
        documentProvider: mockDocProvider
      });
      assert.strictEqual(typeof handler, 'function');
    });

    test('should create handler with optional logger and metrics', async () => {
      const handler = await createApplyEditHandler({
        documentProvider: mockDocProvider,
        logger: mockLogger,
        metrics: mockMetrics
      });
      assert.strictEqual(typeof handler, 'function');
    });

    test('should reject null or missing documentProvider', async () => {
      try {
        await createApplyEditHandler({ documentProvider: null });
        assert.fail('Should have thrown TypeError');
      } catch (err) {
        assert.ok(err instanceof TypeError);
        assert.match(err.message, /documentProvider/i);
      }
    });
  });

  // ==========================================================================
  // SUITE 2: Single Edit Operations
  // ==========================================================================

  describe('Suite 2: Single Edit Operations (4 tests)', () => {
    test('should insert text at position', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 9, end: 9 }, text: 'world' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.ok(result.applied);
      assert.strictEqual(result.editCount, 1);
      assert.match(result.newText, /function world/);
    });

    test('should replace substring', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 17, end: 22 }, text: 'goodbye' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /goodbye/);
    });

    test('should delete range', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 9, end: 14 }, text: '' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /function\s+\(\)/);
    });

    test('should append to end of file', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const doc = SAMPLE_DOCS['/test/simple.js'];
      const message = createEditRequest('/test/simple.js', [
        { range: { start: doc.length, end: doc.length }, text: '\n// END' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /\/\/ END$/);
    });
  });

  // ==========================================================================
  // SUITE 3: Multiple Edits
  // ==========================================================================

  describe('Suite 3: Multiple Edits (4 tests)', () => {
    test('should apply sequential non-overlapping edits', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const edits = [
        { range: { start: 0, end: 5 }, text: 'fn' }, // "function" -> "fn"
        { range: { start: 25, end: 30 }, text: 'test' } // "world" -> "test"
      ];
      const message = createEditRequest('/test/simple.js', edits);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.strictEqual(result.editCount, 2);
      assert.match(result.newText, /^fn/);
    });

    test('should reject overlapping edit ranges', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const edits = [
        { range: { start: 0, end: 10 }, text: 'x' },
        { range: { start: 5, end: 15 }, text: 'y' } // overlaps
      ];
      const message = createEditRequest('/test/simple.js', edits);

      try {
        await handler(message, {});
        assert.fail('Should have thrown ApplyEditRangeError');
      } catch (err) {
        assert.ok(err instanceof ApplyEditRangeError);
        assert.match(err.message, /Overlapping/i);
      }
    });

    test('should auto-sort and apply out-of-order edits', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      // Deliberately out of order
      const edits = [
        { range: { start: 15, end: 20 }, text: 'X' },
        { range: { start: 0, end: 5 }, text: 'FN' }
      ];
      const message = createEditRequest('/test/simple.js', edits);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.strictEqual(result.editCount, 2);
      assert.match(result.newText, /^FN/);
    });

    test('should apply large batch (50+ edits)', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const largeDoc = SAMPLE_DOCS['/test/large.js'];
      const lines = largeDoc.split('\n');
      const edits = lines.slice(0, 50).map((line, i) => ({
        range: { start: line.length + i, end: line.length + i },
        text: ' [MARKED]'
      }));
      const message = createEditRequest('/test/large.js', edits);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.strictEqual(result.editCount, 50);
      assert.match(result.newText, /\[MARKED\]/);
    });
  });

  // ==========================================================================
  // SUITE 4: Edge Cases
  // ==========================================================================

  describe('Suite 4: Edge Cases (4 tests)', () => {
    test('should handle empty range edits (insert at same position)', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 5, end: 5 }, text: 'INSERTED' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /INSERTED/);
    });

    test('should handle full document replacement', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const doc = SAMPLE_DOCS['/test/simple.js'];
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 0, end: doc.length }, text: 'NEW_CONTENT' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.strictEqual(result.newText, 'NEW_CONTENT');
    });

    test('should handle EOL normalization (CRLF vs LF)', async () => {
      const handler = await createApplyEditHandler({
        documentProvider: {
          async getDocument(path) {
            return { path, text: 'line1\r\nline2\r\nline3' };
          }
        }
      });
      const message = createEditRequest('/test/crlf.js', [
        { range: { start: 7, end: 7 }, text: 'INSERTED' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /INSERTED/);
    });

    test('should handle unicode characters (emoji, multi-byte)', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/unicode.js', [
        { range: { start: 20, end: 20 }, text: '🔥' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.match(result.newText, /🔥/);
    });
  });

  // ==========================================================================
  // SUITE 5: Error Recovery
  // ==========================================================================

  describe('Suite 5: Error Recovery (4 tests)', () => {
    test('should reject invalid range (start > end)', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 10, end: 5 }, text: 'x' }
      ]);

      try {
        await handler(message, {});
        assert.fail('Should have thrown ApplyEditRangeError');
      } catch (err) {
        assert.ok(err instanceof ApplyEditRangeError);
      }
    });

    test('should reject out-of-bounds range', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const doc = SAMPLE_DOCS['/test/simple.js'];
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 0, end: doc.length + 100 }, text: 'x' }
      ]);

      try {
        await handler(message, {});
        assert.fail('Should have thrown ApplyEditRangeError');
      } catch (err) {
        assert.ok(err instanceof ApplyEditRangeError);
        assert.match(err.message, /out of bounds/i);
      }
    });

    test('should reject missing filePath', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest(null, [
        { range: { start: 0, end: 5 }, text: 'x' }
      ]);

      try {
        await handler(message, {});
        assert.fail('Should have thrown ApplyEditValidationError');
      } catch (err) {
        assert.ok(err instanceof ApplyEditValidationError);
        assert.match(err.message, /filePath/i);
      }
    });

    test('should handle documentProvider returning null', async () => {
      const handler = await createApplyEditHandler({
        documentProvider: {
          async getDocument(path) {
            return null;
          }
        }
      });
      const message = createEditRequest('/nonexistent.js', [
        { range: { start: 0, end: 5 }, text: 'x' }
      ]);

      try {
        await handler(message, {});
        assert.fail('Should have thrown ApplyEditIOError');
      } catch (err) {
        assert.ok(err instanceof ApplyEditIOError);
        assert.match(err.message, /not found/i);
      }
    });
  });

  // ==========================================================================
  // SUITE 6: Metadata & Tracking
  // ==========================================================================

  describe('Suite 6: Metadata & Tracking (3 tests)', () => {
    test('should calculate line delta correctly', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 0, end: 0 }, text: 'line0\n' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.ok(result.metadata.lineDelta > 0);
      assert.ok(result.metadata.charDelta > 0);
    });

    test('should track character shift and undo info', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const edits = [
        { range: { start: 0, end: 5 }, text: 'FN' }
      ];
      const message = createEditRequest('/test/simple.js', edits);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.ok(result.metadata.undoInfo);
      assert.ok(result.metadata.undoInfo.originalText);
      assert.deepStrictEqual(result.metadata.undoInfo.originalEdits, edits);
      assert.ok(result.metadata.charDelta < 0); // Replacement reduced size
    });

    test('should record metrics and logs when provided', async () => {
      const handler = await createApplyEditHandler({
        documentProvider: mockDocProvider,
        logger: mockLogger,
        metrics: mockMetrics
      });
      const message = createEditRequest('/test/simple.js', [
        { range: { start: 0, end: 5 }, text: 'FN' }
      ]);

      const result = await handler(message, {});

      assert.ok(result.success);

      // Check metrics
      const ops = mockMetrics.getOperations();
      assert.strictEqual(ops.length, 1);
      assert.strictEqual(ops[0].type, 'bridge:applyEdit');
      assert.ok(ops[0].data.success);
      assert.ok(ops[0].data.duration >= 0);

      // Check logs
      const logs = mockLogger.getLogs();
      assert.ok(logs.debug.length > 0);
      assert.match(logs.debug[0].msg, /Applied.*edits/i);
    });
  });

  // ==========================================================================
  // INTEGRATION TEST
  // ==========================================================================

  describe('Integration Test', () => {
    test('should handle empty edits array gracefully', async () => {
      const handler = await createApplyEditHandler({ documentProvider: mockDocProvider });
      const message = createEditRequest('/test/simple.js', []);

      const result = await handler(message, {});

      assert.ok(result.success);
      assert.ok(result.applied);
      assert.strictEqual(result.editCount, 0);
    });
  });
});
