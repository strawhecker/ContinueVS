#!/usr/bin/env node

/**
 * Mock Fixtures for Go-To-Definition Handler Tests (Step 56)
 *
 * Provides reusable test fixtures including:
 * - Symbol table structures (classes, methods, properties)
 * - Document content with known symbol positions
 * - Alternative definitions (overloads, base implementations)
 * - Edge case scenarios
 *
 * @module src/versions/v2.0.0/tests/mocks/go-to-definition-fixtures.mjs
 * @version 1.0.0
 */

/**
 * Sample symbol table with a simple class definition.
 * @returns {Object} Symbol table with class at line 5
 */
export function getSimpleClassSymbolTable() {
  return {
    symbols: [
      {
        name: 'SimpleClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 5,
        column: 0,
        endLine: 15,
        endColumn: 1,
      },
    ],
  };
}

/**
 * Sample symbol table with nested class and methods.
 * @returns {Object} Symbol table with class containing methods
 */
export function getNestedSymbolTable() {
  return {
    symbols: [
      {
        name: 'OuterClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 1,
        column: 0,
        endLine: 30,
        endColumn: 1,
        children: [
          {
            name: 'Constructor',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 5,
            column: 4,
            endLine: 10,
            endColumn: 5,
          },
          {
            name: 'DoSomething',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 12,
            column: 4,
            endLine: 20,
            endColumn: 5,
          },
          {
            name: 'MyProperty',
            kind: 'property',
            file: '/path/to/file.cs',
            line: 22,
            column: 4,
            endLine: 22,
            endColumn: 25,
          },
        ],
      },
    ],
  };
}

/**
 * Sample symbol table with multiple top-level symbols.
 * @returns {Object} Symbol table with classes and interfaces
 */
export function getMultipleSymbolsTable() {
  return {
    symbols: [
      {
        name: 'IMyInterface',
        kind: 'interface',
        file: '/path/to/file.cs',
        line: 1,
        column: 0,
        endLine: 8,
        endColumn: 1,
        children: [
          {
            name: 'ExecuteAsync',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 3,
            column: 4,
            endLine: 3,
            endColumn: 30,
          },
        ],
      },
      {
        name: 'MyImplementation',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 10,
        column: 0,
        endLine: 25,
        endColumn: 1,
        children: [
          {
            name: 'ExecuteAsync',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 12,
            column: 4,
            endLine: 20,
            endColumn: 5,
          },
        ],
      },
    ],
  };
}

/**
 * Sample symbol table with deeply nested hierarchy.
 * @returns {Object} Symbol table with 4 levels of nesting
 */
export function getDeeplyNestedSymbolTable() {
  return {
    symbols: [
      {
        name: 'Level1',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 1,
        column: 0,
        endLine: 50,
        endColumn: 1,
        children: [
          {
            name: 'Level2',
            kind: 'class',
            file: '/path/to/file.cs',
            line: 5,
            column: 4,
            endLine: 40,
            endColumn: 5,
            children: [
              {
                name: 'Level3Method',
                kind: 'method',
                file: '/path/to/file.cs',
                line: 10,
                column: 8,
                endLine: 30,
                endColumn: 9,
                children: [
                  {
                    name: 'LocalClass',
                    kind: 'class',
                    file: '/path/to/file.cs',
                    line: 15,
                    column: 12,
                    endLine: 25,
                    endColumn: 13,
                  },
                ],
              },
            ],
          },
        ],
      },
    ],
  };
}

/**
 * Sample symbol table with documented symbols.
 * @returns {Object} Symbol table with XML documentation
 */
export function getDocumentedSymbolTable() {
  return {
    symbols: [
      {
        name: 'DocumentedClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 1,
        column: 0,
        endLine: 20,
        endColumn: 1,
        documentation: '/// <summary>This is a well-documented class.</summary>',
        children: [
          {
            name: 'DoWork',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 5,
            column: 4,
            endLine: 15,
            endColumn: 5,
            documentation: '/// <summary>Does important work.</summary>\n/// <param name="input">Input data.</param>\n/// <returns>Result.</returns>',
          },
        ],
      },
    ],
  };
}

/**
 * Sample symbol table with overloaded methods (alternatives).
 * @returns {Object} Symbol table with multiple DoSomething overloads
 */
export function getOverloadedMethodsTable() {
  return {
    symbols: [
      {
        name: 'MyClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 1,
        column: 0,
        endLine: 40,
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
          {
            name: 'DoSomething',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 12,
            column: 4,
            endLine: 18,
            endColumn: 5,
          },
          {
            name: 'DoSomething',
            kind: 'method',
            file: '/path/to/file.cs',
            line: 20,
            column: 4,
            endLine: 28,
            endColumn: 5,
          },
        ],
      },
    ],
  };
}

/**
 * Empty symbol table (edge case).
 * @returns {Object} Symbol table with no symbols
 */
export function getEmptySymbolTable() {
  return {
    symbols: [],
  };
}

/**
 * Large symbol table with 100 top-level symbols.
 * @returns {Object} Symbol table for performance testing
 */
export function getLargeSymbolTable() {
  const symbols = Array.from({ length: 100 }, (_, i) => ({
    name: `Class${i}`,
    kind: 'class',
    file: '/path/to/file.cs',
    line: i * 5,
    column: 0,
    endLine: i * 5 + 4,
    endColumn: 1,
    children: Array.from({ length: 5 }, (_, j) => ({
      name: `Method${j}`,
      kind: 'method',
      file: '/path/to/file.cs',
      line: i * 5 + j + 1,
      column: 4,
      endLine: i * 5 + j + 1,
      endColumn: 20,
    })),
  }));

  return { symbols };
}

/**
 * Sample document content for integration testing.
 * @returns {Object} Document with filepath and content
 */
export function getSampleDocument() {
  return {
    filepath: '/path/to/file.cs',
    content: `using System;

public class MyClass
{
    public MyClass()
    {
        // Constructor
    }

    public void DoWork(string input)
    {
        Console.WriteLine(input);
    }

    public string MyProperty { get; set; }
}
`,
  };
}

/**
 * Alternative definitions (overloads) for testing.
 * @returns {Array<Object>} Array of DefinitionLocation objects
 */
export function getAlternativeDefinitions() {
  return [
    {
      file: '/path/to/file.cs',
      line: 12,
      column: 4,
      name: 'DoSomething',
      kind: 'method',
    },
    {
      file: '/path/to/file.cs',
      line: 20,
      column: 4,
      name: 'DoSomething',
      kind: 'method',
    },
    {
      file: '/path/to/base.cs',
      line: 15,
      column: 4,
      name: 'DoSomething',
      kind: 'method',
    },
  ];
}

/**
 * Symbol table with boundary symbols (at edges of ranges).
 * @returns {Object} Symbol table for boundary testing
 */
export function getBoundarySymbolTable() {
  return {
    symbols: [
      {
        name: 'FirstClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 0,
        column: 0,
        endLine: 5,
        endColumn: 0,
      },
      {
        name: 'MiddleClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 6,
        column: 0,
        endLine: 10,
        endColumn: 0,
      },
      {
        name: 'LastClass',
        kind: 'class',
        file: '/path/to/file.cs',
        line: 11,
        column: 0,
        endLine: 20,
        endColumn: 1,
      },
    ],
  };
}

/**
 * Test case: Valid input for go-to-definition request.
 * @returns {Object} BridgeMessage-like object
 */
export function getValidGoToDefinitionMessage() {
  return {
    messageType: 'bridge:goToDefinition',
    messageId: 'msg-123',
    data: {
      filepath: '/path/to/file.cs',
      line: 5,
      column: 10,
      searchScope: 'file',
    },
  };
}

/**
 * Test case: Invalid input (missing filepath).
 * @returns {Object} Invalid message
 */
export function getInvalidMessageMissingFilepath() {
  return {
    messageType: 'bridge:goToDefinition',
    messageId: 'msg-123',
    data: {
      line: 5,
      column: 10,
    },
  };
}

/**
 * Test case: Invalid input (negative line).
 * @returns {Object} Invalid message
 */
export function getInvalidMessageNegativeLine() {
  return {
    messageType: 'bridge:goToDefinition',
    messageId: 'msg-123',
    data: {
      filepath: '/path/to/file.cs',
      line: -1,
      column: 10,
    },
  };
}

/**
 * Test case: Invalid input (invalid searchScope).
 * @returns {Object} Invalid message
 */
export function getInvalidMessageBadScope() {
  return {
    messageType: 'bridge:goToDefinition',
    messageId: 'msg-123',
    data: {
      filepath: '/path/to/file.cs',
      line: 5,
      column: 10,
      searchScope: 'invalid',
    },
  };
}

/**
 * Expected response: Definition found.
 * @returns {Object} Handler response with definition location
 */
export function getExpectedDefinitionFoundResponse() {
  return {
    success: true,
    data: {
      location: {
        file: '/path/to/file.cs',
        line: 5,
        column: 0,
        name: 'MyClass',
        kind: 'class',
      },
      alternatives: undefined,
    },
  };
}

/**
 * Expected response: Definition not found.
 * @returns {Object} Handler response with null location
 */
export function getExpectedDefinitionNotFoundResponse() {
  return {
    success: true,
    data: {
      location: null,
      alternatives: undefined,
    },
  };
}

/**
 * Expected response: Validation error.
 * @returns {Object} Handler error response
 */
export function getExpectedValidationErrorResponse() {
  return {
    success: false,
    error: expect.stringMatching(/validation|filepath|line|column/i),
  };
}

/**
 * Mock SymbolExtractor for testing.
 * @returns {Object} Mock with extractSymbols method
 */
export function createMockSymbolExtractor() {
  return {
    extractSymbols: async (filepath) => ({
      success: true,
      data: getSimpleClassSymbolTable(),
    }),
  };
}

/**
 * Mock DocumentProvider for testing.
 * @returns {Object} Mock with getDocument method
 */
export function createMockDocumentProvider() {
  return {
    getDocument: async (filepath) => ({
      filepath,
      content: getSampleDocument().content,
      language: 'csharp',
    }),
    getOpenDocuments: async () => [getSampleDocument()],
  };
}

/**
 * Mock Logger for testing.
 * @returns {Object} Mock logger with common methods
 */
export function createMockLogger() {
  return {
    debug: (msg) => console.log(`[DEBUG] ${msg}`),
    info: (msg) => console.log(`[INFO] ${msg}`),
    warn: (msg) => console.warn(`[WARN] ${msg}`),
    error: (msg) => console.error(`[ERROR] ${msg}`),
  };
}

/**
 * Mock Metrics for testing.
 * @returns {Object} Mock metrics collector
 */
export function createMockMetrics() {
  const recorded = new Map();

  return {
    record: (metric, value) => {
      recorded.set(metric, value);
    },
    getRecorded: () => recorded,
    clear: () => recorded.clear(),
  };
}
