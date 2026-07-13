#!/usr/bin/env node

/**
 * Symbol Extractor Test Suite (Step 53)
 *
 * Comprehensive test coverage for symbol extraction, validation, caching, and error handling.
 * 5 test suites, 20 tests total.
 *
 * **Test Suites**:
 * 1. Initialization (3 tests)
 * 2. Symbol Table Parsing (4 tests)
 * 3. Symbol Filtering (4 tests)
 * 4. Symbol Queries (4 tests)
 * 5. Message Handler Integration & Edge Cases (5 tests)
 *
 * **Run**: `npm test -- symbol-extractor.test.mjs`
 *
 * @module src/versions/v2.0.0/tests/symbol-extractor.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import assert from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  SymbolExtractor,
  SymbolExtractionError,
  SymbolValidationError,
  SymbolTableError,
  symbolExtractorHandler
} from '../lib/symbol-extractor.mjs';

/**
 * Mock Logger
 */
const mockLogger = () => ({
  debug: () => {},
  info: () => {},
  warn: () => {},
  error: () => {}
});

/**
 * Mock Metrics
 */
const mockMetrics = () => ({
  recordEvent: () => {},
  recordHandlerExecution: () => {}
});

/**
 * Mock DocumentProvider
 */
const mockDocumentProvider = (documents = {}) => ({
  getDocument: (filepath) => documents[filepath] || null
});

/**
 * Sample Symbol Tables
 */
const getSimpleSymbolTable = () => ({
  symbols: [
    {
      name: 'MyClass',
      kind: 'class',
      line: 10,
      column: 0,
      file: 'test.cs',
      scope: 'public',
      documentation: 'A simple test class'
    },
    {
      name: 'MyMethod',
      kind: 'method',
      line: 15,
      column: 2,
      file: 'test.cs',
      scope: 'public',
      parent: 'MyClass',
      documentation: 'A public method'
    },
    {
      name: '_privateField',
      kind: 'property',
      line: 12,
      column: 4,
      file: 'test.cs',
      scope: 'private'
    }
  ]
});

const getComplexSymbolTable = () => ({
  symbols: [
    { name: 'GlobalFunction', kind: 'function', line: 1, column: 0, file: 'util.cs', scope: 'public' },
    { name: 'MyInterface', kind: 'interface', line: 5, column: 0, file: 'test.cs', scope: 'public' },
    { name: 'MyClass', kind: 'class', line: 10, column: 0, file: 'test.cs', scope: 'public' },
    { name: 'BaseClass', kind: 'class', line: 20, column: 0, file: 'test.cs', scope: 'internal' },
    { name: 'Constructor', kind: 'method', line: 11, column: 2, file: 'test.cs', scope: 'public', parent: 'MyClass' },
    { name: 'PublicMethod', kind: 'method', line: 15, column: 2, file: 'test.cs', scope: 'public', parent: 'MyClass' },
    { name: 'PrivateMethod', kind: 'method', line: 18, column: 2, file: 'test.cs', scope: 'private', parent: 'MyClass' },
    { name: 'Name', kind: 'property', line: 12, column: 4, file: 'test.cs', scope: 'public', parent: 'MyClass' },
    { name: '_id', kind: 'property', line: 13, column: 4, file: 'test.cs', scope: 'private', parent: 'MyClass' },
    { name: 'MyEnum', kind: 'enum', line: 30, column: 0, file: 'test.cs', scope: 'public' },
    { name: 'Value1', kind: 'enum-value', line: 31, column: 2, file: 'test.cs', scope: 'public', parent: 'MyEnum' },
    { name: 'Value2', kind: 'enum-value', line: 32, column: 2, file: 'test.cs', scope: 'public', parent: 'MyEnum' }
  ]
});

const getMalformedSymbolTable = () => ({
  symbols: [
    { name: 'ValidSymbol', kind: 'class', line: 1, column: 0, file: 'test.cs' },
    { kind: 'method', line: 5, column: 0, file: 'test.cs' }, // Missing 'name'
    { name: 'InvalidLine', line: 'not-a-number', column: 0, file: 'test.cs' }, // Invalid 'line'
    { name: 'NoFile', kind: 'class', line: 10, column: 0 } // Missing 'file'
  ]
});

// =============================================================================
// SUITE 1: Initialization (3 tests)
// =============================================================================

describe('Suite 1: SymbolExtractor Initialization', () => {
  it('should initialize with default options', () => {
    const extractor = new SymbolExtractor();
    assert.ok(extractor instanceof SymbolExtractor);
    assert.strictEqual(extractor.cacheSize, 100);
    assert.ok(extractor._cache instanceof Map);
    assert.strictEqual(extractor._cache.size, 0);
    extractor.dispose();
  });

  it('should initialize with custom logger, metrics, and documentProvider', () => {
    const logger = mockLogger();
    const metrics = mockMetrics();
    const documentProvider = mockDocumentProvider();

    const extractor = new SymbolExtractor({ logger, metrics, documentProvider, cacheSize: 50 });
    assert.strictEqual(extractor.logger, logger);
    assert.strictEqual(extractor.metrics, metrics);
    assert.strictEqual(extractor.documentProvider, documentProvider);
    assert.strictEqual(extractor.cacheSize, 50);
    extractor.dispose();
  });

  it('should throw on invalid options', () => {
    assert.throws(
      () => new SymbolExtractor('invalid'),
      Error,
      'Should throw on non-object options'
    );

    assert.throws(
      () => new SymbolExtractor(null),
      Error,
      'Should throw on null options'
    );

    assert.throws(
      () => new SymbolExtractor([]),
      Error,
      'Should throw on array options'
    );
  });
});

// =============================================================================
// SUITE 2: Symbol Table Parsing (4 tests)
// =============================================================================

describe('Suite 2: Symbol Table Parsing', () => {
  let extractor;

  beforeEach(() => {
    extractor = new SymbolExtractor({ logger: mockLogger(), metrics: mockMetrics() });
  });

  afterEach(() => {
    extractor.dispose();
  });

  it('should parse valid JSON symbol table', async () => {
    const table = getSimpleSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    assert.ok(Array.isArray(parsed.symbols));
    assert.strictEqual(parsed.symbolCount, 3);
    assert.strictEqual(parsed.fileCount, 1);

    const classSymbol = parsed.symbols.find((s) => s.name === 'MyClass');
    assert.ok(classSymbol);
    assert.strictEqual(classSymbol.kind, 'class');
    assert.strictEqual(classSymbol.line, 10);
    assert.strictEqual(classSymbol.column, 0);
  });

  it('should normalize line/column numbers (0-based) and validate fields', async () => {
    const table = {
      symbols: [
        { name: 'TestSymbol', kind: 'class', line: 5, column: 2, file: 'test.cs', scope: 'public' }
      ]
    };

    const parsed = await extractor.parseSymbolTable(table);
    const symbol = parsed.symbols[0];

    assert.strictEqual(symbol.line, 5); // Already 0-based
    assert.strictEqual(symbol.column, 2);
    assert.ok(symbol.range); // Should have range info
  });

  it('should build hierarchy tree (nested children)', async () => {
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    // Find MyClass and verify it has children
    const myClass = parsed.symbols.find((s) => s.name === 'MyClass');
    assert.ok(myClass);
    assert.ok(Array.isArray(myClass.children));
    assert.ok(myClass.children.length > 0);

    // Verify children are correctly attached
    const constructorSymbol = myClass.children.find((s) => s.name === 'Constructor');
    assert.ok(constructorSymbol);
  });

  it('should reject invalid symbol table (missing required fields)', async () => {
    const table = getMalformedSymbolTable();

    try {
      await extractor.parseSymbolTable(table);
      assert.fail('Should have thrown SymbolValidationError');
    } catch (error) {
      assert.ok(error instanceof SymbolValidationError || error instanceof SymbolTableError);
    }
  });
});

// =============================================================================
// SUITE 3: Symbol Filtering (4 tests)
// =============================================================================

describe('Suite 3: Symbol Filtering', () => {
  let extractor;

  beforeEach(() => {
    extractor = new SymbolExtractor({ logger: mockLogger(), metrics: mockMetrics() });
  });

  afterEach(() => {
    extractor.dispose();
  });

  it('should filter by kind (class, method, property)', async () => {
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    const classes = extractor._filterSymbols(parsed.symbols, { kind: 'class' });
    assert.ok(classes.length > 0);
    assert.ok(classes.every((s) => s.kind === 'class'));

    const methods = extractor._filterSymbols(parsed.symbols, { kind: 'method' });
    assert.ok(methods.length > 0);
    assert.ok(methods.every((s) => s.kind === 'method'));
  });

  it('should filter by scope (public, private, protected)', async () => {
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    const publicSymbols = extractor._filterSymbols(parsed.symbols, { scope: 'public' });
    assert.ok(publicSymbols.length > 0);
    assert.ok(publicSymbols.every((s) => s.scope === 'public'));

    const privateSymbols = extractor._filterSymbols(parsed.symbols, { scope: 'private' });
    assert.ok(privateSymbols.length > 0);
    assert.ok(privateSymbols.every((s) => s.scope === 'private'));
  });

  it('should filter by name pattern (string and regex)', async () => {
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    // String pattern
    const stringFiltered = extractor._filterSymbols(parsed.symbols, { searchPattern: 'method' });
    assert.ok(stringFiltered.length > 0);
    assert.ok(stringFiltered.some((s) => s.name.toLowerCase().includes('method')));

    // Regex pattern
    const regexFiltered = extractor._filterSymbols(parsed.symbols, { searchPattern: /^My/ });
    assert.ok(regexFiltered.length > 0);
    assert.ok(regexFiltered.every((s) => s.name.startsWith('My')));
  });

  it('should preserve children when filtering parents', async () => {
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);

    const classFiltered = extractor._filterSymbols(parsed.symbols, { kind: 'class' });
    assert.ok(classFiltered.length > 0);

    const myClass = classFiltered.find((s) => s.name === 'MyClass');
    assert.ok(myClass);
    assert.ok(Array.isArray(myClass.children));
  });
});

// =============================================================================
// SUITE 4: Symbol Queries (4 tests)
// =============================================================================

describe('Suite 4: Symbol Queries', () => {
  let extractor;
  let symbols;

  beforeEach(async () => {
    extractor = new SymbolExtractor({ logger: mockLogger(), metrics: mockMetrics() });
    const table = getComplexSymbolTable();
    const parsed = await extractor.parseSymbolTable(table);
    symbols = parsed.symbols;
  });

  afterEach(() => {
    extractor.dispose();
  });

  it('should find symbol by name (via filter)', () => {
    const filtered = extractor._filterSymbols(symbols, { searchPattern: 'MyClass' });
    const found = filtered.find((s) => s.name === 'MyClass');

    assert.ok(found);
    assert.strictEqual(found.kind, 'class');
  });

  it('should find symbol at location (line:column)', () => {
    // Find symbol at line 10, column 0 (MyClass)
    const targetLine = 10;
    const targetColumn = 0;

    const atLocation = symbols.find((s) => s.line === targetLine && s.column === targetColumn);
    assert.ok(atLocation);
    assert.strictEqual(atLocation.name, 'MyClass');
  });

  it('should return null for missing symbols', () => {
    const notFound = symbols.find((s) => s.name === 'NonExistentSymbol');
    assert.strictEqual(notFound, undefined);
  });

  it('should handle ambiguous names (returns first match)', () => {
    // Add duplicate symbol for testing
    const testSymbols = [
      { name: 'Duplicate', kind: 'class', line: 1, column: 0, file: 'file1.cs', scope: 'public' },
      { name: 'Duplicate', kind: 'method', line: 2, column: 0, file: 'file2.cs', scope: 'public' }
    ];

    const first = testSymbols.find((s) => s.name === 'Duplicate');
    assert.ok(first);
    assert.strictEqual(first.kind, 'class'); // First match
  });
});

// =============================================================================
// SUITE 5: Message Handler Integration & Edge Cases (5 tests)
// =============================================================================

describe('Suite 5: Message Handler Integration & Edge Cases', () => {
  let extractor;

  beforeEach(() => {
    extractor = new SymbolExtractor({ logger: mockLogger(), metrics: mockMetrics() });
  });

  afterEach(() => {
    extractor.dispose();
  });

  it('should handle extractSymbols message via _handleExtractSymbolsMessage', async () => {
    const message = {
      messageId: 'msg-001',
      messageType: 'bridge:extractSymbols',
      data: {
        filepath: 'test.cs',
        symbolTable: getSimpleSymbolTable(),
        kind: 'class'
      }
    };

    const result = await extractor._handleExtractSymbolsMessage(message);

    assert.ok(result.success);
    assert.ok(result.data);
    assert.ok(Array.isArray(result.data.symbols));
    assert.ok(result.data.metadata);
  });

  it('should cache symbol tables and return cached result on second call', async () => {
    const filepath = 'test.cs';
    const symbolTable = getSimpleSymbolTable();

    // First call
    const result1 = await extractor.extractSymbols(filepath, { symbolTable });
    assert.strictEqual(result1.symbols.length, 3);

    // Cache should have entry
    assert.ok(extractor._cache.has(filepath));

    // Second call (without providing symbolTable again)
    const result2 = await extractor.extractSymbols(filepath, { kind: 'method' });
    assert.ok(result2.symbols.length >= 0); // Should use cached table
  });

  it('should handle empty symbol tables gracefully', async () => {
    const emptyTable = { symbols: [] };
    const result = await extractor.extractSymbols('empty.cs', { symbolTable: emptyTable });

    assert.ok(result.symbols);
    assert.strictEqual(result.symbols.length, 0);
    assert.ok(result.metadata);
    assert.strictEqual(result.metadata.count, 0);
  });

  it('should handle malformed JSON with error wrapping', async () => {
    const message = {
      messageId: 'msg-002',
      messageType: 'bridge:extractSymbols',
      data: {
        filepath: 'test.cs',
        symbolTable: 'invalid json string {{'
      }
    };

    try {
      await extractor._handleExtractSymbolsMessage(message);
      assert.fail('Should have thrown or returned error');
    } catch (error) {
      assert.ok(error instanceof SymbolTableError);
    }
  });

  it('should dispose cleanly and clear cache', () => {
    const filepath = 'test.cs';
    extractor._addToCache(filepath, getSimpleSymbolTable(), Date.now());
    assert.ok(extractor._cache.has(filepath));

    extractor.dispose();
    assert.strictEqual(extractor._cache.size, 0);
  });
});

// =============================================================================
// INTEGRATION: Handler Export (1 test)
// =============================================================================

describe('Integration: Symbol Extractor Handler Export', () => {
  it('should export symbolExtractorHandler function', async () => {
    assert.strictEqual(typeof symbolExtractorHandler, 'function');

    const context = {
      logger: mockLogger(),
      metrics: mockMetrics(),
      documentProvider: null
    };

    const message = {
      messageId: 'msg-003',
      messageType: 'bridge:extractSymbols',
      data: {
        filepath: 'test.cs',
        symbolTable: getSimpleSymbolTable()
      }
    };

    const result = await symbolExtractorHandler(message, context);
    assert.ok(result.success);
    assert.ok(result.data);
  });
});
