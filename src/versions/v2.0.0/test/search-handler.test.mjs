#!/usr/bin/env node

/**
 * Search Handler Tests (Step 55)
 *
 * Comprehensive test suite for the search handler, covering:
 * - Basic text search functionality
 * - Filter options (regex, case-sensitivity, whole-word)
 * - Pagination and truncation
 * - Result formatting with context
 * - Error handling and validation
 *
 * Total: 16 tests across 5 suites
 *
 * Run: `npx mocha src/versions/v2.0.0/test/search-handler.test.mjs --timeout 10000`
 *
 * @module src/versions/v2.0.0/test/search-handler.test.mjs
 */

import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import {
  searchHandler,
  validateSearchRequest,
  buildMatcher,
  performSearch,
  extractLineContext,
  SearchError,
  SearchValidationError,
} from '../lib/search-handler.mjs';

/**
 * Create mock DocumentProvider for tests.
 */
function createMockDocumentProvider(documents) {
  return {
    getAllDocuments: () => documents,
  };
}

/**
 * Create mock context with logger and metrics.
 */
function createMockContext(documentProvider) {
  return {
    documentProvider: documentProvider || createMockDocumentProvider([]),
    logger: {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {},
    },
    metrics: {
      recordEvent: () => {},
    },
  };
}

/**
 * Create a test document.
 */
function createTestDocument(filepath, content) {
  return {
    filepath,
    language: 'text',
    lines: content.split('\n'),
    content,
    isDirty: false,
  };
}

// ============================================================================
// SUITE 1: Basic Text Search
// ============================================================================

describe('Search Handler - Basic Text Search', () => {
  let context;
  let documents;

  beforeEach(() => {
    documents = [
      createTestDocument('C:\\src\\Main.cs', 'class Main {\n  public void handleRequest() {\n    // impl\n  }\n}'),
      createTestDocument('C:\\src\\Handler.cs', 'public class RequestHandler {\n  void handleRequest(Request req) {\n    if (req == null) throw new ArgumentNullException();\n  }\n}'),
    ];
    context = createMockContext(createMockDocumentProvider(documents));
  });

  it('should find simple substring matches', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '1',
      data: { query: 'handleRequest' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.totalMatches, 2);
    assert.strictEqual(result.data.results.length, 2);
    assert.strictEqual(result.data.results[0].matchText, 'handleRequest');
    assert.strictEqual(result.data.results[1].matchText, 'handleRequest');
  });

  it('should report correct line and column numbers', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '2',
      data: { query: 'impl' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.results.length, 1);
    const match = result.data.results[0];
    assert.strictEqual(match.file, 'C:\\src\\Main.cs');
    assert.strictEqual(match.line, 3); // 1-indexed
    assert.strictEqual(match.column, 7);
  });

  it('should find multiple matches in the same file', async () => {
    const multiMatch = createTestDocument('C:\\test.txt', 'foo bar\nfoo baz\nbar foo');
    context.documentProvider = createMockDocumentProvider([multiMatch]);

    const message = {
      messageType: 'bridge:search',
      messageId: '3',
      data: { query: 'foo' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.totalMatches, 3);
  });
});

// ============================================================================
// SUITE 2: Filters (Regex, Case-Sensitivity, Whole-Word)
// ============================================================================

describe('Search Handler - Filters', () => {
  let context;
  let documents;

  beforeEach(() => {
    documents = [
      createTestDocument(
        'C:\\test.cs',
        'class Main {\n' +
          '  void Main() { }\n' +
          '  void MainHelper() { }\n' +
          '  void main_func() { }\n' +
          '  void MAIN() { }\n'
      ),
    ];
    context = createMockContext(createMockDocumentProvider(documents));
  });

  it('should respect case-insensitive filter (default)', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '1',
      data: { query: 'main', caseSensitive: false },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert(result.data.totalMatches >= 4, 'Should match multiple case variants');
  });

  it('should respect case-sensitive filter', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '2',
      data: { query: 'main', caseSensitive: true },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.results.length, 1);
    assert.strictEqual(result.data.results[0].line, 4);
  });

  it('should respect whole-word filter', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '3',
      data: { query: 'Main', wholeWord: true, caseSensitive: false },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    const lines = result.data.results.map((r) => r.line);
    assert(lines.includes(1), 'Should match Main in line 1');
    assert(lines.includes(2), 'Should match Main in line 2');
    assert(!lines.includes(3), 'Should not match partial word MainHelper');
  });

  it('should support regex matching', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '4',
      data: { query: '^\\s*void\\s+\\w+\\(', regex: true, caseSensitive: true },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert(result.data.totalMatches >= 4, 'Should match all void declarations');
  });
});

// ============================================================================
// SUITE 3: Pagination & Truncation
// ============================================================================

describe('Search Handler - Pagination & Truncation', () => {
  let context;
  let documents;

  beforeEach(() => {
    let content = '';
    for (let i = 0; i < 100; i++) {
      content += `line ${i}: target value\n`;
    }
    documents = [createTestDocument('C:\\large.txt', content)];
    context = createMockContext(createMockDocumentProvider(documents));
  });

  it('should apply default limit of 50 results', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '1',
      data: { query: 'target' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.results.length, 50);
    assert.strictEqual(result.data.truncated, true);
  });

  it('should support custom offset and limit', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '2',
      data: { query: 'target', offset: 50, limit: 25 },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.results.length, 25);
    assert.strictEqual(result.data.results[0].line, 51);
  });

  it('should set truncated flag correctly', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '3',
      data: { query: 'target', offset: 0, limit: 100 },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.truncated, false);
  });

  it('should handle offset beyond total matches', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '4',
      data: { query: 'target', offset: 200, limit: 50 },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    assert.strictEqual(result.data.results.length, 0);
    assert.strictEqual(result.data.truncated, false);
  });
});

// ============================================================================
// SUITE 4: Result Formatting & Context
// ============================================================================

describe('Search Handler - Result Formatting', () => {
  let context;
  let documents;

  beforeEach(() => {
    const content =
      'line 1\n' +
      'line 2\n' +
      'TARGET LINE\n' +
      'line 4\n' +
      'line 5\n' +
      'line 6\n' +
      'line 7\n';
    documents = [createTestDocument('C:\\context.txt', content)];
    context = createMockContext(createMockDocumentProvider(documents));
  });

  it('should include preview context with surrounding lines', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '1',
      data: { query: 'TARGET' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    const match = result.data.results[0];
    assert(match.preview, 'Preview should exist');
    assert(Array.isArray(match.preview), 'Preview should be an array');
    assert.strictEqual(match.preview.length, 5);
  });

  it('should include line numbers in preview', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '2',
      data: { query: 'TARGET' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    const match = result.data.results[0];
    assert(/^\d+:/.test(match.preview[0]), 'Preview lines should have line numbers');
    assert(match.preview[0].includes('1:'), 'First preview line should be numbered 1');
  });

  it('should handle edge case: match at beginning of file', async () => {
    documents = [createTestDocument('C:\\edge.txt', 'TARGET\nline 2\nline 3\nline 4')];
    context.documentProvider = createMockDocumentProvider(documents);

    const message = {
      messageType: 'bridge:search',
      messageId: '3',
      data: { query: 'TARGET' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    const match = result.data.results[0];
    assert.strictEqual(match.line, 1);
    assert(match.preview, 'Preview should exist for first line match');
  });

  it('should truncate very long lines to 1000 chars', async () => {
    const longLine = 'x'.repeat(2000);
    documents = [createTestDocument('C:\\long.txt', longLine + '\nline 2')];
    context.documentProvider = createMockDocumentProvider(documents);

    const message = {
      messageType: 'bridge:search',
      messageId: '4',
      data: { query: 'x' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, true);
    const match = result.data.results[0];
    assert(match.lineContent.length <= 1003, 'Line content should be truncated');
  });
});

// ============================================================================
// SUITE 5: Error Handling & Validation
// ============================================================================

describe('Search Handler - Error Handling', () => {
  let context;

  beforeEach(() => {
    const documents = [createTestDocument('C:\\test.txt', 'test content')];
    context = createMockContext(createMockDocumentProvider(documents));
  });

  it('should reject empty query', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '1',
      data: { query: '' },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, false);
    assert(result.error.includes('query'), 'Error should mention query field');
  });

  it('should reject invalid regex', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '2',
      data: { query: '[invalid regex', regex: true },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, false);
    assert(result.error.includes('regex'), 'Error should mention regex');
  });

  it('should reject negative offset', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '3',
      data: { query: 'test', offset: -1 },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, false);
    assert(result.error.includes('offset'), 'Error should mention offset');
  });

  it('should reject limit exceeding 100', async () => {
    const message = {
      messageType: 'bridge:search',
      messageId: '4',
      data: { query: 'test', limit: 150 },
    };

    const result = await searchHandler(message, context);

    assert.strictEqual(result.success, false);
    assert(result.error.includes('limit'), 'Error should mention limit');
  });

  it('should handle missing DocumentProvider gracefully', async () => {
    const badContext = {
      documentProvider: null,
      logger: createMockContext().logger,
      metrics: createMockContext().metrics,
    };

    const message = {
      messageType: 'bridge:search',
      messageId: '5',
      data: { query: 'test' },
    };

    const result = await searchHandler(message, badContext);

    assert.strictEqual(result.success, false);
    assert(result.error.includes('DocumentProvider'), 'Error should mention DocumentProvider');
  });
});

// ============================================================================
// Unit Tests for Internal Functions
// ============================================================================

describe('Search Handler - Unit Functions', () => {
  describe('validateSearchRequest', () => {
    it('should return normalized options', () => {
      const data = { query: 'test', regex: true, caseSensitive: false, offset: 10, limit: 25 };
      const options = validateSearchRequest(data);

      assert.strictEqual(options.query, 'test');
      assert.strictEqual(options.regex, true);
      assert.strictEqual(options.caseSensitive, false);
      assert.strictEqual(options.offset, 10);
      assert.strictEqual(options.limit, 25);
    });

    it('should apply defaults', () => {
      const data = { query: 'test' };
      const options = validateSearchRequest(data);

      assert.strictEqual(options.regex, false);
      assert.strictEqual(options.caseSensitive, false);
      assert.strictEqual(options.wholeWord, false);
      assert.strictEqual(options.offset, 0);
      assert.strictEqual(options.limit, 50);
    });

    it('should throw on query too long', () => {
      const data = { query: 'x'.repeat(501) };
      assert.throws(() => validateSearchRequest(data), SearchValidationError);
    });
  });

  describe('buildMatcher', () => {
    it('should create substring matcher', () => {
      const matcher = buildMatcher('test', { regex: false, caseSensitive: false, wholeWord: false });

      assert.strictEqual(matcher.matches('this is a test'), true);
      assert.strictEqual(matcher.matches('TEST'), true);
      assert.strictEqual(matcher.matches('no match here'), false);
    });

    it('should create regex matcher', () => {
      const matcher = buildMatcher('t[aeiou]st', { regex: true, caseSensitive: true, wholeWord: false });

      assert.strictEqual(matcher.matches('test'), true);
      assert.strictEqual(matcher.matches('tost'), true);
      assert.strictEqual(matcher.matches('tst'), false);
    });

    it('should find multiple match positions', () => {
      const matcher = buildMatcher('foo', { regex: false, caseSensitive: false, wholeWord: false });
      const positions = matcher.matchPositions('foo bar foo baz foo');

      assert.deepStrictEqual(positions, [0, 8, 17]);
    });
  });

  describe('extractLineContext', () => {
    it('should extract surrounding context', () => {
      const lines = ['line 1', 'line 2', 'line 3', 'line 4', 'line 5'];
      const ctx = extractLineContext(lines, 2, 2);

      assert.strictEqual(ctx.preview.length, 5);
      assert(ctx.preview[0].includes('1:'));
      assert(ctx.preview[2].includes('3:'));
      assert(ctx.preview[4].includes('5:'));
    });

    it('should handle edge cases at file start', () => {
      const lines = ['line 1', 'line 2', 'line 3'];
      const ctx = extractLineContext(lines, 0, 2);

      assert(ctx.preview.length <= 3);
    });
  });

  describe('performSearch', () => {
    it('should return empty results for no matches', () => {
      const documents = [createTestDocument('C:\\test.txt', 'hello world')];
      const options = {
        query: 'notfound',
        regex: false,
        caseSensitive: false,
        wholeWord: false,
        offset: 0,
        limit: 50,
      };

      const result = performSearch(options.query, options, documents);

      assert.strictEqual(result.totalMatches, 0);
      assert.strictEqual(result.results.length, 0);
      assert.strictEqual(result.truncated, false);
    });

    it('should sort results by file then line', () => {
      const documents = [
        createTestDocument('C:\\b.txt', 'foo\nbar\nfoo'),
        createTestDocument('C:\\a.txt', 'foo\nbar'),
      ];
      const options = {
        query: 'foo',
        regex: false,
        caseSensitive: false,
        wholeWord: false,
        offset: 0,
        limit: 50,
      };

      const result = performSearch(options.query, options, documents);

      assert.strictEqual(result.results[0].file, 'C:\\a.txt');
      assert.strictEqual(result.results[0].line, 1);
      assert.strictEqual(result.results[1].file, 'C:\\b.txt');
      assert.strictEqual(result.results[1].line, 1);
    });
  });
});
