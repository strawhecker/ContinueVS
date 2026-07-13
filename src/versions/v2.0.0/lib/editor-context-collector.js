#!/usr/bin/env node

/**
 * Editor Context Collector
 *
 * Provides a centralized cache for IDE editor state (active file, cursor position, selection).
 * Receives updates from the C# EditorContextProvider via message handlers and normalizes
 * them into a queryable state object. Handlers (Steps 50–51) query this collector instead
 * of re-fetching from the IDE, improving performance and reducing coupling.
 *
 * @module src/versions/v2.0.0/lib/editor-context-collector.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (wrapped service; message routing)
 *   - Step 46: webview-bootstrap-handler.js (initializes bridge; may call registerMessageHandlers)
 *   - Step 49: selection-tracker.js (parallel step; similar message subscription pattern)
 *   - Step 50: getEditorState handler (consumes collector.getActiveFile())
 *   - Step 51: onEditorStateChange handler (subscribes via collector.onStateChange())
 *   - Step 67: handler tests (editor context) — validates collector functionality
 *   - Step 71: handler registration — registers handlers that depend on collector
 */

/**
 * Error thrown during editor context collector setup or message handler registration.
 *
 * @class EditorContextError
 * @extends {Error}
 *
 * @example
 * try {
 *   collector.registerMessageHandlers(null);
 * } catch (error) {
 *   if (error instanceof EditorContextError) {
 *     console.error(`Setup failed: ${error.message}`);
 *   }
 * }
 */
export class EditorContextError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType] - Type of operation that failed (e.g., 'registration', 'initialization')
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'EditorContextError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when incoming editor state data is invalid or malformed.
 *
 * @class StateValidationError
 * @extends {Error}
 *
 * @example
 * try {
 *   collector.updateFileContext('', 'contents', {line: 0, character: 0});
 * } catch (error) {
 *   if (error instanceof StateValidationError) {
 *     console.error(`Validation failed: ${error.fieldName} — ${error.message}`);
 *   }
 * }
 */
export class StateValidationError extends Error {
  /**
   * @param {string} fieldName - Name of the field that failed validation
   * @param {string} message - Validation failure reason
   * @param {any} [value] - The invalid value (for debugging)
   */
  constructor(fieldName, message, value = null) {
    super(`${fieldName}: ${message}`);
    this.name = 'StateValidationError';
    this.fieldName = fieldName;
    this.value = value;
  }
}

/**
 * @typedef {Object} CursorPosition
 * @property {number} line - 0-based line number
 * @property {number} character - 0-based column/character offset
 */

/**
 * @typedef {Object} Selection
 * @property {CursorPosition} start - Selection start position
 * @property {CursorPosition} end - Selection end position
 * @property {string} text - Selected text content
 */

/**
 * @typedef {Object} ActiveFile
 * @property {string} filepath - Absolute file path
 * @property {string} contents - Full file contents
 * @property {number} cursorLine - 0-based cursor line (from cursorPosition)
 * @property {number} cursorColumn - 0-based cursor column (from cursorPosition)
 */

/**
 * @typedef {Object} EditorState
 * @property {ActiveFile|null} activeFile - Current active file and cursor position, or null if no file open
 * @property {Selection|null} selection - Current text selection, or null if no selection
 * @property {string} lastUpdate - ISO timestamp of last state mutation
 */

/**
 * Centralized cache and manager for IDE editor context state.
 *
 * Receives editor state updates from the C# EditorContextProvider via message handlers
 * ("currentFile", "didChangeActiveTextEditor"). Normalizes and caches the state,
 * exposes synchronous getters for handlers to query without fetching from IDE,
 * and manages subscription callbacks for listeners (onEditorStateChange).
 *
 * **Thread-safe**: Single-threaded Node.js event loop; all mutations happen in message handlers.
 *
 * @class EditorContextCollector
 * @example
 * // Step 46: During bridge initialization
 * const collector = new EditorContextCollector({ logger, metrics });
 * await collector.registerMessageHandlers(server);
 *
 * // Step 50: In getEditorState handler
 * const activeFile = collector.getActiveFile();
 * if (activeFile) {
 *   return {
 *     filepath: activeFile.filepath,
 *     cursorPosition: { line: activeFile.cursorLine, character: activeFile.cursorColumn }
 *   };
 * }
 *
 * // Step 51: In onEditorStateChange subscription
 * collector.onStateChange((newState, oldState) => {
 *   console.log(`Editor changed: ${newState.activeFile?.filepath || 'no file'}`);
 * });
 */
export class EditorContextCollector {
  /**
   * Create an editor context collector.
   *
   * @param {Object} [options={}] - Configuration options
   * @param {Object} [options.logger=null] - Optional logger instance (e.g., from context)
   * @param {Object} [options.metrics=null] - Optional metrics collector instance
   * @param {Function} [options.logger.debug] - Logger.debug(msg, ...args)
   * @param {Function} [options.logger.error] - Logger.error(msg, ...args)
   * @param {Function} [options.metrics.recordEvent] - Metrics.recordEvent(eventName, data)
   *
   * @throws {Error} If options is not a plain object
   */
  constructor(options = {}) {
    if (typeof options !== 'object' || options === null || Array.isArray(options)) {
      throw new Error('EditorContextCollector options must be a plain object');
    }

    this.logger = options.logger || this._createMockLogger();
    this.metrics = options.metrics || this._createMockMetrics();

    /**
     * @private
     * @type {EditorState}
     */
    this._state = {
      activeFile: null,
      selection: null,
      lastUpdate: new Date().toISOString()
    };

    /**
     * @private
     * @type {Function[]}
     */
    this._listeners = [];

    this.logger.debug('EditorContextCollector initialized');
  }

  /**
   * Register message handlers for "currentFile" and "didChangeActiveTextEditor" messages.
   *
   * Must be called after bridge initialization (Step 46) to start receiving editor state updates.
   * Subscribes the collector to IDE messages via server.messageHandler.
   *
   * @async
   * @param {Object} server - Bridge server instance with messageHandler
   * @param {Object} server.messageHandler - Message handler registry
   * @param {Function} server.messageHandler.on - Register listener: on(messageType, callback)
   *
   * @returns {Promise<void>}
   *
   * @throws {EditorContextError} If server or messageHandler is invalid
   *
   * @example
   * await collector.registerMessageHandlers(bridgeServer);
   * // Now collector receives "currentFile" and "didChangeActiveTextEditor" updates
   */
  async registerMessageHandlers(server) {
    if (!server || typeof server !== 'object') {
      throw new EditorContextError(
        'server must be a valid object',
        'registration',
        null
      );
    }

    if (!server.messageHandler || typeof server.messageHandler.on !== 'function') {
      throw new EditorContextError(
        'server.messageHandler.on() not available',
        'registration',
        null
      );
    }

    try {
      server.messageHandler.on('currentFile', (message) => {
        this._handleCurrentFileMessage(message);
      });

      server.messageHandler.on('didChangeActiveTextEditor', (message) => {
        this._handleDidChangeActiveTextEditorMessage(message);
      });

      this.logger.debug('EditorContextCollector registered for "currentFile" and "didChangeActiveTextEditor" messages');
      this.metrics.recordEvent('editor_context_collector_registered', { timestamp: Date.now() });
    } catch (error) {
      throw new EditorContextError(
        `Failed to register message handlers: ${error.message}`,
        'registration',
        error
      );
    }
  }

  /**
   * Update the active file context (called when IDE sends "currentFile" message).
   *
   * Normalizes incoming data and updates internal state. Invokes state change listeners.
   *
   * @param {string} filepath - Absolute file path
   * @param {string} contents - Full file contents
   * @param {CursorPosition} cursorPosition - Cursor position {line, character}
   *
   * @throws {StateValidationError} If any parameter is invalid
   *
   * @example
   * try {
   *   collector.updateFileContext(
   *     'C:\\src\\Main.cs',
   *     'using System;...',
   *     { line: 42, character: 10 }
   *   );
   * } catch (error) {
   *   console.error(`Update failed: ${error.fieldName}`);
   * }
   */
  updateFileContext(filepath, contents, cursorPosition) {
    this._validateFileContext(filepath, contents, cursorPosition);

    const oldState = { ...this._state };

    this._state.activeFile = {
      filepath,
      contents,
      cursorLine: cursorPosition.line,
      cursorColumn: cursorPosition.character
    };

    this._state.lastUpdate = new Date().toISOString();

    this.logger.debug(`Editor context updated: ${filepath} @ line ${cursorPosition.line}`);
    this._notifyListeners(this._state, oldState);
  }

  /**
   * Update the active editor filepath (lightweight update; called when IDE sends "didChangeActiveTextEditor").
   *
   * Updates only the filepath of activeFile; preserves existing contents and cursor position
   * until the next "currentFile" message arrives.
   *
   * @param {string} filepath - Absolute file path
   *
   * @throws {StateValidationError} If filepath is invalid
   *
   * @example
   * collector.updateActiveEditor('C:\\src\\NewFile.cs');
   */
  updateActiveEditor(filepath) {
    if (typeof filepath !== 'string' || filepath.trim() === '') {
      throw new StateValidationError('filepath', 'must be a non-empty string', filepath);
    }

    const oldState = { ...this._state };

    if (this._state.activeFile) {
      this._state.activeFile.filepath = filepath;
    } else {
      this._state.activeFile = {
        filepath,
        contents: '',
        cursorLine: 0,
        cursorColumn: 0
      };
    }

    this._state.lastUpdate = new Date().toISOString();

    this.logger.debug(`Active editor changed: ${filepath}`);
    this._notifyListeners(this._state, oldState);
  }

  /**
   * Get the current active file context (synchronous).
   *
   * Returns the cached active file object or null if no file is open.
   *
   * @returns {ActiveFile|null} Current active file or null
   *
   * @example
   * const activeFile = collector.getActiveFile();
   * if (activeFile) {
   *   console.log(`File: ${activeFile.filepath}`);
   *   console.log(`Cursor: line ${activeFile.cursorLine}, column ${activeFile.cursorColumn}`);
   * }
   */
  getActiveFile() {
    return this._state.activeFile;
  }

  /**
   * Get the current cursor position (synchronous).
   *
   * Returns the cursor position from the cached active file, or null if no file open.
   *
   * @returns {CursorPosition|null} Current cursor position or null
   *
   * @example
   * const cursor = collector.getCursorPosition();
   * if (cursor) {
   *   console.log(`Cursor at ${cursor.line}:${cursor.character}`);
   * }
   */
  getCursorPosition() {
    if (!this._state.activeFile) return null;
    return {
      line: this._state.activeFile.cursorLine,
      character: this._state.activeFile.cursorColumn
    };
  }

  /**
   * Get the current text selection (synchronous).
   *
   * Returns the cached selection object or null if no selection.
   *
   * @returns {Selection|null} Current selection or null
   *
   * @example
   * const selection = collector.getSelection();
   * if (selection) {
   *   console.log(`Selected: "${selection.text}"`);
   * }
   */
  getSelection() {
    return this._state.selection;
  }

  /**
   * Register a callback listener for editor state changes.
   *
   * The callback is invoked whenever editor state is updated (file, cursor, selection).
   * Receives both new and old state for diffing.
   *
   * Multiple listeners can be registered; all are invoked on state change.
   *
   * @param {Function} callback - Listener function: (newState, oldState) => void
   * @returns {void}
   *
   * @throws {TypeError} If callback is not a function
   *
   * @example
   * collector.onStateChange((newState, oldState) => {
   *   console.log(`State changed at ${newState.lastUpdate}`);
   *   if (newState.activeFile?.filepath !== oldState.activeFile?.filepath) {
   *     console.log('Active file changed');
   *   }
   * });
   */
  onStateChange(callback) {
    if (typeof callback !== 'function') {
      throw new TypeError('onStateChange callback must be a function');
    }
    this._listeners.push(callback);
  }

  /**
   * Clean up and remove all subscriptions (for test cleanup and graceful shutdown).
   *
   * Clears the listener list. Subsequent state changes do not trigger callbacks.
   * Call this during bridge shutdown (Step 45 lifecycle manager) or test cleanup.
   *
   * @returns {void}
   *
   * @example
   * // During bridge shutdown
   * collector.dispose();
   * // Collector is still usable; re-register listeners if needed
   */
  dispose() {
    this._listeners = [];
    this.logger.debug('EditorContextCollector disposed (all listeners removed)');
  }

  /**
   * @private
   * Handle "currentFile" message from IDE.
   *
   * @param {Object} message - Message object
   * @param {string} message.data.filepath - File path
   * @param {string} message.data.contents - File contents
   * @param {Object} message.data.cursorPosition - Cursor position {line, character}
   * @returns {void}
   */
  _handleCurrentFileMessage(message) {
    if (!message || !message.data) {
      this.logger.error('Received invalid "currentFile" message (no data)');
      return;
    }

    const { filepath, contents, cursorPosition } = message.data;

    try {
      this.updateFileContext(filepath, contents, cursorPosition);
    } catch (error) {
      this.logger.error(`Failed to update file context: ${error.message}`);
      this.metrics.recordEvent('editor_context_update_failed', { error: error.message });
    }
  }

  /**
   * @private
   * Handle "didChangeActiveTextEditor" message from IDE.
   *
   * @param {Object} message - Message object
   * @param {string} message.data.filepath - New active file path
   * @returns {void}
   */
  _handleDidChangeActiveTextEditorMessage(message) {
    if (!message || !message.data || !message.data.filepath) {
      this.logger.error('Received invalid "didChangeActiveTextEditor" message (no filepath)');
      return;
    }

    const { filepath } = message.data;

    try {
      this.updateActiveEditor(filepath);
    } catch (error) {
      this.logger.error(`Failed to update active editor: ${error.message}`);
      this.metrics.recordEvent('editor_context_update_failed', { error: error.message });
    }
  }

  /**
   * @private
   * Validate file context parameters before updating state.
   *
   * @param {string} filepath - File path
   * @param {string} contents - File contents
   * @param {CursorPosition} cursorPosition - Cursor position
   * @throws {StateValidationError} If any parameter is invalid
   * @returns {void}
   */
  _validateFileContext(filepath, contents, cursorPosition) {
    if (typeof filepath !== 'string' || filepath.trim() === '') {
      throw new StateValidationError('filepath', 'must be a non-empty string', filepath);
    }

    if (typeof contents !== 'string') {
      throw new StateValidationError('contents', 'must be a string', typeof contents);
    }

    if (!cursorPosition || typeof cursorPosition !== 'object') {
      throw new StateValidationError('cursorPosition', 'must be an object', cursorPosition);
    }

    if (typeof cursorPosition.line !== 'number' || cursorPosition.line < 0) {
      throw new StateValidationError('cursorPosition.line', 'must be a non-negative number', cursorPosition.line);
    }

    if (typeof cursorPosition.character !== 'number' || cursorPosition.character < 0) {
      throw new StateValidationError('cursorPosition.character', 'must be a non-negative number', cursorPosition.character);
    }
  }

  /**
   * @private
   * Invoke all registered state change listeners.
   *
   * @param {EditorState} newState - New state
   * @param {EditorState} oldState - Previous state
   * @returns {void}
   */
  _notifyListeners(newState, oldState) {
    for (const listener of this._listeners) {
      try {
        listener(newState, oldState);
      } catch (error) {
        this.logger.error(`State change listener threw error: ${error.message}`);
      }
    }
  }

  /**
   * @private
   * Create a mock logger if none provided.
   *
   * @returns {Object} Mock logger
   */
  _createMockLogger() {
    return {
      debug: () => {},
      error: () => {},
      info: () => {}
    };
  }

  /**
   * @private
   * Create a mock metrics collector if none provided.
   *
   * @returns {Object} Mock metrics
   */
  _createMockMetrics() {
    return {
      recordEvent: () => {}
    };
  }
}
