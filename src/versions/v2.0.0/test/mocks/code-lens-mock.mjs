#!/usr/bin/env node

/**
 * Code-Lens Handler Test Fixtures & Mocks (Step 90)
 *
 * Provides reusable mock factories and test fixtures for CodeLens handler testing.
 * Enables consistent, isolated test environments without external dependencies.
 *
 * **Exports**:
 * - createMockCodeLensHandler() — Mock handler factory
 * - createMockSymbolExtractor(symbolsMap) — Pre-populated symbol cache
 * - createMockDocumentProvider(docsMap) — Pre-populated documents
 * - getValidCodeLensMessage() — Valid test message template
 * - getValidLensObject() — Valid lens object fixture
 * - getTestSymbols() — Common test symbol collections
 * - getMockDependencies() — Complete mock dependency set
 *
 * @module src/versions/v2.0.0/test/mocks/code-lens-mock.mjs
 * @version 1.0.0
 */

/**
 * Creates a mock CodeLens handler for testing.
 *
 * **Usage**:
 * ```javascript
 * const mockHandler = createMockCodeLensHandler({
 *   shouldSucceed: true,
 *   delayMs: 10,
 *   lensCount: 5
 * });
 * const response = await mockHandler({ filePath: 'src/Code.cs' }, {});
 * ```
 *
 * @param {Object} config - Configuration object
 * @param {boolean} config.shouldSucceed - Whether handler succeeds (default: true)
 * @param {number} config.delayMs - Artificial delay to simulate work (default: 5)
 * @param {number} config.lensCount - Number of lenses to generate (default: 5)
 * @param {Error} config.errorToThrow - Error to throw if shouldSucceed=false
 * @returns {Function} Async handler function
 *
 * @example
 * const handler = createMockCodeLensHandler({ shouldSucceed: true, lensCount: 10 });
 * const response = await handler({ filePath: 'test.cs' }, {});
 * // response.success === true, response.data.lenses.length === 10
 */
export function createMockCodeLensHandler(config = {}) {
  const {
    shouldSucceed = true,
    delayMs = 5,
    lensCount = 5,
    errorToThrow = null,
  } = config;

  return async function mockHandler(message, context) {
    // Simulate processing delay
    if (delayMs > 0) {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }

    if (!shouldSucceed) {
      if (errorToThrow) {
        throw errorToThrow;
      }
      return {
        success: false,
        error: {
          code: 'MOCK_ERROR',
          message: 'Mock handler configured to fail',
        },
      };
    }

    const lenses = Array(lensCount)
      .fill(0)
      .map((_, i) => ({
        line: i * 10,
        command: ['runTest', 'debugTest', 'viewReferences', 'viewImplementations', 'goToDefinition'][i % 5],
        title: ['Run Test', 'Debug Test', 'View References', 'View Implementations', 'Go to Definition'][i % 5],
        data: {
          symbolName: `MockSymbol${i}`,
          type: 'method',
        },
      }));

    return {
      success: true,
      data: {
        lenses,
        count: lenses.length,
        file: message?.filePath || 'mock/file.cs',
        symbolsProcessed: lensCount,
      },
    };
  };
}

/**
 * Creates a mock SymbolExtractor with pre-populated symbols.
 *
 * **Usage**:
 * ```javascript
 * const symbolsMap = {
 *   'src/Code.cs': [
 *     { name: 'Method1', type: 'method', line: 10, isPublic: true, isTest: false },
 *     { name: 'TestMethod', type: 'method', line: 30, isPublic: true, isTest: true }
 *   ]
 * };
 * const extractor = createMockSymbolExtractor(symbolsMap);
 * const symbols = await extractor.extractSymbols('src/Code.cs');
 * ```
 *
 * @param {Object} symbolsMap - Map of file paths to symbol arrays
 * @param {Object} config - Configuration
 * @param {number} config.delayMs - Artificial delay to simulate extraction
 * @param {boolean} config.throwError - Whether to throw instead of returning symbols
 * @returns {Object} Mock extractor with extractSymbols(filePath, range?) method
 *
 * @example
 * const extractor = createMockSymbolExtractor({
 *   'src/test.cs': getTestSymbols()
 * });
 */
export function createMockSymbolExtractor(symbolsMap = {}, config = {}) {
  const { delayMs = 0, throwError = false } = config;

  return {
    extractSymbols: async function (filePath, range) {
      if (throwError) {
        throw new Error(`Failed to extract symbols from ${filePath}`);
      }

      if (delayMs > 0) {
        await new Promise((resolve) => setTimeout(resolve, delayMs));
      }

      let symbols = symbolsMap[filePath] || [];

      // Filter by range if provided
      if (range) {
        symbols = symbols.filter(
          (s) => s.line >= range.start.line && s.line <= range.end.line
        );
      }

      return symbols;
    },
  };
}

/**
 * Creates a mock DocumentProvider with pre-populated documents.
 *
 * **Usage**:
 * ```javascript
 * const docsMap = {
 *   'src/Code.cs': {
 *     content: 'public class Code { ... }',
 *     lineCount: 42
 *   }
 * };
 * const provider = createMockDocumentProvider(docsMap);
 * const doc = await provider.getDocument('src/Code.cs');
 * ```
 *
 * @param {Object} docsMap - Map of file paths to document objects
 * @param {Object} config - Configuration
 * @param {number} config.delayMs - Artificial delay
 * @param {boolean} config.throwError - Whether to throw on missing file
 * @returns {Object} Mock provider with getDocument(filePath) method
 *
 * @example
 * const provider = createMockDocumentProvider({
 *   'src/test.cs': { content: 'public class Test {}', lineCount: 1 }
 * });
 */
export function createMockDocumentProvider(docsMap = {}, config = {}) {
  const { delayMs = 0, throwError = false } = config;

  return {
    getDocument: async function (filePath) {
      if (throwError) {
        throw new Error(`Failed to load document: ${filePath}`);
      }

      if (delayMs > 0) {
        await new Promise((resolve) => setTimeout(resolve, delayMs));
      }

      return docsMap[filePath] || null;
    },
  };
}

/**
 * Returns a valid test message for bridge:getCodeLenses.
 *
 * **Usage**:
 * ```javascript
 * const message = getValidCodeLensMessage();
 * // {
 * //   messageType: 'bridge:getCodeLenses',
 * //   filePath: 'src/TestFile.cs',
 * //   range: { start: { line: 0, char: 0 }, end: { line: 100, char: 0 } }
 * // }
 * ```
 *
 * @param {Object} overrides - Partial message to override defaults
 * @returns {Object} Valid CodeLens message
 *
 * @example
 * const message = getValidCodeLensMessage({
 *   filePath: 'src/Custom.cs',
 *   excludeTypes: ['peekDefinition']
 * });
 */
export function getValidCodeLensMessage(overrides = {}) {
  const defaults = {
    messageType: 'bridge:getCodeLenses',
    filePath: 'src/TestFile.cs',
    range: {
      start: { line: 0, char: 0 },
      end: { line: 100, char: 0 },
    },
  };

  return { ...defaults, ...overrides };
}

/**
 * Returns a valid test CodeLens object.
 *
 * **Usage**:
 * ```javascript
 * const lens = getValidLensObject();
 * // {
 * //   line: 42,
 * //   command: 'runTest',
 * //   title: 'Run Test',
 * //   data: { symbolName: 'TestMethod', type: 'method' }
 * // }
 * ```
 *
 * @param {Object} overrides - Partial lens to override defaults
 * @returns {Object} Valid CodeLens object
 *
 * @example
 * const lens = getValidLensObject({
 *   line: 10,
 *   command: 'viewReferences',
 *   title: 'View References (5)'
 * });
 */
export function getValidLensObject(overrides = {}) {
  const defaults = {
    line: 42,
    command: 'runTest',
    title: 'Run Test',
    data: {
      symbolName: 'TestMethod',
      type: 'method',
      tags: [],
    },
  };

  return { ...defaults, ...overrides };
}

/**
 * Returns common test symbol collections for different scenarios.
 *
 * **Available Collections**:
 * - `testMethods` — Test functions (xunit, nunit, etc.)
 * - `publicMethods` — Public methods suitable for references
 * - `interfaces` — Interface definitions
 * - `mixed` — Mix of all symbol types
 * - `empty` — Empty array
 * - `large` — 100+ symbols for performance testing
 *
 * **Usage**:
 * ```javascript
 * const symbols = getTestSymbols('testMethods');
 * const extractor = createMockSymbolExtractor({ 'src/test.cs': symbols });
 * ```
 *
 * @param {string} collectionName - Name of collection to return
 * @returns {Object[]} Array of symbol objects
 *
 * @example
 * const symbols = getTestSymbols('mixed');
 * // Returns [
 * //   { name: 'TestCompile', type: 'method', line: 5, isPublic: true, isTest: true, tags: ['xunit'] },
 * //   { name: 'Process', type: 'method', line: 20, isPublic: true, isTest: false, tags: [] },
 * //   ...
 * // ]
 */
export function getTestSymbols(collectionName = 'mixed') {
  const collections = {
    testMethods: [
      {
        name: 'TestCompile',
        type: 'method',
        line: 5,
        isPublic: true,
        isTest: true,
        tags: ['xunit'],
      },
      {
        name: 'TestExecute',
        type: 'method',
        line: 15,
        isPublic: true,
        isTest: true,
        tags: ['xunit', 'async'],
      },
      {
        name: 'TestFailure',
        type: 'method',
        line: 25,
        isPublic: true,
        isTest: true,
        tags: ['nunit'],
      },
    ],

    publicMethods: [
      {
        name: 'ProcessData',
        type: 'method',
        line: 10,
        isPublic: true,
        isTest: false,
        tags: [],
      },
      {
        name: 'ValidateInput',
        type: 'method',
        line: 30,
        isPublic: true,
        isTest: false,
        tags: [],
      },
      {
        name: 'GetResult',
        type: 'method',
        line: 50,
        isPublic: true,
        isTest: false,
        tags: [],
      },
    ],

    interfaces: [
      {
        name: 'IProcessor',
        type: 'interface',
        line: 1,
        isPublic: true,
        isTest: false,
        tags: [],
      },
      {
        name: 'ILogger',
        type: 'interface',
        line: 8,
        isPublic: true,
        isTest: false,
        tags: [],
      },
    ],

    mixed: [
      {
        name: 'TestMethod',
        type: 'method',
        line: 5,
        isPublic: true,
        isTest: true,
        tags: ['xunit'],
      },
      {
        name: 'IService',
        type: 'interface',
        line: 12,
        isPublic: true,
        isTest: false,
        tags: [],
      },
      {
        name: 'PublicMethod',
        type: 'method',
        line: 20,
        isPublic: true,
        isTest: false,
        tags: [],
      },
      {
        name: 'PrivateHelper',
        type: 'method',
        line: 35,
        isPublic: false,
        isTest: false,
        tags: [],
      },
      {
        name: 'Property',
        type: 'property',
        line: 45,
        isPublic: true,
        isTest: false,
        tags: [],
      },
    ],

    empty: [],

    large: Array(100)
      .fill(0)
      .map((_, i) => ({
        name: `Symbol${i}`,
        type: i % 3 === 0 ? 'method' : i % 3 === 1 ? 'property' : 'class',
        line: i * 5,
        isPublic: i % 5 !== 0,
        isTest: i % 10 === 0,
        tags: i % 20 === 0 ? ['abstract'] : [],
      })),
  };

  return collections[collectionName] || collections.mixed;
}

/**
 * Creates a complete set of mock dependencies for handler testing.
 *
 * **Usage**:
 * ```javascript
 * const deps = getMockDependencies({
 *   symbolsMap: { 'src/test.cs': getTestSymbols('testMethods') },
 *   withMetrics: true
 * });
 * const handler = createCodeLensHandler(deps);
 * ```
 *
 * @param {Object} config - Configuration
 * @param {Object} config.symbolsMap - Pre-populated symbols map
 * @param {Object} config.docsMap - Pre-populated documents map
 * @param {boolean} config.withMetrics - Include metrics collector
 * @param {boolean} config.withLogger - Include logger
 * @returns {Object} Complete dependencies object
 *
 * @example
 * const deps = getMockDependencies({
 *   symbolsMap: { 'src/Code.cs': getTestSymbols('mixed') },
 *   withMetrics: true,
 *   withLogger: true
 * });
 */
export function getMockDependencies(config = {}) {
  const {
    symbolsMap = { 'src/TestFile.cs': getTestSymbols('mixed') },
    docsMap = { 'src/TestFile.cs': { content: 'public class TestFile {}', lineCount: 1 } },
    withMetrics = false,
    withLogger = false,
  } = config;

  const deps = {
    symbolExtractor: createMockSymbolExtractor(symbolsMap),
    documentProvider: createMockDocumentProvider(docsMap),
  };

  if (withLogger) {
    deps.logger = {
      debug: () => {},
      warn: () => {},
      error: () => {},
    };
  }

  if (withMetrics) {
    deps.metrics = {
      recordHandlerLatency: () => {},
      recordCustomMetric: () => {},
    };
  }

  return deps;
}

/**
 * Test data builder for creating complex test scenarios.
 *
 * **Usage**:
 * ```javascript
 * const testData = new TestDataBuilder()
 *   .addSymbol('TestMethod', 'method', { isTest: true, line: 10 })
 *   .addSymbol('ProcessData', 'method', { isPublic: true, line: 20 })
 *   .build();
 * ```
 */
export class TestDataBuilder {
  constructor() {
    this.symbols = [];
  }

  addSymbol(name, type, overrides = {}) {
    const symbol = {
      name,
      type,
      line: this.symbols.length * 10,
      isPublic: true,
      isTest: false,
      tags: [],
      ...overrides,
    };
    this.symbols.push(symbol);
    return this;
  }

  addTestMethod(name, line = null) {
    return this.addSymbol(name, 'method', {
      line: line ?? this.symbols.length * 10,
      isPublic: true,
      isTest: true,
      tags: ['xunit'],
    });
  }

  addPublicMethod(name, line = null) {
    return this.addSymbol(name, 'method', {
      line: line ?? this.symbols.length * 10,
      isPublic: true,
      isTest: false,
    });
  }

  addInterface(name, line = null) {
    return this.addSymbol(name, 'interface', {
      line: line ?? this.symbols.length * 10,
      isPublic: true,
      isTest: false,
    });
  }

  build() {
    return [...this.symbols];
  }

  buildAsExtractor() {
    const symbols = this.build();
    return createMockSymbolExtractor({ 'src/TestFile.cs': symbols });
  }
}

/**
 * Helper to create range objects for testing.
 *
 * **Usage**:
 * ```javascript
 * const range = createRange(0, 100);  // Lines 0-100
 * const singleLine = createRange(42, 42);  // Just line 42
 * ```
 *
 * @param {number} startLine - Start line (0-based)
 * @param {number} endLine - End line (0-based)
 * @param {number} startChar - Start character (default 0)
 * @param {number} endChar - End character (default 0)
 * @returns {Object} Range object
 */
export function createRange(startLine, endLine, startChar = 0, endChar = 0) {
  return {
    start: { line: startLine, char: startChar },
    end: { line: endLine, char: endChar },
  };
}

export default {
  createMockCodeLensHandler,
  createMockSymbolExtractor,
  createMockDocumentProvider,
  getValidCodeLensMessage,
  getValidLensObject,
  getTestSymbols,
  getMockDependencies,
  TestDataBuilder,
  createRange,
};
