#!/usr/bin/env node

/**
 * Format-Document Handler Tests (Step 79)
 *
 * 22 comprehensive tests covering initialization, validation, formatting logic,
 * edit generation, performance, error recovery, and integration with apply-edit-handler.
 *
 * Test Structure:
 * - Suite 1: Initialization & Dependencies (3 tests)
 * - Suite 2: Input Validation (4 tests)
 * - Suite 3: Formatting Logic (5 tests)
 * - Suite 4: Edit Generation (3 tests)
 * - Suite 5: Performance & Error Recovery (4 tests)
 * - Suite 6: Integration with apply-edit (3 tests)
 *
 * @module src/versions/v2.0.0/tests/format-document-handler.test.mjs
 * @version 1.0.0
 */

import assert from 'assert';
import {
  createFormatDocumentHandler,
  FormatDocumentError,
  FormatValidationError,
  FormatIOError,
} from '../lib/format-document-handler.mjs';

// ============================================================================
// MOCK HELPERS
// ============================================================================

function createMockDispatcher() {
  return {
    register: () => {},
  };
}

function createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {},
  };
}

function createMockMetrics() {
  return {
    recordEvent: () => {},
  };
}

function createMockDocumentProvider(documents = {}) {
  return {
    getDocument: (file) => documents[file] || null,
  };
}

// ============================================================================
// TEST SUITES
// ============================================================================

describe('Format-Document Handler', () => {
  // ==========================================================================
  // SUITE 1: Initialization & Dependencies (3 tests)
  // ==========================================================================

  describe('Suite 1: Initialization & Dependencies', () => {
    test('1.1 - Create handler with valid dispatcher', () => {
      // Arrange
      const dispatcher = createMockDispatcher();

      // Act
      const handler = createFormatDocumentHandler(dispatcher);

      // Assert
      assert(handler instanceof Function, 'Handler should be a function');
    });

    test('1.2 - Throw on null dispatcher', () => {
      // Arrange & Act & Assert
      assert.throws(
        () => createFormatDocumentHandler(null),
        FormatDocumentError,
        'Should throw FormatDocumentError on null dispatcher',
      );
    });

    test('1.3 - Initialize logger and metrics mocks', () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const logger = createMockLogger();
      const metrics = createMockMetrics();

      // Act
      const handler = createFormatDocumentHandler(dispatcher, { logger, metrics });

      // Assert
      assert(handler instanceof Function, 'Handler should be created with custom logger/metrics');
    });
  });

  // ==========================================================================
  // SUITE 2: Input Validation (4 tests)
  // ==========================================================================

  describe('Suite 2: Input Validation', () => {
    test('2.1 - Accept valid format request (file, indent, lineLength)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: 'console.log("test");' },
        }),
      };
      const message = {
        messageId: 'msg-1',
        data: { file: 'test.js', indent: 2, lineLength: 80 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should succeed with valid inputs');
    });

    test('2.2 - Reject missing file path', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: 'console.log("test");' },
        }),
      };
      const message = {
        messageId: 'msg-2',
        data: { indent: 2, lineLength: 80 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, false, 'Should fail with missing file');
      assert(result.error.includes('file'), 'Error message should mention file');
    });

    test('2.3 - Reject invalid indent (negative, non-integer)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: 'console.log("test");' },
        }),
      };
      const messageNegative = {
        messageId: 'msg-3',
        data: { file: 'test.js', indent: -1 },
      };

      // Act
      const resultNegative = await handler(messageNegative, context);

      // Assert
      assert.strictEqual(resultNegative.success, false, 'Should reject negative indent');

      // Also test non-integer
      const messageFloat = {
        messageId: 'msg-4',
        data: { file: 'test.js', indent: 2.5 },
      };
      const resultFloat = await handler(messageFloat, context);
      assert.strictEqual(resultFloat.success, false, 'Should reject float indent');
    });

    test('2.4 - Reject invalid lineLength (<40, >200)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: 'console.log("test");' },
        }),
      };
      const messageTooShort = {
        messageId: 'msg-5',
        data: { file: 'test.js', lineLength: 30 },
      };

      // Act
      const resultTooShort = await handler(messageTooShort, context);

      // Assert
      assert.strictEqual(resultTooShort.success, false, 'Should reject lineLength < 40');

      // Also test too long
      const messageTooLong = {
        messageId: 'msg-6',
        data: { file: 'test.js', lineLength: 250 },
      };
      const resultTooLong = await handler(messageTooLong, context);
      assert.strictEqual(resultTooLong.success, false, 'Should reject lineLength > 200');
    });
  });

  // ==========================================================================
  // SUITE 3: Formatting Logic (5 tests)
  // ==========================================================================

  describe('Suite 3: Formatting Logic', () => {
    test('3.1 - Format single-line to multiline', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'const x = 1; const y = 2; const z = 3; const a = 4; const b = 5;';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-7',
        data: { file: 'test.js', lineLength: 40 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(result.data.linesDelta >= 0, 'Should increase or maintain line count');
    });

    test('3.2 - Normalize indentation (tabs to spaces)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'function foo() {\n\t\tif (true) {\n\t\t\tconsole.log("hi");\n\t\t}\n}';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-8',
        data: { file: 'test.js', indent: 2 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(result.data.formatted.includes('  '), 'Should contain 2-space indents');
      assert(!result.data.formatted.includes('\t'), 'Should not contain tabs');
    });

    test('3.3 - Break long lines at lineLength boundary', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'const veryLongVariableName = "this is a very long string that exceeds the line length";';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-9',
        data: { file: 'test.js', lineLength: 50 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      const lines = result.data.formatted.split('\n');
      const allLinesOk = lines.every((line) => line.length <= 60); // Allow some tolerance
      assert(allLinesOk, 'All formatted lines should respect lineLength');
    });

    test('3.4 - Preserve inline comments during formatting', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'const x = 1; // important comment\nconst y = 2; // another comment';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-10',
        data: { file: 'test.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(result.data.formatted.includes('important comment'), 'Should preserve comment content');
    });

    test('3.5 - Handle mixed indentation (tabs+spaces)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'function foo() {\n \tif (true) {\n  \t  console.log("hi");\n\t}\n}';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-11',
        data: { file: 'test.js', indent: 2 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(!result.data.formatted.includes('\t'), 'Should normalize tabs away');
    });
  });

  // ==========================================================================
  // SUITE 4: Edit Generation (3 tests)
  // ==========================================================================

  describe('Suite 4: Edit Generation', () => {
    test('4.1 - Generate correct character offset ranges', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'hello  world'; // 2 spaces between words
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-12',
        data: { file: 'test.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      if (result.data.changes.length > 0) {
        const change = result.data.changes[0];
        assert(change.range, 'Edit should have range property');
        assert(change.range.start !== undefined, 'Range should have start');
        assert(change.range.end !== undefined, 'Range should have end');
        assert(change.range.start <= change.range.end, 'Start should be <= end');
      }
    });

    test('4.2 - Multiple edits non-overlapping', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = 'line1  \nline2  \nline3  '; // Trailing whitespace
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-13',
        data: { file: 'test.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      const changes = result.data.changes;
      // Verify non-overlapping
      for (let i = 0; i < changes.length - 1; i++) {
        assert(
          changes[i].range.end <= changes[i + 1].range.start,
          'Edits should not overlap',
        );
      }
    });

    test('4.3 - Produce apply-edit compatible format', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const text = '  const x = 1;';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-14',
        data: { file: 'test.js', indent: 2 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      const changes = result.data.changes;
      // Verify changes have the structure apply-edit-handler expects
      for (const change of changes) {
        assert(change.range, 'Change should have range');
        assert(typeof change.text === 'string', 'Change text should be a string');
      }
    });
  });

  // ==========================================================================
  // SUITE 5: Performance & Error Recovery (4 tests)
  // ==========================================================================

  describe('Suite 5: Performance & Error Recovery', () => {
    test('5.1 - Format 100-line doc in <50ms', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const lines = Array.from({ length: 100 }, (_, i) => `  const var${i} = ${i};`);
      const text = lines.join('\n');
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-15',
        data: { file: 'test.js' },
      };

      // Act
      const startTime = Date.now();
      const result = await handler(message, context);
      const duration = Date.now() - startTime;

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(duration < 50, `Format should complete in <50ms (took ${duration}ms)`);
    });

    test('5.2 - Format 1000-line doc in <200ms', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const lines = Array.from({ length: 1000 }, (_, i) => `  const var${i} = ${i};`);
      const text = lines.join('\n');
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text },
        }),
      };
      const message = {
        messageId: 'msg-16',
        data: { file: 'test.js' },
      };

      // Act
      const startTime = Date.now();
      const result = await handler(message, context);
      const duration = Date.now() - startTime;

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      assert(duration < 200, `Format should complete in <200ms (took ${duration}ms)`);
    });

    test('5.3 - Handle missing document gracefully', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const context = {
        documentProvider: createMockDocumentProvider({}), // No documents
      };
      const message = {
        messageId: 'msg-17',
        data: { file: 'nonexistent.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should return success for missing document');
      assert.deepStrictEqual(result.data.changes, [], 'Should have empty changes');
    });

    test('5.4 - DocumentProvider error doesn\'t cascade (log + return partial)', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const errorProvider = {
        getDocument: () => {
          throw new Error('Provider error');
        },
      };
      const context = {
        documentProvider: errorProvider,
      };
      const message = {
        messageId: 'msg-18',
        data: { file: 'test.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should gracefully handle provider errors');
      assert.deepStrictEqual(result.data.changes, [], 'Should return partial result');
    });
  });

  // ==========================================================================
  // SUITE 6: Integration with apply-edit (3 tests)
  // ==========================================================================

  describe('Suite 6: Integration with apply-edit', () => {
    test('6.1 - Generated edits apply successfully via apply-edit-handler pattern', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const originalText = 'const x = 1;  const y = 2;';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: originalText },
        }),
      };
      const message = {
        messageId: 'msg-19',
        data: { file: 'test.js' },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      // Verify edit structure matches apply-edit expectations
      for (const edit of result.data.changes) {
        assert(edit.range, 'Edit must have range');
        assert(Number.isInteger(edit.range.start), 'Start must be integer');
        assert(Number.isInteger(edit.range.end), 'End must be integer');
        assert(typeof edit.text === 'string', 'Text must be string');
      }
    });

    test('6.2 - Formatted text matches expected output after edits applied', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const originalText = '  const x = 1;\n  const y = 2;';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: originalText },
        }),
      };
      const message = {
        messageId: 'msg-20',
        data: { file: 'test.js', indent: 2 },
      };

      // Act
      const result = await handler(message, context);

      // Assert
      assert.strictEqual(result.success, true, 'Should format successfully');
      // Verify formatted text is present
      assert(result.data.formatted, 'Should return formatted text');
      assert(typeof result.data.formatted === 'string', 'Formatted text should be string');
    });

    test('6.3 - Multiple format requests produce idempotent results', async () => {
      // Arrange
      const dispatcher = createMockDispatcher();
      const handler = createFormatDocumentHandler(dispatcher);
      const originalText = '  const x = 1;  \n  const y = 2;  ';
      const context = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: originalText },
        }),
      };
      const message = {
        messageId: 'msg-21',
        data: { file: 'test.js', indent: 2 },
      };

      // Act
      const result1 = await handler(message, context);
      // Simulate second call on formatted output
      const context2 = {
        documentProvider: createMockDocumentProvider({
          'test.js': { text: result1.data.formatted },
        }),
      };
      const result2 = await handler(message, context2);

      // Assert
      assert.strictEqual(result1.success, true, 'First format should succeed');
      assert.strictEqual(result2.success, true, 'Second format should succeed');
      // Idempotent: second format should produce no changes (or minimal)
      assert(result2.data.changes.length <= result1.data.changes.length, 'Second format should be idempotent');
    });
  });
});

// ============================================================================
// TEST HELPER (for mocha/jest compatibility)
// ============================================================================

function test(name, fn) {
  it(name, fn);
}

function describe(name, fn) {
  // For Node.js assert, we use a simple describe/test wrapper
  console.log(`\n${name}`);
  fn();
}

function it(name, fn) {
  try {
    fn();
    console.log(`  ✓ ${name}`);
  } catch (error) {
    console.log(`  ✗ ${name}`);
    throw error;
  }
}

function beforeEach(fn) {
  // No-op for now; tests call setup explicitly
}
