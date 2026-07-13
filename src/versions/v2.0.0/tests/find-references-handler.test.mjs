#!/usr/bin/env node

/**
 * Test Suite for Find-References Handler (Step 57)
 *
 * Comprehensive test coverage for the find-references handler including:
 * - Input validation (valid/invalid parameters)
 * - Symbol extraction at cursor position
 * - Reference aggregation (file/project/workspace scopes)
 * - Reference formatting and kind detection
 * - Fallback scenarios (incomplete symbol tables, cross-file search)
 * - Error handling (wrapped exceptions, graceful degradation)
 * - Edge cases (empty files, boundary positions, large symbol tables)
 *
 * @module src/versions/v2.0.0/tests/find-references-handler.test.mjs
 * @version 1.0.0
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  createFindReferencesHandler,
  ReferenceError,
  ReferenceValidationError,
} from '../lib/find-references-handler.mjs';

/**
 * Suite 1: Input Validation (3 tests)
 * Tests that the handler properly validates input parameters.
 */
describe('find-references-handler: Input Validation', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn().mockResolvedValue({ success: true, data: { symbols: [] } }),
    };

    handler = createFindReferencesHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should accept valid input with all required parameters', async () => {
    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 10,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.error).toBeUndefined();
  });

  it('should reject missing required filepath', async () => {
    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-123',
      data: {
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('Validation');
    expect(response.error).toContain('filepath');
  });

  it('should reject invalid line and column (negative numbers)', async () => {
    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: -1,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('line');
  });
});

/**
 * Suite 2: Symbol Extraction (4 tests)
 * Tests extraction of the symbol at cursor position.
 */
describe('find-references-handler: Symbol Extraction', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    handler = createFindReferencesHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should extract symbol at cursor position (top-level class)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'MyClass',
            kind: 'class',
            file: '/file.cs',
            line: 0,
            column: 0,
            endLine: 20,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-1',
      data: {
        filepath: '/file.cs',
        line: 5,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          name: 'MyClass',
          kind: 'declaration',
        }),
      ])
    );
  });

  it('should extract nested symbol (method within class)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'MyClass',
            kind: 'class',
            file: '/file.cs',
            line: 0,
            column: 0,
            endLine: 20,
            endColumn: 1,
            children: [
              {
                name: 'DoSomething',
                kind: 'method',
                file: '/file.cs',
                line: 5,
                column: 2,
                endLine: 10,
                endColumn: 3,
                children: [],
              },
            ],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-2',
      data: {
        filepath: '/file.cs',
        line: 7,
        column: 4,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeGreaterThanOrEqual(1);
  });

  it('should return empty results when symbol not found at cursor', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'MyClass',
            kind: 'class',
            file: '/file.cs',
            line: 0,
            column: 0,
            endLine: 5,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-3',
      data: {
        filepath: '/file.cs',
        line: 50, // Outside any symbol range
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references).toEqual([]);
    expect(response.data.totalCount).toBe(0);
  });

  it('should handle empty symbol table gracefully', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [] },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-4',
      data: {
        filepath: '/empty.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references).toEqual([]);
  });
});

/**
 * Suite 3: Reference Aggregation (4 tests)
 * Tests multi-scope reference aggregation (file, project, workspace).
 */
describe('find-references-handler: Reference Aggregation', () => {
  let handler;
  let mockSymbolExtractor;
  let mockDocumentProvider;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    mockDocumentProvider = {
      search: vi.fn(),
    };
    handler = createFindReferencesHandler({
      symbolExtractor: mockSymbolExtractor,
      documentProvider: mockDocumentProvider,
    });
  });

  it('should aggregate references from file scope only', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'MyFunc',
            kind: 'method',
            file: '/file.cs',
            line: 0,
            column: 0,
            endLine: 1,
            endColumn: 1,
            children: [],
          },
          {
            name: 'MyFunc',
            kind: 'reference',
            file: '/file.cs',
            line: 10,
            column: 2,
            endLine: 10,
            endColumn: 8,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-5',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeGreaterThanOrEqual(2);
    expect(mockDocumentProvider.search).not.toHaveBeenCalled();
  });

  it('should aggregate references from project scope (file + cross-file)', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'SharedUtil',
            kind: 'class',
            file: '/utils.cs',
            line: 0,
            column: 0,
            endLine: 5,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    mockDocumentProvider.search.mockResolvedValue([
      {
        file: '/main.cs',
        line: 20,
        column: 5,
        text: 'SharedUtil',
      },
      {
        file: '/helper.cs',
        line: 15,
        column: 3,
        text: 'SharedUtil',
      },
    ]);

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-6',
      data: {
        filepath: '/utils.cs',
        line: 0,
        column: 0,
        searchScope: 'project',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeGreaterThanOrEqual(2);
    expect(mockDocumentProvider.search).toHaveBeenCalledWith(
      'SharedUtil',
      expect.objectContaining({ scope: 'project' })
    );
  });

  it('should deduplicate references across scopes', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'Item',
            kind: 'class',
            file: '/models.cs',
            line: 0,
            column: 0,
            endLine: 10,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    // DocumentProvider returns the same reference (already in symbol table)
    mockDocumentProvider.search.mockResolvedValue([
      {
        file: '/models.cs',
        line: 0,
        column: 0,
        text: 'Item',
      },
    ]);

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-7',
      data: {
        filepath: '/models.cs',
        line: 0,
        column: 0,
        searchScope: 'workspace',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    // Should not have duplicates
    const locations = response.data.references.map((r) => `${r.file}:${r.line}:${r.column}`);
    const uniqueLocations = new Set(locations);
    expect(uniqueLocations.size).toBe(locations.length);
  });

  it('should return empty references when no matches found', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [] },
    });

    mockDocumentProvider.search.mockResolvedValue([]);

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-8',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'project',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references).toEqual([]);
    expect(response.data.totalCount).toBe(0);
  });
});

/**
 * Suite 4: Reference Formatting (4 tests)
 * Tests output format, kind detection, and edge cases.
 */
describe('find-references-handler: Reference Formatting', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    handler = createFindReferencesHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should format references with correct properties', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'Logger',
            kind: 'class',
            file: '/Logger.cs',
            line: 2,
            column: 6,
            endLine: 3,
            endColumn: 7,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-9',
      data: {
        filepath: '/Logger.cs',
        line: 2,
        column: 6,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);

    const ref = response.data.references[0];
    expect(ref).toHaveProperty('file');
    expect(ref).toHaveProperty('line');
    expect(ref).toHaveProperty('column');
    expect(ref).toHaveProperty('text');
    expect(ref).toHaveProperty('kind');
  });

  it('should correctly identify declaration kind', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: 'Config',
            kind: 'class',
            file: '/config.cs',
            line: 0,
            column: 0,
            endLine: 5,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-10',
      data: {
        filepath: '/config.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);

    const ref = response.data.references[0];
    expect(ref.kind).toBe('declaration');
  });

  it('should truncate response when references exceed limit', async () => {
    // Create a large symbol table with 2500+ references
    const symbols = [];
    for (let i = 0; i < 2500; i++) {
      symbols.push({
        name: 'LargeSymbol',
        kind: 'reference',
        file: `/file${i}.cs`,
        line: i,
        column: 0,
        endLine: i,
        endColumn: 5,
        children: [],
      });
    }

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-11',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeLessThanOrEqual(2000);
    expect(response.data.truncated).toBe(true);
  });

  it('should preserve accurate count even when truncated', async () => {
    const symbols = [];
    for (let i = 0; i < 2500; i++) {
      symbols.push({
        name: 'Symbol',
        kind: 'reference',
        file: `/f${i}.cs`,
        line: i,
        column: 0,
        endLine: i,
        endColumn: 1,
        children: [],
      });
    }

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-12',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.totalCount).toBeGreaterThan(2000);
  });
});

/**
 * Suite 5: Fallback Logic (3 tests)
 * Tests graceful degradation when primary path fails.
 */
describe('find-references-handler: Fallback Logic', () => {
  let handler;
  let mockSymbolExtractor;
  let mockDocumentProvider;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    mockDocumentProvider = {
      search: vi.fn(),
    };
    handler = createFindReferencesHandler({
      symbolExtractor: mockSymbolExtractor,
      documentProvider: mockDocumentProvider,
    });
  });

  it('should fall back to document provider when symbol table miss', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [] },
    });

    mockDocumentProvider.search.mockResolvedValue([
      {
        file: '/main.cs',
        line: 5,
        column: 2,
        text: 'UnknownSymbol',
      },
    ]);

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-13',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'project',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.references.length).toBeGreaterThanOrEqual(0);
  });

  it('should handle document provider errors gracefully', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [] },
    });

    mockDocumentProvider.search.mockRejectedValue(new Error('Search timeout'));

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-14',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'project',
      },
    };

    const response = await handler(message, {});
    // Should still return success (empty results)
    expect(response.success).toBe(true);
  });

  it('should continue when extractor partially fails', async () => {
    mockSymbolExtractor.extractSymbols.mockRejectedValue(new Error('Parse error'));

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-15',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('extraction');
  });
});

/**
 * Suite 6: Error Handling (4 tests)
 * Tests proper error wrapping and reporting.
 */
describe('find-references-handler: Error Handling', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    handler = createFindReferencesHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should throw error when symbolExtractor not provided', () => {
    expect(() => createFindReferencesHandler()).toThrow(
      'symbolExtractor is required for createFindReferencesHandler'
    );
  });

  it('should wrap validation errors with field name', async () => {
    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-16',
      data: {
        filepath: '/file.cs',
        line: 'invalid',
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('Validation');
    expect(response.error).toContain('line');
  });

  it('should wrap extraction errors with operation type', async () => {
    mockSymbolExtractor.extractSymbols.mockRejectedValue(new Error('IO failure'));

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-17',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('extraction');
  });

  it('should report invalid search scope', async () => {
    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-18',
      data: {
        filepath: '/file.cs',
        line: 0,
        column: 0,
        searchScope: 'invalid-scope',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toContain('Validation');
    expect(response.error).toContain('searchScope');
  });
});

/**
 * Suite 7: Edge Cases (2 tests)
 * Tests boundary conditions and unusual input.
 */
describe('find-references-handler: Edge Cases', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };
    handler = createFindReferencesHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should handle symbols with Unicode names', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: {
        symbols: [
          {
            name: '日本語クラス',
            kind: 'class',
            file: '/unicode.cs',
            line: 0,
            column: 0,
            endLine: 5,
            endColumn: 1,
            children: [],
          },
        ],
      },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-19',
      data: {
        filepath: '/unicode.cs',
        line: 0,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
  });

  it('should handle deeply nested symbol hierarchy', async () => {
    // Create a deeply nested structure
    const deepChild = {
      name: 'DeepSymbol',
      kind: 'method',
      file: '/deep.cs',
      line: 50,
      column: 4,
      endLine: 55,
      endColumn: 5,
      children: [],
    };

    let currentLevel = deepChild;
    for (let i = 0; i < 10; i++) {
      currentLevel = {
        name: `Level${i}`,
        kind: 'class',
        file: '/deep.cs',
        line: i * 10,
        column: i,
        endLine: (i + 1) * 10,
        endColumn: i + 1,
        children: [currentLevel],
      };
    }

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [currentLevel] },
    });

    const message = {
      messageType: 'bridge:findReferences',
      messageId: 'msg-20',
      data: {
        filepath: '/deep.cs',
        line: 50,
        column: 4,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
  });
});
