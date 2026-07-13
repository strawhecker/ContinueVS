#!/usr/bin/env node

/**
 * Selection Tracker
 *
 * Manages fine-grained text selection state within the active editor.
 * Subscribes to "currentFile" messages from EditorContextCollector, extracts selection data,
 * caches normalized selection state, and emits change events for handlers (Step 51+).
 *
 * @module src/versions/v2.0.0/lib/selection-tracker.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 46: webview-bootstrap-handler.mjs (instantiates & registers SelectionTracker)
 *   - Step 48: editor-context-collector.js (emits "currentFile" messages via messageHandler)
 *   - Step 51: onEditorStateChange-handler.mjs (consumes SelectionTracker.onSelectionChange())
 *   - Step 67: handler tests (editor context) — validates SelectionTracker integration
 *   - Step 60+: Various handlers may query SelectionTracker.getSelection()
 *
 * Usage Example:
 *
 *   // Step 46: Initialize during bridge bootstrap
 *   const tracker = new SelectionTracker({ logger, metrics });
 *   await tracker.registerMessageHandlers(server);
 *
 *   // Step 51: Subscribe to selection changes
 *   tracker.onSelectionChange((newSelection, oldSelection) => {
 *     console.log(`Selection changed: ${newSelection?.text || '(cleared)'}`);
 *   });
 *
 *   // Step 60+: Query selection in handlers
 *   if (tracker.hasSelection()) {
 *     const range = tracker.getSelectedRange();
 *     console.log(`User selected lines ${range.startLine}–${range.endLine}`);
 *   }
 */

/**
 * Error thrown during selection tracker setup or message handler registration.
 *
 * @class SelectionTrackerError
 * @extends {Error}
 *
 * @example
 * try {
 *   await tracker.registerMessageHandlers(null);
 * } catch (error) {
 *   if (error instanceof SelectionTrackerError) {
 *     console.error(`Registration failed: ${error.message}`);
 *   }
 * }
 */
export class SelectionTrackerError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType] - Type of operation that failed (e.g., 'registration', 'initialization')
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'SelectionTrackerError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when incoming selection data is invalid or malformed.
 *
 * @class StateValidationError
 * @extends {Error}
 *
 * @example
 * try {
 *   tracker.updateSelection({}, { line: 5 }, 'text');
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
 * @property {CursorPosition} start - Selection start position (anchor)
 * @property {CursorPosition} end - Selection end position (focus)
 * @property {string} text - Selected text content (as received from IDE)
 * @property {boolean} isMultiline - Whether selection spans multiple lines
 */

/**
 * @typedef {Object} SelectionRange
 * @property {number} startLine - Start line number (0-based)
 * @property {number} endLine - End line number (0-based)
 * @property {number} startChar - Start character offset (0-based)
 * @property {number} endChar - End character offset (0-based)
 */

/**
 * @typedef {Object} SelectionChange
 * @property {Selection} newSelection - New selection state
 * @property {Selection|null} oldSelection - Previous selection state (null if first change)
 * @property {string} timestamp - ISO timestamp of change
 */

/**
 * Centralized cache and manager for IDE text selection state.
 *
 * Receives selection data from EditorContextCollector via "currentFile" messages.
 * Normalizes and caches the state, exposes synchronous query methods for handlers
 * to determine if text is selected and its properties, and manages subscription callbacks
 * for listeners (onEditorStateChange — Step 51).
 *
 * **Thread-safe**: Single-threaded Node.js event loop; all mutations happen in message handlers.
 *
 * @class SelectionTracker
 * @example
 * // Step 46: During bridge initialization
 * const tracker = new SelectionTracker({ logger, metrics });
 * await tracker.registerMessageHandlers(server);
 *
 * // Step 51: In onEditorStateChange handler
 * tracker.onSelectionChange((newSel, oldSel) => {
 *   if (newSel && oldSel && newSel.text !== oldSel.text) {
 *     console.log(`Selection changed from "${oldSel.text}" to "${newSel.text}"`);
 *   }
 * });
 *
 * // Step 60+: In refactor or code action handlers
 * if (tracker.isMultilineSelection()) {
 *   const range = tracker.getSelectedRange();
 *   console.log(`Action applies to lines ${range.startLine}–${range.endLine}`);
 * }
 */
export class SelectionTracker {
  /**
   * Create a selection tracker.
   *
   * @param {Object} [options={}] - Configuration options
   * @param {Object} [options.logger=null] - Optional logger instance (e.g., from BridgeLogger)
   * @param {Object} [options.metrics=null] - Optional metrics collector instance
   * @param {Function} [options.logger.debug] - Logger.debug(msg, ...args)
   * @param {Function} [options.logger.error] - Logger.error(msg, ...args)
   * @param {Function} [options.metrics.recordEvent] - Metrics.recordEvent(eventName, data)
   *
   * @example
   * const tracker = new SelectionTracker({
   *   logger: bridgeLogger,
   *   metrics: telemetryCollector
   * });
   *
   * const trackerMinimal = new SelectionTracker(); // Use defaults (silent)
   */
  constructor(options = {}) {
    this.logger = options.logger || {
      debug: () => {},
      error: () => {}
    };

    this.metrics = options.metrics || {
      recordEvent: () => {}
    };

    /**
     * Current selection state.
     * @private
     * @type {{ selection: Selection|null, lastUpdate: string }}
     */
    this._state = {
      selection: null,
      lastUpdate: new Date().toISOString()
    };

    /**
     * Array of subscription callbacks.
     * @private
     * @type {Function[]}
     */
    this._listeners = [];

    this.logger.debug('SelectionTracker initialized');
    this.metrics.recordEvent('selection_tracker_initialized', { timestamp: Date.now() });
  }

  /**
   * Register message handlers with the bridge server.
   *
   * Subscribes to "currentFile" messages emitted by EditorContextCollector.
   * Each message contains selection data that is extracted and processed.
   *
   * **Important**: Must be called after the server is fully initialized.
   *
   * @async
   * @param {Object} server - CoreServer instance (or compatible message broker)
   * @param {Object} server.messageHandler - Message handler instance
   * @param {Function} server.messageHandler.on - Subscribe to message type: (type, callback) => void
   * @returns {Promise<void>}
   * @throws {SelectionTrackerError} If server is invalid or messageHandler unavailable
   *
   * @example
   * try {
   *   await tracker.registerMessageHandlers(server);
   *   console.log('SelectionTracker listening for messages');
   * } catch (error) {
   *   console.error(`Failed to register: ${error.message}`);
   * }
   */
  async registerMessageHandlers(server) {
    if (!server || typeof server !== 'object') {
      throw new SelectionTrackerError(
        'server must be a valid object',
        'registration',
        null
      );
    }

    if (!server.messageHandler || typeof server.messageHandler.on !== 'function') {
      throw new SelectionTrackerError(
        'server.messageHandler.on() not available',
        'registration',
        null
      );
    }

    try {
      server.messageHandler.on('currentFile', (message) => {
        this._handleSelectionMessage(message);
      });

      this.logger.debug('SelectionTracker registered for "currentFile" messages');
      this.metrics.recordEvent('selection_tracker_registered', { timestamp: Date.now() });
    } catch (error) {
      throw new SelectionTrackerError(
        `Failed to register message handlers: ${error.message}`,
        'registration',
        error
      );
    }
  }

  /**
   * Update the current selection state.
   *
   * Normalizes incoming position data, validates the selection range,
   * and updates internal state. Only emits change event if selection actually changed.
   *
   * @param {CursorPosition} start - Selection start position { line, character }
   * @param {CursorPosition} end - Selection end position { line, character }
   * @param {string} text - Selected text content
   * @returns {void}
   * @throws {StateValidationError} If any parameter is invalid or malformed
   *
   * @example
   * try {
   *   tracker.updateSelection(
   *     { line: 0, character: 0 },
   *     { line: 0, character: 5 },
   *     'hello'
   *   );
   * } catch (error) {
   *   console.error(`Selection update failed: ${error.message}`);
   * }
   */
  updateSelection(start, end, text) {
    // Validate start position
    if (!start || typeof start !== 'object' || !('line' in start) || !('character' in start)) {
      throw new StateValidationError('start', 'must be an object with line and character properties', start);
    }
    if (typeof start.line !== 'number' || typeof start.character !== 'number') {
      throw new StateValidationError('start', 'line and character must be numbers', start);
    }
    if (start.line < 0 || start.character < 0) {
      throw new StateValidationError('start', 'line and character must be non-negative', start);
    }

    // Validate end position
    if (!end || typeof end !== 'object' || !('line' in end) || !('character' in end)) {
      throw new StateValidationError('end', 'must be an object with line and character properties', end);
    }
    if (typeof end.line !== 'number' || typeof end.character !== 'number') {
      throw new StateValidationError('end', 'line and character must be numbers', end);
    }
    if (end.line < 0 || end.character < 0) {
      throw new StateValidationError('end', 'line and character must be non-negative', end);
    }

    // Validate text
    if (typeof text !== 'string') {
      throw new StateValidationError('text', 'must be a string', text);
    }

    // Calculate isMultiline
    const isMultiline = start.line !== end.line;

    // Build new selection object
    const newSelection = {
      start: { line: start.line, character: start.character },
      end: { line: end.line, character: end.character },
      text,
      isMultiline
    };

    // Store old selection for change notification
    const oldSelection = this._state.selection;

    // Check if selection actually changed (to avoid spurious events)
    if (this._selectionsEqual(oldSelection, newSelection)) {
      return;
    }

    // Update state
    this._state.selection = newSelection;
    this._state.lastUpdate = new Date().toISOString();

    this.logger.debug(`Selection updated: isMultiline=${isMultiline}, length=${text.length}`);
    this.metrics.recordEvent('selection_updated', {
      isMultiline,
      textLength: text.length,
      timestamp: Date.now()
    });

    // Notify listeners
    this._notifyListeners(newSelection, oldSelection);
  }

  /**
   * Get the current selection state (synchronous).
   *
   * Returns a copy of the current selection or null if no selection.
   *
   * @returns {Selection|null} Current selection or null if no selection
   *
   * @example
   * const selection = tracker.getSelection();
   * if (selection) {
   *   console.log(`Selected: "${selection.text}" (multiline: ${selection.isMultiline})`);
   * }
   */
  getSelection() {
    if (!this._state.selection) return null;
    return { ...this._state.selection };
  }

  /**
   * Check if there is an active selection (boolean).
   *
   * Fast check without returning the full selection object.
   *
   * @returns {boolean} True if selection exists, false otherwise
   *
   * @example
   * if (tracker.hasSelection()) {
   *   console.log('Text is currently selected');
   * }
   */
  hasSelection() {
    return this._state.selection !== null;
  }

  /**
   * Check if the current selection spans multiple lines.
   *
   * Returns false if no selection.
   *
   * @returns {boolean} True if selection is multiline, false otherwise
   *
   * @example
   * if (tracker.isMultilineSelection()) {
   *   console.log('User selected multiple lines');
   * }
   */
  isMultilineSelection() {
    if (!this._state.selection) return false;
    return this._state.selection.isMultiline;
  }

  /**
   * Get the selection as a structured range (line and character bounds).
   *
   * Useful for handlers that work with document ranges.
   * Returns null if no selection.
   *
   * @returns {SelectionRange|null} Range object or null if no selection
   *
   * @example
   * const range = tracker.getSelectedRange();
   * if (range) {
   *   console.log(`Selected: lines ${range.startLine}–${range.endLine}`);
   *   console.log(`  Start column: ${range.startChar}, End column: ${range.endChar}`);
   * }
   */
  getSelectedRange() {
    if (!this._state.selection) return null;
    const { start, end } = this._state.selection;
    return {
      startLine: start.line,
      startChar: start.character,
      endLine: end.line,
      endChar: end.character
    };
  }

  /**
   * Get the length (character count) of the selected text.
   *
   * Returns 0 if no selection or empty selection.
   *
   * @returns {number} Number of characters selected
   *
   * @example
   * const len = tracker.getSelectionLength();
   * console.log(`User selected ${len} characters`);
   */
  getSelectionLength() {
    if (!this._state.selection) return 0;
    return this._state.selection.text.length;
  }

  /**
   * Register a callback listener for selection changes.
   *
   * The callback is invoked whenever the selection state is updated.
   * Receives both new and old selection for diffing.
   *
   * Multiple listeners can be registered; all are invoked on selection change.
   *
   * @param {Function} callback - Listener function: (newSelection, oldSelection) => void
   * @returns {void}
   * @throws {TypeError} If callback is not a function
   *
   * @example
   * tracker.onSelectionChange((newSel, oldSel) => {
   *   console.log(`Selection changed at ${newSel.timestamp || 'unknown time'}`);
   *   if (newSel && oldSel && newSel.isMultiline !== oldSel.isMultiline) {
   *     console.log(`Multiline flag changed: ${oldSel.isMultiline} → ${newSel.isMultiline}`);
   *   }
   * });
   */
  onSelectionChange(callback) {
    if (typeof callback !== 'function') {
      throw new TypeError('onSelectionChange callback must be a function');
    }
    this._listeners.push(callback);
  }

  /**
   * Clean up and remove all subscriptions.
   *
   * Clears all listeners and resets selection state. Call this during bridge shutdown
   * (Step 45 lifecycle manager) or test cleanup.
   *
   * @returns {void}
   *
   * @example
   * // During bridge shutdown
   * tracker.dispose();
   * // Tracker is still usable; re-register listeners if needed
   */
  dispose() {
    this._listeners = [];
    this._state.selection = null;
    this._state.lastUpdate = new Date().toISOString();
    this.logger.debug('SelectionTracker disposed (all listeners removed)');
  }

  /**
   * @private
   * Handle "currentFile" message from EditorContextCollector.
   *
   * Extracts selection data from message and updates state.
   * Gracefully handles malformed messages (logs error, does not throw).
   *
   * @param {Object} message - Message object from messageHandler
   * @param {Object} message.data - Message payload
   * @param {CursorPosition} [message.data.selection.start] - Selection start position
   * @param {CursorPosition} [message.data.selection.end] - Selection end position
   * @param {string} [message.data.selection.text] - Selected text
   * @returns {void}
   */
  _handleSelectionMessage(message) {
    if (!message || !message.data) {
      this.logger.debug('Received invalid "currentFile" message (no data); ignoring');
      return;
    }

    const { selection } = message.data;

    // No selection data — skip gracefully
    if (!selection) {
      // Treat as "clear selection"
      if (this._state.selection !== null) {
        const oldSelection = this._state.selection;
        this._state.selection = null;
        this._state.lastUpdate = new Date().toISOString();
        this._notifyListeners(null, oldSelection);
      }
      return;
    }

    // Extract selection fields
    const { start, end, text } = selection;

    try {
      this.updateSelection(start, end, text);
    } catch (error) {
      this.logger.error(`Failed to update selection: ${error.message}`);
      this.metrics.recordEvent('selection_update_failed', {
        error: error.message,
        fieldName: error.fieldName || 'unknown'
      });
    }
  }

  /**
   * @private
   * Notify all registered listeners of selection change.
   *
   * Wraps each callback invocation in try-catch to prevent cascade failures.
   *
   * @param {Selection|null} newSelection - New selection state
   * @param {Selection|null} oldSelection - Previous selection state
   * @returns {void}
   */
  _notifyListeners(newSelection, oldSelection) {
    for (const callback of this._listeners) {
      try {
        callback(newSelection, oldSelection);
      } catch (error) {
        this.logger.error(`Selection listener callback failed: ${error.message}`);
      }
    }
  }

  /**
   * @private
   * Deep equality check for selection objects.
   *
   * Returns true if both selections have identical start, end, and text.
   *
   * @param {Selection|null} a - First selection
   * @param {Selection|null} b - Second selection
   * @returns {boolean}
   */
  _selectionsEqual(a, b) {
    if (a === null && b === null) return true;
    if (a === null || b === null) return false;
    return (
      a.start.line === b.start.line &&
      a.start.character === b.start.character &&
      a.end.line === b.end.line &&
      a.end.character === b.end.character &&
      a.text === b.text
    );
  }
}

export default SelectionTracker;
