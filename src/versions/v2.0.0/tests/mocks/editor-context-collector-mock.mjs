#!/usr/bin/env node

/**
 * Mock EditorContextCollector for Testing
 *
 * Reusable mock implementation of EditorContextCollector for handler tests.
 * Simulates the behavior of the real EditorContextCollector (Step 48)
 * without requiring the full bridge infrastructure.
 *
 * **Use Cases**:
 * - Step 50: getEditorState handler tests
 * - Step 51: onEditorStateChange handler tests
 * - Step 67: Handler tests (editor context)
 * - Step 70: Handler integration tests
 *
 * **Features**:
 * - Configurable initial state
 * - Synchronous state queries (getActiveFile, getSelection, getCursorPosition)
 * - State mutation with setState method
 * - Listener tracking for subscription tests
 * - Snapshot/restore for test isolation
 *
 * @module src/versions/v2.0.0/tests/mocks/editor-context-collector-mock.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 48: editor-context-collector.js (real implementation)
 *   - Step 50: get-editor-state-handler.test.mjs (uses this mock)
 *   - Step 51: onEditorStateChange handler (uses this mock)
 *   - Step 67: handler tests (editor context) (uses this mock)
 *
 * @example
 * // Create mock with default state (no file open)
 * const collector = createEditorContextCollectorMock();
 *
 * // Create mock with initial state
 * const collector = createEditorContextCollectorMock({
 *   activeFile: {
 *     filepath: '/home/user/main.cs',
 *     contents: 'using System;',
 *     cursorLine: 0,
 *     cursorColumn: 6,
 *     language: 'csharp',
 *     projectPath: '/home/user/project',
 *     diagnosticsCount: 0
 *   },
 *   cursorPosition: { line: 0, character: 6 },
 *   selection: { text: 'System', start: 6, end: 12 }
 * });
 *
 * // Query state
 * const activeFile = collector.getActiveFile();
 * const cursor = collector.getCursorPosition();
 * const selection = collector.getSelection();
 *
 * // Update state
 * collector.setState({
 *   activeFile: { ...activeFile, cursorLine: 1, cursorColumn: 0 }
 * });
 *
 * // Subscribe to changes (for Step 51 tests)
 * collector.onStateChange((newState, oldState) => {
 *   console.log('State changed');
 * });
 *
 * // Cleanup
 * collector.dispose();
 */

/**
 * Create a mock EditorContextCollector for testing.
 *
 * @param {Object} [initialState={}] - Initial state override
 * @param {Object} [initialState.activeFile] - ActiveFile object or null
 * @param {Object} [initialState.cursorPosition] - CursorPosition object or null
 * @param {Object} [initialState.selection] - Selection object or null
 *
 * @returns {Object} Mock EditorContextCollector instance
 */
export function createEditorContextCollectorMock(initialState = {}) {
  const defaults = {
    activeFile: null,
    cursorPosition: null,
    selection: null
  };

  const state = { ...defaults, ...initialState };

  return {
    /**
     * @private
     * Internal state storage
     */
    _state: state,

    /**
     * @private
     * Subscription listeners for onStateChange
     */
    _listeners: [],

    /**
     * @private
     * History snapshots for restore() operation
     */
    _history: [],

    /**
     * Get the current active file context (synchronous).
     * @returns {Object|null} ActiveFile or null
     */
    getActiveFile() {
      return this._state.activeFile;
    },

    /**
     * Get the current cursor position (synchronous).
     * @returns {Object|null} CursorPosition or null
     */
    getCursorPosition() {
      return this._state.cursorPosition;
    },

    /**
     * Get the current text selection (synchronous).
     * @returns {Object|null} Selection or null
     */
    getSelection() {
      return this._state.selection;
    },

    /**
     * Update the internal state.
     * @param {Object} newPartialState - Partial state to merge
     */
    setState(newPartialState) {
      const oldState = { ...this._state };
      this._state = { ...this._state, ...newPartialState };

      // Notify all listeners (for Step 51 tests)
      this._listeners.forEach(listener => {
        try {
          listener(this._state, oldState);
        } catch (e) {
          console.error('Listener error:', e);
        }
      });
    },

    /**
     * Register a state change listener.
     * @param {Function} callback - Listener function: (newState, oldState) => void
     */
    onStateChange(callback) {
      if (typeof callback !== 'function') {
        throw new TypeError('callback must be a function');
      }
      this._listeners.push(callback);
    },

    /**
     * Take a snapshot of current state for test isolation.
     * Snapshots can be restored with restore().
     */
    snapshot() {
      this._history.push({ ...this._state });
    },

    /**
     * Restore the most recent snapshot.
     * @throws {Error} If no snapshots available
     */
    restore() {
      if (this._history.length === 0) {
        throw new Error('No snapshots available');
      }
      this._state = this._history.pop();
    },

    /**
     * Clear all snapshots.
     */
    clearSnapshots() {
      this._history = [];
    },

    /**
     * Dispose and cleanup (remove listeners).
     */
    dispose() {
      this._listeners = [];
      this._history = [];
    },

    /**
     * Get internal state (for advanced testing).
     * @private
     * @returns {Object} Current state object
     */
    _getState() {
      return this._state;
    },

    /**
     * Get listener count (for testing listener registration).
     * @private
     * @returns {number} Number of registered listeners
     */
    _getListenerCount() {
      return this._listeners.length;
    }
  };
}

export default createEditorContextCollectorMock;
