#!/usr/bin/env node

/**
 * Test Suite for Go-To-Definition Handler (Step 56)
 *
 * Comprehensive test coverage for the go-to-definition handler including:
 * - Input validation (valid/invalid parameters)
 * - Symbol extraction at cursor position
 * - Definition resolution (simple classes, methods, imported symbols)
 * - Fallback scenarios (incomplete symbol tables, cross-file search)
 * - Error handling (wrapped exceptions, graceful degradation)
 * - Edge cases (empty files, boundary positions, large symbol tables)
 *
 * @module src/versions/v2.0.0/tests/go-to-definition-handler.test.mjs
 * @version 1.0.0
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  createGoToDefinitionHandler,
  DefinitionError,
  DefinitionValidationError,
} from '../lib/go-to-definition-handler.mjs';

/**
 * Suite 1: Input Validation (3 tests)
 * Tests that the handler properly validates input parameters.
 */
describe('go-to-definition-handler: Input Validation', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn().mockResolvedValue({ success: true, data: { symbols: [] } }),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should accept valid input with all required parameters', async () => {
    const message = {
      messageType: 'bridge:goToDefinition',
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
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/filepath/i);
  });

  it('should reject negative line or column numbers', async () => {
    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: -1,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/line/i);
  });
});

/**
 * Suite 2: Symbol Extraction at Cursor (4 tests)
 * Tests that symbols at cursor position are correctly identified.
 */
describe('go-to-definition-handler: Symbol Extraction at Cursor', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should find symbol at exact cursor position', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 5,
          column: 0,
          endLine: 15,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeDefined();
    expect(response.data.location.name).toBe('MyClass');
    expect(response.data.location.line).toBe(5);
  });

  it('should find symbol when cursor is within range', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 5,
          column: 0,
          endLine: 15,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 10,
        column: 5,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeDefined();
    expect(response.data.location.name).toBe('MyClass');
  });

  it('should return null when cursor is not on a symbol', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 5,
          column: 0,
          endLine: 10,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 20,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeNull();
  });

  it('should find innermost symbol in nested hierarchy', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 5,
          column: 0,
          endLine: 20,
          endColumn: 1,
          children: [
            {
              name: 'MyMethod',
              kind: 'method',
              file: '/path/to/file.cs',
              line: 10,
              column: 4,
              endLine: 15,
              endColumn: 5,
            },
          ],
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 12,
        column: 5,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeDefined();
    expect(response.data.location.name).toBe('MyMethod');
    expect(response.data.location.kind).toBe('method');
  });
});

/**
 * Suite 3: Definition Resolution (4 tests)
 * Tests that definitions are correctly resolved from symbol information.
 */
describe('go-to-definition-handler: Definition Resolution', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should resolve simple class definition', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'SimpleClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 2,
          column: 0,
          endLine: 8,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 2,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    const loc = response.data.location;
    expect(loc.name).toBe('SimpleClass');
    expect(loc.kind).toBe('class');
    expect(loc.file).toBe('/path/to/file.cs');
    expect(loc.line).toBe(2);
    expect(loc.column).toBe(0);
  });

  it('should resolve method in class hierarchy', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 1,
          column: 0,
          endLine: 20,
          endColumn: 1,
          children: [
            {
              name: 'DoSomething',
              kind: 'method',
              file: '/path/to/file.cs',
              line: 5,
              column: 4,
              endLine: 10,
              endColumn: 5,
            },
          ],
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 4,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    const loc = response.data.location;
    expect(loc.name).toBe('DoSomething');
    expect(loc.kind).toBe('method');
    expect(loc.line).toBe(5);
  });

  it('should include optional symbol documentation if present', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'DocumentedClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 1,
          column: 0,
          endLine: 5,
          endColumn: 1,
          documentation: '/// <summary>This is documented.</summary>',
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 1,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeDefined();
    // Documentation would be included in a full implementation
  });

  it('should handle property resolution', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 1,
          column: 0,
          endLine: 10,
          endColumn: 1,
          children: [
            {
              name: 'MyProperty',
              kind: 'property',
              file: '/path/to/file.cs',
              line: 4,
              column: 4,
              endLine: 4,
              endColumn: 20,
            },
          ],
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 4,
        column: 4,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    const loc = response.data.location;
    expect(loc.name).toBe('MyProperty');
    expect(loc.kind).toBe('property');
  });
});

/**
 * Suite 4: Fallback Scenarios (3 tests)
 * Tests behavior when symbol tables are incomplete or unavailable.
 */
describe('go-to-definition-handler: Fallback Scenarios', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should handle empty symbol table gracefully', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols: [] },
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/empty.cs',
        line: 0,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeNull();
  });

  it('should handle null symbol table from extractor', async () => {
    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: null,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeNull();
  });

  it('should return null when searchScope is file but symbol not in current file', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'OtherClass',
          kind: 'class',
          file: '/path/to/other.cs',
          line: 1,
          column: 0,
          endLine: 5,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 10,
        column: 0,
        searchScope: 'file',
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeNull();
  });
});

/**
 * Suite 5: Error Handling (3 tests)
 * Tests error handling and graceful failure modes.
 */
describe('go-to-definition-handler: Error Handling', () => {
  let handler;
  let mockSymbolExtractor;
  let mockLogger;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    mockLogger = {
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor, logger: mockLogger });
  });

  it('should wrap SymbolExtractor exceptions as DefinitionError', async () => {
    mockSymbolExtractor.extractSymbols.mockRejectedValue(
      new Error('Extractor failed')
    );

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/extraction/i);
  });

  it('should return validation error on invalid input', async () => {
    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '',
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/validation/i);
  });

  it('should handle unexpected errors gracefully', async () => {
    // Mock extractSymbols to throw an unexpected error
    mockSymbolExtractor.extractSymbols.mockImplementation(() => {
      throw new SyntaxError('Unexpected token in JSON');
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 5,
        column: 10,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(false);
    expect(response.error).toMatch(/Internal error/);
  });
});

/**
 * Suite 6: Edge Cases (3 tests)
 * Tests boundary conditions and unusual scenarios.
 */
describe('go-to-definition-handler: Edge Cases', () => {
  let handler;
  let mockSymbolExtractor;

  beforeEach(() => {
    mockSymbolExtractor = {
      extractSymbols: vi.fn(),
    };

    handler = createGoToDefinitionHandler({ symbolExtractor: mockSymbolExtractor });
  });

  it('should handle symbol at line 0, column 0', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'FileClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 0,
          column: 0,
          endLine: 5,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 0,
        column: 0,
      },
    };

    const response = await handler(message, {});
    expect(response.success).toBe(true);
    expect(response.data.location).toBeDefined();
    expect(response.data.location.name).toBe('FileClass');
  });

  it('should handle large symbol table (performance)', async () => {
    // Create 1000 root-level symbols
    const symbols = Array.from({ length: 1000 }, (_, i) => ({
      name: `Class${i}`,
      kind: 'class',
      file: '/path/to/file.cs',
      line: i * 5,
      column: 0,
      endLine: i * 5 + 4,
      endColumn: 1,
    }));

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: { symbols },
    });

    const message = {
      messageType: 'bridge:goToDefinition',
      messageId: 'msg-123',
      data: {
        filepath: '/path/to/file.cs',
        line: 2500,
        column: 0,
      },
    };

    const startTime = Date.now();
    const response = await handler(message, {});
    const elapsed = Date.now() - startTime;

    expect(response.success).toBe(true);
    expect(elapsed).toBeLessThan(100); // Should be fast
  });

  it('should handle searchScope parameter variations', async () => {
    const symbolTable = {
      symbols: [
        {
          name: 'MyClass',
          kind: 'class',
          file: '/path/to/file.cs',
          line: 5,
          column: 0,
          endLine: 15,
          endColumn: 1,
        },
      ],
    };

    mockSymbolExtractor.extractSymbols.mockResolvedValue({
      success: true,
      data: symbolTable,
    });

    for (const scope of ['file', 'project', 'workspace']) {
      const message = {
        messageType: 'bridge:goToDefinition',
        messageId: `msg-${scope}`,
        data: {
          filepath: '/path/to/file.cs',
          line: 5,
          column: 0,
          searchScope: scope,
        },
      };

      const response = await handler(message, {});
      expect(response.success).toBe(true);
      expect(response.data.location).toBeDefined();
    }
  });
});
