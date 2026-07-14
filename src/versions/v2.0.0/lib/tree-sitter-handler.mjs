#!/usr/bin/env node

/**
 * tree-sitter-handler.mjs
 *
 * Optional handler for AST analysis requests (Step 80).
 *
 * **Message Type**: `bridge:analyzeAST`
 *
 * **Purpose**: Provides bridge interface to tree-sitter parsing and querying.
 * Receives AST analysis requests from Continue and other handlers.
 * Falls back gracefully if tree-sitter unavailable.
 *
 * **Handler Lifecycle**:
 * - Registered conditionally in core-server.mjs if TREE_SITTER_ENABLED feature flag is true
 * - Single instance per bridge session (stateless)
 * - Non-blocking; errors logged at WARN level (non-fatal)
 *
 * **Input Message**:
 * ```javascript
 * {
 *   messageType: "bridge:analyzeAST",
 *   messageId: "<uuid>",
 *   data: {
 *     filepath: "/path/to/file.cs",
 *     code: "<source code>",
 *     language: "csharp",
 *     position?: { line: 10, column: 5 },
 *     queryType?: "functionAtPos" | "classAtPos" | "scope" | "allSymbols"
 *   }
 * }
 * ```
 *
 * **Output Response**:
 * ```javascript
 * {
 *   success: true,
 *   data: {
 *     ast?: Node | null,
 *     symbol?: SymbolInfo | null,
 *     scope?: string,
 *     symbols?: SymbolInfo[]
 *   }
 * }
 * ```
 *
 * **Error Handling**:
 * - Missing filepath/code → success: true, data: null
 * - tree-sitter unavailable → success: true, data: null
 * - Parse error → success: true, data: null (logged at WARN)
 * - Query error → success: true, data: null (logged at WARN)
 *
 * **Integration Points**:
 * - Step 14: HandlerDispatcher (dispatcher routing)
 * - Step 25: BridgeLogger (optional logging)
 * - Step 26: TelemetryCollector (optional metrics)
 * - Step 53: symbol-extractor (optional enhancement)
 * - Step 56: go-to-definition-handler (optional enhancement)
 * - Step 58: code-completion-handler (optional enhancement)
 * - Step 71: Handler registration (conditional registration via feature flag)
 * - Step 80: TreeSitterBridge (core dependency)
 *
 * @module src/versions/v2.0.0/lib/tree-sitter-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 */

import {
  createTreeSitterBridgeLazy,
  TreeSitterInitializationError,
  ParseError,
  QueryError,
} from './tree-sitter-bridge.mjs';

/**
 * Global tree-sitter bridge instance (lazy-loaded on first request).
 * @type {TreeSitterBridge|null}
 */
let globalBridge = null;

/**
 * Ensures bridge is initialized (idempotent).
 *
 * @private
 * @param {Object} context - Handler context with logger and metrics
 * @returns {Promise<TreeSitterBridge|null>} Bridge instance or null if unavailable
 */
async function ensureBridge(context) {
  if (globalBridge) {
    return globalBridge;
  }

  const logger = context?.logger || null;
  const metrics = context?.metrics || null;

  globalBridge = createTreeSitterBridgeLazy({ logger, metrics });

  try {
    await globalBridge.initialize();
  } catch (error) {
    if (error instanceof TreeSitterInitializationError) {
      if (logger?.warn) {
        logger.warn(`[tree-sitter-handler] tree-sitter unavailable: ${error.message}`);
      }
      // Bridge remains available but returns null for queries
      return globalBridge;
    }
    throw error;
  }

  return globalBridge;
}

/**
 * Validate input message data.
 *
 * @private
 * @param {Object} data - Message data
 * @returns {Object|null} Validation error object or null if valid
 */
function validateInput(data) {
  if (!data) {
    return { field: 'data', message: 'data is required' };
  }

  if (!data.filepath || typeof data.filepath !== 'string') {
    return { field: 'filepath', message: 'filepath must be a non-empty string' };
  }

  if (!data.code || typeof data.code !== 'string') {
    return { field: 'code', message: 'code must be a non-empty string' };
  }

  if (!data.language || typeof data.language !== 'string') {
    return { field: 'language', message: 'language must be a non-empty string' };
  }

  if (data.position) {
    if (typeof data.position.line !== 'number' || typeof data.position.column !== 'number') {
      return {
        field: 'position',
        message: 'position.line and position.column must be numbers',
      };
    }
  }

  if (data.queryType) {
    const validTypes = [
      'functionAtPos',
      'classAtPos',
      'scope',
      'allSymbols',
      'symbolType',
    ];
    if (!validTypes.includes(data.queryType)) {
      return {
        field: 'queryType',
        message: `queryType must be one of: ${validTypes.join(', ')}`,
      };
    }
  }

  return null;
}

/**
 * Execute a query on the AST.
 *
 * @private
 * @param {TreeSitterBridge} bridge - Bridge instance
 * @param {Object} data - Query parameters
 * @returns {Promise<Object>} Query result
 */
async function executeQuery(bridge, data) {
  const { filepath, code, language, position, queryType = 'allSymbols' } = data;

  // Parse file to AST
  const tree = await bridge.parseFile(filepath, code, language);
  if (!tree) {
    return { success: true, data: null };
  }

  // Execute query based on type
  switch (queryType) {
    case 'functionAtPos': {
      if (!position) {
        return { error: 'position required for functionAtPos query' };
      }
      const func = bridge.extractFunctionAtPosition(tree, position.line, position.column);
      return {
        success: true,
        data: func
          ? {
              type: 'function',
              node: func,
              nodeType: func.type,
              startPosition: func.startPosition,
              endPosition: func.endPosition,
            }
          : null,
      };
    }

    case 'classAtPos': {
      if (!position) {
        return { error: 'position required for classAtPos query' };
      }
      const cls = bridge.extractClassAtPosition(tree, position.line, position.column);
      return {
        success: true,
        data: cls
          ? {
              type: 'class',
              node: cls,
              nodeType: cls.type,
              startPosition: cls.startPosition,
              endPosition: cls.endPosition,
            }
          : null,
      };
    }

    case 'scope': {
      if (!position) {
        return { error: 'position required for scope query' };
      }
      const scope = bridge.extractScope(tree, position.line, position.column);
      return {
        success: true,
        data: scope ? { scope, position } : null,
      };
    }

    case 'symbolType': {
      if (!data.symbolName) {
        return { error: 'symbolName required for symbolType query' };
      }
      const symbols = bridge.queryBySymbolType(tree, data.symbolName);
      return {
        success: true,
        data: symbols.length > 0 ? { symbols, count: symbols.length } : null,
      };
    }

    case 'allSymbols':
    default: {
      // Return basic AST info
      return {
        success: true,
        data: {
          ast: tree,
          rootNode: tree.rootNode ? { type: tree.rootNode.type } : null,
          language,
        },
      };
    }
  }
}

/**
 * Main handler function.
 *
 * **Message Type**: `bridge:analyzeAST`
 *
 * @param {BridgeMessage} message - Incoming message
 * @param {HandlerContext} context - Handler context (logger, metrics, server)
 * @returns {Promise<HandlerResponse>} Handler response
 *
 * @example
 * // Called by dispatcher when message.messageType === 'bridge:analyzeAST'
 * const response = await handle(message, context);
 * // response = {
 * //   success: true,
 * //   data: { ... }
 * // }
 */
export async function handle(message, context) {
  const startTime = performance.now();
  const logger = context?.logger || null;
  const metrics = context?.metrics || null;

  try {
    // Validate input
    const validation = validateInput(message.data);
    if (validation) {
      if (logger?.warn) {
        logger.warn(
          `[tree-sitter-handler] Validation error: ${validation.field} - ${validation.message}`
        );
      }
      metrics?.record?.('tree_sitter_handler.validation_error', 1);
      return {
        success: false,
        error: `Invalid input: ${validation.field} - ${validation.message}`,
      };
    }

    // Ensure bridge is available
    const bridge = await ensureBridge(context);
    if (!bridge) {
      if (logger?.warn) {
        logger.warn('[tree-sitter-handler] Bridge unavailable');
      }
      metrics?.record?.('tree_sitter_handler.bridge_unavailable', 1);
      return {
        success: true,
        data: null,
      };
    }

    // Execute query
    const result = await executeQuery(bridge, message.data);

    // Record metrics
    const duration = performance.now() - startTime;
    metrics?.record?.('tree_sitter_handler.query_time_ms', duration);
    if (logger?.log) {
      logger.log(
        `[tree-sitter-handler] Query ${message.data.queryType || 'allSymbols'} on ` +
          `${message.data.filepath} completed in ${duration.toFixed(2)}ms`
      );
    }

    return result;
  } catch (error) {
    const duration = performance.now() - startTime;

    if (logger?.warn) {
      logger.warn(
        `[tree-sitter-handler] Error: ${error.name} - ${error.message}`
      );
    }

    metrics?.record?.('tree_sitter_handler.error', 1);
    metrics?.record?.('tree_sitter_handler.error_duration_ms', duration);

    // Return graceful error response
    return {
      success: false,
      error: `AST analysis failed: ${error.message}`,
    };
  }
}

/**
 * Optional handler lifecycle callback (called when handler is registered).
 *
 * @param {Object} context - Handler context
 * @returns {Promise<void>}
 */
export async function onRegister(context) {
  const logger = context?.logger || null;
  if (logger?.log) {
    logger.log('[tree-sitter-handler] Registered and ready to receive bridge:analyzeAST messages');
  }
}

/**
 * Optional handler lifecycle callback (called when handler is unregistered).
 *
 * @param {Object} context - Handler context
 * @returns {Promise<void>}
 */
export async function onUnregister(context) {
  const logger = context?.logger || null;
  if (logger?.log) {
    logger.log('[tree-sitter-handler] Unregistering and disposing resources');
  }
  if (globalBridge) {
    globalBridge.dispose();
    globalBridge = null;
  }
}

/**
 * Reset global bridge (useful for testing).
 *
 * @private
 * @returns {void}
 */
export function _resetBridge() {
  if (globalBridge) {
    globalBridge.dispose();
  }
  globalBridge = null;
}

/**
 * Get global bridge instance (useful for testing).
 *
 * @private
 * @returns {TreeSitterBridge|null}
 */
export function _getBridge() {
  return globalBridge;
}
