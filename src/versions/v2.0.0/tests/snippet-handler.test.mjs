#!/usr/bin/env node

/**
 * Snippet Handler Tests (Step 91)
 *
 * Comprehensive test suite for the snippet handler.
 * Tests parsing, validation, placeholder extraction, insertion, and error handling.
 *
 * **Test Coverage**: 37 test cases across 6 suites
 * - Suite 1: Initialization & Dependencies (3 tests)
 * - Suite 2: Snippet Parsing (8 tests)
 * - Suite 3: Validation (8 tests)
 * - Suite 4: Placeholder Extraction (6 tests)
 * - Suite 5: Integration & Insertion (6 tests)
 * - Suite 6: Error Handling (6 tests)
 *
 * **Run**: npx mocha tests/snippet-handler.test.mjs --timeout 10000
 *
 * @module src/versions/v2.0.0/tests/snippet-handler.test.mjs
 */

import assert from 'assert';
import {
  createSnippetHandler,
  parseSnippetTemplate,
  validateSnippetSyntax,
  extractPlaceholders,
  interpolateVariables,
  expandSnippetPlaceholders,
  processEscapes,
  SnippetError,
  SnippetValidationError,
  PositionError,
  SnippetOperationType,
} from '../lib/snippet-handler.mjs';

/**
 * Mock DocumentProvider for testing
 */
function createMockDocumentProvider() {
  const documents = new Map();

  return {
    documents,
    getDocument(filePath) {
      if (!documents.has(filePath)) {
        throw new Error(`Document not found: ${filePath}`);
      }
      return documents.get(filePath);
    },
    setDocument(filePath, content) {
      documents.set(filePath, content);
    },
    updateDocument(filePath, content) {
      if (!documents.has(filePath)) {
        throw new Error(`Cannot update: document not found: ${filePath}`);
      }
      documents.set(filePath, content);
    },
  };
}

/**
 * Mock Logger for testing
 */
function createMockLogger() {
  const logs = [];

  return {
    logs,
    debug(msg) {
      logs.push({ level: 'debug', msg });
    },
    error(msg) {
      logs.push({ level: 'error', msg });
    },
    getLogs() {
      return logs;
    },
  };
}

/**
 * Mock Metrics for testing
 */
function createMockMetrics() {
  const metrics = [];

  return {
    metrics,
    recordMetric(name, value) {
      metrics.push({ name, value });
    },
    getMetrics() {
      return metrics;
    },
  };
}

// ============================================================================
// SUITE 1: Initialization & Dependencies
// ============================================================================

describe('Suite 1: Initialization & Dependencies', function () {
  it('Should throw SnippetError if DocumentProvider missing', function () {
    assert.throws(
      () => createSnippetHandler({}),
      (err) => err instanceof SnippetError
    );
  });

  it('Should create handler with valid dependencies', function () {
    const docProvider = createMockDocumentProvider();
    const handler = createSnippetHandler({ documentProvider: docProvider });

    assert.strictEqual(typeof handler, 'function');
    // Verify handler is callable and async
    assert(handler.length === 2 || handler.constructor.name === 'AsyncFunction');
  });

  it('Should accept optional logger and metrics', function () {
    const docProvider = createMockDocumentProvider();
    const logger = createMockLogger();
    const metrics = createMockMetrics();

    const handler = createSnippetHandler({
      documentProvider: docProvider,
      logger,
      metrics,
    });

    assert.strictEqual(typeof handler, 'function');
  });
});

// ============================================================================
// SUITE 2: Snippet Parsing
// ============================================================================

describe('Suite 2: Snippet Parsing', function () {
  it('Should parse simple placeholder ${1:default}', function () {
    const result = parseSnippetTemplate('hello ${1:world}');
    assert.deepStrictEqual(result.foundNumbers, [1]);
    assert.strictEqual(result.placeholders.length, 1);
    assert.strictEqual(result.placeholders[0].type, 'placeholder');
    assert.strictEqual(result.placeholders[0].number, 1);
    assert.strictEqual(result.placeholders[0].default, 'world');
  });

  it('Should parse multi-stop placeholders', function () {
    const result = parseSnippetTemplate('${1:first} ${2:second} ${3:third}');
    assert.deepStrictEqual(result.foundNumbers, [1, 2, 3]);
    assert.strictEqual(result.placeholders.length, 3);
  });

  it('Should parse final stop ${0}', function () {
    const result = parseSnippetTemplate('${1:name} ${0}');
    assert(result.foundNumbers.includes(0));
    assert(result.foundNumbers.includes(1));
  });

  it('Should parse variables like ${TM_FILENAME}', function () {
    const result = parseSnippetTemplate('File: ${TM_FILENAME}');
    assert.strictEqual(result.placeholders.length, 1);
    assert.strictEqual(result.placeholders[0].type, 'variable');
    assert.strictEqual(result.placeholders[0].name, 'TM_FILENAME');
  });

  it('Should parse escaped dollar \\$', function () {
    const result = parseSnippetTemplate('Price: \\$${1:amount}');
    assert.strictEqual(result.template, 'Price: \\$${1:amount}');
  });

  it('Should parse complex mix of placeholders, variables, and text', function () {
    const template = 'function ${1:name}() {\\n  ${2:// ${TM_FILENAME}}\\n}';
    const result = parseSnippetTemplate(template);
    assert(result.placeholders.length > 0);
  });

  it('Should parse single-char placeholder default', function () {
    const result = parseSnippetTemplate('${1:x}');
    assert.strictEqual(result.placeholders[0].default, 'x');
  });

  it('Should preserve whitespace in template', function () {
    const template = 'line1\n  ${1:indented}\nline3';
    const result = parseSnippetTemplate(template);
    assert.strictEqual(result.template, template);
  });
});

// ============================================================================
// SUITE 3: Validation
// ============================================================================

describe('Suite 3: Validation', function () {
  it('Should accept valid sequential numbering (1, 2, 3)', function () {
    assert.doesNotThrow(() =>
      validateSnippetSyntax('${1:a} ${2:b} ${3:c}')
    );
  });

  it('Should accept numbered placeholders with final stop ${0}', function () {
    assert.doesNotThrow(() =>
      validateSnippetSyntax('${1:a} ${2:b} ${0}')
    );
  });

  it('Should reject non-sequential numbering', function () {
    assert.throws(
      () => validateSnippetSyntax('${1:a} ${3:c}'),
      (err) => err instanceof SnippetValidationError
    );
  });

  it('Should reject unmatched braces', function () {
    assert.throws(
      () => parseSnippetTemplate('${1:a'),
      (err) => err instanceof SnippetValidationError
    );
  });

  it('Should reject unknown variables', function () {
    assert.throws(
      () => validateSnippetSyntax('${UNKNOWN_VAR}'),
      (err) => err instanceof SnippetValidationError
    );
  });

  it('Should accept valid variables like ${TM_FILENAME}', function () {
    assert.doesNotThrow(() =>
      validateSnippetSyntax('File: ${TM_FILENAME}')
    );
  });

  it('Should accept choice syntax ${1|option1,option2|}', function () {
    assert.doesNotThrow(() =>
      validateSnippetSyntax('${1|first|second|}')
    );
  });

  it('Should reject templates larger than 64KB', function () {
    const huge = 'x'.repeat(65537);
    assert.throws(
      () => parseSnippetTemplate(huge),
      (err) => err instanceof SnippetError
    );
  });
});

// ============================================================================
// SUITE 4: Placeholder Extraction
// ============================================================================

describe('Suite 4: Placeholder Extraction', function () {
  it('Should extract primary position for ${1}', function () {
    const template = '${1:name}';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert(result.primaryStop !== null);
    assert.strictEqual(result.primaryStop.number, 1);
  });

  it('Should extract multiple tab stops in order', function () {
    const template = '${1:first} ${2:second} ${3:third}';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert.strictEqual(result.stops.length, 3);
    assert.strictEqual(result.stops[0].number, 1);
    assert.strictEqual(result.stops[1].number, 2);
    assert.strictEqual(result.stops[2].number, 3);
  });

  it('Should extract final stop ${0} position', function () {
    const template = '${1:name} ${0}';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert(result.finalStop !== null);
    assert.strictEqual(result.finalStop.number, 0);
  });

  it('Should calculate offsets correctly', function () {
    const template = 'hello ${1:world}';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert(result.primaryStop !== null);
    // Offset should be 6 (after 'hello ')
    assert.strictEqual(result.primaryStop.index, 6);
  });

  it('Should return empty array if no placeholders', function () {
    const template = 'just plain text';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert.strictEqual(result.stops.length, 0);
  });

  it('Should handle multi-line snippets', function () {
    const template = '${1:line1}\n${2:line2}\n${3:line3}';
    const parsed = parseSnippetTemplate(template);
    const result = extractPlaceholders(template, parsed);

    assert.strictEqual(result.stops.length, 3);
  });
});

// ============================================================================
// SUITE 5: Integration & Insertion
// ============================================================================

describe('Suite 5: Integration & Insertion', async function () {
  it('Should insert simple snippet at line/column', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'let x = ');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    const message = {
      payload: {
        filePath: '/test.js',
        line: 0,
        column: 8,
        template: '${1:value}',
      },
    };

    const response = await handler(message, {});

    assert.strictEqual(response.success, true);
    assert(response.data.insertedText.includes('value'));
  });

  it('Should return cursor position for IDE navigation', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'function ');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    const message = {
      payload: {
        filePath: '/test.js',
        line: 0,
        column: 9,
        template: '${1:myFunc}() {}',
      },
    };

    const response = await handler(message, {});

    assert(response.data.primaryStop !== null);
    assert.strictEqual(response.data.primaryStop.line, 0);
    assert.strictEqual(response.data.primaryStop.number, 1);
  });

  it('Should handle multiple stops and calculate offsets correctly', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'x = ');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    const message = {
      payload: {
        filePath: '/test.js',
        line: 0,
        column: 4,
        template: '${1:first} ${2:second}',
      },
    };

    const response = await handler(message, {});

    assert.strictEqual(response.data.stops.length, 2);
    assert.strictEqual(response.data.stops[0].number, 1);
    assert.strictEqual(response.data.stops[1].number, 2);
    assert(response.data.stops[1].column > response.data.stops[0].column);
  });

  it('Should record metrics when metrics provided', async function () {
    const docProvider = createMockDocumentProvider();
    const metrics = createMockMetrics();
    docProvider.setDocument('/test.js', 'x = ');

    const handler = createSnippetHandler({
      documentProvider: docProvider,
      metrics,
    });

    const message = {
      payload: {
        filePath: '/test.js',
        line: 0,
        column: 4,
        template: '${1:1}',
      },
    };

    await handler(message, {});

    assert(metrics.getMetrics().length > 0);
    const metric = metrics.getMetrics()[0];
    assert.strictEqual(metric.name, 'snippet_insertion');
    assert(metric.value >= 0);
  });

  it('Should update document via provider', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'hello ');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    await handler(
      {
        payload: {
          filePath: '/test.js',
          line: 0,
          column: 6,
          template: 'world',
        },
      },
      {}
    );

    const updated = docProvider.getDocument('/test.js');
    assert.strictEqual(updated, 'hello world');
  });
});

// ============================================================================
// SUITE 6: Error Handling
// ============================================================================

describe('Suite 6: Error Handling', async function () {
  it('Should throw SnippetError for missing filePath', async function () {
    const docProvider = createMockDocumentProvider();
    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler({ payload: { line: 0, column: 0, template: 'test' } }, {});
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof SnippetError);
    }
  });

  it('Should throw PositionError for invalid line number', async function () {
    const docProvider = createMockDocumentProvider();
    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler(
        {
          payload: {
            filePath: '/test.js',
            line: -1,
            column: 0,
            template: 'test',
          },
        },
        {}
      );
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof PositionError);
    }
  });

  it('Should throw SnippetValidationError for invalid template', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', '');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler(
        {
          payload: {
            filePath: '/test.js',
            line: 0,
            column: 0,
            template: '${1}${3}', // Non-sequential
          },
        },
        {}
      );
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof SnippetValidationError);
    }
  });

  it('Should throw SnippetError for document not found', async function () {
    const docProvider = createMockDocumentProvider();
    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler(
        {
          payload: {
            filePath: '/missing.js',
            line: 0,
            column: 0,
            template: 'test',
          },
        },
        {}
      );
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof SnippetError);
    }
  });

  it('Should throw PositionError for line exceeding bounds', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'line1\nline2');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler(
        {
          payload: {
            filePath: '/test.js',
            line: 99,
            column: 0,
            template: 'test',
          },
        },
        {}
      );
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof PositionError);
    }
  });

  it('Should throw PositionError for column exceeding line length', async function () {
    const docProvider = createMockDocumentProvider();
    docProvider.setDocument('/test.js', 'short');

    const handler = createSnippetHandler({ documentProvider: docProvider });

    try {
      await handler(
        {
          payload: {
            filePath: '/test.js',
            line: 0,
            column: 100,
            template: 'test',
          },
        },
        {}
      );
      assert.fail('Should have thrown');
    } catch (err) {
      assert(err instanceof PositionError);
    }
  });
});

// ============================================================================
// BONUS: Utility Function Tests
// ============================================================================

describe('Bonus: Utility Functions', function () {
  it('Should expand placeholders correctly', function () {
    const result = expandSnippetPlaceholders('${1:hello} ${2:world}');
    assert.strictEqual(result, 'hello world');
  });

  it('Should interpolate variables', function () {
    const result = interpolateVariables('File: ${TM_FILENAME}', {
      TM_FILENAME: 'app.js',
    });
    assert.strictEqual(result, 'File: app.js');
  });

  it('Should process escapes correctly', function () {
    const result = processEscapes('Price: \\$100');
    assert.strictEqual(result, 'Price: $100');
  });

  it('Should handle escaped backslash', function () {
    const result = processEscapes('Path: C:\\\\Users');
    assert.strictEqual(result, 'Path: C:\\Users');
  });
});
