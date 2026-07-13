#!/usr/bin/env node

/**
 * Diagnostics Collector
 *
 * Provides a centralized cache for IDE diagnostics (errors, warnings, info) from the C# compiler,
 * code analyzers, and linters. Receives updates from the C# DiagnosticsProvider via message handlers
 * and maintains a queryable, per-file diagnostic cache. Handlers (Steps 54–61) query this collector
 * to avoid repeated RPC roundtrips to the IDE, improving performance and reducing coupling.
 *
 * @module src/versions/v2.0.0/lib/diagnostics-collector.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 52: document-provider.mjs (parallel context collector; similar pattern)
 *   - Step 53: symbol-extractor.mjs (parallel context collection)
 *   - Step 54–61: handlers (consume collector via getDiagnosticsForFile, etc.)
 *   - Step 66: handler-registry.mjs (may wrap collector dependency)
 *   - Step 71: handler-dispatcher.js (registers handlers that depend on collector)
 *   - Step 67: handler tests (editor context) — validates diagnostics functionality
 */

/**
 * Error thrown during diagnostics collector setup or message handler registration.
 *
 * @class DiagnosticsCollectorError
 * @extends {Error}
 *
 * @example
 * try {
 *   collector.registerMessageHandlers(null);
 * } catch (error) {
 *   if (error instanceof DiagnosticsCollectorError) {
 *     console.error(`Setup failed: ${error.message}`);
 *   }
 * }
 */
export class DiagnosticsCollectorError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType] - Type of operation that failed (e.g., 'registration', 'initialization')
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'DiagnosticsCollectorError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Error thrown when incoming diagnostic data is invalid or malformed.
 *
 * @class DiagnosticsValidationError
 * @extends {Error}
 *
 * @example
 * try {
 *   collector.updateDiagnostics('file.cs', diagnostics);
 * } catch (error) {
 *   if (error instanceof DiagnosticsValidationError) {
 *     console.error(`Validation failed: ${error.fieldName} — ${error.message}`);
 *   }
 * }
 */
export class DiagnosticsValidationError extends Error {
  /**
   * @param {string} fieldName - Name of the field that failed validation
   * @param {string} message - Validation failure reason
   * @param {any} [value] - The invalid value (for debugging)
   */
  constructor(fieldName, message, value = null) {
    super(`${fieldName}: ${message}`);
    this.name = 'DiagnosticsValidationError';
    this.fieldName = fieldName;
    this.value = value;
  }
}

/**
 * @typedef {Object} Diagnostic
 * @property {string} code - Diagnostic code (e.g., "CS0001", "IDE0001")
 * @property {string} message - Human-readable diagnostic message
 * @property {string} severity - Diagnostic severity: "error" | "warning" | "info"
 * @property {number} line - 0-based line number
 * @property {number} column - 0-based column/character offset
 * @property {number} [endLine] - 0-based end line (optional)
 * @property {number} [endColumn] - 0-based end column (optional)
 * @property {string} file - Absolute file path containing the diagnostic
 */

/**
 * @typedef {Object} DiagnosticsChangeEvent
 * @property {string} filepath - File path that changed
 * @property {Diagnostic[]} diagnostics - New diagnostics for the file
 * @property {string} changeType - "open" | "update" | "close"
 */

/**
 * DiagnosticsCollector
 *
 * Caches IDE diagnostics per file and provides queryable methods (by severity, by range, etc.).
 * Subscribes to C# IDE messages: didOpenDiagnostics, didUpdateDiagnostics, didCloseDiagnostics.
 * Emits diagnostics change events to registered listeners.
 *
 * @example
 * const collector = new DiagnosticsCollector({
 *   logger: context.logger,
 *   metrics: context.metrics
 * });
 *
 * await collector.registerMessageHandlers(server);
 *
 * // Query diagnostics
 * const fileErrors = collector.getDiagnosticsForFile('src/main.cs', 'error');
 * const allDiags = collector.getAllDiagnostics();
 *
 * // Listen for changes
 * collector.onDiagnosticsChange((event) => {
 *   console.log(`${event.filepath} updated: ${event.diagnostics.length} diagnostics`);
 * });
 *
 * // Cleanup
 * collector.dispose();
 */
export class DiagnosticsCollector {
  /**
   * @param {Object} [options={}] - Configuration options
   * @param {Object} [options.logger] - Logger instance (mocked if not provided)
   * @param {Object} [options.metrics] - Metrics collector instance (mocked if not provided)
   */
  constructor(options = {}) {
    if (typeof options !== 'object' || options === null || Array.isArray(options)) {
      throw new Error('DiagnosticsCollector options must be a plain object');
    }
    this.logger = options.logger || this._createMockLogger();
    this.metrics = options.metrics || this._createMockMetrics();

    /**
     * Map of filepath → Diagnostic[]
     * Stores all diagnostics keyed by file path.
     * @private
     * @type {Map<string, Diagnostic[]>}
     */
    this._diagnostics = new Map();

    /**
     * Array of listener callbacks for diagnostics changes.
     * @private
     * @type {Function[]}
     */
    this._changeListeners = [];

    /**
     * Track last update timestamp for metrics.
     * @private
     * @type {number}
     */
    this._lastUpdate = Date.now();

    this.logger.debug('DiagnosticsCollector initialized');
    this.metrics.recordEvent('diagnostics_collector_initialized', { timestamp: this._lastUpdate });
  }

  /**
   * Register message handlers with the bridge server.
   * Subscribes to: didOpenDiagnostics, didUpdateDiagnostics, didCloseDiagnostics.
   *
   * @async
   * @param {Object} server - Bridge server with messageHandler
   * @param {Object} server.messageHandler - Message handler with on() method
   * @throws {DiagnosticsCollectorError} If server is invalid or registration fails
   * @example
   * await collector.registerMessageHandlers(bridgeServer);
   */
  async registerMessageHandlers(server) {
    if (!server || typeof server !== 'object') {
      throw new DiagnosticsCollectorError('server must be a valid object', 'registration', null);
    }
    if (!server.messageHandler || typeof server.messageHandler.on !== 'function') {
      throw new DiagnosticsCollectorError('server.messageHandler.on() not available', 'registration', null);
    }

    try {
      server.messageHandler.on('didOpenDiagnostics', (message) => this._handleDidOpenDiagnosticsMessage(message));
      server.messageHandler.on('didUpdateDiagnostics', (message) => this._handleDidUpdateDiagnosticsMessage(message));
      server.messageHandler.on('didCloseDiagnostics', (message) => this._handleDidCloseDiagnosticsMessage(message));

      this.logger.debug('DiagnosticsCollector registered for diagnostics messages');
      this.metrics.recordEvent('diagnostics_collector_registered', { timestamp: Date.now() });
    } catch (error) {
      throw new DiagnosticsCollectorError(
        `Failed to register message handlers: ${error.message}`,
        'registration',
        error
      );
    }
  }

  /**
   * Update diagnostics for a file. Validates input and notifies listeners.
   *
   * @param {string} filepath - Absolute file path
   * @param {Diagnostic[]} diagnostics - Array of diagnostic objects
   * @param {string} [changeType='update'] - Type of change: "open" | "update" | "close"
   * @throws {DiagnosticsValidationError} If filepath or diagnostics are invalid
   * @private
   */
  updateDiagnostics(filepath, diagnostics, changeType = 'update') {
    if (!filepath || typeof filepath !== 'string') {
      throw new DiagnosticsValidationError('filepath', 'must be a non-empty string', filepath);
    }
    if (!Array.isArray(diagnostics)) {
      throw new DiagnosticsValidationError('diagnostics', 'must be an array', diagnostics);
    }

    // Validate each diagnostic
    for (let i = 0; i < diagnostics.length; i++) {
      const diag = diagnostics[i];
      this._validateDiagnostic(diag, i);
    }

    // Store diagnostics, sorted by line/column for efficient range queries
    const sortedDiags = this._sortDiagnostics(diagnostics);
    this._diagnostics.set(filepath, sortedDiags);
    this._lastUpdate = Date.now();

    // Notify listeners
    this._notifyChangeListeners({
      filepath,
      diagnostics: sortedDiags,
      changeType
    });

    this.metrics.recordEvent('diagnostics_updated', {
      filepath,
      count: diagnostics.length,
      changeType,
      timestamp: this._lastUpdate
    });
  }

  /**
   * Get diagnostics for a specific file, optionally filtered by severity.
   *
   * @param {string} filepath - Absolute file path
   * @param {string} [severity] - Optional severity filter: "error" | "warning" | "info"
   * @returns {Diagnostic[]} Copy of diagnostics array (empty if file not found)
   * @example
   * const errors = collector.getDiagnosticsForFile('src/main.cs', 'error');
   */
  getDiagnosticsForFile(filepath, severity = null) {
    if (!filepath || typeof filepath !== 'string') {
      return [];
    }

    const diags = this._diagnostics.get(filepath);
    if (!diags) {
      return [];
    }

    // Return copy (avoid external mutations)
    const result = diags.map((d) => ({ ...d }));

    // Filter by severity if specified
    if (severity && ['error', 'warning', 'info'].includes(severity)) {
      return result.filter((d) => d.severity === severity);
    }

    return result;
  }

  /**
   * Get all diagnostics across all files.
   *
   * @returns {Map<string, Diagnostic[]>} Map of filepath → Diagnostic[] (copies)
   * @example
   * const allDiags = collector.getAllDiagnostics();
   * for (const [file, diags] of allDiags.entries()) {
   *   console.log(`${file}: ${diags.length} issues`);
   * }
   */
  getAllDiagnostics() {
    const result = new Map();
    for (const [filepath, diags] of this._diagnostics.entries()) {
      // Store copies to prevent external mutations
      result.set(filepath, diags.map((d) => ({ ...d })));
    }
    return result;
  }

  /**
   * Get diagnostics filtered by severity level.
   *
   * @param {string} severity - Severity level: "error" | "warning" | "info"
   * @returns {Map<string, Diagnostic[]>} Map of filepath → filtered Diagnostic[] (copies)
   * @throws {DiagnosticsValidationError} If severity is invalid
   * @example
   * const errors = collector.getDiagnosticsBySeverity('error');
   */
  getDiagnosticsBySeverity(severity) {
    if (!severity || !['error', 'warning', 'info'].includes(severity)) {
      throw new DiagnosticsValidationError('severity', 'must be "error", "warning", or "info"', severity);
    }

    const result = new Map();
    for (const [filepath, diags] of this._diagnostics.entries()) {
      const filtered = diags.filter((d) => d.severity === severity).map((d) => ({ ...d }));
      if (filtered.length > 0) {
        result.set(filepath, filtered);
      }
    }
    return result;
  }

  /**
   * Get diagnostics that overlap a specific range in a file.
   *
   * @param {string} filepath - Absolute file path
   * @param {number} line - Start line (0-based)
   * @param {number} column - Start column (0-based)
   * @param {number} [endLine] - End line (0-based); defaults to line if not provided
   * @param {number} [endColumn] - End column (0-based); defaults to column + 1 if not provided
   * @returns {Diagnostic[]} Diagnostics overlapping the range (copies)
   * @throws {DiagnosticsValidationError} If line/column are invalid
   * @example
   * // Get diagnostics at cursor position
   * const diags = collector.getDiagnosticsRange('src/main.cs', 10, 5);
   * // Get diagnostics in a selection
   * const selected = collector.getDiagnosticsRange('src/main.cs', 10, 5, 10, 20);
   */
  getDiagnosticsRange(filepath, line, column, endLine = null, endColumn = null) {
    if (!filepath || typeof filepath !== 'string') {
      throw new DiagnosticsValidationError('filepath', 'must be a non-empty string', filepath);
    }
    if (typeof line !== 'number' || line < 0) {
      throw new DiagnosticsValidationError('line', 'must be a non-negative number', line);
    }
    if (typeof column !== 'number' || column < 0) {
      throw new DiagnosticsValidationError('column', 'must be a non-negative number', column);
    }

    const diags = this._diagnostics.get(filepath);
    if (!diags) {
      return [];
    }

    // Normalize range (default to single position)
    const actualEndLine = endLine !== null ? endLine : line;
    const actualEndColumn = endColumn !== null ? endColumn : column + 1;

    // Filter diagnostics that overlap the range
    const overlapping = diags.filter((d) => {
      const dStartLine = d.line;
      const dStartColumn = d.column;
      const dEndLine = d.endLine !== undefined ? d.endLine : d.line;
      const dEndColumn = d.endColumn !== undefined ? d.endColumn : d.column + 1;

      // Check for overlap: diagnostic range must intersect query range
      const noOverlap =
        dEndLine < line || // Diagnostic ends before query starts
        dStartLine > actualEndLine || // Diagnostic starts after query ends
        (dEndLine === line && dEndColumn <= column) || // Diagnostic ends at query start but before column
        (dStartLine === actualEndLine && dStartColumn >= actualEndColumn); // Diagnostic starts at query end or after

      return !noOverlap;
    });

    return overlapping.map((d) => ({ ...d }));
  }

  /**
   * Get the count of diagnostics for a file.
   *
   * @param {string} filepath - Absolute file path
   * @returns {number} Number of diagnostics (0 if file not found)
   * @example
   * const count = collector.getDiagnosticsCount('src/main.cs');
   */
  getDiagnosticsCount(filepath = null) {
    if (filepath) {
      if (typeof filepath !== 'string') {
        return 0;
      }
      return (this._diagnostics.get(filepath) || []).length;
    }

    // Count all diagnostics across all files
    let total = 0;
    for (const diags of this._diagnostics.values()) {
      total += diags.length;
    }
    return total;
  }

  /**
   * Check if a file has any diagnostics.
   *
   * @param {string} filepath - Absolute file path
   * @returns {boolean} True if file has diagnostics
   * @example
   * if (collector.hasDiagnostics('src/main.cs')) {
   *   console.log('File has issues');
   * }
   */
  hasDiagnostics(filepath) {
    if (!filepath || typeof filepath !== 'string') {
      return false;
    }
    return this._diagnostics.has(filepath) && this._diagnostics.get(filepath).length > 0;
  }

  /**
   * Register a listener callback for diagnostics changes.
   * Callback receives a DiagnosticsChangeEvent.
   *
   * @param {Function} callback - Callback function (event) => void
   * @returns {Function} Unsubscribe function
   * @throws {TypeError} If callback is not a function
   * @example
   * const unsubscribe = collector.onDiagnosticsChange((event) => {
   *   console.log(`${event.filepath}: ${event.diagnostics.length} issues`);
   * });
   *
   * // Later, unsubscribe
   * unsubscribe();
   */
  onDiagnosticsChange(callback) {
    if (typeof callback !== 'function') {
      throw new TypeError('onDiagnosticsChange callback must be a function');
    }

    this._changeListeners.push(callback);

    // Return unsubscribe function
    return () => {
      const idx = this._changeListeners.indexOf(callback);
      if (idx >= 0) {
        this._changeListeners.splice(idx, 1);
      }
    };
  }

  /**
   * Dispose of the collector, clearing all caches and listeners.
   *
   * @example
   * collector.dispose();
   */
  dispose() {
    this._diagnostics.clear();
    this._changeListeners = [];
    this.logger.debug('DiagnosticsCollector disposed');
    this.metrics.recordEvent('diagnostics_collector_disposed', { timestamp: Date.now() });
  }

  // ============= Private Methods =============

  /**
   * Validate a single diagnostic object.
   * @private
   */
  _validateDiagnostic(diag, index) {
    if (!diag || typeof diag !== 'object') {
      throw new DiagnosticsValidationError(`diagnostics[${index}]`, 'must be an object', diag);
    }
    if (!diag.code || typeof diag.code !== 'string') {
      throw new DiagnosticsValidationError(`diagnostics[${index}].code`, 'must be a non-empty string', diag.code);
    }
    if (!diag.message || typeof diag.message !== 'string') {
      throw new DiagnosticsValidationError(
        `diagnostics[${index}].message`,
        'must be a non-empty string',
        diag.message
      );
    }
    if (!diag.severity || !['error', 'warning', 'info'].includes(diag.severity)) {
      throw new DiagnosticsValidationError(
        `diagnostics[${index}].severity`,
        'must be "error", "warning", or "info"',
        diag.severity
      );
    }
    if (typeof diag.line !== 'number' || diag.line < 0) {
      throw new DiagnosticsValidationError(`diagnostics[${index}].line`, 'must be a non-negative number', diag.line);
    }
    if (typeof diag.column !== 'number' || diag.column < 0) {
      throw new DiagnosticsValidationError(
        `diagnostics[${index}].column`,
        'must be a non-negative number',
        diag.column
      );
    }
    if (!diag.file || typeof diag.file !== 'string') {
      throw new DiagnosticsValidationError(`diagnostics[${index}].file`, 'must be a non-empty string', diag.file);
    }

    // Optional endLine/endColumn validation
    if (diag.endLine !== undefined && (typeof diag.endLine !== 'number' || diag.endLine < 0)) {
      throw new DiagnosticsValidationError(
        `diagnostics[${index}].endLine`,
        'must be a non-negative number or undefined',
        diag.endLine
      );
    }
    if (diag.endColumn !== undefined && (typeof diag.endColumn !== 'number' || diag.endColumn < 0)) {
      throw new DiagnosticsValidationError(
        `diagnostics[${index}].endColumn`,
        'must be a non-negative number or undefined',
        diag.endColumn
      );
    }
  }

  /**
   * Sort diagnostics by line/column for efficient range queries.
   * @private
   */
  _sortDiagnostics(diagnostics) {
    return [...diagnostics].sort((a, b) => {
      if (a.line !== b.line) {
        return a.line - b.line;
      }
      return a.column - b.column;
    });
  }

  /**
   * Notify all listeners of a diagnostics change.
   * @private
   */
  _notifyChangeListeners(event) {
    for (const listener of this._changeListeners) {
      try {
        listener(event);
      } catch (error) {
        this.logger.error(`Error in diagnostics change listener: ${error.message}`);
        this.metrics.recordEvent('diagnostics_listener_error', {
          error: error.message,
          timestamp: Date.now()
        });
      }
    }
  }

  /**
   * Handle incoming didOpenDiagnostics message.
   * @private
   */
  _handleDidOpenDiagnosticsMessage(message) {
    try {
      if (!message || !message.data) {
        this.logger.error('Invalid didOpenDiagnostics message structure');
        return;
      }

      const { filepath, diagnostics } = message.data;
      this.updateDiagnostics(filepath, diagnostics || [], 'open');
    } catch (error) {
      this.logger.error(`Error handling didOpenDiagnostics: ${error.message}`);
      this.metrics.recordEvent('diagnostics_message_error', {
        messageType: 'didOpenDiagnostics',
        error: error.message,
        timestamp: Date.now()
      });
    }
  }

  /**
   * Handle incoming didUpdateDiagnostics message.
   * @private
   */
  _handleDidUpdateDiagnosticsMessage(message) {
    try {
      if (!message || !message.data) {
        this.logger.error('Invalid didUpdateDiagnostics message structure');
        return;
      }

      const { filepath, diagnostics } = message.data;
      this.updateDiagnostics(filepath, diagnostics || [], 'update');
    } catch (error) {
      this.logger.error(`Error handling didUpdateDiagnostics: ${error.message}`);
      this.metrics.recordEvent('diagnostics_message_error', {
        messageType: 'didUpdateDiagnostics',
        error: error.message,
        timestamp: Date.now()
      });
    }
  }

  /**
   * Handle incoming didCloseDiagnostics message.
   * @private
   */
  _handleDidCloseDiagnosticsMessage(message) {
    try {
      if (!message || !message.data || !message.data.filepath) {
        this.logger.error('Invalid didCloseDiagnostics message structure');
        return;
      }

      const { filepath } = message.data;
      this._diagnostics.delete(filepath);
      this._lastUpdate = Date.now();

      // Notify listeners of close event
      this._notifyChangeListeners({
        filepath,
        diagnostics: [],
        changeType: 'close'
      });

      this.metrics.recordEvent('diagnostics_closed', {
        filepath,
        timestamp: this._lastUpdate
      });
    } catch (error) {
      this.logger.error(`Error handling didCloseDiagnostics: ${error.message}`);
      this.metrics.recordEvent('diagnostics_message_error', {
        messageType: 'didCloseDiagnostics',
        error: error.message,
        timestamp: Date.now()
      });
    }
  }

  /**
   * Create a mock logger (used if none provided).
   * @private
   */
  _createMockLogger() {
    return {
      debug: () => {},
      info: () => {},
      warn: () => {},
      error: () => {}
    };
  }

  /**
   * Create a mock metrics collector (used if none provided).
   * @private
   */
  _createMockMetrics() {
    return {
      recordEvent: () => {}
    };
  }
}
