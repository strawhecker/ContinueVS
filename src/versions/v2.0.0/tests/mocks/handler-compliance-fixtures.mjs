#!/usr/bin/env node

/**
 * Handler Compliance Test Fixtures
 *
 * Provides standardized valid/invalid message fixtures for all 20 handlers (Steps 76-95).
 * Used by handler-compliance tests (Step 97), performance tests (Step 98), and stress tests (Step 99).
 *
 * @module src/versions/v2.0.0/tests/mocks/handler-compliance-fixtures.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Each handler has:
 *   - validMessages: Array of 3 valid JSON-RPC messages (minimal, full, edge-case)
 *   - invalidMessages: Array of 4 invalid messages (missing field, wrong type, out-of-range, oversized)
 *   - expectedSchema: Response schema {type: 'success'|'error', fields: [...]}
 *   - expectedErrorCodes: Array of expected error codes from JSON-RPC standard
 *   - metadata: {tier: 'core'|'optional', timeout: 'fast'|'medium'|'slow', stability: 'stable'|'experimental'}
 *
 * Usage:
 *   const fixture = getHandlerFixture('refactor-handler');
 *   for (const msg of fixture.validMessages) {
 *     await handler.handle(msg);
 *   }
 */

/**
 * Get fixture for a specific handler
 * @param {string} handlerName - Handler name from Step 76-95
 * @returns {Object} - Fixture object with valid/invalid messages and metadata
 */
export function getHandlerFixture(handlerName) {
  const fixtures = {
    // Step 76: Refactor Handler
    'refactor-handler': {
      validMessages: [
        // Minimal valid message
        {
          id: 1,
          method: 'refactor:start',
          params: {
            documentUri: 'file:///path/to/file.cs',
            range: { start: { line: 0, character: 0 }, end: { line: 5, character: 0 } },
            refactoringType: 'extract-method',
          },
        },
        // Full message with all options
        {
          id: 2,
          method: 'refactor:start',
          params: {
            documentUri: 'file:///path/to/file.cs',
            range: { start: { line: 10, character: 5 }, end: { line: 20, character: 10 } },
            refactoringType: 'extract-method',
            options: { newMethodName: 'ExtractedMethod', makeStatic: true },
            context: { workspace: '/workspace', language: 'csharp' },
          },
        },
        // Edge case: large selection
        {
          id: 3,
          method: 'refactor:start',
          params: {
            documentUri: 'file:///path/to/large-file.cs',
            range: { start: { line: 0, character: 0 }, end: { line: 5000, character: 0 } },
            refactoringType: 'extract-method',
          },
        },
      ],
      invalidMessages: [
        // Missing required field
        {
          id: 4,
          method: 'refactor:start',
          params: {
            range: { start: { line: 0, character: 0 }, end: { line: 5, character: 0 } },
            refactoringType: 'extract-method',
          },
        },
        // Wrong type
        {
          id: 5,
          method: 'refactor:start',
          params: {
            documentUri: 123,
            range: 'invalid',
            refactoringType: 'extract-method',
          },
        },
        // Out of range
        {
          id: 6,
          method: 'refactor:start',
          params: {
            documentUri: 'file:///path/to/file.cs',
            range: { start: { line: -1, character: 0 }, end: { line: 5, character: 0 } },
            refactoringType: 'extract-method',
          },
        },
        // Oversized payload
        {
          id: 7,
          method: 'refactor:start',
          params: {
            documentUri: 'file:///path/to/file.cs',
            range: { start: { line: 0, character: 0 }, end: { line: 5, character: 0 } },
            refactoringType: 'extract-method',
            largePayload: 'x'.repeat(10 * 1024 * 1024),
          },
        },
      ],
      expectedSchema: {
        type: 'object',
        properties: {
          id: { type: 'number' },
          result: {
            oneOf: [
              {
                type: 'object',
                properties: {
                  refactoringId: { type: 'string' },
                  preview: { type: 'string' },
                  edits: { type: 'array' },
                },
              },
              {
                type: 'object',
                properties: { error: { type: 'object' } },
              },
            ],
          },
        },
      },
      expectedErrorCodes: [-32602, -32603, -32000],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 76 },
    },

    // Step 77: Fix Suggestion Handler
    'fix-suggestion-handler': {
      validMessages: [
        {
          id: 8,
          method: 'fixSuggestion:get',
          params: { documentUri: 'file:///path/to/file.cs', line: 10 },
        },
        {
          id: 9,
          method: 'fixSuggestion:get',
          params: {
            documentUri: 'file:///path/to/file.cs',
            line: 10,
            column: 5,
            includeRefactorings: true,
          },
        },
        {
          id: 10,
          method: 'fixSuggestion:get',
          params: { documentUri: 'file:///path/to/file.cs', line: 0 },
        },
      ],
      invalidMessages: [
        {
          id: 11,
          method: 'fixSuggestion:get',
          params: { line: 10 },
        },
        {
          id: 12,
          method: 'fixSuggestion:get',
          params: { documentUri: 123, line: 'ten' },
        },
        {
          id: 13,
          method: 'fixSuggestion:get',
          params: { documentUri: 'file:///path/to/file.cs', line: -100 },
        },
        {
          id: 14,
          method: 'fixSuggestion:get',
          params: { documentUri: 'file:///path/to/file.cs', line: 999999999 },
        },
      ],
      expectedSchema: {
        type: 'object',
        properties: {
          id: { type: 'number' },
          result: { type: 'array' },
        },
      },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 77 },
    },

    // Step 78: Apply Edit Handler
    'apply-edit-handler': {
      validMessages: [
        {
          id: 15,
          method: 'applyEdit:single',
          params: {
            documentUri: 'file:///path/to/file.cs',
            edit: { range: { start: { line: 0, character: 0 }, end: { line: 1, character: 0 } }, text: 'new code' },
          },
        },
        {
          id: 16,
          method: 'applyEdit:batch',
          params: {
            edits: [
              { documentUri: 'file:///path/to/file.cs', edits: [{ range: { start: { line: 0, character: 0 }, end: { line: 0, character: 5 } }, text: 'replaced' }] },
            ],
          },
        },
        {
          id: 17,
          method: 'applyEdit:single',
          params: { documentUri: 'file:///path/to/file.cs', edit: { range: { start: { line: 100, character: 0 }, end: { line: 100, character: 0 } }, text: '\n' } },
        },
      ],
      invalidMessages: [
        { id: 18, method: 'applyEdit:single', params: { edit: {} } },
        { id: 19, method: 'applyEdit:single', params: { documentUri: 123, edit: 'not-object' } },
        { id: 20, method: 'applyEdit:batch', params: { edits: 'not-array' } },
        { id: 21, method: 'applyEdit:single', params: { documentUri: '', edit: {} } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 78 },
    },

    // Step 79: Format Document Handler
    'format-document-handler': {
      validMessages: [
        { id: 22, method: 'formatDocument:execute', params: { documentUri: 'file:///path/to/file.cs' } },
        { id: 23, method: 'formatDocument:execute', params: { documentUri: 'file:///path/to/file.cs', options: { tabSize: 4, insertSpaces: true } } },
        { id: 24, method: 'formatDocument:range', params: { documentUri: 'file:///path/to/file.cs', range: { start: { line: 0, character: 0 }, end: { line: 10, character: 0 } } } },
      ],
      invalidMessages: [
        { id: 25, method: 'formatDocument:execute', params: {} },
        { id: 26, method: 'formatDocument:execute', params: { documentUri: 123 } },
        { id: 27, method: 'formatDocument:range', params: { documentUri: 'file:///path/to/file.cs', range: 'invalid' } },
        { id: 28, method: 'formatDocument:execute', params: { documentUri: '', options: null } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'array' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 79 },
    },

    // Step 80: Tree Sitter Integration (optional)
    'tree-sitter-handler': {
      validMessages: [
        { id: 29, method: 'treeSitter:parse', params: { documentUri: 'file:///path/to/file.cs', language: 'csharp' } },
        { id: 30, method: 'treeSitter:query', params: { documentUri: 'file:///path/to/file.cs', query: '(identifier)' } },
        { id: 31, method: 'treeSitter:parse', params: { documentUri: 'file:///path/to/file.js', language: 'javascript' } },
      ],
      invalidMessages: [
        { id: 32, method: 'treeSitter:parse', params: { language: 'csharp' } },
        { id: 33, method: 'treeSitter:parse', params: { documentUri: 123, language: 'csharp' } },
        { id: 34, method: 'treeSitter:query', params: { documentUri: 'file:///path/to/file.cs', query: null } },
        { id: 35, method: 'treeSitter:parse', params: { documentUri: 'file:///path/to/file.cs', language: 'unknown-lang' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'optional', timeout: 'fast', stability: 'experimental', relatedStep: 80 },
    },

    // Step 81: Git Integration Handler
    'git-integration-handler': {
      validMessages: [
        { id: 36, method: 'git:getDiff', params: { documentUri: 'file:///path/to/file.cs' } },
        { id: 37, method: 'git:getBlame', params: { documentUri: 'file:///path/to/file.cs', line: 10 } },
        { id: 38, method: 'git:getStatus', params: {} },
      ],
      invalidMessages: [
        { id: 39, method: 'git:getDiff', params: {} },
        { id: 40, method: 'git:getBlame', params: { line: 10 } },
        { id: 41, method: 'git:getBlame', params: { documentUri: 123, line: -1 } },
        { id: 42, method: 'git:getDiff', params: { documentUri: '' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { oneOf: [{ type: 'string' }, { type: 'array' }] } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 81 },
    },

    // Step 82: Terminal Handler
    'terminal-handler': {
      validMessages: [
        { id: 43, method: 'terminal:execute', params: { command: 'dotnet build' } },
        { id: 44, method: 'terminal:execute', params: { command: 'npm test', cwd: '/workspace' } },
        { id: 45, method: 'terminal:execute', params: { command: 'git status', timeout: 5000 } },
      ],
      invalidMessages: [
        { id: 46, method: 'terminal:execute', params: {} },
        { id: 47, method: 'terminal:execute', params: { command: 123 } },
        { id: 48, method: 'terminal:execute', params: { command: '', cwd: 123 } },
        { id: 49, method: 'terminal:execute', params: { command: 'x'.repeat(10000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object', properties: { stdout: { type: 'string' }, exitCode: { type: 'number' } } } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'slow', stability: 'stable', relatedStep: 82 },
    },

    // Step 83: File System Handler
    'file-system-handler': {
      validMessages: [
        { id: 50, method: 'fs:readFile', params: { path: '/workspace/file.cs' } },
        { id: 51, method: 'fs:writeFile', params: { path: '/workspace/file.cs', content: 'new content' } },
        { id: 52, method: 'fs:exists', params: { path: '/workspace' } },
      ],
      invalidMessages: [
        { id: 53, method: 'fs:readFile', params: {} },
        { id: 54, method: 'fs:writeFile', params: { path: 123, content: 'content' } },
        { id: 55, method: 'fs:exists', params: { path: '' } },
        { id: 56, method: 'fs:readFile', params: { path: 'x'.repeat(10000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { oneOf: [{ type: 'string' }, { type: 'boolean' }, { type: 'array' }] } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 83 },
    },

    // Step 84: Project Info Handler
    'project-info-handler': {
      validMessages: [
        { id: 57, method: 'projectInfo:get', params: {} },
        { id: 58, method: 'projectInfo:get', params: { includeSymbols: true } },
        { id: 59, method: 'projectInfo:getTargetFrameworks', params: {} },
      ],
      invalidMessages: [
        { id: 60, method: 'projectInfo:get', params: { includeSymbols: 'yes' } },
        { id: 61, method: 'projectInfo:getTargetFrameworks', params: { invalid: true } },
        { id: 62, method: 'projectInfo:get', params: null },
        { id: 63, method: 'projectInfo:get', params: { x: 'y'.repeat(10000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 84 },
    },

    // Step 85: Inline Message Handler
    'inline-message-handler': {
      validMessages: [
        { id: 64, method: 'inlineMessage:show', params: { documentUri: 'file:///path/to/file.cs', line: 10, message: 'Hello' } },
        { id: 65, method: 'inlineMessage:show', params: { documentUri: 'file:///path/to/file.cs', line: 10, message: 'Suggestion', type: 'info' } },
        { id: 66, method: 'inlineMessage:hide', params: {} },
      ],
      invalidMessages: [
        { id: 67, method: 'inlineMessage:show', params: { line: 10, message: 'Hello' } },
        { id: 68, method: 'inlineMessage:show', params: { documentUri: 123, line: 10, message: 'Hello' } },
        { id: 69, method: 'inlineMessage:show', params: { documentUri: 'file:///path/to/file.cs', line: -1, message: 'Hello' } },
        { id: 70, method: 'inlineMessage:show', params: { documentUri: 'file:///path/to/file.cs', line: 10, message: '' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 85 },
    },

    // Step 86: Sidebar UI Handler
    'sidebar-ui-handler': {
      validMessages: [
        { id: 71, method: 'sidebar:update', params: { sections: [] } },
        { id: 72, method: 'sidebar:update', params: { sections: [{ id: 'section1', title: 'Section 1', items: [] }] } },
        { id: 73, method: 'sidebar:getState', params: {} },
      ],
      invalidMessages: [
        { id: 74, method: 'sidebar:update', params: {} },
        { id: 75, method: 'sidebar:update', params: { sections: 'not-array' } },
        { id: 76, method: 'sidebar:update', params: { sections: [{ id: 123, title: 'Section' }] } },
        { id: 77, method: 'sidebar:update', params: { sections: [{ title: 'x'.repeat(10000) }] } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 86 },
    },

    // Step 87: Context Window Handler
    'context-window-handler': {
      validMessages: [
        { id: 78, method: 'context:getWindow', params: { documentUri: 'file:///path/to/file.cs', line: 10, contextSize: 10 } },
        { id: 79, method: 'context:getWindow', params: { documentUri: 'file:///path/to/file.cs', line: 0 } },
        { id: 80, method: 'context:getWindow', params: { documentUri: 'file:///path/to/file.cs', line: 100, contextSize: 50 } },
      ],
      invalidMessages: [
        { id: 81, method: 'context:getWindow', params: { line: 10 } },
        { id: 82, method: 'context:getWindow', params: { documentUri: 123, line: 10 } },
        { id: 83, method: 'context:getWindow', params: { documentUri: 'file:///path/to/file.cs', line: -1 } },
        { id: 84, method: 'context:getWindow', params: { documentUri: 'file:///path/to/file.cs', line: 10, contextSize: -5 } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'array' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 87 },
    },

    // Step 88: Model Info Handler
    'model-info-handler': {
      validMessages: [
        { id: 85, method: 'modelInfo:get', params: {} },
        { id: 86, method: 'modelInfo:get', params: { includeCapabilities: true } },
        { id: 87, method: 'modelInfo:listAvailable', params: {} },
      ],
      invalidMessages: [
        { id: 88, method: 'modelInfo:get', params: { includeCapabilities: 'yes' } },
        { id: 89, method: 'modelInfo:listAvailable', params: { x: 1 } },
        { id: 90, method: 'modelInfo:get', params: null },
        { id: 91, method: 'modelInfo:get', params: { x: 'x'.repeat(10000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { oneOf: [{ type: 'object' }, { type: 'array' }] } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 88 },
    },

    // Step 89: Streaming Response Handler
    'streaming-response-handler': {
      validMessages: [
        { id: 92, method: 'stream:start', params: { requestId: 'req-1', modelName: 'gpt-4', prompt: 'Hello' } },
        { id: 93, method: 'stream:chunk', params: { requestId: 'req-1', data: { content: 'Hi' } } },
        { id: 94, method: 'stream:end', params: { requestId: 'req-1' } },
      ],
      invalidMessages: [
        { id: 95, method: 'stream:start', params: { modelName: 'gpt-4', prompt: 'Hello' } },
        { id: 96, method: 'stream:chunk', params: { requestId: 123, data: {} } },
        { id: 97, method: 'stream:start', params: { requestId: '', modelName: 'gpt-4', prompt: 'Hello' } },
        { id: 98, method: 'stream:start', params: { requestId: 'req-1', modelName: 'gpt-4', prompt: 'x'.repeat(100000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'slow', stability: 'stable', relatedStep: 89 },
    },

    // Step 90: Code Lens Handler
    'code-lens-handler': {
      validMessages: [
        { id: 99, method: 'codeLens:get', params: { documentUri: 'file:///path/to/file.cs' } },
        { id: 100, method: 'codeLens:resolve', params: { documentUri: 'file:///path/to/file.cs', lens: { line: 10, command: 'cmd' } } },
        { id: 101, method: 'codeLens:get', params: { documentUri: 'file:///path/to/file.js' } },
      ],
      invalidMessages: [
        { id: 102, method: 'codeLens:get', params: {} },
        { id: 103, method: 'codeLens:get', params: { documentUri: 123 } },
        { id: 104, method: 'codeLens:resolve', params: { documentUri: 'file:///path/to/file.cs', lens: null } },
        { id: 105, method: 'codeLens:get', params: { documentUri: '' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'array' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 90 },
    },

    // Step 91: Snippet Handler
    'snippet-handler': {
      validMessages: [
        { id: 106, method: 'snippet:get', params: { language: 'csharp' } },
        { id: 107, method: 'snippet:get', params: { language: 'csharp', prefix: 'foreach' } },
        { id: 108, method: 'snippet:apply', params: { documentUri: 'file:///path/to/file.cs', snippetId: 'foreach-loop' } },
      ],
      invalidMessages: [
        { id: 109, method: 'snippet:get', params: {} },
        { id: 110, method: 'snippet:get', params: { language: 123 } },
        { id: 111, method: 'snippet:apply', params: { documentUri: 'file:///path/to/file.cs' } },
        { id: 112, method: 'snippet:get', params: { language: 'unknown-lang' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { oneOf: [{ type: 'array' }, { type: 'object' }] } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 91 },
    },

    // Step 92: Diff Viewer Handler
    'diff-viewer-handler': {
      validMessages: [
        { id: 113, method: 'diffViewer:show', params: { original: 'code1', modified: 'code2' } },
        { id: 114, method: 'diffViewer:show', params: { original: 'code1', modified: 'code2', language: 'csharp' } },
        { id: 115, method: 'diffViewer:close', params: {} },
      ],
      invalidMessages: [
        { id: 116, method: 'diffViewer:show', params: { modified: 'code2' } },
        { id: 117, method: 'diffViewer:show', params: { original: 123, modified: 'code2' } },
        { id: 118, method: 'diffViewer:show', params: { original: '', modified: '' } },
        { id: 119, method: 'diffViewer:show', params: { original: 'x'.repeat(1000000), modified: 'y'.repeat(1000000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 92 },
    },

    // Step 93: Refactor Tests Handler
    'refactor-tests-handler': {
      validMessages: [
        { id: 120, method: 'refactorTests:analyze', params: { documentUri: 'file:///path/to/test.cs' } },
        { id: 121, method: 'refactorTests:suggest', params: { documentUri: 'file:///path/to/test.cs', testName: 'MyTest' } },
        { id: 122, method: 'refactorTests:apply', params: { documentUri: 'file:///path/to/test.cs', refactoringId: 'refactor-1' } },
      ],
      invalidMessages: [
        { id: 123, method: 'refactorTests:analyze', params: {} },
        { id: 124, method: 'refactorTests:suggest', params: { documentUri: 123 } },
        { id: 125, method: 'refactorTests:apply', params: { documentUri: 'file:///path/to/test.cs' } },
        { id: 126, method: 'refactorTests:analyze', params: { documentUri: '' } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { oneOf: [{ type: 'array' }, { type: 'object' }] } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'medium', stability: 'stable', relatedStep: 93 },
    },

    // Step 94: Workspace Reload Handler
    'workspace-reload-handler': {
      validMessages: [
        { id: 127, method: 'workspace:reload', params: {} },
        { id: 128, method: 'workspace:reload', params: { soft: true } },
        { id: 129, method: 'workspace:reload', params: { includeNodeModules: false } },
      ],
      invalidMessages: [
        { id: 130, method: 'workspace:reload', params: { soft: 'yes' } },
        { id: 131, method: 'workspace:reload', params: { includeNodeModules: 1 } },
        { id: 132, method: 'workspace:reload', params: null },
        { id: 133, method: 'workspace:reload', params: { x: 'y'.repeat(10000) } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'slow', stability: 'stable', relatedStep: 94 },
    },

    // Step 95: Settings Sync Handler
    'settings-sync-handler': {
      validMessages: [
        { id: 134, method: 'settingsSync:load', params: {} },
        { id: 135, method: 'settingsSync:apply', params: { settings: { theme: 'dark', fontSize: 14 } } },
        { id: 136, method: 'settingsSync:save', params: { settings: { autoFormat: true } } },
      ],
      invalidMessages: [
        { id: 137, method: 'settingsSync:apply', params: {} },
        { id: 138, method: 'settingsSync:apply', params: { settings: 'not-object' } },
        { id: 139, method: 'settingsSync:save', params: { settings: null } },
        { id: 140, method: 'settingsSync:apply', params: { settings: { x: 'y'.repeat(100000) } } },
      ],
      expectedSchema: { type: 'object', properties: { id: { type: 'number' }, result: { type: 'object' } } },
      expectedErrorCodes: [-32602, -32603],
      metadata: { tier: 'core', timeout: 'fast', stability: 'stable', relatedStep: 95 },
    },
  };

  const fixture = fixtures[handlerName];
  if (!fixture) {
    throw new Error(`No fixture found for handler: ${handlerName}`);
  }

  return fixture;
}

/**
 * Get list of all handler names with fixtures
 * @returns {Array<string>} - Handler names
 */
export function getAvailableHandlerFixtures() {
  return [
    'refactor-handler',
    'fix-suggestion-handler',
    'apply-edit-handler',
    'format-document-handler',
    'tree-sitter-handler',
    'git-integration-handler',
    'terminal-handler',
    'file-system-handler',
    'project-info-handler',
    'inline-message-handler',
    'sidebar-ui-handler',
    'context-window-handler',
    'model-info-handler',
    'streaming-response-handler',
    'code-lens-handler',
    'snippet-handler',
    'diff-viewer-handler',
    'refactor-tests-handler',
    'workspace-reload-handler',
    'settings-sync-handler',
  ];
}

/**
 * Get all fixtures as a map
 * @returns {Object} - Map of handler name to fixture
 */
export function getAllFixtures() {
  const map = {};
  for (const handlerName of getAvailableHandlerFixtures()) {
    map[handlerName] = getHandlerFixture(handlerName);
  }
  return map;
}
