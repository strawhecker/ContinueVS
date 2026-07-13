#!/usr/bin/env node

/**
 * Code-Completion Handler (Step 58)
 *
 * Provides a bridge handler that queries the DocumentProvider (Step 52) and
 * SymbolExtractor (Step 53) to generate completion suggestions at a cursor position.
 *
 * **Handler Type**: Stateless query handler (no mutations)
 * **Message Type**: bridge:getCompletion
 * **Input**: BridgeMessage with payload `{ file: string, line: number, column: number }`
 * **Output**: BridgeResponse containing CompletionItem[] typedef data
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:getCompletion request { file, line, column }
 *   ↓
 * [core-server dispatcher] routes to codeCompletionHandler
 *   ↓
 * [handler] validates inputs (types, bounds)
 *   ↓
 * [handler] queries DocumentProvider for active document
 *   ↓
 * [handler] calls SymbolExtractor.extractSymbols(file, line, column)
 *   ↓
 * [handler] filters symbols by accessibility (local, imported, keywords)
 *   ↓
 * [handler] ranks by relevance (position-based, type-aware, alphabetical)
 *   ↓
 * [handler] maps each symbol to CompletionItem typedef
 *   ↓
 * [handler] returns { success: true, data: CompletionItem[] }
 *   ↓
 * [core-server] sends response back via stdio
 * ```
 *
 * **Error Handling**:
 * - Invalid input types → CodeCompletionError (validation)
 * - Null DocumentProvider → CodeCompletionError (init)
 * - Missing document → Returns empty CompletionItem[] (graceful)
 * - SymbolExtractor error → Returns partial results with available symbols
 * - No symbols at position → Returns empty CompletionItem[] (valid state)
 *
 * **Thread Safety**:
 * - DocumentProvider is single-threaded (Node.js event loop)
 * - SymbolExtractor is single-threaded (cache + sync extraction)
 * - No mutations here; only queries
 * - Safe for concurrent calls
 *
 * **Dependencies**:
 * - DocumentProvider (Step 52) — injected via context
 * - SymbolExtractor (Step 53) — injected via context
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/code-completion-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 15: handler-adapter.js (wrapper/convenience methods)
 *   - Step 52: document-provider.mjs (document queries)
 *   - Step 53: symbol-extractor.mjs (symbol extraction + filtering)
 *   - Step 62: handlers.d.js (CompletionItem typedef)
 *   - Step 69: handler tests (code completion) — tests this handler
 *   - Step 71: handler registration — registers this handler
 */

/**
 * Operation type enumeration for error classification.
 * @enum {string}
 */
export const CodeCompletionOperationType = {
  INIT: 'init',
  DOCUMENT_QUERY: 'document_query',
  SYMBOL_EXTRACTION: 'symbol_extraction',
  FILTERING: 'filtering',
  VALIDATION: 'validation',
  MAPPING: 'mapping',
};

/**
 * Error thrown when CodeCompletion handler fails to initialize or execute.
 *
 * @class CodeCompletionError
 * @extends {Error}
 *
 * @example
 * try {
 *   const result = await codeCompletionHandler(msg, { documentProvider: null });
 * } catch (error) {
 *   if (error instanceof CodeCompletionError) {
 *     console.error(`Handler failed during: ${error.operationType}`);
 *   }
 * }
 */
export class CodeCompletionError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType='unknown'] - Type of operation that failed (see CodeCompletionOperationType)
   * @param {Error} [originalError=null] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'CodeCompletionError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Create the Code-Completion Handler
 *
 * Factory function that creates an async handler for the `bridge:getCompletion` message.
 * The handler queries DocumentProvider for the active document, extracts symbols at the
 * cursor position, filters and ranks them, then maps to CompletionItem[] format.
 *
 * @param {Object} dispatcher - The handler dispatcher (required for context)
 * @param {Object} [options={}] - Configuration options
 * @param {Object} [options.logger=null] - Logger instance (optional)
 * @param {Object} [options.metrics=null] - Metrics collector (optional)
 * @returns {Function} Async handler function (message, context) => Promise<{ success, data?, error? }>
 *
 * @throws {CodeCompletionError} If dispatcher is null/invalid
 *
 * @example
 * const handler = createCodeCompletionHandler(dispatcher, { logger, metrics });
 * dispatcher.register('bridge:getCompletion', handler);
 */
export function createCodeCompletionHandler(dispatcher, options = {}) {
  // Validate dispatcher
  if (!dispatcher || typeof dispatcher !== 'object') {
    throw new CodeCompletionError(
      'dispatcher must be a valid object',
      CodeCompletionOperationType.INIT
    );
  }

  const logger = options.logger || _createMockLogger();
  const metrics = options.metrics || _createMockMetrics();

  logger.debug('CodeCompletionHandler created');

  /**
   * The actual handler function for bridge:getCompletion
   *
   * @param {Object} message - Bridge message
   * @param {string} message.messageType - "bridge:getCompletion"
   * @param {string} message.messageId - Correlation UUID
   * @param {Object} message.data - Payload { file, line, column }
   * @param {Object} context - Dispatch context
   * @param {Object} context.documentProvider - DocumentProvider instance
   * @param {Object} context.symbolExtractor - SymbolExtractor instance
   * @returns {Promise<Object>} { success: boolean, data?: CompletionItem[], error?: string }
   */
  return async function codeCompletionHandler(message, context) {
    const startTime = Date.now();
    const messageId = message?.messageId || 'unknown';

    try {
      // Validate input
      const payload = message?.data || {};
      _validateInputs(payload);

      const { file, line, column } = payload;

      // Get DocumentProvider and SymbolExtractor from context
      const { documentProvider, symbolExtractor } = context || {};

      if (!documentProvider) {
        throw new CodeCompletionError(
          'DocumentProvider not available in context',
          CodeCompletionOperationType.INIT
        );
      }

      if (!symbolExtractor) {
        throw new CodeCompletionError(
          'SymbolExtractor not available in context',
          CodeCompletionOperationType.INIT
        );
      }

      // Query document (graceful if not found)
      let document = null;
      try {
        document = documentProvider.getDocument(file);
      } catch (err) {
        logger.warn(`Failed to get document ${file}: ${err.message}`);
        metrics.recordEvent('completion_document_query_error', {
          file,
          error: err.message,
        });
      }

      // If no document available, return empty completions (valid state)
      if (!document) {
        logger.debug(`No document found for ${file}, returning empty completions`);
        metrics.recordEvent('completion_no_document', { file });
        return {
          success: true,
          data: [],
        };
      }

      // Extract symbols at cursor position
      let symbols = [];
      try {
        symbols = await symbolExtractor.extractSymbols(file, {
          line,
          column,
          includeKeywords: true,
          maxResults: 200,
        });
      } catch (err) {
        logger.warn(
          `Symbol extraction failed for ${file}:${line}:${column}: ${err.message}`
        );
        metrics.recordEvent('completion_symbol_extraction_error', {
          file,
          line,
          column,
          error: err.message,
        });
        // Gracefully continue with empty symbols
      }

      // Filter symbols by accessibility scope
      let filtered = _filterSymbolsByScope(symbols, { line, column }, document);

      // Rank by relevance
      filtered = _rankSymbolsByRelevance(filtered, line);

      // Map to CompletionItem[]
      const completionItems = filtered.map((symbol) =>
        _mapSymbolToCompletionItem(symbol, document)
      );

      metrics.recordEvent('completion_success', {
        file,
        line,
        column,
        resultCount: completionItems.length,
        latencyMs: Date.now() - startTime,
      });

      logger.debug(
        `Code completion for ${file}:${line}:${column} returned ${completionItems.length} items`
      );

      return {
        success: true,
        data: completionItems,
      };
    } catch (error) {
      const latencyMs = Date.now() - startTime;
      const errorMessage = error?.message || 'Unknown error';
      const operationType = error?.operationType || 'unknown';

      logger.error(
        `CodeCompletionHandler failed: ${errorMessage} (${operationType})`
      );
      metrics.recordEvent('completion_handler_error', {
        messageId,
        operationType,
        error: errorMessage,
        latencyMs,
      });

      return {
        success: false,
        error: errorMessage,
      };
    }
  };
}

/**
 * Validate input payload types and bounds.
 *
 * @param {Object} payload - Input payload { file, line, column }
 * @throws {CodeCompletionError} If validation fails
 */
function _validateInputs(payload) {
  if (!payload || typeof payload !== 'object') {
    throw new CodeCompletionError(
      'payload must be a plain object',
      CodeCompletionOperationType.VALIDATION
    );
  }

  const { file, line, column } = payload;

  if (!file || typeof file !== 'string') {
    throw new CodeCompletionError(
      'file must be a non-empty string',
      CodeCompletionOperationType.VALIDATION
    );
  }

  if (typeof line !== 'number' || line < 0) {
    throw new CodeCompletionError(
      'line must be a non-negative number',
      CodeCompletionOperationType.VALIDATION
    );
  }

  if (typeof column !== 'number' || column < 0) {
    throw new CodeCompletionError(
      'column must be a non-negative number',
      CodeCompletionOperationType.VALIDATION
    );
  }
}

/**
 * Filter symbols by accessibility scope at the cursor position.
 *
 * Filters include:
 * - Local symbols (same scope as cursor)
 * - Imported symbols (from other modules/files)
 * - Keywords (language-specific keywords)
 *
 * Excludes:
 * - Private symbols (not accessible from cursor scope)
 * - Symbols from inaccessible scopes
 *
 * @param {Object[]} symbols - Array of symbols from SymbolExtractor
 * @param {Object} cursorPos - Cursor position { line, column }
 * @param {Object} document - Document metadata
 * @returns {Object[]} Filtered symbols
 */
function _filterSymbolsByScope(symbols, cursorPos, document) {
  if (!Array.isArray(symbols)) {
    return [];
  }

  // For now, return all symbols (accessibility filtering is document-type-specific)
  // In a full implementation, would filter by:
  // - Language (JS/TS/C#/etc.)
  // - Scope rules (public/private, import visibility)
  // - Variable lifetime (not yet declared)
  return symbols.filter((symbol) => {
    // Exclude private symbols
    if (symbol.kind === 'Private' || symbol.isPrivate) {
      return false;
    }

    // Include locals, imports, and keywords
    return true;
  });
}

/**
 * Rank symbols by relevance to the cursor position.
 *
 * Ranking criteria (in order):
 * 1. Distance from cursor (symbols closer to cursor position rank higher)
 * 2. Type (locals > imports > keywords)
 * 3. Frequency of use in document
 * 4. Alphabetical (for equal scores)
 *
 * @param {Object[]} symbols - Array of filtered symbols
 * @param {number} cursorLine - Current line number (0-based)
 * @returns {Object[]} Ranked symbols (highest relevance first)
 */
function _rankSymbolsByRelevance(symbols, cursorLine) {
  if (!Array.isArray(symbols)) {
    return [];
  }

  // Compute relevance score for each symbol
  const scored = symbols.map((symbol) => {
    let score = 0;

    // Distance score: closer symbols rank higher
    // Max 1000 points for symbols on same line, decreases with distance
    const lineDistance = Math.abs((symbol.line || 0) - cursorLine);
    score += Math.max(0, 1000 - lineDistance * 10);

    // Type score: locals > imports > keywords
    if (symbol.kind === 'Local' || symbol.kind === 'Variable') {
      score += 500;
    } else if (symbol.kind === 'Import' || symbol.kind === 'Import Alias') {
      score += 300;
    } else if (symbol.kind === 'Keyword') {
      score += 100;
    } else {
      score += 200;
    }

    // Frequency score: symbols used multiple times rank higher
    const frequency = (symbol.frequency || 1) * 50;
    score += frequency;

    return { symbol, score };
  });

  // Sort by score (descending), then alphabetically
  return scored
    .sort((a, b) => {
      if (b.score !== a.score) {
        return b.score - a.score;
      }
      return (a.symbol.name || '').localeCompare(b.symbol.name || '');
    })
    .map((item) => item.symbol);
}

/**
 * Map a symbol to CompletionItem format.
 *
 * Converts internal symbol representation to the CompletionItem typedef:
 * ```
 * @typedef {Object} CompletionItem
 * @property {string} label - Display text
 * @property {string} kind - Completion kind (Class, Method, Property, etc.)
 * @property {string} [detail] - Additional detail (type, signature)
 * @property {string} [documentation] - Docstring/comment
 * @property {string} [insertText] - Text to insert on selection
 * @property {number} [sortText] - Sort priority
 * ```
 *
 * @param {Object} symbol - Symbol from SymbolExtractor
 * @param {Object} document - Document metadata (for type info)
 * @returns {Object} CompletionItem
 */
function _mapSymbolToCompletionItem(symbol, document) {
  const item = {
    label: symbol.name || '',
    kind: _mapSymbolKind(symbol.kind),
  };

  // Add optional fields
  if (symbol.detail) {
    item.detail = symbol.detail;
  }

  if (symbol.documentation) {
    item.documentation = symbol.documentation;
  }

  if (symbol.insertText) {
    item.insertText = symbol.insertText;
  } else {
    // Default: use the symbol name as insertText
    item.insertText = symbol.name || '';
  }

  // Sort text for consistent ordering in completions menu
  // Use kind as primary sort, name as secondary
  item.sortText = `${_mapSymbolKind(symbol.kind)}_${symbol.name || ''}`;

  return item;
}

/**
 * Map internal symbol kind to CompletionItem kind string.
 *
 * Symbols kinds are mapped from SymbolExtractor kinds (Class, Method, Property, etc.)
 * to CompletionItem kinds for UI display.
 *
 * @param {string} symbolKind - Symbol kind from SymbolExtractor
 * @returns {string} CompletionItem kind for UI
 */
function _mapSymbolKind(symbolKind) {
  const kindMap = {
    Class: 'Class',
    Interface: 'Interface',
    Struct: 'Struct',
    Enum: 'Enum',
    Method: 'Method',
    Function: 'Function',
    Property: 'Property',
    Field: 'Field',
    Variable: 'Variable',
    Local: 'Variable',
    Keyword: 'Keyword',
    Constant: 'Constant',
    Module: 'Module',
    Namespace: 'Module',
    Package: 'Module',
    Import: 'Module',
    'Import Alias': 'Module',
    Operator: 'Operator',
  };

  return kindMap[symbolKind] || 'Text';
}

/**
 * Create a mock logger for testing/fallback.
 * @returns {Object}
 */
function _createMockLogger() {
  return {
    debug: () => {},
    info: () => {},
    warn: () => {},
    error: () => {},
  };
}

/**
 * Create a mock metrics collector for testing/fallback.
 * @returns {Object}
 */
function _createMockMetrics() {
  return {
    recordEvent: () => {},
    recordLatency: () => {},
  };
}

export default createCodeCompletionHandler;
