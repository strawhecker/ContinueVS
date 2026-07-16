#!/usr/bin/env node

/**
 * Sidebar Collector Mock — Test Fixtures (Step 86)
 *
 * Provides mock SidebarCollector implementations, builders, and test fixtures
 * for Node.js test suites. Chainable builder pattern for flexible test setup.
 *
 * @file src/versions/v2.0.0/tests/sidebar-collector-mock.mjs
 */

/**
 * Create a mock SidebarCollector with configurable overrides
 *
 * @param {Object} overrides - Fields to override in default state
 * @returns {Object} Mock collector with GetSidebarStateAsync method
 */
export function createMockSidebarCollector(overrides = {}) {
  const defaultState = {
    messages: [],
    documents: [
      {
        filepath: '/workspace/src/Program.cs',
        language: 'csharp',
        isModified: false,
        lineCount: 250,
      },
      {
        filepath: '/workspace/src/Handlers/MyHandler.cs',
        language: 'csharp',
        isModified: true,
        lineCount: 180,
      },
      {
        filepath: '/workspace/src/Utils/Helper.js',
        language: 'javascript',
        isModified: false,
        lineCount: 95,
      },
    ],
    symbols: [
      {
        name: 'Program',
        kind: 'class',
        line: 5,
        column: 0,
        isBookmarked: false,
      },
      {
        name: 'Main',
        kind: 'method',
        line: 10,
        column: 2,
        isBookmarked: true,
      },
      {
        name: 'HandleRequest',
        kind: 'method',
        line: 25,
        column: 2,
        isBookmarked: false,
      },
    ],
    diagnostics: {
      '/workspace/src/Program.cs': {
        errors: [
          {
            line: 15,
            column: 5,
            message: 'Undefined variable x',
            code: 'CS0103',
          },
        ],
        warnings: [
          {
            line: 42,
            column: 10,
            message: 'Variable unused',
            code: 'CS0219',
          },
        ],
      },
      '/workspace/src/Handlers/MyHandler.cs': {
        errors: [],
        warnings: [
          {
            line: 60,
            column: 0,
            message: 'Method never called',
            code: 'CS0162',
          },
        ],
      },
    },
    actions: [
      {
        title: 'Quick Fix: Remove unused variable',
        type: 'refactor',
        description: 'Automatically remove x',
      },
    ],
    timestamp: Date.now(),
    ...overrides,
  };

  return {
    /**
     * Async method matching C# collector interface
     */
    async GetSidebarStateAsync(filepath = null) {
      if (filepath) {
        // Filter documents
        const filteredDocs = defaultState.documents.filter(d => d.filepath === filepath);
        const filteredDiags = {};
        if (defaultState.diagnostics[filepath]) {
          filteredDiags[filepath] = defaultState.diagnostics[filepath];
        }

        return {
          ...defaultState,
          documents: filteredDocs,
          diagnostics: filteredDiags,
        };
      }
      return defaultState;
    },
  };
}

/**
 * Builder class for fluent mock configuration
 */
export class MockSidebarCollectorBuilder {
  constructor() {
    this.state = {
      messages: [],
      documents: [],
      symbols: [],
      diagnostics: {},
      actions: [],
      timestamp: Date.now(),
    };
  }

  /**
   * Add documents to mock state
   */
  withDocuments(documents) {
    this.state.documents = documents;
    return this;
  }

  /**
   * Add single document
   */
  addDocument(filepath, language = 'plaintext', isModified = false, lineCount = 100) {
    this.state.documents.push({
      filepath,
      language,
      isModified,
      lineCount,
    });
    return this;
  }

  /**
   * Add diagnostics for a file
   */
  withDiagnostics(diagnosticsByFile) {
    this.state.diagnostics = diagnosticsByFile;
    return this;
  }

  /**
   * Add errors to a specific file
   */
  addErrors(filepath, errors) {
    if (!this.state.diagnostics[filepath]) {
      this.state.diagnostics[filepath] = { errors: [], warnings: [] };
    }
    this.state.diagnostics[filepath].errors = errors;
    return this;
  }

  /**
   * Add warnings to a specific file
   */
  addWarnings(filepath, warnings) {
    if (!this.state.diagnostics[filepath]) {
      this.state.diagnostics[filepath] = { errors: [], warnings: [] };
    }
    this.state.diagnostics[filepath].warnings = warnings;
    return this;
  }

  /**
   * Add symbols to mock state
   */
  withSymbols(symbols) {
    this.state.symbols = symbols;
    return this;
  }

  /**
   * Add single symbol
   */
  addSymbol(name, kind = 'variable', line = 0, column = 0, isBookmarked = false) {
    this.state.symbols.push({
      name,
      kind,
      line,
      column,
      isBookmarked,
    });
    return this;
  }

  /**
   * Add messages to mock state
   */
  withMessages(messages) {
    this.state.messages = messages;
    return this;
  }

  /**
   * Add single message
   */
  addMessage(id, content, author = 'user') {
    this.state.messages.push({
      id,
      content,
      author,
      timestamp: Date.now(),
    });
    return this;
  }

  /**
   * Add actions to mock state
   */
  withActions(actions) {
    this.state.actions = actions;
    return this;
  }

  /**
   * Add single action
   */
  addAction(title, type = 'refactor', description = '') {
    this.state.actions.push({
      title,
      type,
      description,
    });
    return this;
  }

  /**
   * Build and return mock collector
   */
  build() {
    return createMockSidebarCollector(this.state);
  }
}

/**
 * Mock Logger for testing
 */
export class MockLogger {
  constructor() {
    this.logs = [];
  }

  debug(msg) {
    this.logs.push({ level: 'debug', message: msg, timestamp: Date.now() });
  }

  info(msg) {
    this.logs.push({ level: 'info', message: msg, timestamp: Date.now() });
  }

  warn(msg) {
    this.logs.push({ level: 'warn', message: msg, timestamp: Date.now() });
  }

  error(msg, err = null) {
    this.logs.push({
      level: 'error',
      message: msg,
      error: err,
      timestamp: Date.now(),
    });
  }

  /**
   * Get logs filtered by level
   */
  getLogs(level = null) {
    if (!level) {
      return this.logs;
    }
    return this.logs.filter(log => log.level === level);
  }

  /**
   * Clear all logs
   */
  clear() {
    this.logs = [];
  }
}

/**
 * Mock Metrics for testing
 */
export class MockMetrics {
  constructor() {
    this.metrics = new Map();
  }

  /**
   * Record a metric value
   */
  recordMetric(name, value) {
    if (!this.metrics.has(name)) {
      this.metrics.set(name, []);
    }
    this.metrics.get(name).push(value);
  }

  /**
   * Get metrics by name
   */
  getMetrics(name = null) {
    if (!name) {
      const result = {};
      for (const [key, values] of this.metrics.entries()) {
        result[key] = values;
      }
      return result;
    }
    return this.metrics.get(name) || [];
  }

  /**
   * Get average of a metric
   */
  getAverage(name) {
    const values = this.metrics.get(name) || [];
    if (values.length === 0) return 0;
    return values.reduce((a, b) => a + b, 0) / values.length;
  }

  /**
   * Get p99 (99th percentile) of a metric
   */
  getP99(name) {
    const values = (this.metrics.get(name) || []).sort((a, b) => a - b);
    if (values.length === 0) return 0;
    const index = Math.ceil(values.length * 0.99) - 1;
    return values[Math.max(0, index)];
  }

  /**
   * Clear all metrics
   */
  clear() {
    this.metrics.clear();
  }
}

/**
 * Test fixture: Minimal sidebar state
 */
export function createMinimalFixture() {
  return {
    messages: [],
    documents: [],
    symbols: [],
    diagnostics: {},
    actions: [],
    timestamp: Date.now(),
  };
}

/**
 * Test fixture: Complex sidebar state with many files
 */
export function createComplexFixture() {
  const documents = [];
  const diagnostics = {};

  for (let i = 1; i <= 10; i++) {
    const filepath = `/workspace/src/Module${i}/Handler.cs`;
    documents.push({
      filepath,
      language: 'csharp',
      isModified: i % 2 === 0,
      lineCount: 100 + i * 10,
    });

    diagnostics[filepath] = {
      errors: i % 3 === 0 ? [{ line: 10, column: 0, message: 'Error', code: 'E001' }] : [],
      warnings: i % 2 === 0 ? [{ line: 20, column: 5, message: 'Warning', code: 'W001' }] : [],
    };
  }

  return {
    messages: [],
    documents,
    symbols: Array(20)
      .fill(0)
      .map((_, i) => ({
        name: `Symbol${i}`,
        kind: i % 3 === 0 ? 'class' : i % 3 === 1 ? 'method' : 'property',
        line: i * 5,
        column: 0,
        isBookmarked: i % 4 === 0,
      })),
    diagnostics,
    actions: [
      { title: 'Refactor: Extract method', type: 'refactor', description: 'Extract repeated code' },
      { title: 'Fix: Add using statement', type: 'quickfix', description: 'Add missing namespace' },
    ],
    timestamp: Date.now(),
  };
}

/**
 * Test fixture: With diagnostics errors
 */
export function createFixtureWithErrors() {
  return {
    messages: [],
    documents: [
      { filepath: '/workspace/Program.cs', language: 'csharp', isModified: true, lineCount: 200 },
    ],
    symbols: [],
    diagnostics: {
      '/workspace/Program.cs': {
        errors: [
          { line: 10, column: 5, message: 'Undefined variable x', code: 'CS0103' },
          { line: 20, column: 10, message: 'Type mismatch', code: 'CS0029' },
        ],
        warnings: [],
      },
    },
    actions: [],
    timestamp: Date.now(),
  };
}

/**
 * Test fixture: With diagnostics warnings
 */
export function createFixtureWithWarnings() {
  return {
    messages: [],
    documents: [
      { filepath: '/workspace/Utils.cs', language: 'csharp', isModified: false, lineCount: 150 },
    ],
    symbols: [
      { name: 'Helper', kind: 'class', line: 5, column: 0, isBookmarked: false },
    ],
    diagnostics: {
      '/workspace/Utils.cs': {
        errors: [],
        warnings: [
          { line: 15, column: 0, message: 'Unused using statement', code: 'CS8019' },
          { line: 42, column: 5, message: 'Variable never assigned', code: 'CS0168' },
        ],
      },
    },
    actions: [],
    timestamp: Date.now(),
  };
}

export default {
  createMockSidebarCollector,
  MockSidebarCollectorBuilder,
  MockLogger,
  MockMetrics,
  createMinimalFixture,
  createComplexFixture,
  createFixtureWithErrors,
  createFixtureWithWarnings,
};
