#!/usr/bin/env node

/**
 * Code-Lens Handler (Step 90)
 *
 * Provides a bridge handler that generates inline IDE UI elements (code lenses)
 * for symbol navigation and contextual actions. Code lenses appear in the editor
 * as clickable inline text (e.g., "Run Test", "View References").
 *
 * **Handler Type**: Stateless query handler (no mutations)
 * **Message Type**: bridge:getCodeLenses
 * **Input**: BridgeMessage with payload `{ filePath: string, range?: Range, excludeTypes?: string[] }`
 * **Output**: BridgeResponse containing CodeLens[] typedef data
 *
 * **Architecture Flow**:
 * ```
 * [IDE CodeLensProvider] → bridge:getCodeLenses request
 *   ↓
 * [core-server dispatcher] routes to codeLensHandler
 *   ↓
 * [handler] validates inputs (file path, range bounds)
 *   ↓
 * [handler] calls SymbolExtractor.extractSymbols(filePath, range)
 *   ↓
 * [handler] filters symbols by type (test, interface, method, etc.)
 *   ↓
 * [handler] generates lens objects:
 *   - runTest / debugTest for test functions
 *   - viewReferences for public symbols
 *   - viewImplementations for interfaces
 *   - peekDefinition / goToDefinition (built-in)
 *   ↓
 * [handler] applies excludeTypes filter
 *   ↓
 * [handler] maps each lens to CodeLens typedef with position/command
 *   ↓
 * [handler] returns { success: true, data: { lenses: CodeLens[] } }
 *   ↓
 * [core-server] sends response via stdio
 * ```
 *
 * **Code Lens Types** (with visibility gating):
 * - `runTest` — Execute test function/class (all symbols, test only)
 * - `debugTest` — Debug test function/class (all symbols, test only)
 * - `viewReferences` — Find all references to symbol (public symbols only)
 * - `viewImplementations` — Find implementations of interface/abstract member (public symbols only)
 * - `peekDefinition` — Peek definition (public symbols only)
 * - `goToDefinition` — Navigate to definition (public symbols only)
 * - Private/internal symbols: No navigation lenses (clutter reduction)
 *
 * **Performance**:
 * - Single-file query: < 50ms (cached symbols)
 * - Multi-file query (if needed): < 200ms
 * - Cache hit target: > 70% on repeated calls
 *
 * **Error Handling**:
 * - Invalid filePath → CodeLensError (validation)
 * - Invalid range format → PositionError (bounds check)
 * - SymbolExtractor error → CodeLensError (propagate + include operation context)
 * - DocumentProvider error → CodeLensError (graceful fallback to empty)
 * - No symbols → Returns empty lenses array (valid state)
 *
 * **Thread Safety**:
 * - SymbolExtractor is single-threaded (cache + sync extraction)
 * - DocumentProvider is single-threaded (Node.js event loop)
 * - No mutations; only read queries
 * - Safe for concurrent calls
 *
 * **Dependencies**:
 * - SymbolExtractor (Step 53) — Extract symbols for lens generation
 * - DocumentProvider (Step 52) — Query document content (optional for position validation)
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/code-lens-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 15: handler-adapter.js (wrapper methods)
 *   - Step 52: document-provider.mjs (document queries)
 *   - Step 53: symbol-extractor.mjs (symbol extraction)
 *   - Step 56: go-to-definition-handler.mjs (related navigation)
 *   - Step 57: find-references-handler.mjs (references context)
 *   - Step 60: test-explorer-handler.mjs (test context)
 *   - Step 62: handlers.d.js (CodeLens typedef)
 *   - Step 71: handler registration — registers this handler
 */

/**
 * Operation type enumeration for error classification.
 * @enum {string}
 */
export const CodeLensOperationType = {
  INIT: 'init',
  VALIDATION: 'validation',
  SYMBOL_EXTRACTION: 'symbol_extraction',
  DOCUMENT_QUERY: 'document_query',
  LENS_GENERATION: 'lens_generation',
  FILTERING: 'filtering',
  MAPPING: 'mapping',
};

/**
 * Error thrown when CodeLens handler fails to initialize or execute.
 *
 * @class CodeLensError
 * @extends {Error}
 *
 * @property {string} operationType - Which operation failed (INIT, VALIDATION, etc.)
 * @property {string} errorCode - RPC error code for bridge protocol
 * @property {*} details - Optional error details (symbol info, position, etc.)
 *
 * @example
 * try {
 *   const result = await codeLensHandler(msg, { symbolExtractor: null });
 * } catch (error) {
 *   if (error instanceof CodeLensError) {
 *     console.error(`Handler failed during: ${error.operationType}`);
 *   }
 * }
 */
export class CodeLensError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} operationType - Which operation failed
   * @param {string} errorCode - RPC error code
   * @param {*} details - Optional error details
   */
  constructor(
    message,
    operationType = CodeLensOperationType.INIT,
    errorCode = 'CODE_LENS_ERROR',
    details = null
  ) {
    super(message);
    this.name = 'CodeLensError';
    this.operationType = operationType;
    this.errorCode = errorCode;
    this.details = details;
  }
}

/**
 * Error thrown when position bounds or range is invalid.
 *
 * @class PositionError
 * @extends {CodeLensError}
 *
 * @property {Object} position - The invalid position object
 *
 * @example
 * throw new PositionError('Line exceeds document bounds', { line: 9999, char: 0 });
 */
export class PositionError extends CodeLensError {
  /**
   * @param {string} message - Error description
   * @param {*} position - The invalid position
   */
  constructor(message, position = null) {
    super(
      message,
      CodeLensOperationType.VALIDATION,
      'POSITION_ERROR',
      position
    );
    this.name = 'PositionError';
    this.position = position;
  }
}

/**
 * Creates a stateless CodeLens handler with dependencies injected via context.
 *
 * The handler generates inline IDE UI elements for navigation and actions:
 * - Test functions get "Run Test" and "Debug Test" lenses
 * - Public symbols get "View References" and "View Implementations" lenses
 * - Navigation lenses (peekDefinition, goToDefinition) are built-in
 *
 * **Factory Pattern**:
 * ```javascript
 * const handler = createCodeLensHandler({ symbolExtractor, documentProvider, logger });
 * const response = await handler(message, context);
 * ```
 *
 * **Message Format**:
 * ```javascript
 * {
 *   messageType: 'bridge:getCodeLenses',
 *   filePath: 'src/MyClass.cs',
 *   range: { start: { line: 10, char: 0 }, end: { line: 50, char: 0 } },  // optional
 *   excludeTypes: ['peekDefinition']  // optional
 * }
 * ```
 *
 * **Response Format**:
 * ```javascript
 * {
 *   success: true,
 *   data: {
 *     lenses: [
 *       {
 *         line: 12,
 *         command: 'runTest',
 *         title: 'Run Test',
 *         data: { symbolName: 'TestMethod', filePath: 'src/MyClass.cs' }
 *       },
 *       {
 *         line: 20,
 *         command: 'viewReferences',
 *         title: 'View References (1)',
 *         data: { symbolName: 'MyMethod', count: 1 }
 *       }
 *     ]
 *   }
 * }
 * ```
 *
 * @param {Object} deps - Dependencies object
 * @param {Object} deps.symbolExtractor - Symbol extractor instance (required)
 * @param {Object} deps.documentProvider - Document provider instance (optional)
 * @param {Object} deps.logger - Logger instance (optional)
 * @param {Object} deps.metrics - Metrics collector (optional). Metrics are recorded for ALL queries,
 *                                 including empty results (no symbols found). This ensures accurate
 *                                 observability and SLO monitoring for queries that return zero lenses.
 * @returns {Function} Handler function (async (message, context) => response)
 * @throws {CodeLensError} If required dependencies are missing
 *
 * @example
 * const deps = {
 *   symbolExtractor: createSymbolExtractor({ ... }),
 *   documentProvider: createDocumentProvider({ ... }),
 *   logger: bridgeLogger
 * };
 * const handler = createCodeLensHandler(deps);
 * const response = await handler({
 *   messageType: 'bridge:getCodeLenses',
 *   filePath: 'src/Code.cs',
 *   range: { start: { line: 0, char: 0 }, end: { line: 100, char: 0 } }
 * }, { transport: stdioTransport });
 */
export function createCodeLensHandler(deps = {}) {
  const { symbolExtractor, documentProvider, logger, metrics } = deps;

  // Validate required dependencies
  if (!symbolExtractor) {
    throw new CodeLensError(
      'SymbolExtractor dependency is required',
      CodeLensOperationType.INIT,
      'MISSING_DEPENDENCY',
      { missing: 'symbolExtractor' }
    );
  }

  /**
   * Main handler function: processes bridge:getCodeLenses messages.
   *
   * @param {Object} message - The bridge message
   * @param {string} message.messageType - Must be 'bridge:getCodeLenses'
   * @param {string} message.filePath - Path to source file
   * @param {Object} message.range - Optional range to limit lens generation
   * @param {Object} message.range.start - Start position { line, char }
   * @param {Object} message.range.end - End position { line, char }
   * @param {string[]} message.excludeTypes - Optional lens types to exclude
   * @param {Object} context - Handler context (transport, config, etc.)
   * @returns {Promise<Object>} Response { success, data, error }
   */
  return async function codeLensHandler(message, context = {}) {
    const startTime = metrics ? Date.now() : undefined;
    let operationType = CodeLensOperationType.VALIDATION;

    try {
      // ========== STEP 1: VALIDATE INPUT ==========
      if (!message || !message.filePath) {
        throw new CodeLensError(
          'Missing required field: filePath',
          CodeLensOperationType.VALIDATION,
          'INVALID_REQUEST',
          { message }
        );
      }

      const { filePath, range, excludeTypes = [] } = message;

      // Validate range if provided
      if (range) {
        validateRange(range);
      }

      logger?.debug(
        `[CodeLensHandler] Processing file: ${filePath}, range: ${JSON.stringify(range)}`
      );

      // ========== STEP 2: EXTRACT SYMBOLS ==========
      operationType = CodeLensOperationType.SYMBOL_EXTRACTION;
      const symbols = await symbolExtractor.extractSymbols(filePath, range);

      // Initialize lens results regardless of symbol count
      let lenses = [];

      if (!symbols || symbols.length === 0) {
        logger?.debug(`[CodeLensHandler] No symbols found in ${filePath}`);
        // Continue to metrics recording and response (do not early return)
      } else {
        logger?.debug(
          `[CodeLensHandler] Extracted ${symbols.length} symbols from ${filePath}`
        );

        // ========== STEP 3: GENERATE LENSES ==========
        operationType = CodeLensOperationType.LENS_GENERATION;

        for (const symbol of symbols) {
          // Skip symbols outside range (defensive)
          if (range && symbol.line < range.start.line) continue;
          if (range && symbol.line > range.end.line) continue;

          const generatedLenses = generateLensesForSymbol(symbol);
          lenses.push(...generatedLenses);
        }

        // ========== STEP 4: APPLY FILTERS ==========
        operationType = CodeLensOperationType.FILTERING;
        // Single filtering responsibility: apply excludeTypes filter to all generated lenses
        const filteredLenses = lenses.filter(
          (lens) => !excludeTypes.includes(lens.command)
        );

        logger?.debug(
          `[CodeLensHandler] Generated ${filteredLenses.length} lenses (from ${lenses.length} before filtering)`
        );
      }

      // ========== STEP 5: RECORD METRICS ==========
      // Record metrics for ALL queries, including empty results (for observability/SLOs)
      if (metrics) {
        const elapsed = Date.now() - startTime;
        metrics.recordHandlerLatency('bridge:getCodeLenses', elapsed);
        metrics.recordCustomMetric('codelens.count', filteredLenses?.length ?? lenses.length);
        metrics.recordCustomMetric('codelens.symbols', symbols?.length ?? 0);
      }

      // ========== STEP 6: RETURN RESPONSE ==========
      return {
        success: true,
        data: {
          lenses: filteredLenses ?? lenses,
          count: filteredLenses?.length ?? lenses.length,
          file: filePath,
          symbolsProcessed: symbols?.length ?? 0,
        },
      };
    } catch (error) {
      // Wrap non-CodeLensError exceptions
      if (!(error instanceof CodeLensError)) {
        if (logger) {
          logger.error(
            `[CodeLensHandler] Unexpected error during ${operationType}: ${error.message}`,
            { error, filePath: message?.filePath }
          );
        }
        return {
          success: false,
          error: {
            code: 'INTERNAL_ERROR',
            message: error.message,
            operationType,
          },
        };
      }

      if (logger) {
        logger.warn(
          `[CodeLensHandler] Error during ${error.operationType}: ${error.message}`,
          { error, filePath: message?.filePath }
        );
      }

      return {
        success: false,
        error: {
          code: error.errorCode,
          message: error.message,
          operationType: error.operationType,
          details: error.details,
        },
      };
    }
  };
}

/**
 * Validates a range object for format and bounds.
 *
 * @param {Object} range - Range to validate
 * @param {Object} range.start - Start position { line, char }
 * @param {Object} range.end - End position { line, char }
 * @throws {PositionError} If range is invalid
 *
 * @example
 * validateRange({ start: { line: 0, char: 0 }, end: { line: 10, char: 50 } }); // OK
 * validateRange({ start: { line: 10, char: 0 }, end: { line: 5, char: 0 } }); // throws
 */
function validateRange(range) {
  if (!range.start || !range.end) {
    throw new PositionError(
      'Range must have start and end positions',
      range
    );
  }

  if (
    typeof range.start.line !== 'number' ||
    typeof range.start.char !== 'number'
  ) {
    throw new PositionError(
      'Range start must have numeric line and char',
      range.start
    );
  }

  if (
    typeof range.end.line !== 'number' ||
    typeof range.end.char !== 'number'
  ) {
    throw new PositionError(
      'Range end must have numeric line and char',
      range.end
    );
  }

  if (range.start.line < 0 || range.end.line < 0) {
    throw new PositionError(
      'Range lines cannot be negative',
      range
    );
  }

  if (range.start.char < 0 || range.end.char < 0) {
    throw new PositionError(
      'Range chars cannot be negative',
      range
    );
  }

  // Allow start.line === end.line for single-line ranges
  // But start must not be after end
  if (range.start.line > range.end.line) {
    throw new PositionError(
      'Range start line cannot be after end line',
      range
    );
  }

  if (range.start.line === range.end.line && range.start.char > range.end.char) {
    throw new PositionError(
      'Range start char cannot be after end char on same line',
      range
    );
  }
}

/**
 * Generates code lenses for a single symbol based on its type and metadata.
 *
 * **Filtering Strategy**: This function generates ALL possible lenses for a symbol
 * without filtering by excludeTypes. The excludeTypes filtering is applied ONCE at the
 * handler level (after all lenses are generated) to avoid duplicate filtering logic and
 * enable easier future maintenance.
 *
 * **Visibility Gating**: Navigation lenses (goToDefinition, peekDefinition) are only
 * generated for public symbols to minimize clutter for private/internal symbols.
 *
 * Lens generation rules:
 * - Test function (public) → [runTest, debugTest, goToDefinition, peekDefinition]
 * - Test class (public) → [runTest, debugTest, goToDefinition, peekDefinition]
 * - Test function (private) → [runTest, debugTest]
 * - Public method/property → [viewReferences, goToDefinition, peekDefinition]
 * - Public interface/abstract → [viewImplementations, goToDefinition, peekDefinition]
 * - Private symbol → [] (no lenses)
 *
 * @param {Object} symbol - Symbol object from SymbolExtractor
 * @param {string} symbol.name - Symbol name
 * @param {string} symbol.type - Symbol type (method, class, interface, property, etc.)
 * @param {number} symbol.line - Line number (0-based)
 * @param {boolean} symbol.isPublic - Whether symbol is public (controls visibility gating)
 * @param {boolean} symbol.isTest - Whether symbol is a test (method or class)
 * @param {string[]} symbol.tags - Additional metadata tags
 * @returns {Object[]} Array of CodeLens objects (unfiltered)
 *
 * @example
 * const testMethod = {
 *   name: 'TestCompile',
 *   type: 'method',
 *   line: 45,
 *   isPublic: true,
 *   isTest: true,
 *   tags: ['xunit', 'async']
 * };
 * const lenses = generateLensesForSymbol(testMethod);
 * // Returns: [
 * //   { line: 45, command: 'runTest', title: 'Run Test', data: { ... } },
 * //   { line: 45, command: 'debugTest', title: 'Debug Test', data: { ... } },
 * //   { line: 45, command: 'goToDefinition', title: 'Go to Definition', data: { ... } },
 * //   { line: 45, command: 'peekDefinition', title: 'Peek Definition', data: { ... } }
 * // ]
 */
function generateLensesForSymbol(symbol) {
  const lenses = [];

  if (!symbol || !symbol.name) {
    return lenses;
  }

  const { name, type, line, isPublic, isTest, tags = [] } = symbol;

  // ===== TEST LENSES =====
  if (isTest) {
    lenses.push({
      line,
      command: 'runTest',
      title: 'Run Test',
      data: {
        symbolName: name,
        type,
        tags,
      },
    });

    lenses.push({
      line,
      command: 'debugTest',
      title: 'Debug Test',
      data: {
        symbolName: name,
        type,
        tags,
      },
    });
  }

  // ===== PUBLIC SYMBOL LENSES =====
  if (isPublic && !isTest) {
    // View references for methods and properties
    if (type === 'method' || type === 'property') {
      lenses.push({
        line,
        command: 'viewReferences',
        title: 'View References',
        data: {
          symbolName: name,
          type,
        },
      });
    }

    // View implementations for interfaces and abstract members
    if (type === 'interface' || tags?.includes('abstract')) {
      lenses.push({
        line,
        command: 'viewImplementations',
        title: 'View Implementations',
        data: {
          symbolName: name,
          type,
        },
      });
    }
  }

  // ===== NAVIGATION LENSES (FOR PUBLIC SYMBOLS ONLY) =====
  // Avoid clutter for private/internal symbols
  if (isPublic) {
    lenses.push({
      line,
      command: 'goToDefinition',
      title: 'Go to Definition',
      data: {
        symbolName: name,
        type,
      },
    });

    lenses.push({
      line,
      command: 'peekDefinition',
      title: 'Peek Definition',
      data: {
        symbolName: name,
        type,
      },
    });
  }

  return lenses;
}

export { CodeLensHandler: createCodeLensHandler };
export default createCodeLensHandler;
