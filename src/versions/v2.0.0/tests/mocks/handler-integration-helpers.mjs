#!/usr/bin/env node

/**
 * Handler Integration Test Helpers (Step 69 Support Module)
 *
 * Provides reusable mock factories and orchestration utilities for
 * handler integration testing (Steps 67, 68, 69, 70).
 *
 * Exports:
 * - createSharedDocumentProvider() — Mock with lifecycle tracking
 * - createSharedSymbolExtractor() — Mock with cache instrumentation
 * - createSharedDiagnosticsCollector() — Mock with error injection
 * - createCompletionHoverScenario() — Orchestrate realistic flows
 * - measureLatency() — Performance capture helper
 * - validateCacheHit() — Verify cache effectiveness
 * - createHandlerPair() — Factory for paired handler setup
 *
 * @module src/versions/v2.0.0/tests/mocks/handler-integration-helpers.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Usage:
 *   import {
 *     createSharedDocumentProvider,
 *     createSharedSymbolExtractor,
 *     createCompletionHoverScenario,
 *   } from './mocks/handler-integration-helpers.mjs';
 *
 *   const docProvider = createSharedDocumentProvider();
 *   const symbolExtractor = createSharedSymbolExtractor();
 *   const scenario = createCompletionHoverScenario(docProvider, symbolExtractor);
 */

/**
 * Creates a mock DocumentProvider shared by multiple handlers.
 * Tracks document updates, lifecycle events, and provides multi-document support.
 *
 * @param {Object} [initialDocs={}] - Initial document map { filepath: { content, language, ... } }
 * @returns {Object} Mock DocumentProvider with lifecycle tracking
 *
 * Interface:
 * - getDocument(filepath) → Document | null
 * - getDocumentMetadata(filepath) → Metadata | null
 * - updateDocument(filepath, content) → void
 * - onDocumentChange(callback) → void
 * - getUpdateCount() → number
 * - getAllDocuments() → Document[]
 */
export function createSharedDocumentProvider(initialDocs = {}) {
  const state = {
    _documents: { ...initialDocs },
    _listeners: [],
    _updateCount: 0,
    _accessLog: [],
  };

  return {
    getDocument(filepath) {
      state._accessLog.push({ type: 'getDocument', filepath, timestamp: Date.now() });
      const doc = state._documents[filepath];
      if (!doc) return undefined;
      const content = typeof doc === 'string' ? doc : (doc.content || '');
      return {
        filepath: (typeof doc === 'object' && doc.filepath) ? doc.filepath : filepath,
        language: (typeof doc === 'object' && doc.language) ? doc.language : 'unknown',
        content: content,
        lines: content.split('\n'),
        metadata: (typeof doc === 'object' && doc.metadata) ? doc.metadata : {},
        isDirty: (typeof doc === 'object' && doc.isDirty) ? doc.isDirty : false,
        lastModified: (typeof doc === 'object' && doc.lastModified) ? doc.lastModified : Date.now(),
      };
    },

    getDocumentMetadata(filepath) {
      return this.getDocument(filepath) ? { filepath, language: 'unknown' } : undefined;
    },

    updateDocument(filepath, newContent) {
      const content = typeof newContent === 'string' ? newContent : (newContent.content || '');
      state._documents[filepath] = {
        ...(typeof state._documents[filepath] === 'object' ? state._documents[filepath] : {}),
        filepath,
        content: content,
        isDirty: true,
        lastModified: Date.now(),
      };
      state._updateCount++;
      state._accessLog.push({ type: 'updateDocument', filepath, timestamp: Date.now() });
      state._listeners.forEach(cb => cb(filepath, content));
    },

    onDocumentChange(callback) {
      if (typeof callback !== 'function') throw new Error('Callback must be function');
      state._listeners.push(callback);
    },

    getUpdateCount() {
      return state._updateCount;
    },

    getAllDocuments() {
      return Object.values(state._documents);
    },

    getAccessLog() {
      return state._accessLog;
    },

    clearAccessLog() {
      state._accessLog = [];
    },
  };
}

/**
 * Creates a mock SymbolExtractor with cache instrumentation.
 * Shared by handlers to test cache effectiveness and hit rates.
 *
 * @param {Object} [symbolMap={}] - Symbol map { filepath: Symbol[] }
 * @returns {Object} Mock SymbolExtractor with cache tracking
 *
 * Interface:
 * - extractSymbols(filepath, options?) → Promise<Symbol[]>
 * - getCacheStats() → { hits, misses, hitRate, queryCount }
 * - clearCache() → void
 * - resetStats() → void
 * - getQueryLog() → QueryLog[]
 */
export function createSharedSymbolExtractor(symbolMap = {}) {
  const state = {
    _symbols: symbolMap,
    _cache: new Map(),
    _cacheHits: 0,
    _cacheMisses: 0,
    _queryCount: 0,
    _queryLog: [],
  };

  return {
    async extractSymbols(filepath, options = {}) {
      const cacheKey = `${filepath}:${JSON.stringify(options)}`;
      state._queryCount++;
      const queryStart = Date.now();

      let source = 'cache';
      if (state._cache.has(cacheKey)) {
        state._cacheHits++;
      } else {
        state._cacheMisses++;
        source = 'extract';
        const fileSymbols = state._symbols[filepath] || [];
        state._cache.set(cacheKey, fileSymbols);
      }

      const result = state._cache.get(cacheKey);
      state._queryLog.push({
        filepath,
        options,
        source,
        queryCount: state._queryCount,
        timestamp: queryStart,
        duration: Date.now() - queryStart,
      });

      return result || [];
    },

    getCacheStats() {
      const total = state._cacheHits + state._cacheMisses;
      return {
        hits: state._cacheHits,
        misses: state._cacheMisses,
        hitRate: total > 0 ? (state._cacheHits / total) * 100 : 0,
        queryCount: state._queryCount,
      };
    },

    clearCache() {
      state._cache.clear();
    },

    resetStats() {
      state._cacheHits = 0;
      state._cacheMisses = 0;
      state._queryCount = 0;
      state._queryLog = [];
    },

    getQueryLog() {
      return state._queryLog;
    },
  };
}

/**
 * Creates a mock DiagnosticsCollector for shared use across handlers.
 *
 * @returns {Object} Mock DiagnosticsCollector
 *
 * Interface:
 * - getDiagnostics(filepath) → Diagnostic[]
 * - getDiagnosticsAt(filepath, line, column) → Diagnostic[]
 * - addDiagnostic(filepath, diagnostic) → void
 * - clearDiagnostics(filepath?) → void
 */
export function createSharedDiagnosticsCollector() {
  const state = {
    _diagnostics: {},
    _events: [],
  };

  return {
    getDiagnostics(filepath) {
      return state._diagnostics[filepath] || [];
    },

    getDiagnosticsAt(filepath, line, column) {
      const all = state._diagnostics[filepath] || [];
      return all.filter(d => d.line === line && d.column === column);
    },

    addDiagnostic(filepath, diagnostic) {
      if (!state._diagnostics[filepath]) {
        state._diagnostics[filepath] = [];
      }
      state._diagnostics[filepath].push(diagnostic);
    },

    clearDiagnostics(filepath) {
      if (filepath) {
        delete state._diagnostics[filepath];
      } else {
        state._diagnostics = {};
      }
    },

    getAllDiagnostics() {
      return Object.entries(state._diagnostics).reduce((acc, [fp, diags]) => {
        acc[fp] = diags.length;
        return acc;
      }, {});
    },

    recordEvent(type, data = {}) {
      state._events.push({ type, data, timestamp: Date.now() });
    },

    getEvents() {
      return state._events;
    },

    clearEvents() {
      state._events = [];
    },
  };
}

/**
 * Creates a shared logger for both handlers.
 * Useful for debugging handler interactions and error paths.
 *
 * @returns {Object} Mock Logger
 */
export function createSharedLogger() {
  const state = {
    _logs: [],
  };

  return {
    debug(msg) {
      state._logs.push({ level: 'debug', msg, timestamp: Date.now() });
    },

    info(msg) {
      state._logs.push({ level: 'info', msg, timestamp: Date.now() });
    },

    warn(msg) {
      state._logs.push({ level: 'warn', msg, timestamp: Date.now() });
    },

    error(msg) {
      state._logs.push({ level: 'error', msg, timestamp: Date.now() });
    },

    getLogs() {
      return state._logs;
    },

    getLogsByLevel(level) {
      return state._logs.filter(l => l.level === level);
    },

    clearLogs() {
      state._logs = [];
    },

    getAllLogMessages() {
      return state._logs.map(l => l.msg).join('\n');
    },
  };
}

/**
 * Creates a shared metrics collector for performance tracking.
 *
 * @returns {Object} Mock Metrics
 */
export function createSharedMetrics() {
  const state = {
    _metrics: [],
    _events: [],
  };

  return {
    record(name, value, tags = {}) {
      state._metrics.push({ name, value, tags, timestamp: Date.now() });
    },

    recordEvent(type, data = {}) {
      state._events.push({ type, data, timestamp: Date.now() });
    },

    getMetrics(name) {
      return state._metrics.filter(m => m.name === name);
    },

    getAllMetrics() {
      return state._metrics;
    },

    getEvents() {
      return state._events;
    },

    getLatency(name) {
      const metrics = this.getMetrics(name);
      if (metrics.length === 0) return null;
      const values = metrics.map(m => m.value);
      return {
        min: Math.min(...values),
        max: Math.max(...values),
        avg: values.reduce((a, b) => a + b, 0) / values.length,
        count: values.length,
        p50: this._percentile(values, 0.50),
        p95: this._percentile(values, 0.95),
        p99: this._percentile(values, 0.99),
      };
    },

    _percentile(arr, p) {
      if (arr.length === 0) return null;
      const sorted = arr.slice().sort((a, b) => a - b);
      const index = Math.ceil(sorted.length * p) - 1;
      return sorted[Math.max(0, index)];
    },

    clearMetrics() {
      state._metrics = [];
    },

    clearEvents() {
      state._events = [];
    },
  };
}

/**
 * Orchestrates a completion + hover scenario for realistic testing.
 * Simulates user: gets completions → hovers on one → edits → completes again.
 *
 * @param {Object} docProvider - DocumentProvider instance
 * @param {Object} symbolExtractor - SymbolExtractor instance
 * @param {Object} handlers - { completion, hover } handler instances
 * @returns {Object} Scenario orchestrator
 *
 * Example:
 *   const scenario = createCompletionHoverScenario(docProvider, symbolExtractor, handlers);
 *   const results = await scenario.runBasicFlow('/test.cs', 5, 10);
 */
export function createCompletionHoverScenario(docProvider, symbolExtractor, handlers) {
  return {
    /**
     * Runs: completion → hover on 3 results → document edit → completion again
     */
    async runBasicFlow(filepath, line, column) {
      const results = {
        completions: null,
        hovers: [],
        afterEditCompletions: null,
        timeline: [],
      };

      // Get initial completions
      const completionStart = Date.now();
      results.completions = await handlers.completion.handle({
        data: { file: filepath, line, column },
      });
      results.timeline.push({
        step: 'initial-completion',
        duration: Date.now() - completionStart,
      });

      // Hover on each completion
      for (let i = 0; i < 3; i++) {
        const hoverStart = Date.now();
        const hoverResult = await handlers.hover.handle({
          data: { filepath, line, column: column + i * 5 },
        });
        results.hovers.push(hoverResult);
        results.timeline.push({
          step: `hover-${i}`,
          duration: Date.now() - hoverStart,
        });
      }

      // Edit document
      const editStart = Date.now();
      docProvider.updateDocument(filepath, 'edited content');
      results.timeline.push({
        step: 'document-edit',
        duration: Date.now() - editStart,
      });

      // Completions after edit
      const editedCompletionStart = Date.now();
      results.afterEditCompletions = await handlers.completion.handle({
        data: { file: filepath, line, column },
      });
      results.timeline.push({
        step: 'completion-after-edit',
        duration: Date.now() - editedCompletionStart,
      });

      return results;
    },

    /**
     * Measures cache effectiveness: completion queries first, then hovers reuse cache
     */
    async measureCacheEffectiveness(filepath, positions) {
      const beforeStats = symbolExtractor.getCacheStats();

      // Completion queries (populate cache)
      for (const pos of positions) {
        await handlers.completion.handle({
          data: { file: filepath, line: pos.line, column: pos.column },
        });
      }

      const afterCompletionStats = symbolExtractor.getCacheStats();

      // Hover queries (should hit cache)
      for (const pos of positions) {
        await handlers.hover.handle({
          data: { filepath, line: pos.line, column: pos.column },
        });
      }

      const afterHoverStats = symbolExtractor.getCacheStats();

      return {
        before: beforeStats,
        afterCompletion: afterCompletionStats,
        afterHover: afterHoverStats,
        cacheHitGain: afterHoverStats.hits - afterCompletionStats.hits,
      };
    },
  };
}

/**
 * Helper to measure operation latency.
 *
 * @param {Function} fn - Async function to measure
 * @returns {Promise<{result: any, duration: number}>}
 */
export async function measureLatency(fn) {
  const start = performance.now();
  const result = await fn();
  const duration = performance.now() - start;
  return { result, duration };
}

/**
 * Helper to validate cache hit effectiveness.
 * Asserts that a given operation resulted in a cache hit.
 *
 * @param {Object} symbolExtractor - SymbolExtractor mock
 * @param {Function} operation - Operation to perform
 * @returns {Promise<{wasHit: boolean, statsBefore: any, statsAfter: any}>}
 */
export async function validateCacheHit(symbolExtractor, operation) {
  const statsBefore = symbolExtractor.getCacheStats();
  await operation();
  const statsAfter = symbolExtractor.getCacheStats();

  const wasHit = statsAfter.hits > statsBefore.hits;
  return {
    wasHit,
    statsBefore,
    statsAfter,
    hitsDelta: statsAfter.hits - statsBefore.hits,
  };
}

/**
 * Factory to create a paired handler setup for integration tests.
 * Simplifies common test initialization.
 *
 * @param {Function} completionHandlerFactory - Factory to create completion handler
 * @param {Function} hoverHandlerFactory - Factory to create hover handler
 * @param {Object} [config={}] - Configuration
 * @returns {Object} Initialized handler pair with shared dependencies
 */
export function createHandlerPair(
  completionHandlerFactory,
  hoverHandlerFactory,
  config = {}
) {
  const docProvider = config.docProvider || createSharedDocumentProvider(config.initialDocs || {});
  const symbolExtractor = config.symbolExtractor || createSharedSymbolExtractor(config.symbols || {});
  const diagnosticsCollector = config.diagnosticsCollector || createSharedDiagnosticsCollector();
  const logger = config.logger || createSharedLogger();
  const metrics = config.metrics || createSharedMetrics();

  const dependencies = {
    documentProvider: docProvider,
    symbolExtractor,
    diagnosticsCollector,
    logger,
    metrics,
  };

  return {
    completion: completionHandlerFactory(dependencies),
    hover: hoverHandlerFactory(dependencies),
    dependencies: {
      docProvider,
      symbolExtractor,
      diagnosticsCollector,
      logger,
      metrics,
    },
  };
}

/**
 * Export all helpers as a collection for convenience.
 */
export const HandlerIntegrationHelpers = {
  createSharedDocumentProvider,
  createSharedSymbolExtractor,
  createSharedDiagnosticsCollector,
  createSharedLogger,
  createSharedMetrics,
  createCompletionHoverScenario,
  measureLatency,
  validateCacheHit,
  createHandlerPair,
};
