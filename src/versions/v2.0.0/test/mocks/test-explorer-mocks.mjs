#!/usr/bin/env node

/**
 * Test-Explorer-Handler Mock Implementations
 *
 * Provides mock service implementations (DocumentProvider, SymbolExtractor,
 * DiagnosticsCollector, Logger, Metrics) for testing the TestExplorerHandler.
 *
 * @module src/versions/v2.0.0/tests/mocks/test-explorer-mocks.mjs
 */

/**
 * Mock DocumentProvider for testing
 */
export class MockDocumentProvider {
  constructor() {
    this._documents = new Map();
    this._callCount = 0;
  }

  getDocument(filepath) {
    this._callCount++;
    return this._documents.get(filepath) || null;
  }

  getAllDocuments() {
    this._callCount++;
    return Array.from(this._documents.values());
  }

  getDocumentByLanguage(language) {
    this._callCount++;
    return Array.from(this._documents.values()).filter((doc) => doc.language === language);
  }

  hasDocument(filepath) {
    return this._documents.has(filepath);
  }

  addDocument(filepath, content, language = 'csharp') {
    this._documents.set(filepath, { filepath, content, language, isDirty: false, lines: content.split('\n').length });
  }

  clearDocuments() {
    this._documents.clear();
  }

  getCallCount() {
    return this._callCount;
  }

  resetCallCount() {
    this._callCount = 0;
  }
}

/**
 * Mock SymbolExtractor for testing
 */
export class MockSymbolExtractor {
  constructor() {
    this._symbols = new Map();
    this._callCount = 0;
  }

  extractSymbols(filepath) {
    this._callCount++;
    return this._symbols.get(filepath) || [];
  }

  addSymbols(filepath, symbols) {
    this._symbols.set(filepath, symbols);
  }

  clearSymbols() {
    this._symbols.clear();
  }

  getCallCount() {
    return this._callCount;
  }

  resetCallCount() {
    this._callCount = 0;
  }
}

/**
 * Mock DiagnosticsCollector for testing
 */
export class MockDiagnosticsCollector {
  constructor() {
    this._diagnostics = new Map();
    this._callCount = 0;
  }

  getDiagnosticsAt(filepath, line, column) {
    this._callCount++;
    const key = `${filepath}:${line}:${column}`;
    return this._diagnostics.get(key) || [];
  }

  getDiagnosticsForFile(filepath) {
    this._callCount++;
    return Array.from(this._diagnostics.entries())
      .filter(([key]) => key.startsWith(`${filepath}:`))
      .flatMap(([, diags]) => diags);
  }

  addDiagnostic(filepath, line, column, diagnostic) {
    const key = `${filepath}:${line}:${column}`;
    if (!this._diagnostics.has(key)) {
      this._diagnostics.set(key, []);
    }
    this._diagnostics.get(key).push(diagnostic);
  }

  clearDiagnostics() {
    this._diagnostics.clear();
  }

  getCallCount() {
    return this._callCount;
  }

  resetCallCount() {
    this._callCount = 0;
  }
}

/**
 * Mock Logger for testing
 */
export class MockLogger {
  constructor() {
    this._logs = [];
  }

  info(message, context = {}) {
    this._logs.push({ level: 'info', message, context, timestamp: Date.now() });
  }

  debug(message, context = {}) {
    this._logs.push({ level: 'debug', message, context, timestamp: Date.now() });
  }

  warn(message, context = {}) {
    this._logs.push({ level: 'warn', message, context, timestamp: Date.now() });
  }

  error(message, context = {}) {
    this._logs.push({ level: 'error', message, context, timestamp: Date.now() });
  }

  getLogs(level = null) {
    if (level) {
      return this._logs.filter((log) => log.level === level);
    }
    return this._logs;
  }

  getLogCount(level = null) {
    return this.getLogs(level).length;
  }

  clear() {
    this._logs = [];
  }

  hasLogMessage(message, level = null) {
    return this.getLogs(level).some((log) => log.message.includes(message));
  }
}

/**
 * Mock Metrics collector for testing
 */
export class MockMetrics {
  constructor() {
    this._records = [];
    this._histograms = [];
  }

  record(metric, value, tags = {}) {
    this._records.push({ metric, value, tags, timestamp: Date.now() });
  }

  recordHistogram(metric, value, tags = {}) {
    this._histograms.push({ metric, value, tags, timestamp: Date.now() });
  }

  getRecords(metric = null) {
    if (metric) {
      return this._records.filter((r) => r.metric === metric);
    }
    return this._records;
  }

  getHistograms(metric = null) {
    if (metric) {
      return this._histograms.filter((h) => h.metric === metric);
    }
    return this._histograms;
  }

  clear() {
    this._records = [];
    this._histograms = [];
  }

  hasRecord(metric) {
    return this._records.some((r) => r.metric === metric);
  }

  hasHistogram(metric) {
    return this._histograms.some((h) => h.metric === metric);
  }
}

/**
 * Fluent builder for test setup
 */
export class MockTestExplorerBuilder {
  constructor() {
    this._documentProvider = new MockDocumentProvider();
    this._symbolExtractor = new MockSymbolExtractor();
    this._diagnosticsCollector = new MockDiagnosticsCollector();
    this._logger = new MockLogger();
    this._metrics = new MockMetrics();
  }

  /**
   * Add C# test files and symbols
   */
  withCSharpTests(count = 3) {
    for (let i = 0; i < count; i++) {
      const filepath = `/test${i}.cs`;
      const content = `[TestFixture]\npublic class Tests\n{\n${Array(count)
        .fill(0)
        .map((_, j) => `  [Fact]\n  public void Test${j}() { }\n`)
        .join('')}\n}`;

      this._documentProvider.addDocument(filepath, content, 'csharp');

      const symbols = [];
      for (let j = 0; j < count; j++) {
        symbols.push({
          name: `Test${j}`,
          kind: 'method',
          line: 3 + j * 2,
          column: 4,
          endLine: 5 + j * 2,
          endColumn: 5,
          attributes: ['[Fact]'],
        });
      }
      this._symbolExtractor.addSymbols(filepath, symbols);
    }
    return this;
  }

  /**
   * Add TypeScript test files and symbols
   */
  withTypeScriptTests(count = 2) {
    for (let i = 0; i < count; i++) {
      const filepath = `/test${i}.test.ts`;
      const content = `describe('Suite ${i}', () => {\n${Array(count)
        .fill(0)
        .map((_, j) => `  it('test ${j}', () => {\n    expect(true).toBe(true);\n  });\n`)
        .join('')}\n});`;

      this._documentProvider.addDocument(filepath, content, 'typescript');

      const symbols = [];
      for (let j = 0; j < count; j++) {
        symbols.push({
          name: `test ${j}`,
          kind: 'test',
          line: 1 + j * 2,
          column: 2,
          endLine: 3 + j * 2,
          endColumn: 3,
          attributes: ['it'],
        });
      }
      this._symbolExtractor.addSymbols(filepath, symbols);
    }
    return this;
  }

  /**
   * Add test failures/diagnostics
   */
  withFailures(testIds = []) {
    for (const testId of testIds) {
      const [filepath, line, column] = testId.split(':');
      this._diagnosticsCollector.addDiagnostic(filepath, parseInt(line), parseInt(column), {
        severity: 'error',
        message: 'Test failed',
        code: 'ASSERTION_FAILED',
      });
    }
    return this;
  }

  /**
   * Add skipped tests
   */
  withSkipped(testIds = []) {
    for (const testId of testIds) {
      const [filepath, line, column] = testId.split(':');
      this._diagnosticsCollector.addDiagnostic(filepath, parseInt(line), parseInt(column), {
        severity: 'info',
        message: 'Test skipped',
        code: 'TEST_SKIPPED',
      });
    }
    return this;
  }

  /**
   * Build dependencies object
   */
  build() {
    return {
      documentProvider: this._documentProvider,
      symbolExtractor: this._symbolExtractor,
      diagnosticsCollector: this._diagnosticsCollector,
      logger: this._logger,
      metrics: this._metrics,
    };
  }

  /**
   * Get mock instances for assertion
   */
  getMocks() {
    return {
      documentProvider: this._documentProvider,
      symbolExtractor: this._symbolExtractor,
      diagnosticsCollector: this._diagnosticsCollector,
      logger: this._logger,
      metrics: this._metrics,
    };
  }

  /**
   * Reset all mocks
   */
  reset() {
    this._documentProvider.clearDocuments();
    this._symbolExtractor.clearSymbols();
    this._diagnosticsCollector.clearDiagnostics();
    this._logger.clear();
    this._metrics.clear();
    return this;
  }
}

/**
 * Helper to create a basic mock handler setup
 */
export function createBasicMockSetup() {
  return new MockTestExplorerBuilder()
    .withCSharpTests(2)
    .withTypeScriptTests(1)
    .build();
}

/**
 * Helper to create a setup with failures
 */
export function createMockSetupWithFailures() {
  const builder = new MockTestExplorerBuilder();
  builder.withCSharpTests(3);
  builder.withFailures(['/test0.cs:4:4', '/test0.cs:6:4']);
  return builder.build();
}

/**
 * Helper to create an empty setup (no tests)
 */
export function createEmptyMockSetup() {
  return new MockTestExplorerBuilder().build();
}

/**
 * Helper to create a large setup (for performance testing)
 */
export function createLargeMockSetup(testCount = 100) {
  const builder = new MockTestExplorerBuilder();
  const filesPerLanguage = Math.ceil(testCount / 20);
  builder.withCSharpTests(filesPerLanguage);
  builder.withTypeScriptTests(filesPerLanguage);
  return builder.build();
}
