/**
 * TypeScript JSDoc Type Definitions for Hover-Info Handler (Step 59)
 *
 * Provides IDE intellisense and type checking for hover-info handler
 * and related structures.
 */

/**
 * Hover information descriptor
 * @typedef {Object} HoverInfo
 * @property {string} kind - Symbol kind: 'class'|'method'|'property'|'variable'|'parameter'|'field'|'enum'|'interface'|'function'|'diagnostic'|'unknown'
 * @property {string} text - Primary hover text (short form, usually type signature)
 * @property {string} [documentation] - Full documentation (JSDoc, XmlDoc, diagnostic message, or comment)
 * @property {string} [signature] - Full method/function signature if applicable
 * @property {boolean} [deprecated] - True if symbol is marked deprecated
 * @property {string} source - Source of hover info: 'symbol'|'comment'|'diagnostic'|'none'
 * @property {{start: {line: number, column: number}, end: {line: number, column: number}}} range - Range of hovered text
 */

/**
 * Request payload for hover information query
 * @typedef {Object} HoverRequest
 * @property {string} filepath - Absolute or workspace-relative file path
 * @property {number} line - 0-based line number
 * @property {number} column - 0-based column position
 * @property {boolean} [includeDocumentation=true] - Include full documentation
 * @property {boolean} [includeSignature=true] - Include full signature (for methods/functions)
 * @property {boolean} [includeDeprecation=true] - Include deprecation status
 */

/**
 * Response payload for hover information query
 * @typedef {Object} HoverResponse
 * @property {HoverInfo|null} hoverInfo - Hover information (null if no info available)
 * @property {string} source - Source of the hover info: 'symbol'|'comment'|'diagnostic'|'none'
 * @property {boolean} cacheHit - True if result came from cache
 * @property {number} queryTime - Time taken to process query (milliseconds)
 */

/**
 * BridgeMessage for hover information request
 * @typedef {Object} HoverBridgeMessage
 * @property {string} type - Message type: 'bridge:hoverInfo'
 * @property {string} id - Unique message identifier for correlation
 * @property {HoverRequest} data - Request payload
 */

/**
 * BridgeResponse for hover information
 * @typedef {Object} HoverBridgeResponse
 * @property {string} id - Message id (correlates to request)
 * @property {boolean} success - True if query succeeded
 * @property {HoverResponse} [data] - Response payload (present if success=true)
 * @property {{code: string, message: string, operationType: string, queryTime: number}} [error] - Error details (present if success=false)
 */

/**
 * Cache statistics for performance monitoring
 * @typedef {Object} CacheStats
 * @property {number} hits - Number of cache hits
 * @property {number} misses - Number of cache misses
 * @property {number} evictions - Number of LRU evictions
 * @property {number} ttlExpiries - Number of entries expired by TTL
 * @property {number} size - Current cache size
 */

/**
 * Diagnostic issue at a position (for diagnostic hover)
 * @typedef {Object} DiagnosticInfo
 * @property {string} message - Error/warning message
 * @property {string} severity - 'error'|'warning'|'information'|'hint'
 * @property {string} [code] - Error code (e.g., 'CS0246')
 * @property {string} [source] - Source of diagnostic (e.g., 'Roslyn', 'ESLint')
 * @property {{start: {line: number, column: number}, end: {line: number, column: number}}} [range] - Diagnostic range
 */

/**
 * Symbol information for symbol hover
 * @typedef {Object} SymbolInfo
 * @property {string} name - Symbol name
 * @property {string} kind - Symbol kind: 'class'|'method'|'property'|'variable'|'parameter'|'field'|'enum'|'interface'|'function'
 * @property {string} [signature] - Full signature (for methods/functions)
 * @property {string} [documentation] - Associated documentation
 * @property {boolean} [deprecated] - Deprecation status
 * @property {{start: {line: number, column: number}, end: {line: number, column: number}}} [range] - Symbol range
 */

/**
 * Error class for hover-info handler
 * @class HoverInfoError
 * @property {string} message - Error message
 * @property {string} name - 'HoverInfoError'
 * @property {string} operationType - Type of operation that failed: 'stateValidation'|'symbolQuery'|'diagnosticQuery'|'documentQuery'|'cacheQuery'|'unknown'
 * @property {Error} [originalError] - Original error that was wrapped
 */

/**
 * Validation error for invalid state
 * @class StateValidationError
 * @extends HoverInfoError
 * @property {string} name - 'StateValidationError'
 * @property {string} fieldName - Name of invalid field
 * @property {*} value - Value of the invalid field
 * @property {string} reason - Reason for validation failure
 */

/**
 * Handler options for HoverInfoHandler constructor
 * @typedef {Object} HoverInfoHandlerOptions
 * @property {Object} [logger] - Logger instance ({ info, debug, warn, error })
 * @property {Object} [metrics] - Metrics instance ({ record, recordHistogram })
 * @property {Object} [symbolExtractor] - SymbolExtractor instance with async extractSymbols(filepath, options) method
 * @property {Object} [diagnosticsCollector] - DiagnosticsCollector instance with async getDiagnosticsAt(filepath, line, column) method
 * @property {Object} [documentProvider] - DocumentProvider instance with async getDocumentContent(filepath) method
 * @property {number} [cacheSize=500] - Maximum number of entries in hover cache
 * @property {number} [cacheTtlMs=300000] - Cache entry TTL in milliseconds (default: 5 minutes)
 */

/**
 * HoverInfoHandler class
 * Main request handler for bridge:hoverInfo messages
 * @class HoverInfoHandler
 * @param {HoverInfoHandlerOptions} options - Handler configuration
 * @property {Function} async handle(message) - Main RPC handler, returns Promise<BridgeResponse>
 * @property {Function} getCacheStats() - Returns CacheStats
 * @property {Function} clearCache() - Clears internal cache
 */

// Export JSDoc types for IDE intellisense
// Usage: import type { HoverInfo, HoverRequest, HoverResponse } from './hover-info.d.js';
export const hoverInfoTypes = {
  // This file provides only type definitions via JSDoc
  // No runtime exports needed
};
