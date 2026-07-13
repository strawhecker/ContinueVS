#!/usr/bin/env node

/**
 * Get Editor State Handler Test Suite (Step 50)
 *
 * Comprehensive unit tests for getEditorState handler (Step 50).
 * Tests cover happy path, error scenarios, edge cases, and state consistency.
 *
 * @module src/versions/v2.0.0/tests/get-editor-state-handler.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha + Node.js assert
 * Coverage: 15 test cases across 6 suites
 * Execution: npm test -- src/versions/v2.0.0/tests/get-editor-state-handler.test.mjs
 * Expected: 15/15 passing
 *
 * Related Steps:
 *   - Step 48: editor-context-collector.js (code under test — collector)
 *   - Step 50: get-editor-state-handler.mjs (handler under test)
 *   - Step 67: handler tests (editor context) — integration layer
 *   - Step 71: handler registration — uses this test pattern
 */

import assert from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  getEditorStateHandler,
  createGetEditorStateHandler,
  GetEditorStateError
} from '../lib/get-editor-state-handler.mjs';

/**
 * Test Fixtures & Mocks
 */

/**
 * Creates a mock EditorContextCollector for testing.
 * Allows setting state and tracking method calls.
 */
function createMockCollector(initialState = {}) {
  const defaults = {
    activeFile: null,
    cursorPosition: null,
    selection: null
  };

  const state = { ...defaults, ...initialState };

  return {
    _state: state,
    getActiveFile: function() {
      return this._state.activeFile;
    },
    getCursorPosition: function() {
      return this._state.cursorPosition;
    },
    getSelection: function() {
      return this._state.selection;
    },
    setState: function(newState) {
      this._state = { ...this._state, ...newState };
    }
  };
}

/**
 * Creates a mock logger for testing.
 * Captures all log calls for assertion.
 */
function createMockLogger() {
  return {
    calls: [],
    debug: function(message, context) {
      this.calls.push({ level: 'debug', message, context });
    },
    error: function(message, error, context) {
      this.calls.push({ level: 'error', message, error, context });
    }
  };
}

/**
 * Creates a mock metrics collector for testing.
 * Captures all telemetry calls for assertion.
 */
function createMockMetrics() {
  return {
    calls: [],
    recordHandlerExecution: function(handlerName, success, latencyMs) {
      this.calls.push({ handlerName, success, latencyMs });
    }
  };
}

/**
 * Creates a test message envelope.
 */
function createMessage(overrides = {}) {
  const defaults = {
    messageType: 'bridge:getEditorState',
    messageId: 'test-uuid-123',
    data: {}
  };
  return { ...defaults, ...overrides };
}

/**
 * Test Suites
 */

describe('getEditorStateHandler', () => {
  let mockCollector;
  let mockLogger;
  let mockMetrics;
  let context;

  beforeEach(() => {
    mockCollector = createMockCollector();
    mockLogger = createMockLogger();
    mockMetrics = createMockMetrics();
    context = {
      editorContextCollector: mockCollector,
      logger: mockLogger,
      metrics: mockMetrics
    };
  });

  /**
   * Suite 1: Happy Path
   * Verifies successful editor state retrieval.
   */
  describe('Suite 1: Happy Path', () => {
    it('should return editor state with active file, cursor, and selection', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Main.cs',
          contents: 'using System;\nclass Program {}',
          cursorLine: 5,
          cursorColumn: 10,
          language: 'csharp',
          projectPath: 'C:\\project',
          diagnosticsCount: 2
        },
        cursorPosition: { line: 5, character: 10 },
        selection: { text: 'Program', start: 40, end: 47 }
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert(response.data, 'response.data should exist');
      assert.strictEqual(response.data.activeFile, 'C:\\project\\Main.cs', 'activeFile should match');
      assert.strictEqual(response.data.cursorLine, 5, 'cursorLine should be 5');
      assert.strictEqual(response.data.cursorColumn, 10, 'cursorColumn should be 10');
      assert.strictEqual(response.data.selectedText, 'Program', 'selectedText should match');
      assert.strictEqual(response.data.selectionStart, 40, 'selectionStart should be 40');
      assert.strictEqual(response.data.selectionEnd, 47, 'selectionEnd should be 47');
      assert.strictEqual(response.data.language, 'csharp', 'language should be csharp');
      assert.strictEqual(response.data.projectPath, 'C:\\project', 'projectPath should match');
      assert.strictEqual(response.data.diagnosticsCount, 2, 'diagnosticsCount should be 2');
    });

    it('should record metrics on successful execution', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: { filepath: 'C:\\project\\Main.cs', contents: '', cursorLine: 0, cursorColumn: 0 },
        cursorPosition: { line: 0, character: 0 },
        selection: null
      });
      const message = createMessage();

      // Act
      await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(mockMetrics.calls.length, 1, 'metrics.recordHandlerExecution should be called once');
      const call = mockMetrics.calls[0];
      assert.strictEqual(call.handlerName, 'bridge:getEditorState', 'handlerName should be correct');
      assert.strictEqual(call.success, true, 'success should be true');
      assert(call.latencyMs >= 0, 'latencyMs should be non-negative');
    });

    it('should log debug messages when logger available', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: { filepath: 'C:\\project\\Main.cs', contents: '', cursorLine: 0, cursorColumn: 0 },
        cursorPosition: { line: 0, character: 0 },
        selection: null
      });
      const message = createMessage();

      // Act
      await getEditorStateHandler(message, context);

      // Assert
      assert(mockLogger.calls.length >= 2, 'logger.debug should be called at least twice');
      assert.strictEqual(mockLogger.calls[0].level, 'debug', 'first log should be debug level');
      assert(mockLogger.calls[0].message.includes('querying'), 'first log should mention querying');
    });
  });

  /**
   * Suite 2: Null/Missing Collector
   * Verifies error handling when collector is not available.
   */
  describe('Suite 2: Null/Missing Collector', () => {
    it('should return error when collector is null', async () => {
      // Arrange
      const message = createMessage();
      const invalidContext = { editorContextCollector: null, logger: mockLogger, metrics: mockMetrics };

      // Act
      const response = await getEditorStateHandler(message, invalidContext);

      // Assert
      assert.strictEqual(response.success, false, 'response.success should be false');
      assert.strictEqual(response.error.code, 'EDITOR_STATE_ERROR', 'error code should be EDITOR_STATE_ERROR');
      assert(response.error.message.includes('not initialized'), 'error message should mention initialization');
    });

    it('should return error when context is null', async () => {
      // Arrange
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, null);

      // Assert
      assert.strictEqual(response.success, false, 'response.success should be false');
      assert.strictEqual(response.error.code, 'EDITOR_STATE_ERROR', 'error code should be correct');
    });

    it('should return error when context is undefined', async () => {
      // Arrange
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, undefined);

      // Assert
      assert.strictEqual(response.success, false, 'response.success should be false');
    });

    it('should record failed metrics when collector unavailable', async () => {
      // Arrange
      const message = createMessage();
      const invalidContext = { editorContextCollector: null, metrics: mockMetrics };

      // Act
      await getEditorStateHandler(message, invalidContext);

      // Assert
      assert.strictEqual(mockMetrics.calls.length, 1, 'metrics should record failure');
      assert.strictEqual(mockMetrics.calls[0].success, false, 'success should be false');
    });
  });

  /**
   * Suite 3: No Active File
   * Verifies graceful handling when no file is open.
   */
  describe('Suite 3: No Active File', () => {
    it('should return state with null activeFile when no file open', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: null,
        cursorPosition: null,
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert.strictEqual(response.data.activeFile, null, 'activeFile should be null');
      assert.strictEqual(response.data.cursorLine, 0, 'cursorLine should default to 0');
      assert.strictEqual(response.data.cursorColumn, 0, 'cursorColumn should default to 0');
      assert.strictEqual(response.data.selectedText, '', 'selectedText should default to empty');
    });

    it('should return correct defaults when state is empty', async () => {
      // Arrange
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert.strictEqual(response.data.language, 'unknown', 'language should default to unknown');
      assert.strictEqual(response.data.projectPath, '', 'projectPath should default to empty');
      assert.strictEqual(response.data.diagnosticsCount, 0, 'diagnosticsCount should default to 0');
    });
  });

  /**
   * Suite 4: Partial State
   * Verifies handling of partial editor state (file but no selection, etc.).
   */
  describe('Suite 4: Partial State', () => {
    it('should handle file without selection', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Main.cs',
          contents: 'using System;',
          cursorLine: 0,
          cursorColumn: 6,
          language: 'csharp',
          projectPath: 'C:\\project',
          diagnosticsCount: 0
        },
        cursorPosition: { line: 0, character: 6 },
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert.strictEqual(response.data.selectedText, '', 'selectedText should be empty');
      assert.strictEqual(response.data.selectionStart, -1, 'selectionStart should be -1 when no selection');
      assert.strictEqual(response.data.selectionEnd, -1, 'selectionEnd should be -1 when no selection');
      assert.strictEqual(response.data.activeFile, 'C:\\project\\Main.cs', 'activeFile should still be set');
    });

    it('should handle cursor without selection', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Test.cs',
          contents: 'var x = 42;',
          cursorLine: 0,
          cursorColumn: 7,
          language: 'csharp',
          projectPath: 'C:\\project'
        },
        cursorPosition: { line: 0, character: 7 },
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.data.cursorLine, 0, 'cursorLine should be 0');
      assert.strictEqual(response.data.cursorColumn, 7, 'cursorColumn should be 7');
    });
  });

  /**
   * Suite 5: Edge Cases
   * Verifies handling of edge cases (large files, special characters, etc.).
   */
  describe('Suite 5: Edge Cases', () => {
    it('should handle large file content', async () => {
      // Arrange
      const largeContent = 'line\n'.repeat(10000);
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Large.cs',
          contents: largeContent,
          cursorLine: 5000,
          cursorColumn: 100,
          language: 'csharp',
          projectPath: 'C:\\project',
          diagnosticsCount: 50
        },
        cursorPosition: { line: 5000, character: 100 },
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert(response.data.fileContent.length >= 50000, 'fileContent should contain large content (at least 50KB)');
      assert.strictEqual(response.data.cursorLine, 5000, 'cursorLine should be 5000');
    });

    it('should handle selection with special characters', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Special.cs',
          contents: 'var str = "hello\\nworld";',
          cursorLine: 0,
          cursorColumn: 10,
          language: 'csharp',
          projectPath: 'C:\\project'
        },
        cursorPosition: { line: 0, character: 10 },
        selection: { text: '"hello\\nworld"', start: 10, end: 25 }
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert.strictEqual(response.data.selectedText, '"hello\\nworld"', 'selectedText should preserve special chars');
    });

    it('should handle cursor at end of file', async () => {
      // Arrange
      const content = 'using System;\nclass Program {}';
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\EOF.cs',
          contents: content,
          cursorLine: 1,
          cursorColumn: 16,
          language: 'csharp',
          projectPath: 'C:\\project'
        },
        cursorPosition: { line: 1, character: 16 },
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.data.cursorLine, 1, 'cursorLine should be at second line');
      assert.strictEqual(response.data.cursorColumn, 16, 'cursorColumn should be at position 16');
    });

    it('should handle cursor at beginning of file', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Start.cs',
          contents: 'using System;',
          cursorLine: 0,
          cursorColumn: 0,
          language: 'csharp',
          projectPath: 'C:\\project'
        },
        cursorPosition: { line: 0, character: 0 },
        selection: null
      });
      const message = createMessage();

      // Act
      const response = await getEditorStateHandler(message, context);

      // Assert
      assert.strictEqual(response.data.cursorLine, 0, 'cursorLine should be 0');
      assert.strictEqual(response.data.cursorColumn, 0, 'cursorColumn should be 0');
    });
  });

  /**
   * Suite 6: Factory Function & Dependency Injection
   * Verifies createGetEditorStateHandler factory.
   */
  describe('Suite 6: Factory Function & Dependency Injection', () => {
    it('should create bound handler with collector', async () => {
      // Arrange
      mockCollector.setState({
        activeFile: {
          filepath: 'C:\\project\\Main.cs',
          contents: 'code',
          cursorLine: 0,
          cursorColumn: 0,
          language: 'csharp',
          projectPath: 'C:\\project'
        },
        cursorPosition: { line: 0, character: 0 },
        selection: null
      });
      const boundHandler = createGetEditorStateHandler(mockCollector);
      const message = createMessage();
      const minimalContext = { logger: mockLogger, metrics: mockMetrics };

      // Act
      const response = await boundHandler(message, minimalContext);

      // Assert
      assert.strictEqual(response.success, true, 'response.success should be true');
      assert.strictEqual(response.data.activeFile, 'C:\\project\\Main.cs', 'activeFile should be set');
    });

    it('should throw error when creating handler with null collector', () => {
      // Act & Assert
      assert.throws(
        () => createGetEditorStateHandler(null),
        TypeError,
        'should throw TypeError for null collector'
      );
    });

    it('should throw error when creating handler with non-object collector', () => {
      // Act & Assert
      assert.throws(
        () => createGetEditorStateHandler('not-an-object'),
        TypeError,
        'should throw TypeError for string collector'
      );
    });

    it('should throw error when creating handler with array collector', () => {
      // Act & Assert
      assert.throws(
        () => createGetEditorStateHandler([]),
        TypeError,
        'should throw TypeError for array collector'
      );
    });
  });
});

/**
 * GetEditorStateError Class Tests
 */

describe('GetEditorStateError', () => {
  it('should be instance of Error', () => {
    const error = new GetEditorStateError('test error');
    assert(error instanceof Error, 'GetEditorStateError should be instance of Error');
  });

  it('should set operationType on construction', () => {
    const error = new GetEditorStateError('test', 'init');
    assert.strictEqual(error.operationType, 'init', 'operationType should be set');
  });

  it('should have name property set to GetEditorStateError', () => {
    const error = new GetEditorStateError('test');
    assert.strictEqual(error.name, 'GetEditorStateError', 'name should be GetEditorStateError');
  });

  it('should store original error', () => {
    const originalError = new Error('original');
    const wrappedError = new GetEditorStateError('wrapped', 'query', originalError);
    assert.strictEqual(wrappedError.originalError, originalError, 'originalError should be stored');
  });
});
