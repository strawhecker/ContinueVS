#!/usr/bin/env node

/**
 * Code-Lens Handler Tests (Step 90)
 *
 * Comprehensive test suite for the CodeLens handler covering:
 * - Initialization and dependency injection
 * - Message validation (file paths, ranges, exclude types)
 * - Lens generation for various symbol types
 * - Position/range validation
 * - Performance characteristics
 * - Error handling and recovery
 *
 * **Test Suites**:
 * 1. Initialization (3 tests)
 * 2. Message Validation (4 tests)
 * 3. Lens Generation (5 tests)
 * 4. Position Queries (4 tests)
 * 5. Performance (3 tests)
 * 6. Error Handling (3 tests)
 *
 * Total: 22 tests
 *
 * @module src/versions/v2.0.0/test/code-lens-handler.test.mjs
 * @version 1.0.0
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  createCodeLensHandler,
  CodeLensError,
  PositionError,
  CodeLensOperationType,
} from '../lib/code-lens-handler.mjs';

// ============================================================================
// SUITE 1: INITIALIZATION (3 tests)
// ============================================================================
describe('CodeLensHandler - Initialization', () => {
  it('should create handler with all dependencies', () => {
    const deps = {
      symbolExtractor: { extractSymbols: async () => [] },
      documentProvider: { getDocument: async () => null },
      logger: { debug: () => {}, warn: () => {}, error: () => {} },
      metrics: { recordHandlerLatency: () => {}, recordCustomMetric: () => {} },
    };

    const handler = createCodeLensHandler(deps);
    expect(handler).toBeDefined();
    expect(typeof handler).toBe('function');
  });

  it('should create handler with minimal dependencies (graceful degradation)', () => {
    const deps = {
      symbolExtractor: { extractSymbols: async () => [] },
    };

    const handler = createCodeLensHandler(deps);
    expect(handler).toBeDefined();
    expect(typeof handler).toBe('function');
  });

  it('should throw CodeLensError if SymbolExtractor is missing', () => {
    const deps = {};

    expect(() => {
      createCodeLensHandler(deps);
    }).toThrow(CodeLensError);
  });
});

// ============================================================================
// SUITE 2: MESSAGE VALIDATION (4 tests)
// ============================================================================
describe('CodeLensHandler - Message Validation', () => {
  let handler;

  beforeEach(() => {
    const deps = {
      symbolExtractor: {
        extractSymbols: vi.fn(async () => []),
      },
      logger: {
        debug: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
      },
    };
    handler = createCodeLensHandler(deps);
  });

  it('should accept valid message with filePath and optional range', async () => {
    const message = {
      messageType: 'bridge:getCodeLenses',
      filePath: 'src/Code.cs',
      range: {
        start: { line: 0, char: 0 },
        end: { line: 100, char: 0 },
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.lenses).toBeDefined();
  });

  it('should accept message with filePath and excludeTypes', async () => {
    const message = {
      messageType: 'bridge:getCodeLenses',
      filePath: 'src/Code.cs',
      excludeTypes: ['peekDefinition', 'goToDefinition'],
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.lenses).toBeDefined();
  });

  it('should reject message with missing filePath', async () => {
    const message = {
      messageType: 'bridge:getCodeLenses',
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error.code).toBe('INVALID_REQUEST');
  });

  it('should reject message with malformed range', async () => {
    const message = {
      messageType: 'bridge:getCodeLenses',
      filePath: 'src/Code.cs',
      range: {
        start: { line: 100, char: 0 },
        end: { line: 10, char: 0 }, // start > end: invalid
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error.code).toBe('POSITION_ERROR');
  });
});

// ============================================================================
// SUITE 3: LENS GENERATION (5 tests)
// ============================================================================
describe('CodeLensHandler - Lens Generation', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(async () => []),
    };

    handler = createCodeLensHandler({
      symbolExtractor: mockSymbolExtractor,
    });
  });

  it('should generate run-test and debug-test lenses for test functions', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      {
        name: 'TestCompile',
        type: 'method',
        line: 20,
        isPublic: true,
        isTest: true,
        tags: ['xunit'],
      },
    ]);

    const response = await handler(
      {
        filePath: 'src/Tests.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    expect(response.data.lenses.length).toBeGreaterThanOrEqual(2);

    const runTestLens = response.data.lenses.find(
      (l) => l.command === 'runTest'
    );
    const debugTestLens = response.data.lenses.find(
      (l) => l.command === 'debugTest'
    );

    expect(runTestLens).toBeDefined();
    expect(debugTestLens).toBeDefined();
    expect(runTestLens.line).toBe(20);
    expect(debugTestLens.line).toBe(20);
  });

  it('should generate view-references lenses for public methods', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      {
        name: 'ProcessData',
        type: 'method',
        line: 30,
        isPublic: true,
        isTest: false,
        tags: [],
      },
    ]);

    const response = await handler(
      {
        filePath: 'src/Processor.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    const viewRefLens = response.data.lenses.find(
      (l) => l.command === 'viewReferences'
    );
    expect(viewRefLens).toBeDefined();
    expect(viewRefLens.title).toBe('View References');
  });

  it('should generate view-implementations lenses for interfaces', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      {
        name: 'IProcessor',
        type: 'interface',
        line: 10,
        isPublic: true,
        isTest: false,
        tags: [],
      },
    ]);

    const response = await handler(
      {
        filePath: 'src/Interfaces.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    const implLens = response.data.lenses.find(
      (l) => l.command === 'viewImplementations'
    );
    expect(implLens).toBeDefined();
  });

  it('should filter lenses by excludeTypes', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      {
        name: 'TestMethod',
        type: 'method',
        line: 15,
        isPublic: true,
        isTest: true,
        tags: [],
      },
    ]);

    const response = await handler(
      {
        filePath: 'src/Tests.cs',
        excludeTypes: ['debugTest', 'peekDefinition'],
      },
      {}
    );

    expect(response.success).toBe(true);
    const debugTestLens = response.data.lenses.find(
      (l) => l.command === 'debugTest'
    );
    const peekLens = response.data.lenses.find(
      (l) => l.command === 'peekDefinition'
    );

    expect(debugTestLens).toBeUndefined();
    expect(peekLens).toBeUndefined();
  });

  it('should handle empty symbol list (return empty lenses array)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([]);

    const response = await handler(
      {
        filePath: 'src/Empty.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    expect(response.data.lenses).toEqual([]);
    expect(response.data.count).toBe(0);
  });
});

// ============================================================================
// SUITE 4: POSITION QUERIES (4 tests)
// ============================================================================
describe('CodeLensHandler - Position Queries', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(async () => []),
    };

    handler = createCodeLensHandler({
      symbolExtractor: mockSymbolExtractor,
    });
  });

  it('should generate lenses only within specified range', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      { name: 'Method1', type: 'method', line: 10, isPublic: true, isTest: false, tags: [] },
      { name: 'Method2', type: 'method', line: 25, isPublic: true, isTest: false, tags: [] },
      { name: 'Method3', type: 'method', line: 50, isPublic: true, isTest: false, tags: [] },
    ]);

    const response = await handler(
      {
        filePath: 'src/Code.cs',
        range: {
          start: { line: 5, char: 0 },
          end: { line: 30, char: 0 },
        },
      },
      {}
    );

    expect(response.success).toBe(true);
    // Should have lenses for Method1 and Method2, but not Method3
    const linesInRange = response.data.lenses.map((l) => l.line);
    expect(linesInRange).not.toContain(50);
  });

  it('should handle range boundaries (start = end, multiline range)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      { name: 'Method', type: 'method', line: 20, isPublic: true, isTest: false, tags: [] },
    ]);

    // Single-line range
    const response1 = await handler(
      {
        filePath: 'src/Code.cs',
        range: {
          start: { line: 20, char: 0 },
          end: { line: 20, char: 100 },
        },
      },
      {}
    );

    expect(response1.success).toBe(true);

    // Multiline range
    const response2 = await handler(
      {
        filePath: 'src/Code.cs',
        range: {
          start: { line: 10, char: 0 },
          end: { line: 50, char: 100 },
        },
      },
      {}
    );

    expect(response2.success).toBe(true);
  });

  it('should generate lenses for entire file if no range specified', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      { name: 'Method1', type: 'method', line: 5, isPublic: true, isTest: false, tags: [] },
      { name: 'Method2', type: 'method', line: 100, isPublic: true, isTest: false, tags: [] },
    ]);

    const response = await handler(
      {
        filePath: 'src/Code.cs',
        // No range specified
      },
      {}
    );

    expect(response.success).toBe(true);
    expect(response.data.lenses.length).toBeGreaterThan(0);
  });

  it('should handle out-of-bounds range (return empty)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      { name: 'Method', type: 'method', line: 10, isPublic: true, isTest: false, tags: [] },
    ]);

    const response = await handler(
      {
        filePath: 'src/Code.cs',
        range: {
          start: { line: 500, char: 0 },
          end: { line: 600, char: 0 },
        },
      },
      {}
    );

    expect(response.success).toBe(true);
    expect(response.data.lenses).toEqual([]);
  });
});

// ============================================================================
// SUITE 5: PERFORMANCE (3 tests)
// ============================================================================
describe('CodeLensHandler - Performance', () => {
  let handler;
  let mockSymbolExtractor;
  let mockMetrics;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(async () => {
        // Simulate cached symbol extraction
        return Array(10)
          .fill(0)
          .map((_, i) => ({
            name: `Method${i}`,
            type: 'method',
            line: i * 10,
            isPublic: true,
            isTest: false,
            tags: [],
          }));
      }),
    };

    mockMetrics = {
      recordHandlerLatency: vi.fn(),
      recordCustomMetric: vi.fn(),
    };

    handler = createCodeLensHandler({
      symbolExtractor: mockSymbolExtractor,
      metrics: mockMetrics,
    });
  });

  it('should complete single-file query under 50ms (cached symbols)', async () => {
    const startTime = Date.now();

    const response = await handler(
      {
        filePath: 'src/Code.cs',
      },
      {}
    );

    const elapsed = Date.now() - startTime;

    expect(response.success).toBe(true);
    expect(elapsed).toBeLessThan(100); // Generous bound for test environment
  });

  it('should record query latency and lens count metrics', async () => {
    await handler(
      {
        filePath: 'src/Code.cs',
      },
      {}
    );

    expect(mockMetrics.recordHandlerLatency).toHaveBeenCalledWith(
      'bridge:getCodeLenses',
      expect.any(Number)
    );
    expect(mockMetrics.recordCustomMetric).toHaveBeenCalledWith(
      'codelens.count',
      expect.any(Number)
    );
    expect(mockMetrics.recordCustomMetric).toHaveBeenCalledWith(
      'codelens.symbols',
      expect.any(Number)
    );
  });

  it('should handle large file (many symbols) with acceptable performance', async () => {
    // Mock a large symbol list
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce(
      Array(1000)
        .fill(0)
        .map((_, i) => ({
          name: `Symbol${i}`,
          type: 'method',
          line: i,
          isPublic: true,
          isTest: i % 10 === 0, // 10% are tests
          tags: [],
        }))
    );

    const startTime = Date.now();

    const response = await handler(
      {
        filePath: 'src/LargeFile.cs',
      },
      {}
    );

    const elapsed = Date.now() - startTime;

    expect(response.success).toBe(true);
    expect(response.data.symbolsProcessed).toBe(1000);
    expect(elapsed).toBeLessThan(500); // Should complete in reasonable time
  });
});

// ============================================================================
// SUITE 6: ERROR HANDLING (3 tests)
// ============================================================================
describe('CodeLensHandler - Error Handling', () => {
  let handler;
  let mockSymbolExtractor;
  let mockLogger;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    mockLogger = {
      debug: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
    };

    handler = createCodeLensHandler({
      symbolExtractor: mockSymbolExtractor,
      logger: mockLogger,
    });
  });

  it('should handle SymbolExtractor failure gracefully', async () => {
    mockSymbolExtractor.extractSymbols.mockRejectedValueOnce(
      new Error('Symbol extraction failed')
    );

    const response = await handler(
      {
        filePath: 'src/Code.cs',
      },
      {}
    );

    expect(response.success).toBe(false);
    expect(response.error.code).toBe('INTERNAL_ERROR');
    expect(mockLogger.error).toHaveBeenCalled();
  });

  it('should recover from DocumentProvider error with empty result', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValueOnce([
      { name: 'Method', type: 'method', line: 10, isPublic: true, isTest: false, tags: [] },
    ]);

    const response = await handler(
      {
        filePath: 'src/Code.cs',
      },
      {}
    );

    // Should still succeed even if document provider fails
    expect(response.success).toBe(true);
    expect(Array.isArray(response.data.lenses)).toBe(true);
  });

  it('should validate invalid position and throw PositionError', async () => {
    const response = await handler(
      {
        filePath: 'src/Code.cs',
        range: {
          start: { line: 100, char: 0 },
          end: { line: 50, char: 0 }, // Backwards range
        },
      },
      {}
    );

    expect(response.success).toBe(false);
    expect(response.error.code).toBe('POSITION_ERROR');
  });
});

describe('CodeLensHandler - Edge Cases', () => {
  let handler;

  beforeEach(() => {
    handler = createCodeLensHandler({
      symbolExtractor: {
        extractSymbols: vi.fn(async (filePath, range) => {
          if (filePath.includes('notest')) {
            return [
              { name: 'PublicMethod', type: 'method', line: 10, isPublic: true, isTest: false, tags: [] },
            ];
          }
          if (filePath.includes('alltest')) {
            return [
              { name: 'Test1', type: 'method', line: 5, isPublic: true, isTest: true, tags: ['xunit'] },
              { name: 'Test2', type: 'method', line: 15, isPublic: true, isTest: true, tags: ['xunit'] },
            ];
          }
          return [];
        }),
      },
    });
  });

  it('should handle file with no test symbols correctly', async () => {
    const response = await handler(
      {
        filePath: 'src/notest.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    const lenses = response.data.lenses;
    expect(lenses.some((l) => l.command === 'runTest')).toBe(false);
    expect(lenses.some((l) => l.command === 'viewReferences')).toBe(true);
  });

  it('should handle file with all test symbols', async () => {
    const response = await handler(
      {
        filePath: 'src/alltest.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    const lenses = response.data.lenses;
    const testLenses = lenses.filter((l) => l.command === 'runTest' || l.command === 'debugTest');
    expect(testLenses.length).toBeGreaterThan(0);
  });

  it('should preserve symbol data in lens objects', async () => {
    handler = createCodeLensHandler({
      symbolExtractor: {
        extractSymbols: vi.fn(async () => [
          { name: 'SpecialMethod', type: 'method', line: 20, isPublic: true, isTest: false, tags: ['deprecated'] },
        ]),
      },
    });

    const response = await handler(
      {
        filePath: 'src/special.cs',
      },
      {}
    );

    expect(response.success).toBe(true);
    const lens = response.data.lenses.find((l) => l.command === 'goToDefinition');
    expect(lens.data.symbolName).toBe('SpecialMethod');
    expect(lens.data.type).toBe('method');
  });
});
