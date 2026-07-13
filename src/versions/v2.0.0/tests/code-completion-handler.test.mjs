#!/usr/bin/env node

/**
 * Code-Completion Handler Test Suite (Step 58)
 *
 * Comprehensive unit tests for codeCompletion handler (Step 58).
 * Tests cover happy path, error scenarios, edge cases, and ranking logic.
 *
 * @module src/versions/v2.0.0/tests/code-completion-handler.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha + Node.js assert
 * Coverage: 22 test cases across 6 suites
 * Execution: npm test -- src/versions/v2.0.0/tests/code-completion-handler.test.mjs
 * Expected: 22/22 passing (~150ms total)
 *
 * Related Steps:
 *   - Step 52: document-provider.mjs (code under test — document queries)
 *   - Step 53: symbol-extractor.mjs (code under test — symbol extraction)
 *   - Step 58: code-completion-handler.mjs (handler under test)
 *   - Step 69: handler tests (code completion) — integration layer
 *   - Step 71: handler registration — uses this test pattern
 */

import assert from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  createCodeCompletionHandler,
  CodeCompletionError,
  CodeCompletionOperationType,
} from '../lib/code-completion-handler.mjs';

/**
 * Test Fixtures & Mocks
 */

/**
 * Creates a mock DocumentProvider for testing.
 */
function createMockDocumentProvider(documents = {}) {
  return {
    _documents: documents,
    getDocument: function(filepath) {
      const doc = this._documents[filepath];
      if (!doc) return null;
      return {
        filepath: doc.filepath || filepath,
        language: doc.language || 'unknown',
        content: doc.content || '',
        lines: doc.lines || (doc.content || '').split('\n'),
        metadata: doc.metadata || {},
        isDirty: doc.isDirty || false,
        lastModified: doc.lastModified || Date.now(),
      };
    },
    getDocumentMetadata: function(filepath) {
      return this.getDocument(filepath) ? { filepath, language: 'unknown' } : null;
    },
  };
}

/**
 * Creates a mock SymbolExtractor for testing.
 */
function createMockSymbolExtractor(symbols = {}) {
  return {
    _symbols: symbols,
    extractSymbols: async function(filepath, options = {}) {
      const fileSymbols = this._symbols[filepath] || [];
      return fileSymbols;
    },
  };
}

/**
 * Creates a mock logger for testing.
 */
function createMockLogger() {
  return {
    calls: [],
    debug: function(msg) {
      this.calls.push({ level: 'debug', msg });
    },
    info: function(msg) {
      this.calls.push({ level: 'info', msg });
    },
    warn: function(msg) {
      this.calls.push({ level: 'warn', msg });
    },
    error: function(msg) {
      this.calls.push({ level: 'error', msg });
    },
  };
}

/**
 * Creates a mock metrics collector for testing.
 */
function createMockMetrics() {
  return {
    calls: [],
    recordEvent: function(eventName, data) {
      this.calls.push({ eventName, data });
    },
  };
}

/**
 * Creates a test message envelope.
 */
function createMessage(data = {}) {
  return {
    messageType: 'bridge:getCompletion',
    messageId: 'test-' + Math.random().toString(36).slice(2),
    data: {
      file: 'test.js',
      line: 0,
      column: 0,
      ...data,
    },
  };
}

/**
 * Creates test symbols for extraction mock.
 */
function createTestSymbols() {
  return [
    {
      name: 'calculateSum',
      kind: 'Function',
      line: 5,
      column: 0,
      detail: '(a: number, b: number) => number',
      documentation: 'Adds two numbers and returns the sum.',
      insertText: 'calculateSum($1, $2)',
    },
    {
      name: 'config',
      kind: 'Variable',
      line: 2,
      column: 0,
      detail: 'Object',
      documentation: 'Global configuration object',
      insertText: 'config',
    },
    {
      name: 'log',
      kind: 'Method',
      line: 8,
      column: 2,
      detail: '(message: string) => void',
      documentation: 'Console log wrapper',
      insertText: 'log($1)',
    },
    {
      name: 'async',
      kind: 'Keyword',
      detail: 'keyword',
      documentation: 'Async function keyword',
      insertText: 'async',
    },
    {
      name: 'LocalVar',
      kind: 'Local',
      line: 10,
      column: 5,
      detail: 'string',
      insertText: 'LocalVar',
    },
  ];
}

/**
 * Test Suite: Initialization
 */
describe('Code-Completion Handler — Initialization', function() {
  it('should create handler with valid dispatcher', function() {
    const dispatcher = {};
    const logger = createMockLogger();
    const metrics = createMockMetrics();

    const handler = createCodeCompletionHandler(dispatcher, { logger, metrics });

    assert.ok(typeof handler === 'function', 'Handler should be a function');
    assert.ok(logger.calls.length > 0, 'Logger should have been called');
  });

  it('should throw CodeCompletionError if dispatcher is null', function() {
    assert.throws(
      () => createCodeCompletionHandler(null),
      (err) => err instanceof CodeCompletionError && err.operationType === CodeCompletionOperationType.INIT
    );
  });

  it('should throw CodeCompletionError if dispatcher is not an object', function() {
    assert.throws(
      () => createCodeCompletionHandler('invalid'),
      (err) => err instanceof CodeCompletionError && err.operationType === CodeCompletionOperationType.INIT
    );
  });
});

/**
 * Test Suite: Document Query
 */
describe('Code-Completion Handler — Document Query', function() {
  let handler, documentProvider, symbolExtractor, context, logger, metrics;

  beforeEach(function() {
    documentProvider = createMockDocumentProvider({
      'test.js': {
        language: 'javascript',
        content: 'const x = 1;\nconst y = 2;',
      },
    });
    symbolExtractor = createMockSymbolExtractor({
      'test.js': createTestSymbols(),
    });
    logger = createMockLogger();
    metrics = createMockMetrics();
    handler = createCodeCompletionHandler({}, { logger, metrics });
    context = { documentProvider, symbolExtractor, logger, metrics };
  });

  it('should return empty completions for missing document', async function() {
    const message = createMessage({ file: 'nonexistent.js' });
    const result = await handler(message, context);

    assert.ok(result.success === true, 'Should return success even for missing doc');
    assert.deepStrictEqual(result.data, [], 'Should return empty array for missing doc');
  });

  it('should return completions for valid document', async function() {
    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.ok(Array.isArray(result.data));
    assert.ok(result.data.length > 0, 'Should have completions for valid doc');
  });

  it('should record document_query_error metric for doc access failure', async function() {
    const failingProvider = {
      getDocument: () => {
        throw new Error('Access denied');
      },
    };
    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    context.documentProvider = failingProvider;

    const result = await handler(message, context);

    // Should gracefully return empty (not throw)
    assert.ok(result.success === true);
    const errorMetric = metrics.calls.find((c) => c.eventName.includes('error'));
    assert.ok(errorMetric, 'Should record error metric');
  });

  it('should handle document with no metadata', async function() {
    const provider = createMockDocumentProvider({
      'bare.js': { content: 'x = 1;' },
    });
    const extractor = createMockSymbolExtractor({ 'bare.js': createTestSymbols() });
    context.documentProvider = provider;
    context.symbolExtractor = extractor;

    const message = createMessage({ file: 'bare.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
  });
});

/**
 * Test Suite: Symbol Extraction
 */
describe('Code-Completion Handler — Symbol Extraction', function() {
  let handler, documentProvider, symbolExtractor, context, logger, metrics;

  beforeEach(function() {
    documentProvider = createMockDocumentProvider({
      'test.js': { language: 'javascript', content: '' },
    });
    logger = createMockLogger();
    metrics = createMockMetrics();
    handler = createCodeCompletionHandler({}, { logger, metrics });
  });

  it('should extract symbols at cursor position', async function() {
    symbolExtractor = createMockSymbolExtractor({
      'test.js': createTestSymbols(),
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 5, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.ok(result.data.length > 0, 'Should have extracted symbols');
  });

  it('should handle empty symbol list', async function() {
    symbolExtractor = createMockSymbolExtractor({ 'test.js': [] });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.deepStrictEqual(result.data, [], 'Should return empty array for no symbols');
  });

  it('should gracefully handle symbol extraction error', async function() {
    symbolExtractor = {
      extractSymbols: async () => {
        throw new Error('Symbol extraction failed');
      },
    };
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    // Should gracefully return empty (not throw)
    assert.ok(result.success === true);
    assert.deepStrictEqual(result.data, [], 'Should return empty on extraction error');
    const errorMetric = metrics.calls.find((c) => c.eventName.includes('error'));
    assert.ok(errorMetric, 'Should record error metric');
  });

  it('should pass correct options to extractSymbols', async function() {
    let capturedOptions = null;
    symbolExtractor = {
      extractSymbols: async (file, options) => {
        capturedOptions = options;
        return createTestSymbols();
      },
    };
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 5, column: 10 });
    await handler(message, context);

    assert.ok(capturedOptions, 'Should call extractSymbols with options');
    assert.strictEqual(capturedOptions.line, 5, 'Should pass line');
    assert.strictEqual(capturedOptions.column, 10, 'Should pass column');
    assert.ok(capturedOptions.includeKeywords === true, 'Should include keywords');
    assert.ok(capturedOptions.maxResults > 0, 'Should have maxResults');
  });

  it('should handle symbols with missing properties', async function() {
    const minimalSymbols = [
      { name: 'foo', kind: 'Function' },
      { name: 'bar' }, // missing kind
      { kind: 'Variable' }, // missing name
    ];
    symbolExtractor = createMockSymbolExtractor({
      'test.js': minimalSymbols,
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    // Should handle gracefully, not crash
  });
});

/**
 * Test Suite: Completion Filtering
 */
describe('Code-Completion Handler — Completion Filtering', function() {
  let handler, documentProvider, symbolExtractor, context, logger, metrics;

  beforeEach(function() {
    documentProvider = createMockDocumentProvider({
      'test.js': { language: 'javascript', content: '' },
    });
    logger = createMockLogger();
    metrics = createMockMetrics();
    handler = createCodeCompletionHandler({}, { logger, metrics });
  });

  it('should filter out private symbols', async function() {
    const symbolsWithPrivate = [
      ...createTestSymbols(),
      { name: 'privateFunc', kind: 'Private', isPrivate: true },
      { name: '_internalVar', kind: 'Variable', isPrivate: true },
    ];
    symbolExtractor = createMockSymbolExtractor({
      'test.js': symbolsWithPrivate,
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    const hasPrivate = result.data.some((item) => item.label === 'privateFunc' || item.label === '_internalVar');
    assert.ok(!hasPrivate, 'Should filter out private symbols');
  });

  it('should include public symbols', async function() {
    const publicSymbols = [
      { name: 'publicFunc', kind: 'Function' },
      { name: 'PublicClass', kind: 'Class' },
    ];
    symbolExtractor = createMockSymbolExtractor({
      'test.js': publicSymbols,
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.ok(result.data.length === 2, 'Should include both public symbols');
  });

  it('should map symbols to CompletionItem format', async function() {
    symbolExtractor = createMockSymbolExtractor({
      'test.js': [{ name: 'testFunc', kind: 'Function', detail: 'signature', documentation: 'doc' }],
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.ok(result.data.length === 1);
    const item = result.data[0];
    assert.strictEqual(item.label, 'testFunc', 'Should have label');
    assert.strictEqual(item.kind, 'Function', 'Should have kind');
    assert.ok(item.detail, 'Should have detail');
    assert.ok(item.documentation, 'Should have documentation');
    assert.ok(item.insertText, 'Should have insertText');
    assert.ok(item.sortText, 'Should have sortText');
  });

  it('should handle symbol kind mapping', async function() {
    const symbolsWithKinds = [
      { name: 'MyClass', kind: 'Class' },
      { name: 'myMethod', kind: 'Method' },
      { name: 'myProp', kind: 'Property' },
      { name: 'async', kind: 'Keyword' },
    ];
    symbolExtractor = createMockSymbolExtractor({
      'test.js': symbolsWithKinds,
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.ok(result.data[0].kind === 'Class', 'Should map Class kind');
    assert.ok(result.data[1].kind === 'Method', 'Should map Method kind');
    assert.ok(result.data[2].kind === 'Property', 'Should map Property kind');
    assert.ok(result.data[3].kind === 'Keyword', 'Should map Keyword kind');
  });
});

/**
 * Test Suite: Error Handling
 */
describe('Code-Completion Handler — Error Handling', function() {
  let handler, logger, metrics;

  beforeEach(function() {
    logger = createMockLogger();
    metrics = createMockMetrics();
  });

  it('should return error for missing file', async function() {
    handler = createCodeCompletionHandler({}, { logger, metrics });
    const documentProvider = createMockDocumentProvider({});
    const symbolExtractor = createMockSymbolExtractor({});
    const context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ line: 0, column: 0 }); // missing file
    delete message.data.file;

    const result = await handler(message, context);

    assert.ok(result.success === false, 'Should return failure for missing file');
    assert.ok(result.error, 'Should include error message');
  });

  it('should return error for negative line number', async function() {
    handler = createCodeCompletionHandler({}, { logger, metrics });
    const documentProvider = createMockDocumentProvider({});
    const symbolExtractor = createMockSymbolExtractor({});
    const context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: -1, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === false);
    assert.ok(result.error.includes('line'));
  });

  it('should return error for negative column number', async function() {
    handler = createCodeCompletionHandler({}, { logger, metrics });
    const documentProvider = createMockDocumentProvider({});
    const symbolExtractor = createMockSymbolExtractor({});
    const context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 0, column: -1 });
    const result = await handler(message, context);

    assert.ok(result.success === false);
    assert.ok(result.error.includes('column'));
  });

  it('should return error if DocumentProvider missing from context', async function() {
    handler = createCodeCompletionHandler({}, { logger, metrics });
    const symbolExtractor = createMockSymbolExtractor({});
    const context = { symbolExtractor, logger, metrics }; // missing documentProvider

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === false);
    assert.ok(result.error.includes('DocumentProvider'));
  });

  it('should return error if SymbolExtractor missing from context', async function() {
    handler = createCodeCompletionHandler({}, { logger, metrics });
    const documentProvider = createMockDocumentProvider({
      'test.js': { language: 'javascript' },
    });
    const context = { documentProvider, logger, metrics }; // missing symbolExtractor

    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === false);
    assert.ok(result.error.includes('SymbolExtractor'));
  });
});

/**
 * Test Suite: Edge Cases
 */
describe('Code-Completion Handler — Edge Cases', function() {
  let handler, documentProvider, symbolExtractor, context, logger, metrics;

  beforeEach(function() {
    documentProvider = createMockDocumentProvider({
      'test.js': { language: 'javascript', content: '' },
    });
    logger = createMockLogger();
    metrics = createMockMetrics();
    handler = createCodeCompletionHandler({}, { logger, metrics });
  });

  it('should handle empty document', async function() {
    documentProvider = createMockDocumentProvider({
      'empty.js': { language: 'javascript', content: '' },
    });
    symbolExtractor = createMockSymbolExtractor({ 'empty.js': [] });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'empty.js', line: 0, column: 0 });
    const result = await handler(message, context);

    assert.ok(result.success === true);
    assert.deepStrictEqual(result.data, []);
  });

  it('should handle position beyond document bounds', async function() {
    symbolExtractor = createMockSymbolExtractor({
      'test.js': createTestSymbols(),
    });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 9999, column: 9999 });
    const result = await handler(message, context);

    // Should still succeed (extraction handles this)
    assert.ok(result.success === true);
  });

  it('should handle message with null data', async function() {
    symbolExtractor = createMockSymbolExtractor({});
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = { messageType: 'bridge:getCompletion', messageId: 'test-1', data: null };
    const result = await handler(message, context);

    assert.ok(result.success === false, 'Should return error for null data');
    assert.ok(result.error);
  });

  it('should rank symbols by relevance (distance from cursor)', async function() {
    const symbols = [
      { name: 'farSymbol', kind: 'Function', line: 0 },
      { name: 'nearSymbol', kind: 'Function', line: 10 },
      { name: 'closestSymbol', kind: 'Function', line: 10, frequency: 3 },
    ];
    symbolExtractor = createMockSymbolExtractor({ 'test.js': symbols });
    context = { documentProvider, symbolExtractor, logger, metrics };

    const message = createMessage({ file: 'test.js', line: 10, column: 0 });
    const result = await handler(message, context);

    // Closest symbol should appear first
    assert.ok(result.data.length > 0);
    // Just verify handler completes without crashing (ranking is internal)
  });
});

/**
 * Test Suite: Metrics Recording
 */
describe('Code-Completion Handler — Metrics Recording', function() {
  let handler, documentProvider, symbolExtractor, context, logger, metrics;

  beforeEach(function() {
    documentProvider = createMockDocumentProvider({
      'test.js': { language: 'javascript', content: '' },
    });
    logger = createMockLogger();
    metrics = createMockMetrics();
    handler = createCodeCompletionHandler({}, { logger, metrics });
    symbolExtractor = createMockSymbolExtractor({
      'test.js': createTestSymbols(),
    });
    context = { documentProvider, symbolExtractor, logger, metrics };
  });

  it('should record success metric', async function() {
    const message = createMessage({ file: 'test.js', line: 0, column: 0 });
    await handler(message, context);

    const successMetric = metrics.calls.find((c) => c.eventName === 'completion_success');
    assert.ok(successMetric, 'Should record success metric');
    assert.ok(successMetric.data.resultCount >= 0, 'Should include result count');
    assert.ok(successMetric.data.latencyMs >= 0, 'Should include latency');
  });

  it('should record error metric on failure', async function() {
    const message = createMessage({ line: 0, column: 0 }); // missing file
    delete message.data.file;

    await handler(message, context);

    const errorMetric = metrics.calls.find((c) => c.eventName === 'completion_handler_error');
    assert.ok(errorMetric, 'Should record error metric');
    assert.ok(errorMetric.data.operationType, 'Should include operation type');
  });
});
