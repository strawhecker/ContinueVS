#!/usr/bin/env node

/**
 * Get Editor State Handler (Step 50)
 *
 * Provides a bridge handler that queries the EditorContextCollector (Step 48)
 * and returns the current editor state snapshot as an EditorState response.
 *
 * **Handler Type**: Stateless query handler (no mutations)
 * **Message Type**: bridge:getEditorState
 * **Input**: BridgeMessage (no parameters required)
 * **Output**: BridgeResponse containing EditorState typedef data
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:getEditorState request
 *   ↓
 * [core-server dispatcher] routes to getEditorStateHandler
 *   ↓
 * [handler] queries EditorContextCollector for cached state
 *   ↓
 * [collector] returns { activeFile, selection, lastUpdate }
 *   ↓
 * [handler] assembles EditorState typedef and wraps in BridgeResponse
 *   ↓
 * [core-server] sends response back via stdio
 * ```
 *
 * **Error Handling**:
 * - Null/undefined collector → GetEditorStateError (operation: 'init')
 * - Missing active file → Returns state with activeFile: null (valid state)
 * - Null selection → Returns state with selectedText: '' (valid state)
 * - Missing fileContent → Gracefully returns available fields
 *
 * **Thread Safety**:
 * - EditorContextCollector is single-threaded (Node.js event loop)
 * - No mutations here; only queries
 * - Safe for concurrent calls
 *
 * **Dependencies**:
 * - EditorContextCollector (Step 48) — injected via context
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/get-editor-state-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 47: message-routing-middleware.js (middleware integration)
 *   - Step 48: editor-context-collector.js (state source)
 *   - Step 49: selection-tracker.js (tracker parallel implementation)
 *   - Step 51: onEditorStateChange-handler.js (subscription variant)
 *   - Step 62: handlers.d.js (EditorState typedef)
 *   - Step 67: handler tests (editor context) — tests this handler
 *   - Step 71: handler registration — registers this handler
 */

/**
 * Error thrown when GetEditorState handler fails to initialize or execute.
 *
 * @class GetEditorStateError
 * @extends {Error}
 *
 * @example
 * try {
 *   const result = await getEditorStateHandler(msg, { editorContextCollector: null });
 * } catch (error) {
 *   if (error instanceof GetEditorStateError) {
 *     console.error(`Handler failed: ${error.operationType}`);
 *   }
 * }
 */
export class GetEditorStateError extends Error {
  /**
   * @param {string} message - Error description
   * @param {string} [operationType='unknown'] - Type of operation that failed ('init', 'query', 'validation')
   * @param {Error} [originalError] - Original error (if wrapping)
   */
  constructor(message, operationType = 'unknown', originalError = null) {
    super(message);
    this.name = 'GetEditorStateError';
    this.operationType = operationType;
    this.originalError = originalError;
  }
}

/**
 * Get the current editor state (Step 50 handler implementation).
 *
 * Async handler that queries the EditorContextCollector for active file context,
 * cursor position, selection, and synthesizes a complete EditorState response.
 *
 * **Behavior**:
 * 1. Validates context and collector availability
 * 2. Queries collector.getActiveFile() for file path and cursor
 * 3. Queries collector.getSelection() for selected text range
 * 4. Queries collector.getCursorPosition() for precise cursor location
 * 5. Assembles EditorState with all available fields
 * 6. Returns BridgeResponse with success flag and EditorState data
 *
 * **Return Value** (on success):
 * ```javascript
 * {
 *   success: true,
 *   data: {
 *     activeFile: "/home/user/file.cs" | null,
 *     cursorLine: 42,
 *     cursorColumn: 10,
 *     selectedText: "foo" | "",
 *     selectionStart: 100,
 *     selectionEnd: 103,
 *     fileContent: "using System;\n...",
 *     language: "csharp",
 *     projectPath: "/home/user/project",
 *     diagnosticsCount: 3,
 *     lastUpdate: "2024-01-15T10:30:00.000Z"
 *   }
 * }
 * ```
 *
 * **Return Value** (on error):
 * ```javascript
 * {
 *   success: false,
 *   error: {
 *     code: "EDITOR_STATE_ERROR",
 *     message: "EditorContextCollector not initialized",
 *     details: { operationType: "init" }
 *   }
 * }
 * ```
 *
 * @async
 * @param {BridgeMessage} message - Incoming bridge message
 * @param {string} message.messageType - Should be 'bridge:getEditorState'
 * @param {string} message.messageId - Unique message identifier (for tracing)
 * @param {Object} [message.data] - Message payload (unused for getEditorState)
 *
 * @param {Object} context - Handler execution context
 * @param {EditorContextCollector} context.editorContextCollector - Editor state collector (required)
 * @param {Object} [context.logger] - Optional logger instance
 * @param {Function} [context.logger.debug] - Logger.debug(msg, ...args)
 * @param {Function} [context.logger.error] - Logger.error(msg, error, ...args)
 * @param {Object} [context.metrics] - Optional metrics collector
 * @param {Function} [context.metrics.recordHandlerExecution] - Metrics.recordHandlerExecution(handlerName, success, latencyMs)
 *
 * @returns {Promise<Object>} BridgeResponse with EditorState or error
 *
 * @throws {GetEditorStateError} If collector is not available (error should be caught and wrapped in BridgeResponse)
 *
 * @example
 * // Step 50: In a handler dispatcher context
 * const handler = createGetEditorStateHandler(editorContextCollector);
 * const response = await handler(message, context);
 * console.log(response.data.activeFile);  // e.g., "/home/user/main.cs"
 *
 * @example
 * // Step 51: Chained from bootstrap (Step 46)
 * // Bootstrap initializes collector
 * // Step 50 handler queries it
 * // Step 51 handler subscribes to changes
 */
export async function getEditorStateHandler(message, context) {
  const startTime = Date.now();

  try {
    // Validate context and collector
    if (!context) {
      throw new GetEditorStateError(
        'Handler context is required',
        'init'
      );
    }

    const { editorContextCollector, logger, metrics } = context;

    if (!editorContextCollector) {
      throw new GetEditorStateError(
        'EditorContextCollector not initialized in context',
        'init'
      );
    }

    // Log handler execution if logger available
    if (logger?.debug) {
      logger.debug('getEditorStateHandler: querying editor state', {
        messageId: message?.messageId
      });
    }

    // Query collector for active file
    const activeFile = editorContextCollector.getActiveFile();

    // Query collector for cursor position
    const cursorPosition = editorContextCollector.getCursorPosition();

    // Query collector for selection
    const selection = editorContextCollector.getSelection();

    // Assemble EditorState response
    const editorState = {
      activeFile: activeFile?.filepath || null,
      cursorLine: cursorPosition?.line ?? 0,
      cursorColumn: cursorPosition?.character ?? 0,
      selectedText: selection?.text || '',
      selectionStart: selection?.start ?? -1,
      selectionEnd: selection?.end ?? -1,
      fileContent: activeFile?.contents || '',
      language: activeFile?.language || 'unknown',
      projectPath: activeFile?.projectPath || '',
      diagnosticsCount: activeFile?.diagnosticsCount ?? 0,
      lastUpdate: activeFile ? new Date().toISOString() : null
    };

    // Record metrics if available
    const latencyMs = Date.now() - startTime;
    if (metrics?.recordHandlerExecution) {
      metrics.recordHandlerExecution('bridge:getEditorState', true, latencyMs);
    }

    if (logger?.debug) {
      logger.debug('getEditorStateHandler: returning editor state', {
        activeFile: editorState.activeFile,
        latencyMs
      });
    }

    // Return success response
    return {
      success: true,
      data: editorState
    };
  } catch (error) {
    const latencyMs = Date.now() - startTime;

    // Log error if logger available (extract from context if it exists)
    const errorLogger = context?.logger;
    if (errorLogger?.error) {
      errorLogger.error('getEditorStateHandler: error', error, {
        messageId: message?.messageId
      });
    }

    // Record metrics if available
    const errorMetrics = context?.metrics;
    if (errorMetrics?.recordHandlerExecution) {
      errorMetrics.recordHandlerExecution('bridge:getEditorState', false, latencyMs);
    }

    // If error is GetEditorStateError, wrap it; otherwise wrap unexpected error
    const handlerError = error instanceof GetEditorStateError
      ? error
      : new GetEditorStateError(
          `Unexpected error: ${error.message}`,
          'query',
          error
        );

    return {
      success: false,
      error: {
        code: 'EDITOR_STATE_ERROR',
        message: handlerError.message,
        details: {
          operationType: handlerError.operationType
        }
      }
    };
  }
}

/**
 * Create a bound handler instance for dependency injection.
 *
 * Factory function that binds an EditorContextCollector instance to the handler,
 * allowing handler registration without manual context assembly.
 *
 * **Usage** (Step 66 handler registry, Step 71 handler registration):
 * ```javascript
 * const handler = createGetEditorStateHandler(editorContextCollector);
 * registry.register('bridge:getEditorState', handler);
 * ```
 *
 * @param {EditorContextCollector} editorContextCollector - Editor context collector instance
 * @returns {Function} Bound handler function (message, context) => Promise<BridgeResponse>
 *
 * @throws {TypeError} If collector is not an object
 *
 * @example
 * // Step 46: During bridge initialization
 * const collector = new EditorContextCollector({ logger, metrics });
 * await collector.registerMessageHandlers(server);
 *
 * // Step 50: Create handler
 * const getEditorStateHandler = createGetEditorStateHandler(collector);
 *
 * // Step 71: Register handler
 * dispatcher.registerHandler('bridge:getEditorState', getEditorStateHandler);
 */
export function createGetEditorStateHandler(editorContextCollector) {
  if (
    typeof editorContextCollector !== 'object' ||
    editorContextCollector === null ||
    Array.isArray(editorContextCollector)
  ) {
    throw new TypeError('editorContextCollector must be an object (not null, array, or primitive)');
  }

  return async (message, context) => {
    return getEditorStateHandler(message, {
      ...context,
      editorContextCollector
    });
  };
}

export default getEditorStateHandler;
