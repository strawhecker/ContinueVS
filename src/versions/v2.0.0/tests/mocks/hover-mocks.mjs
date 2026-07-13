/**
 * Mock Service Implementations for Hover-Info Handler Testing (Step 59)
 * Provides stub implementations of dependencies for isolated testing
 *
 * @module src/versions/v2.0.0/tests/mocks/hover-mocks.mjs
 */

/**
 * Mock Symbol Extractor
 * Stub implementation of symbol extraction for testing
 */
export class MockSymbolExtractor {
  constructor(symbolsToReturn = {}) {
    this.symbolsToReturn = symbolsToReturn; // Map: filepath → symbols array
    this.extractSymbolsCalls = [];
  }

  /**
   * Stub extractSymbols method
   * @param {string} filepath
   * @param {Object} options - { line, column }
   * @returns {Promise<Array>}
   */
  async extractSymbols(filepath, options = {}) {
    this.extractSymbolsCalls.push({ filepath, options });

    if (this.symbolsToReturn[filepath]) {
      return this.symbolsToReturn[filepath];
    }

    return [];
  }

  /**
   * Get all calls made to extractSymbols
   */
  getExtractSymbolsCalls() {
    return this.extractSymbolsCalls;
  }

  /**
   * Reset call history
   */
  resetCalls() {
    this.extractSymbolsCalls = [];
  }
}

/**
 * Mock Diagnostics Collector
 * Stub implementation of diagnostics collection for testing
 */
export class MockDiagnosticsCollector {
  constructor(diagnosticsToReturn = {}) {
    this.diagnosticsToReturn = diagnosticsToReturn; // Map: "filepath:line:column" → diagnostics array
    this.getDiagnosticsAtCalls = [];
  }

  /**
   * Stub getDiagnosticsAt method
   * @param {string} filepath
   * @param {number} line
   * @param {number} column
   * @returns {Promise<Array>}
   */
  async getDiagnosticsAt(filepath, line, column) {
    this.getDiagnosticsAtCalls.push({ filepath, line, column });

    const key = `${filepath}:${line}:${column}`;
    if (this.diagnosticsToReturn[key]) {
      return this.diagnosticsToReturn[key];
    }

    return [];
  }

  /**
   * Get all calls made to getDiagnosticsAt
   */
  getDiagnosticsAtCalls() {
    return this.getDiagnosticsAtCalls;
  }

  /**
   * Reset call history
   */
  resetCalls() {
    this.getDiagnosticsAtCalls = [];
  }
}

/**
 * Mock Document Provider
 * Stub implementation of document/source code access for testing
 */
export class MockDocumentProvider {
  constructor(documentsToReturn = {}) {
    this.documentsToReturn = documentsToReturn; // Map: filepath → document content string
    this.getDocumentContentCalls = [];
  }

  /**
   * Stub getDocumentContent method
   * @param {string} filepath
   * @returns {Promise<string|null>}
   */
  async getDocumentContent(filepath) {
    this.getDocumentContentCalls.push({ filepath });

    if (this.documentsToReturn[filepath]) {
      return this.documentsToReturn[filepath];
    }

    return null;
  }

  /**
   * Get all calls made to getDocumentContent
   */
  getDocumentContentCalls() {
    return this.getDocumentContentCalls;
  }

  /**
   * Reset call history
   */
  resetCalls() {
    this.getDocumentContentCalls = [];
  }
}

/**
 * Mock Logger
 * No-op logger for testing without console output
 */
export class MockLogger {
  constructor(recordCalls = true) {
    this.recordCalls = recordCalls;
    this.calls = [];
  }

  info(message, data) {
    if (this.recordCalls) this.calls.push({ level: 'info', message, data });
  }

  debug(message, data) {
    if (this.recordCalls) this.calls.push({ level: 'debug', message, data });
  }

  warn(message, data) {
    if (this.recordCalls) this.calls.push({ level: 'warn', message, data });
  }

  error(message, data) {
    if (this.recordCalls) this.calls.push({ level: 'error', message, data });
  }

  getCalls() {
    return this.calls;
  }

  getCallsByLevel(level) {
    return this.calls.filter((c) => c.level === level);
  }

  resetCalls() {
    this.calls = [];
  }
}

/**
 * Mock Metrics Collector
 * No-op metrics for testing without external reporting
 */
export class MockMetrics {
  constructor(recordCalls = true) {
    this.recordCalls = recordCalls;
    this.calls = [];
  }

  record(name, value, tags) {
    if (this.recordCalls) this.calls.push({ type: 'record', name, value, tags });
  }

  recordHistogram(name, value, tags) {
    if (this.recordCalls) this.calls.push({ type: 'histogram', name, value, tags });
  }

  getCalls() {
    return this.calls;
  }

  getCallsByName(name) {
    return this.calls.filter((c) => c.name === name);
  }

  resetCalls() {
    this.calls = [];
  }
}

/**
 * Builder class for creating fully-mocked HoverInfoHandler scenarios
 */
export class MockHoverHandlerBuilder {
  constructor() {
    this.logger = new MockLogger();
    this.metrics = new MockMetrics();
    this.symbolExtractor = new MockSymbolExtractor();
    this.diagnosticsCollector = new MockDiagnosticsCollector();
    this.documentProvider = new MockDocumentProvider();
  }

  /**
   * Set symbols that should be returned for a filepath
   */
  withSymbols(filepath, symbols) {
    this.symbolExtractor.symbolsToReturn[filepath] = symbols;
    return this;
  }

  /**
   * Set diagnostics for a position
   */
  withDiagnostics(filepath, line, column, diagnostics) {
    const key = `${filepath}:${line}:${column}`;
    this.diagnosticsCollector.diagnosticsToReturn[key] = diagnostics;
    return this;
  }

  /**
   * Set document content for a filepath
   */
  withDocument(filepath, content) {
    this.documentProvider.documentsToReturn[filepath] = content;
    return this;
  }

  /**
   * Build the mock dependencies object
   */
  build() {
    return {
      logger: this.logger,
      metrics: this.metrics,
      symbolExtractor: this.symbolExtractor,
      diagnosticsCollector: this.diagnosticsCollector,
      documentProvider: this.documentProvider,
    };
  }

  /**
   * Get all mocks as separate properties for inspection
   */
  getMocks() {
    return {
      logger: this.logger,
      metrics: this.metrics,
      symbolExtractor: this.symbolExtractor,
      diagnosticsCollector: this.diagnosticsCollector,
      documentProvider: this.documentProvider,
    };
  }

  /**
   * Reset all call histories
   */
  resetAllCalls() {
    this.logger.resetCalls();
    this.metrics.resetCalls();
    this.symbolExtractor.resetCalls();
    this.diagnosticsCollector.resetCalls();
    this.documentProvider.resetCalls();
    return this;
  }
}

export default {
  MockSymbolExtractor,
  MockDiagnosticsCollector,
  MockDocumentProvider,
  MockLogger,
  MockMetrics,
  MockHoverHandlerBuilder,
};
