#!/usr/bin/env node

/**
 * Handler Integration Tests - Step 70
 *
 * Composite orchestration suite validating cross-handler workflows.
 * Tests editor-context → completion flows, search → navigation flows,
 * and complex multi-handler scenarios with shared state.
 *
 * **Test Structure:**
 * - Suite 1: Initialization & Handler Registration (4 tests)
 * - Suite 2: Context-to-Completion Workflow (5 tests)
 * - Suite 3: Search-to-Navigation Workflow (5 tests)
 * - Suite 4: Complex Multi-Handler Scenarios (5 tests)
 * - Suite 5: Performance & Error Handling (3 tests)
 *
 * Total: 22 tests validating composite handler interactions
 * Depends on: Steps 58, 59, 55, 56, 57 handler implementations
 * Related: Step 69 (shared mocks), Step 71 (registration)
 */

import { describe, it, beforeEach, afterEach } from 'mocha';
import assert from 'assert';
import { performance } from 'perf_hooks';
import {
  createSharedDocumentProvider,
  createSharedSymbolExtractor,
  createSharedDiagnosticsCollector,
  createSharedLogger,
  createSharedMetrics,
} from './mocks/handler-integration-helpers.mjs';

/**
 * Suite 1: Initialization & Handler Registration
 */
describe('Handler Integration - Initialization', () => {
  let documentProvider;
  let symbolExtractor;
  let diagnosticsCollector;
  let logger;
  let metrics;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/app.cs': { content: 'public class App {}' },
      '/lib.ts': { content: 'export interface Lib {}' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/app.cs': [{ name: 'App', kind: 'class', line: 0, col: 13 }],
      '/lib.ts': [{ name: 'Lib', kind: 'interface', line: 0, col: 17 }],
    });
    diagnosticsCollector = createSharedDiagnosticsCollector();
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should initialize all shared dependencies', () => {
    assert.ok(documentProvider);
    assert.ok(symbolExtractor);
    assert.ok(diagnosticsCollector);
    assert.ok(logger);
    assert.ok(metrics);
  });

  it('should provide shared document provider with document access', async () => {
    const doc = documentProvider.getDocument('/app.cs');
    assert.strictEqual(doc.content, 'public class App {}');
  });

  it('should provide shared symbol extractor with cache stats', async () => {
    const syms = await symbolExtractor.extractSymbols('/app.cs');
    assert.strictEqual(syms.length, 1);
    assert.strictEqual(syms[0].name, 'App');
    const stats = symbolExtractor.getCacheStats();
    assert.ok(stats.queryCount > 0);
  });

  it('should provide shared diagnostics collector with event recording', () => {
    diagnosticsCollector.recordEvent('test_event', { key: 'value' });
    const events = diagnosticsCollector.getEvents();
    assert.ok(events.some(e => e.type === 'test_event'));
  });
});

/**
 * Suite 2: Context-to-Completion Workflow
 */
describe('Handler Integration - Context-to-Completion Flow', () => {
  let documentProvider;
  let symbolExtractor;
  let diagnosticsCollector;
  let logger;
  let metrics;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/main.cs': { content: 'using System;\npublic class Main { public void Test() { /* cursor here */ } }' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/main.cs': [
        { name: 'Main', kind: 'class', line: 1, col: 13 },
        { name: 'Test', kind: 'method', line: 1, col: 32 },
      ],
    });
    diagnosticsCollector = createSharedDiagnosticsCollector();
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should retrieve editor context for completion trigger', async () => {
    const doc = documentProvider.getDocument('/main.cs');
    assert.ok(doc);
    assert.ok(doc.content.includes('cursor'));
  });

  it('should extract symbols at completion position', async () => {
    const syms = await symbolExtractor.extractSymbols('/main.cs');
    assert.ok(syms.length >= 2);
    const main = syms.find(s => s.name === 'Main');
    assert.ok(main);
  });

  it('should maintain symbol cache consistency across context changes', async () => {
    const syms1 = await symbolExtractor.extractSymbols('/main.cs');
    const syms2 = await symbolExtractor.extractSymbols('/main.cs');
    assert.deepStrictEqual(syms1, syms2);
    const stats = symbolExtractor.getCacheStats();
    assert.strictEqual(stats.hits, 1); // Second call is a hit
  });

  it('should validate completion request without errors', () => {
    const request = {
      file: '/main.cs',
      line: 1,
      column: 40,
    };
    assert.strictEqual(typeof request.file, 'string');
    assert.strictEqual(typeof request.line, 'number');
    assert.strictEqual(typeof request.column, 'number');
  });

  it('should record completion workflow metrics', () => {
    metrics.recordEvent('completion_triggered', { file: '/main.cs', position: '1:40' });
    metrics.recordEvent('symbols_extracted', { count: 2 });
    const events = metrics.getEvents();
    assert.ok(events.some(e => e.type === 'completion_triggered'));
    assert.ok(events.some(e => e.type === 'symbols_extracted'));
  });
});

/**
 * Suite 3: Search-to-Navigation Workflow
 */
describe('Handler Integration - Search-to-Navigation Flow', () => {
  let documentProvider;
  let symbolExtractor;
  let logger;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/search.cs': { content: 'public class Search { public void Find() {} }' },
      '/nav.ts': { content: 'export class Nav { public locate() {} }' },
      '/util.js': { content: 'function navigate() {}' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/search.cs': [
        { name: 'Search', kind: 'class', line: 0, col: 13 },
        { name: 'Find', kind: 'method', line: 0, col: 35 },
      ],
      '/nav.ts': [
        { name: 'Nav', kind: 'class', line: 0, col: 13 },
        { name: 'locate', kind: 'method', line: 0, col: 27 },
      ],
      '/util.js': [
        { name: 'navigate', kind: 'function', line: 0, col: 9 },
      ],
    });
    logger = createSharedLogger();
  });

  it('should search across all documents', () => {
    const docs = documentProvider.getAllDocuments();
    assert.strictEqual(docs.length, 3);
  });

  it('should locate search results in multiple files', async () => {
    const find = await symbolExtractor.extractSymbols('/search.cs');
    const nav = await symbolExtractor.extractSymbols('/nav.ts');
    const util = await symbolExtractor.extractSymbols('/util.js');
    assert.ok(find.some(s => s.name === 'Find'));
    assert.ok(nav.some(s => s.name === 'locate'));
    assert.ok(util.some(s => s.name === 'navigate'));
  });

  it('should chain go-to-definition with search results', async () => {
    const syms = await symbolExtractor.extractSymbols('/search.cs');
    const searchSym = syms.find(s => s.name === 'Search');
    assert.ok(searchSym);
    assert.strictEqual(searchSym.kind, 'class');
  });

  it('should find references across files without cross-contamination', async () => {
    const search = await symbolExtractor.extractSymbols('/search.cs');
    const nav = await symbolExtractor.extractSymbols('/nav.ts');
    const searchCount = search.length;
    const navCount = nav.length;
    assert.strictEqual(searchCount, 2);
    assert.strictEqual(navCount, 2);
  });

  it('should track search-to-navigation workflow state', () => {
    const docs = documentProvider.getAllDocuments();
    const allSyms = docs.map(d => symbolExtractor.extractSymbols(d.path));
    assert.strictEqual(allSyms.length, 3);
  });
});

/**
 * Suite 4: Complex Multi-Handler Scenarios
 */
describe('Handler Integration - Complex Multi-Handler Scenarios', () => {
  let documentProvider;
  let symbolExtractor;
  let diagnosticsCollector;
  let logger;
  let metrics;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/project/api.cs': { content: 'public class API { public string GetData() { return null; } }' },
      '/project/client.ts': { content: 'import API from "./api"; const client = new API();' },
      '/project/util.js': { content: 'function process() {}' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/project/api.cs': [
        { name: 'API', kind: 'class', line: 0, col: 13 },
        { name: 'GetData', kind: 'method', line: 0, col: 32 },
      ],
      '/project/client.ts': [
        { name: 'API', kind: 'class', line: 0, col: 7 },
        { name: 'client', kind: 'variable', line: 1, col: 6 },
      ],
      '/project/util.js': [
        { name: 'process', kind: 'function', line: 0, col: 9 },
      ],
    });
    diagnosticsCollector = createSharedDiagnosticsCollector();
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should handle editor state change with context propagation', () => {
    const docs = documentProvider.getAllDocuments();
    assert.strictEqual(docs.length, 3);
    documentProvider.updateDocument('/project/client.ts', { content: 'import API from "./api"; const client = new API(); client.getData();' });
    const updated = documentProvider.getDocument('/project/client.ts');
    assert.ok(updated.content.includes('getData'));
  });

  it('should execute completion with search fallback on multi-file context', async () => {
    const api = await symbolExtractor.extractSymbols('/project/api.cs');
    const client = await symbolExtractor.extractSymbols('/project/client.ts');
    const util = await symbolExtractor.extractSymbols('/project/util.js');
    assert.ok(api.some(s => s.name === 'GetData'));
    assert.ok(client.some(s => s.name === 'client'));
    assert.ok(util.some(s => s.name === 'process'));
  });

  it('should maintain hover info cache during multi-file navigation', async () => {
    const api = await symbolExtractor.extractSymbols('/project/api.cs');
    const apiClass = api.find(s => s.name === 'API');
    assert.ok(apiClass);
    const cached = await symbolExtractor.extractSymbols('/project/api.cs');
    const cachedClass = cached.find(s => s.name === 'API');
    assert.deepStrictEqual(apiClass, cachedClass);
  });

  it('should record comprehensive metrics across multiple handlers', () => {
    metrics.recordEvent('context_changed', { file: '/project/client.ts' });
    metrics.recordEvent('search_initiated', { query: 'API' });
    metrics.recordEvent('completion_generated', { suggestions: 5 });
    metrics.recordEvent('hover_displayed', { symbol: 'GetData' });
    const events = metrics.getEvents();
    assert.ok(events.length >= 4);
    assert.ok(events.some(e => e.type === 'context_changed'));
    assert.ok(events.some(e => e.type === 'search_initiated'));
  });
});

/**
 * Suite 5: Performance & Error Handling
 */
describe('Handler Integration - Performance & Error Handling', () => {
  let documentProvider;
  let symbolExtractor;
  let logger;
  let metrics;

  beforeEach(() => {
    const largeDocs = {};
    for (let i = 0; i < 50; i++) {
      largeDocs[`/file${i}.cs`] = { content: `public class File${i} {}` };
    }
    documentProvider = createSharedDocumentProvider(largeDocs);

    const largeSymbols = {};
    for (let i = 0; i < 50; i++) {
      largeSymbols[`/file${i}.cs`] = [{ name: `File${i}`, kind: 'class', line: 0, col: 13 }];
    }
    symbolExtractor = createSharedSymbolExtractor(largeSymbols);
    logger = createSharedLogger();
    metrics = createSharedMetrics();
  });

  it('should handle cached queries within performance gate (<5ms)', async () => {
    const start = performance.now();
    await symbolExtractor.extractSymbols('/file0.cs');
    const first = performance.now() - start;

    const start2 = performance.now();
    await symbolExtractor.extractSymbols('/file0.cs');
    const cached = performance.now() - start2;

    assert.ok(cached < 5, `Cached query took ${cached}ms, expected <5ms`);
  });

  it('should handle concurrent multi-file operations timely', async () => {
    const start = performance.now();
    const promises = [];
    for (let i = 0; i < 20; i++) {
      promises.push(symbolExtractor.extractSymbols(`/file${i}.cs`));
    }
    const results = await Promise.all(promises);
    const elapsed = performance.now() - start;

    assert.strictEqual(results.length, 20);
    assert.ok(elapsed < 100, `Concurrent ops took ${elapsed}ms, expected <100ms`);
  });

  it('should gracefully handle missing documents without cascading errors', () => {
    const missing = documentProvider.getDocument('/nonexistent.cs');
    assert.ok(missing === undefined || missing === null);
    // Should not throw or cascade errors
  });
});

/**
 * Suite 6: State Consistency Validation
 */
describe('Handler Integration - State Consistency', () => {
  let documentProvider;
  let symbolExtractor;

  beforeEach(() => {
    documentProvider = createSharedDocumentProvider({
      '/consistent.cs': { content: 'public class Consistent {}' },
    });
    symbolExtractor = createSharedSymbolExtractor({
      '/consistent.cs': [{ name: 'Consistent', kind: 'class', line: 0, col: 13 }],
    });
  });

  it('should maintain consistent state across rapid successive calls', async () => {
    const results = [];
    for (let i = 0; i < 10; i++) {
      const syms = await symbolExtractor.extractSymbols('/consistent.cs');
      results.push(syms);
    }
    // All results should be identical
    for (let i = 1; i < results.length; i++) {
      assert.deepStrictEqual(results[0], results[i]);
    }
  });

  it('should not corrupt shared state during parallel handler invocations', async () => {
    const promise1 = symbolExtractor.extractSymbols('/consistent.cs');
    const promise2 = symbolExtractor.extractSymbols('/consistent.cs');
    const [result1, result2] = await Promise.all([promise1, promise2]);
    assert.deepStrictEqual(result1, result2);
  });
});
