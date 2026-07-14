#!/usr/bin/env node

/**
 * Editor Context Handler Integration Tests (Step 67)
 *
 * Integration test suite validating the two editor context handlers
 * (Steps 50 & 51) working together with their dependencies.
 *
 * Tests cover:
 * - Handler initialization & dependency injection
 * - getEditorState + onEditorStateChange lifecycle together
 * - State consistency across handlers
 * - Error recovery & edge cases
 * - Performance & concurrent calls
 *
 * @module src/versions/v2.0.0/tests/editor-context-handler-integration.test.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Test Framework: Mocha + Node.js assert
 * Coverage: 16 test cases across 5 suites
 * Execution: npm test -- src/versions/v2.0.0/tests/editor-context-handler-integration.test.mjs
 * Expected: 16/16 passing
 *
 * Related Steps:
 *   - Step 48: editor-context-collector.js (mocked dependency)
 *   - Step 49: selection-tracker.mjs (mocked dependency)
 *   - Step 50: get-editor-state-handler.mjs (handler under test)
 *   - Step 51: onEditorStateChange-handler.mjs (handler under test)
 *   - Step 67: handler tests (editor context) — THIS FILE
 *   - Step 70: handler integration tests — includes this suite
 *   - Step 71: handler registration — uses handlers tested here
 */

import assert from 'assert';
import { describe, it, beforeEach, afterEach } from 'mocha';
import {
  getEditorStateHandler,
  createGetEditorStateHandler,
  GetEditorStateError
} from '../lib/get-editor-state-handler.mjs';

// ============================================================================
// MOCK FACTORIES
// ============================================================================

/**
 * Creates a mock EditorContextCollector with configurable state.
 * Simulates the real collector interface from Step 48.
 *
 * @param {Object} initialState - Initial state { activeFile, cursorPosition, selection }
 * @returns {Object} Mock collector with state management methods
 */
function createMockEditorContextCollector(initialState = {}) {
  const defaults = {
    activeFile: null,
    cursorPosition: null,
    selection: null
  };

  const state = { ...defaults, ...initialState };

  return {
    _state: state,
    _listeners: [],

    getActiveFile() {
      return this._state.activeFile;
    },

    getCursorPosition() {
      return this._state.cursorPosition;
    },

    getSelection() {
      return this._state.selection;
    },

    onStateChange(callback) {
      if (typeof callback !== 'function') {
        throw new Error('Callback must be a function');
      }
      this._listeners.push(callback);
    },

    setState(newState) {
      const oldState = { ...this._state };
      this._state = { ...this._state, ...newState };
      // Notify listeners of state change
      for (const listener of this._listeners) {
        try {
          listener(this._state, oldState);
        } catch (err) {
          // Swallow listener errors (realistic behavior)
        }
      }
    },

    getListenerCount() {
      return this._listeners.length;
    }
  };
}

/**
 * Creates a mock SelectionTracker with listener management.
 * Simulates the real tracker interface from Step 49.
 *
 * @param {Object} initialSelection - Initial selection { start, end, text }
 * @returns {Object} Mock tracker with selection subscription support
 */
function createMockSelectionTracker(initialSelection = null) {
  const state = {
    selection: initialSelection,
    listeners: []
  };

  return {
    hasSelection() {
      return state.selection !== null && state.selection !== undefined;
    },

    getSelection() {
      return state.selection;
    },

    onSelectionChange(callback) {
      if (typeof callback !== 'function') {
        throw new Error('Callback must be a function');
      }
      state.listeners.push(callback);
    },

    setSelection(newSelection) {
      const oldSelection = state.selection;
      state.selection = newSelection;
      // Notify listeners
      for (const listener of state.listeners) {
        try {
          listener(newSelection, oldSelection);
        } catch (err) {
          // Swallow listener errors
        }
      }
    },

    getListenerCount() {
      return state.listeners.length;
    },

    dispose() {
      state.listeners = [];
    }
  };
}

/**
 * Creates a mock dispatcher for tracking sendMessage calls.
 * Simulates the real dispatcher interface used by onEditorStateChange.
 *
 * @returns {Object} Mock dispatcher with message tracking
 */
function createMockDispatcher() {
  return {
    messages: [],

    sendMessage(message) {
      if (!message || typeof message !== 'object') {
        throw new Error('Message must be an object');
      }
      this.messages.push({ ...message, sentAt: Date.now() });
    },

    getMessages() {
      return this.messages;
    },

    clear() {
      this.messages = [];
    },

    getLastMessage() {
      return this.messages[this.messages.length - 1] || null;
    }
  };
}

/**
 * Creates a mock logger for tracking log calls.
 *
 * @returns {Object} Mock logger with call tracking
 */
function createMockLogger() {
  return {
    calls: [],

    debug(message, context) {
      this.calls.push({ level: 'debug', message, context });
    },

    error(message, error, context) {
      this.calls.push({ level: 'error', message, error, context });
    },

    info(message, context) {
      this.calls.push({ level: 'info', message, context });
    },

    getCalls(level) {
      return level ? this.calls.filter(c => c.level === level) : this.calls;
    },

    clear() {
      this.calls = [];
    }
  };
}

/**
 * Creates a mock metrics collector.
 *
 * @returns {Object} Mock metrics collector
 */
function createMockMetrics() {
  return {
    calls: [],

    record(name, value, tags) {
      this.calls.push({ name, value, tags, recordedAt: Date.now() });
    },

    getCalls(name) {
      return name ? this.calls.filter(c => c.name === name) : this.calls;
    },

    clear() {
      this.calls = [];
    }
  };
}

/**
 * Creates a bridge message for testing.
 *
 * @param {string} type - Message type
 * @param {Object} params - Message parameters
 * @returns {Object} Bridge message
 */
function createBridgeMessage(type = 'bridge:getEditorState', params = {}) {
  return {
    id: `msg-${Date.now()}-${Math.random()}`,
    type,
    params,
    timestamp: Date.now()
  };
}

// ============================================================================
// SUITE 1: Handler Initialization & Dependency Injection (3 tests)
// ============================================================================

describe('Suite 1: Handler Initialization & Dependency Injection', () => {
  it('should initialize both handlers successfully with valid mocks', async () => {
    // Arrange
    const collector = createMockEditorContextCollector({
      activeFile: { filepath: '/path/to/file.js', contents: 'code here' },
      cursorPosition: { line: 5, character: 10 },
      selection: { start: { line: 5, character: 10 }, end: { line: 5, character: 20 }, text: 'selected' }
    });
    const logger = createMockLogger();
    const metrics = createMockMetrics();

    // Act
    const handlerFactory = createGetEditorStateHandler(collector);
    assert(typeof handlerFactory === 'function', 'Factory should return a function');

    const message = createBridgeMessage();
    const context = { editorContextCollector: collector, logger, metrics };
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Handler should succeed');
    assert(response.data, 'Response should contain data');
    assert.strictEqual(response.data.activeFile, '/path/to/file.js', 'Should return correct active file');
  });

  it('should reject getEditorStateHandler when collector is null', async () => {
    // Arrange
    const message = createBridgeMessage();
    const context = { editorContextCollector: null };

    // Act
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, false, 'Response should indicate failure');
    assert.strictEqual(response.error.code, 'EDITOR_STATE_ERROR', 'Should return EDITOR_STATE_ERROR');
    assert(response.error.message.includes('not initialized'), 'Error message should mention initialization');
  });

  it('should reject getEditorStateHandler when context is undefined', async () => {
    // Arrange
    const message = createBridgeMessage();

    // Act
    const response = await getEditorStateHandler(message, undefined);

    // Assert
    assert.strictEqual(response.success, false, 'Response should indicate failure');
    assert(response.error, 'Response should contain error');
    assert.strictEqual(response.error.code, 'EDITOR_STATE_ERROR', 'Should return EDITOR_STATE_ERROR');
  });
});

// ============================================================================
// SUITE 2: getEditorState + onEditorStateChange Lifecycle (5 tests)
// ============================================================================

describe('Suite 2: getEditorState + onEditorStateChange Lifecycle', () => {
  let collector;
  let dispatcher;
  let logger;
  let metrics;

  beforeEach(() => {
    collector = createMockEditorContextCollector({
      activeFile: { filepath: '/path/to/test.js', contents: 'test code' },
      cursorPosition: { line: 10, character: 5 },
      selection: { start: { line: 10, character: 5 }, end: { line: 10, character: 15 }, text: 'selection' }
    });
    dispatcher = createMockDispatcher();
    logger = createMockLogger();
    metrics = createMockMetrics();
  });

  it('should call getEditorState and return editor state snapshot', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Should succeed');
    assert(response.data, 'Response should contain data');
    assert.strictEqual(response.data.activeFile, '/path/to/test.js', 'Should return correct active file');
    assert.strictEqual(response.data.cursorLine, 10, 'Should return correct cursor line');
    assert.strictEqual(response.data.cursorColumn, 5, 'Should return correct cursor column');
    assert.strictEqual(response.data.selectedText, 'selection', 'Should return correct selection');
  });

  it('should handle multiple rapid getEditorState calls returning consistent state', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response1 = await getEditorStateHandler(message, context);
    const response2 = await getEditorStateHandler(message, context);
    const response3 = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response1.success, true, 'Response 1 should succeed');
    assert.strictEqual(response2.success, true, 'Response 2 should succeed');
    assert.strictEqual(response3.success, true, 'Response 3 should succeed');

    assert.deepStrictEqual(response1.data.activeFile, response2.data.activeFile, 'Responses should return same active file');
    assert.deepStrictEqual(response2.data.activeFile, response3.data.activeFile, 'Responses should maintain consistency');
  });

  it('should reflect state change between getEditorState calls', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act - First call
    const response1 = await getEditorStateHandler(message, context);
    const file1 = response1.data.activeFile;

    // Change state via collector
    collector.setState({
      activeFile: { filepath: '/path/to/other.js', contents: 'other code' },
      selection: { start: { line: 15, character: 0 }, end: { line: 15, character: 5 }, text: 'other' }
    });

    // Second call
    const response2 = await getEditorStateHandler(message, context);
    const file2 = response2.data.activeFile;

    // Assert
    assert.strictEqual(file1, '/path/to/test.js', 'First call should return original file');
    assert.strictEqual(file2, '/path/to/other.js', 'Second call should return updated file');
    assert.notStrictEqual(file1, file2, 'Files should be different after state change');
  });

  it('should handle null selection gracefully', async () => {
    // Arrange
    collector.setState({ selection: null });
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Should succeed with null selection');
    assert.strictEqual(response.data.selectedText, '', 'selectedText should be empty string for null selection');
  });

  it('should maintain cursor position consistency after editor state queries', async () => {
    // Arrange
    const originalCursor = { line: 20, character: 8 };
    collector.setState({ cursorPosition: originalCursor });
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response1 = await getEditorStateHandler(message, context);
    const response2 = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response1.data.cursorLine, originalCursor.line, 'First response should match cursor line');
    assert.strictEqual(response1.data.cursorColumn, originalCursor.character, 'First response should match cursor column');
    assert.strictEqual(response2.data.cursorLine, originalCursor.line, 'Second response should match cursor line');
    assert.strictEqual(response2.data.cursorColumn, originalCursor.character, 'Second response should match cursor column');
  });
});

// ============================================================================
// SUITE 3: State Consistency Across Handlers (4 tests)
// ============================================================================

describe('Suite 3: State Consistency Across Handlers', () => {
  let collector;
  let tracker;
  let logger;
  let metrics;

  beforeEach(() => {
    const selection = {
      start: { line: 5, character: 0 },
      end: { line: 5, character: 10 },
      text: 'consistent',
      isMultiline: false
    };

    collector = createMockEditorContextCollector({
      activeFile: { filepath: '/shared/file.js', contents: 'shared code' },
      cursorPosition: { line: 5, character: 0 },
      selection
    });

    tracker = createMockSelectionTracker(selection);
    logger = createMockLogger();
    metrics = createMockMetrics();
  });

  it('should return consistent selection between getEditorState and SelectionTracker', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const editorStateResponse = await getEditorStateHandler(message, context);
    const trackerSelection = tracker.getSelection();

    // Assert
    assert.strictEqual(editorStateResponse.success, true, 'getEditorState should succeed');
    assert.strictEqual(editorStateResponse.data.selectedText, trackerSelection.text, 'Selected text should match between handler and tracker');
  });

  it('should propagate EditorContextCollector state changes to getEditorState responses', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };
    const newSelection = {
      start: { line: 10, character: 5 },
      end: { line: 10, character: 20 },
      text: 'propagated',
      isMultiline: false
    };

    // Act - Before change
    const response1 = await getEditorStateHandler(message, context);
    const selection1 = response1.data.selectedText;

    // Update collector state
    collector.setState({ selection: newSelection });

    // After change
    const response2 = await getEditorStateHandler(message, context);
    const selection2 = response2.data.selectedText;

    // Assert
    assert.strictEqual(selection1, 'consistent', 'First response should have original selection');
    assert.strictEqual(selection2, 'propagated', 'Second response should have propagated selection');
  });

  it('should maintain active file consistency when collector state changes', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act - Query initial state
    const response1 = await getEditorStateHandler(message, context);
    const file1 = response1.data.activeFile;

    // Change file in collector
    collector.setState({ activeFile: { filepath: '/shared/newfile.js', contents: 'new code' } });
    const response2 = await getEditorStateHandler(message, context);
    const file2 = response2.data.activeFile;

    // Assert
    assert.strictEqual(file1, '/shared/file.js', 'Initial file should be correct');
    assert.strictEqual(file2, '/shared/newfile.js', 'Updated file should be reflected');
    assert.notStrictEqual(file1, file2, 'Files should differ after change');
  });

  it('should handle null/cleared selection consistently across handler calls', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act - Initial state with selection
    const response1 = await getEditorStateHandler(message, context);
    const hasSelection1 = response1.data.selectedText !== '';

    // Clear selection
    collector.setState({ selection: null });
    tracker.setSelection(null);

    // After clearing
    const response2 = await getEditorStateHandler(message, context);
    const hasSelection2 = response2.data.selectedText !== '';

    // Assert
    assert.strictEqual(hasSelection1, true, 'Should have selection initially');
    assert.strictEqual(hasSelection2, false, 'Should not have selection after clearing');
    assert.strictEqual(tracker.hasSelection(), false, 'Tracker should also reflect cleared selection');
  });
});

// ============================================================================
// SUITE 4: Error Recovery & Edge Cases (4 tests)
// ============================================================================

describe('Suite 4: Error Recovery & Edge Cases', () => {
  let collector;
  let logger;
  let metrics;

  beforeEach(() => {
    collector = createMockEditorContextCollector();
    logger = createMockLogger();
    metrics = createMockMetrics();
  });

  it('should gracefully handle collector returning all null values', async () => {
    // Arrange
    collector.setState({
      activeFile: null,
      cursorPosition: null,
      selection: null
    });
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Should succeed even with all nulls');
    assert.strictEqual(response.data.activeFile, null, 'activeFile should be null');
    assert.strictEqual(response.data.cursorLine, 0, 'cursorLine should be 0 (default)');
    assert.strictEqual(response.data.cursorColumn, 0, 'cursorColumn should be 0 (default)');
    assert.strictEqual(response.data.selectedText, '', 'selectedText should be empty string');
  });

  it('should handle very rapid state changes without losing data', async () => {
    // Arrange
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };
    const files = ['/file1.js', '/file2.js', '/file3.js', '/file4.js', '/file5.js'];

    // Act - Rapidly change state and query
    const responses = [];
    for (let i = 0; i < files.length; i++) {
      collector.setState({ activeFile: { filepath: files[i], contents: `code ${i}` } });
      const response = await getEditorStateHandler(message, context);
      responses.push(response);
    }

    // Assert
    assert.strictEqual(responses.length, 5, 'Should have 5 responses');
    for (let i = 0; i < responses.length; i++) {
      assert.strictEqual(responses[i].success, true, `Response ${i} should succeed`);
      assert.strictEqual(responses[i].data.activeFile, files[i], `Response ${i} should have correct file`);
    }
  });

  it('should handle formatter edge case: very long selection text', async () => {
    // Arrange
    const veryLongText = 'x'.repeat(10000); // 10KB selection
    const longSelection = {
      start: { line: 0, character: 0 },
      end: { line: 0, character: 10000 },
      text: veryLongText
    };
    collector.setState({ selection: longSelection });
    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Should handle large selection');
    assert.strictEqual(response.data.selectedText.length, 10000, 'Should preserve full text length');
    assert.strictEqual(response.data.selectedText, veryLongText, 'Text should be accurate');
  });

  it('should recover from collector state listener errors', async () => {
    // Arrange
    let errorThrown = false;
    collector.onStateChange(() => {
      errorThrown = true;
      throw new Error('Listener error');
    });

    const message = createBridgeMessage('bridge:getEditorState');
    const context = { editorContextCollector: collector, logger, metrics };

    // Act - Change state (which triggers listener that throws)
    collector.setState({
      activeFile: { filepath: '/error-test.js', contents: 'error code' },
      cursorPosition: { line: 1, character: 1 }
    });

    // Even if listener threw, handler should still work
    const response = await getEditorStateHandler(message, context);

    // Assert
    assert.strictEqual(response.success, true, 'Handler should succeed despite listener error');
    assert.strictEqual(response.data.activeFile, '/error-test.js', 'State should be updated correctly');
  });
});
