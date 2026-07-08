/**
 * Type Definitions for Bridge Handlers
 * 
 * JSDoc type definitions used throughout the handler system.
 * No runtime code; imported for IDE support and documentation.
 *
 * @module src/versions/v2.0.0/types/handlers.d.js
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps: 14 (HandlerDispatcher), 50–61 (individual handlers)
 */

/**
 * Standard message envelope from IDE or bridge.
 * 
 * @typedef {Object} BridgeMessage
 * @property {string} messageType - Message type (e.g., "bridge:getEditorState", "continue:chat")
 * @property {string} messageId - Unique correlation ID (UUID v4 recommended)
 * @property {*} data - Payload specific to message type; schema varies per type
 */

/**
 * Response from a handler function.
 * 
 * @typedef {Object} HandlerResponse
 * @property {boolean} success - Whether handler succeeded
 * @property {*} [data] - Response data (present if success=true)
 * @property {string} [error] - Error message (present if success=false)
 */

/**
 * Context passed to handler functions.
 * Provides access to shared services and state.
 * 
 * @typedef {Object} HandlerContext
 * @property {Object} logger - Logger instance (Step 25: BridgeLogger)
 * @property {Object} metrics - Metrics collector (Step 26: TelemetryCollector)
 * @property {Object} server - CoreServer instance (Step 13) for lifecycle access
 */

/**
 * Handler function signature.
 * All handlers are async and must return HandlerResponse.
 * 
 * @typedef {Function} HandlerFunction
 * @param {BridgeMessage} message - Incoming message
 * @param {HandlerContext} context - Shared context and services
 * @returns {Promise<HandlerResponse>} Handler response
 */

/**
 * Dispatcher result from dispatch() operation.
 * Tells calling code whether message was handled or should relay.
 * 
 * @typedef {Object} DispatchResult
 * @property {boolean} handled - True if a handler was invoked
 * @property {boolean} shouldRelay - True if message should pass to Continue
 * @property {BridgeMessage} [response] - Response if handled=true
 */

/**
 * Editor state snapshot (Step 50).
 * Captures current editor context and selection.
 * 
 * @typedef {Object} EditorState
 * @property {string} activeFile - Absolute path to active file
 * @property {number} cursorLine - Cursor line number (0-based)
 * @property {number} cursorColumn - Cursor column (0-based)
 * @property {string} selectedText - Currently selected text (empty if no selection)
 * @property {number} selectionStart - Start offset (0-based)
 * @property {number} selectionEnd - End offset (0-based)
 * @property {string} fileContent - Full file content
 * @property {string} language - Language ID (e.g., "csharp", "python")
 * @property {string} projectPath - Workspace root path
 * @property {number} diagnosticsCount - Number of diagnostics at cursor
 */

/**
 * Symbol information (Step 53).
 * Represents a code symbol (class, method, variable, etc.).
 * 
 * @typedef {Object} SymbolInfo
 * @property {string} name - Symbol name
 * @property {string} kind - Symbol kind (class, method, property, etc.)
 * @property {number} line - Definition line number (0-based)
 * @property {number} column - Definition column (0-based)
 * @property {string} file - File path containing symbol
 * @property {string} [documentation] - Optional docstring/comment
 * @property {SymbolInfo[]} [children] - Child symbols (for classes, etc.)
 */

/**
 * Diagnostic (error/warning) information (Step 54).
 * Represents a code issue found by analyzer or compiler.
 * 
 * @typedef {Object} Diagnostic
 * @property {string} code - Diagnostic code (e.g., "CS0001")
 * @property {string} message - Human-readable message
 * @property {string} severity - "error" | "warning" | "info"
 * @property {number} line - Line number (0-based)
 * @property {number} column - Column (0-based)
 * @property {number} [endLine] - End line (0-based)
 * @property {number} [endColumn] - End column (0-based)
 * @property {string} file - File path
 */

/**
 * Search result from global search handler (Step 55).
 * 
 * @typedef {Object} SearchResult
 * @property {string} file - File path
 * @property {number} line - Line number (0-based)
 * @property {number} column - Column (0-based)
 * @property {string} text - Matched text
 * @property {string} preview - Line preview with context
 */

/**
 * Definition location from go-to-definition handler (Step 56).
 * 
 * @typedef {Object} DefinitionLocation
 * @property {string} file - File path
 * @property {number} line - Line number (0-based)
 * @property {number} column - Column (0-based)
 * @property {string} name - Symbol name
 * @property {string} kind - Symbol kind
 */

/**
 * Reference location from find-references handler (Step 57).
 * 
 * @typedef {Object} ReferenceLocation
 * @property {string} file - File path
 * @property {number} line - Line number (0-based)
 * @property {number} column - Column (0-based)
 * @property {string} text - Reference text
 * @property {string} kind - Reference kind (declaration, read, write, etc.)
 */

/**
 * Completion item from code-completion handler (Step 58).
 * 
 * @typedef {Object} CompletionItem
 * @property {string} label - Display text
 * @property {string} kind - Completion kind (Class, Method, Property, etc.)
 * @property {string} [detail] - Additional detail (type, signature)
 * @property {string} [documentation] - Docstring/comment
 * @property {string} [insertText] - Text to insert on selection
 * @property {number} [sortText] - Sort priority
 */

/**
 * Hover information from hover-info handler (Step 59).
 * 
 * @typedef {Object} HoverInfo
 * @property {string} contents - Markdown-formatted hover text
 * @property {number} [range] - Hover range in document
 */

// Export type definitions for documentation
export const Types = {
  BridgeMessage: 'Object with messageType, messageId, data',
  HandlerResponse: 'Object with success, data?, error?',
  HandlerContext: 'Object with logger, metrics, server',
  HandlerFunction: 'async (message, context) => HandlerResponse',
  DispatchResult: 'Object with handled, shouldRelay, response?',
  EditorState: 'Object with activeFile, cursor, selection, etc.',
  SymbolInfo: 'Object with name, kind, line, column, file',
  Diagnostic: 'Object with code, message, severity, line, column',
  SearchResult: 'Object with file, line, column, text, preview',
  DefinitionLocation: 'Object with file, line, column, name, kind',
  ReferenceLocation: 'Object with file, line, column, text, kind',
  CompletionItem: 'Object with label, kind, detail, documentation, insertText',
  HoverInfo: 'Object with contents, range?',
};

export default Types;
