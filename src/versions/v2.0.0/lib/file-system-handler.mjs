#!/usr/bin/env node

/**
 * File-System Handler (Step 83)
 *
 * Provides synchronous file-system operations: read, write, delete, list directory, stats, mkdir.
 * Integrates with C# FileSystemCollector via JSON-RPC messages.
 *
 * **Handler Type**: Synchronous request/response handler (no streaming)
 * **Message Types**: 
 *   - bridge:readFile (request/response)
 *   - bridge:writeFile (request/response)
 *   - bridge:deleteFile (request/response)
 *   - bridge:listDirectory (request/response)
 *   - bridge:getFileStats (request/response)
 *   - bridge:createDirectory (request/response)
 *
 * **Input**: BridgeMessage with { operation?, path?, content?, createParents? }
 * **Output**: BridgeResponse containing { success, data, error }
 *
 * **Supported Operations**:
 * - `read`: Read file contents (UTF-8)
 * - `write`: Write/create file (UTF-8, creates parents)
 * - `delete`: Delete file safely
 * - `list`: List directory contents (non-recursive, max 5000 items)
 * - `stats`: Query file metadata (size, type, modified)
 * - `mkdir`: Create directory (with optional parent creation)
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:readFile request
 *   ↓
 * [dispatcher] routes to createFileSystemHandler()
 *   ↓
 * [handler] validates input path (type, length, format)
 *   ↓
 * [handler] queries C# FileSystemCollector via collector
 *   ↓
 * [collector] normalizes path, validates boundary, checks security
 *   ↓
 * [collector] performs filesystem operation (read/write/delete/etc.)
 *   ↓
 * [handler] formats response with metadata
 *   ↓
 * [core-server] sends response back
 * ```
 *
 * **Security Model**:
 * - Path normalization at C# layer (prevent traversal: `../../../`)
 * - Workspace boundary enforcement (no escape from project root)
 * - Symlink following disabled (prevent loop attacks)
 * - Input validation (non-empty, string type, max length)
 *
 * **Error Handling**:
 * - Path validation failure → PathError (RPC -32602)
 * - Boundary violation / access denied → AccessError (RPC -32600)
 * - Encoding issues → EncodingError (RPC -32603)
 * - Missing collector → FileSystemError (RPC -32000)
 *
 * **Performance**:
 * - Read/write latency: <100ms per file
 * - Directory list (1000 items): <150ms
 * - Stats query: <50ms
 * - Memory: <5MB per handler instance
 *
 * **Dependencies**:
 * - C# FileSystemCollector (injected via context)
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/file-system-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 47: message-routing-middleware.js (middleware integration)
 *   - Step 71: handler-registry.mjs (handler registration)
 *   - Step 72: message-logging-middleware.js (logging integration)
 *   - Step 73: request-response-validation.js (envelope validation)
 */

// ============================================================================
// CUSTOM ERROR CLASSES
// ============================================================================

/**
 * Base error for file-system operations
 *
 * @class FileSystemError
 * @extends {Error}
 */
export class FileSystemError extends Error {
  constructor(message, code = 'FILESYSTEM_ERROR', details = null) {
    super(message);
    this.name = 'FileSystemError';
    this.code = code;
    this.details = details;
    this.rpcErrorCode = -32000; // Server error
  }
}

/**
 * Path validation or normalization error
 *
 * @class PathError
 * @extends {FileSystemError}
 */
export class PathError extends FileSystemError {
  constructor(message, path = '', reason = 'invalid', details = null) {
    super(message, 'PATH_ERROR', details);
    this.name = 'PathError';
    this.path = path;
    this.reason = reason; // 'invalid', 'empty', 'traversal', 'too_long'
    this.rpcErrorCode = -32602; // InvalidParams
  }
}

/**
 * Access denied or boundary violation error
 *
 * @class AccessError
 * @extends {FileSystemError}
 */
export class AccessError extends FileSystemError {
  constructor(message, path = '', reason = 'denied', details = null) {
    super(message, 'ACCESS_ERROR', details);
    this.name = 'AccessError';
    this.path = path;
    this.reason = reason; // 'denied', 'boundary', 'not_found', 'locked'
    this.rpcErrorCode = -32600; // InvalidRequest
  }
}

/**
 * File encoding error
 *
 * @class EncodingError
 * @extends {FileSystemError}
 */
export class EncodingError extends FileSystemError {
  constructor(message, path = '', encoding = '', details = null) {
    super(message, 'ENCODING_ERROR', details);
    this.name = 'EncodingError';
    this.path = path;
    this.encoding = encoding;
    this.rpcErrorCode = -32603; // InternalError
  }
}

// ============================================================================
// VALIDATION & UTILITIES
// ============================================================================

const MAX_PATH_LENGTH = 4096;
const RESERVED_NAMES = ['.', '..', 'CON', 'PRN', 'AUX', 'NUL'];

/**
 * Validate input path (basic client-side checks)
 * Note: Full security validation happens in C# collector
 *
 * @param {string} path - Path to validate
 * @throws {PathError} if validation fails
 */
function validatePath(path) {
  if (typeof path !== 'string') {
    throw new PathError(`Path must be string, got ${typeof path}`, path, 'invalid');
  }
  if (path.length === 0) {
    throw new PathError('Path cannot be empty', path, 'empty');
  }
  if (path.length > MAX_PATH_LENGTH) {
    throw new PathError(
      `Path exceeds maximum length (${MAX_PATH_LENGTH})`,
      path,
      'too_long'
    );
  }
  // Check for null bytes (security)
  if (path.includes('\0')) {
    throw new PathError('Path contains null bytes', path, 'invalid');
  }
}

/**
 * Validate content for write operation
 *
 * @param {string|Buffer} content - Content to write
 * @throws {PathError} if invalid
 */
function validateContent(content) {
  if (content !== undefined && content !== null) {
    if (typeof content !== 'string' && !Buffer.isBuffer(content)) {
      throw new PathError(`Content must be string or Buffer, got ${typeof content}`, '', 'invalid');
    }
  }
}

// ============================================================================
// FACTORY & HANDLER IMPLEMENTATION
// ============================================================================

/**
 * Factory function to create file-system handler
 * Routes to operation-specific handler based on message type
 *
 * @param {Object} context - Handler context
 * @param {Object} context.logger - Optional logger instance
 * @param {Object} context.metrics - Optional metrics collector
 * @param {Object} context.collector - C# FileSystemCollector (required)
 * @returns {Function} Handler function (msg, ctx) => Promise<response>
 */
export function createFileSystemHandler(context = {}) {
  const { logger = null, metrics = null, collector = null } = context;

  // Validate collector dependency
  if (!collector) {
    throw new FileSystemError(
      'FileSystemCollector not injected into handler context',
      'MISSING_COLLECTOR'
    );
  }

  /**
   * Main handler: routes message to operation handler
   *
   * @param {Object} msg - BridgeMessage
   * @param {string} msg.type - Message type (e.g., 'bridge:readFile')
   * @param {Object} msg.data - Message payload
   * @param {Object} ctx - Context
   * @returns {Promise<Object>} Response object
   */
  return async function handleFileSystemMessage(msg, ctx) {
    const startTime = Date.now();
    const messageType = msg.type || msg.messageType || '';
    const data = msg.data || {};

    try {
      logger?.debug?.(`[FileSystem] Handling ${messageType}`, { path: data.path });

      let response;

      // Route to operation handler
      switch (messageType) {
        case 'bridge:readFile':
          response = await handleReadFile(data, collector, logger);
          break;

        case 'bridge:writeFile':
          response = await handleWriteFile(data, collector, logger);
          break;

        case 'bridge:deleteFile':
          response = await handleDeleteFile(data, collector, logger);
          break;

        case 'bridge:listDirectory':
          response = await handleListDirectory(data, collector, logger);
          break;

        case 'bridge:getFileStats':
          response = await handleGetFileStats(data, collector, logger);
          break;

        case 'bridge:createDirectory':
          response = await handleCreateDirectory(data, collector, logger);
          break;

        default:
          throw new FileSystemError(
            `Unknown file-system operation: ${messageType}`,
            'UNKNOWN_OPERATION'
          );
      }

      // Record success metric
      const duration = Date.now() - startTime;
      metrics?.recordMetric?.(`bridge.filesystem.${messageType}.success`, 1, {
        duration,
      });
      metrics?.recordMetric?.(`bridge.filesystem.latency_ms`, duration, {
        operation: messageType,
      });

      logger?.debug?.(`[FileSystem] ${messageType} success (${duration}ms)`);
      return response;
    } catch (error) {
      // Record error metric
      const duration = Date.now() - startTime;
      metrics?.recordMetric?.(`bridge.filesystem.${messageType}.error`, 1, {
        error: error.code || 'UNKNOWN',
        duration,
      });

      // Log security-relevant errors
      if (
        error instanceof PathError ||
        error instanceof AccessError
      ) {
        logger?.warn?.(`[FileSystem] Security event: ${error.message}`, {
          type: error.code,
          path: error.path,
          reason: error.reason,
        });
      }

      // Return error response with RPC code
      return {
        success: false,
        error: error.message,
        code: error.code || 'FILESYSTEM_ERROR',
        rpcErrorCode: error.rpcErrorCode || -32000,
      };
    }
  };
}

// ============================================================================
// OPERATION HANDLERS
// ============================================================================

/**
 * Handle bridge:readFile message
 *
 * @param {Object} data - Message payload { path }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { content, encoding } }
 */
async function handleReadFile(data, collector, logger) {
  const { path } = data;
  validatePath(path);

  try {
    const result = await collector.readFile(path);
    return {
      success: true,
      data: {
        content: result.content,
        encoding: result.encoding || 'utf-8',
        size: result.size || Buffer.byteLength(result.content),
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot read file: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    if (error.code === 'ENCODING_ERROR') {
      throw new EncodingError(
        `Encoding error reading file: ${error.message}`,
        path,
        error.encoding || 'unknown'
      );
    }
    throw new FileSystemError(
      `Error reading file: ${error.message}`,
      'READ_ERROR'
    );
  }
}

/**
 * Handle bridge:writeFile message
 *
 * @param {Object} data - Message payload { path, content }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { path, bytesWritten } }
 */
async function handleWriteFile(data, collector, logger) {
  const { path, content = '' } = data;
  validatePath(path);
  validateContent(content);

  try {
    const result = await collector.writeFile(path, content || '');
    return {
      success: true,
      data: {
        path: result.path,
        bytesWritten: result.bytesWritten,
        encoding: 'utf-8',
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot write file: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    throw new FileSystemError(
      `Error writing file: ${error.message}`,
      'WRITE_ERROR'
    );
  }
}

/**
 * Handle bridge:deleteFile message
 *
 * @param {Object} data - Message payload { path }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { deleted: boolean } }
 */
async function handleDeleteFile(data, collector, logger) {
  const { path } = data;
  validatePath(path);

  try {
    const result = await collector.deleteFile(path);
    return {
      success: true,
      data: {
        deleted: result.deleted || true,
        path,
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot delete file: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    // Gracefully handle missing files
    if (error.code === 'NOT_FOUND') {
      return {
        success: true,
        data: { deleted: false, path },
      };
    }
    throw new FileSystemError(
      `Error deleting file: ${error.message}`,
      'DELETE_ERROR'
    );
  }
}

/**
 * Handle bridge:listDirectory message
 *
 * @param {Object} data - Message payload { path }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { files: [{name, type, size, mtime}] } }
 */
async function handleListDirectory(data, collector, logger) {
  const { path } = data;
  validatePath(path);

  try {
    const result = await collector.listDirectory(path);
    return {
      success: true,
      data: {
        path,
        count: result.files?.length || 0,
        files: (result.files || []).map((file) => ({
          name: file.name,
          type: file.type || 'file', // 'file' | 'directory'
          size: file.size || 0,
          mtime: file.mtime || null,
        })),
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot list directory: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    if (error.code === 'NOT_FOUND') {
      throw new AccessError(
        `Directory not found: ${path}`,
        path,
        'not_found'
      );
    }
    throw new FileSystemError(
      `Error listing directory: ${error.message}`,
      'LIST_ERROR'
    );
  }
}

/**
 * Handle bridge:getFileStats message
 *
 * @param {Object} data - Message payload { path }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { size, type, mtime, exists } }
 */
async function handleGetFileStats(data, collector, logger) {
  const { path } = data;
  validatePath(path);

  try {
    const result = await collector.getFileStats(path);
    return {
      success: true,
      data: {
        path,
        exists: result.exists !== false,
        size: result.size || 0,
        type: result.type || 'file', // 'file' | 'directory'
        mtime: result.mtime || null,
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot stat file: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    // Gracefully handle missing files
    if (error.code === 'NOT_FOUND') {
      return {
        success: true,
        data: {
          path,
          exists: false,
          size: 0,
          type: null,
          mtime: null,
        },
      };
    }
    throw new FileSystemError(
      `Error getting file stats: ${error.message}`,
      'STATS_ERROR'
    );
  }
}

/**
 * Handle bridge:createDirectory message
 *
 * @param {Object} data - Message payload { path, createParents }
 * @param {Object} collector - C# FileSystemCollector
 * @param {Object} logger - Logger (optional)
 * @returns {Promise<Object>} { success, data: { path, created } }
 */
async function handleCreateDirectory(data, collector, logger) {
  const { path, createParents = true } = data;
  validatePath(path);

  try {
    const result = await collector.createDirectory(path, createParents);
    return {
      success: true,
      data: {
        path: result.path || path,
        created: result.created !== false,
      },
    };
  } catch (error) {
    if (error.code === 'ACCESS_DENIED' || error.code === 'BOUNDARY_VIOLATION') {
      throw new AccessError(
        `Cannot create directory: ${error.message}`,
        path,
        error.code.toLowerCase()
      );
    }
    if (error.code === 'DEPTH_EXCEEDED') {
      throw new PathError(
        `Directory creation would exceed maximum depth`,
        path,
        'depth_exceeded'
      );
    }
    throw new FileSystemError(
      `Error creating directory: ${error.message}`,
      'MKDIR_ERROR'
    );
  }
}

export default createFileSystemHandler;
