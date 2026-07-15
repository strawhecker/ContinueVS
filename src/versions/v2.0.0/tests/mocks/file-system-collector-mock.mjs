#!/usr/bin/env node

/**
 * Mock File-System Collector (Step 83 Testing)
 *
 * In-memory file-system simulator for unit testing file-system handler
 * without hitting the actual filesystem or C# layer.
 *
 * Features:
 * - Simulates file storage (Map-based)
 * - Track operation counts
 * - Inject errors (access denied, encoding, boundary violations)
 * - Validate path security rules (same as C#)
 * - Performance assertions
 *
 * @module src/versions/v2.0.0/tests/mocks/file-system-collector-mock.mjs
 */

// ============================================================================
// MOCK FILE-SYSTEM COLLECTOR
// ============================================================================

/**
 * In-memory file-system mock for testing
 *
 * Provides fake C# FileSystemCollector interface:
 * - readFile(path) → {content, encoding}
 * - writeFile(path, content) → {path, bytesWritten}
 * - deleteFile(path) → {deleted}
 * - listDirectory(path) → {files}
 * - getFileStats(path) → {size, type, mtime, exists}
 * - createDirectory(path, createParents) → {path, created}
 */
export class MockFileSystemCollector {
  constructor() {
    // In-memory file store: path → {content, type, mtime}
    this.files = new Map();
    // In-memory directory store: path → true
    this.directories = new Set();
    // Operation tracking
    this.operations = [];
    // Error injection: path → {code, message}
    this.injectedErrors = new Map();
    // Boundary enforcement: base path
    this.workspaceRoot = '/home/user/project';
  }

  // ========================================================================
  // ERROR INJECTION API (for testing)
  // ========================================================================

  /**
   * Inject an error for a specific path
   *
   * @param {string} path - Path to inject error for
   * @param {string} code - Error code (e.g., 'ACCESS_DENIED', 'BOUNDARY_VIOLATION')
   * @param {string} message - Error message
   */
  injectError(path, code, message) {
    this.injectedErrors.set(path, { code, message });
  }

  /**
   * Simulate boundary violation for a path
   */
  simulateBoundaryViolation(path) {
    this.injectError(
      path,
      'BOUNDARY_VIOLATION',
      `Path ${path} violates workspace boundary`
    );
  }

  /**
   * Simulate encoding error for a path
   */
  simulateEncodingError(path) {
    this.injectError(path, 'ENCODING_ERROR', `Cannot decode file at ${path}`);
  }

  /**
   * Simulate depth exceeded error
   */
  simulateDepthExceeded(path) {
    this.injectError(path, 'DEPTH_EXCEEDED', `Directory depth exceeds limit`);
  }

  // ========================================================================
  // SIMULATION API (populate test data)
  // ========================================================================

  /**
   * Simulate a file in the mock filesystem
   *
   * @param {string} path - File path
   * @param {string} content - File content
   */
  simulateFile(path, content) {
    this.validatePath(path);
    this.files.set(path, {
      content,
      type: 'file',
      mtime: new Date().toISOString(),
      size: Buffer.byteLength(content),
    });
    // Ensure parent directory exists
    const parent = path.substring(0, path.lastIndexOf('/'));
    if (parent && parent !== '') {
      this.directories.add(parent);
    }
  }

  /**
   * Simulate a directory in the mock filesystem
   *
   * @param {string} path - Directory path
   * @param {Array} files - Array of {name, type, size, mtime} objects
   */
  simulateDirectory(path, files) {
    this.validatePath(path);
    this.directories.add(path);
    // Store directory contents
    files.forEach((file) => {
      const fullPath = `${path}/${file.name}`;
      if (file.type === 'file') {
        this.files.set(fullPath, {
          content: '',
          type: 'file',
          mtime: file.mtime || new Date().toISOString(),
          size: file.size || 0,
        });
      } else {
        this.directories.add(fullPath);
      }
    });
  }

  /**
   * Get file content directly (for test assertions)
   */
  getFile(path) {
    const file = this.files.get(path);
    return file ? file.content : null;
  }

  /**
   * Clear all mocked data
   */
  reset() {
    this.files.clear();
    this.directories.clear();
    this.operations = [];
    this.injectedErrors.clear();
  }

  // ========================================================================
  // PATH VALIDATION (matches C# logic)
  // ========================================================================

  /**
   * Validate path (matches C# FileSystemCollector.ValidatePath)
   *
   * @throws {Error} if validation fails
   */
  validatePath(path) {
    if (!path || typeof path !== 'string') {
      throw new Error('Path must be non-empty string');
    }
    if (path.includes('\0')) {
      throw new Error('Path contains null bytes');
    }
  }

  /**
   * Normalize and validate path against workspace boundary
   *
   * @throws {Error} if boundary violated
   */
  normalizePath(path) {
    this.validatePath(path);

    // Check for traversal attempts (simplified, real C# uses Path.GetFullPath)
    if (path.includes('..')) {
      throw new Error(`TRAVERSAL: Path contains .. components`);
    }

    // Check injected errors first
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    return path;
  }

  // ========================================================================
  // HANDLER IMPLEMENTATIONS (C# API)
  // ========================================================================

  /**
   * Simulate C# ReadFileAsync
   *
   * @param {string} path - File path
   * @returns {Promise<{content, encoding, size}>}
   */
  async readFile(path) {
    this.operations.push({ op: 'readFile', path });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    const file = this.files.get(path);
    if (!file) {
      const error = new Error(`File not found: ${path}`);
      error.code = 'NOT_FOUND';
      throw error;
    }

    return {
      content: file.content,
      encoding: 'utf-8',
      size: file.size,
    };
  }

  /**
   * Simulate C# WriteFileAsync
   *
   * @param {string} path - File path
   * @param {string} content - File content
   * @returns {Promise<{path, bytesWritten}>}
   */
  async writeFile(path, content) {
    this.operations.push({ op: 'writeFile', path, contentLength: content.length });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    // Create parent directories
    const parent = path.substring(0, path.lastIndexOf('/'));
    if (parent && parent !== '') {
      this.directories.add(parent);
    }

    // Write file
    const bytesWritten = Buffer.byteLength(content);
    this.files.set(path, {
      content,
      type: 'file',
      mtime: new Date().toISOString(),
      size: bytesWritten,
    });

    return {
      path,
      bytesWritten,
    };
  }

  /**
   * Simulate C# DeleteFileAsync
   *
   * @param {string} path - File path
   * @returns {Promise<{deleted}>}
   */
  async deleteFile(path) {
    this.operations.push({ op: 'deleteFile', path });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    const exists = this.files.has(path);
    if (exists) {
      this.files.delete(path);
    }

    return { deleted: exists };
  }

  /**
   * Simulate C# ListDirectoryAsync
   *
   * @param {string} path - Directory path
   * @returns {Promise<{files}>}
   */
  async listDirectory(path) {
    this.operations.push({ op: 'listDirectory', path });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    if (!this.directories.has(path)) {
      const error = new Error(`Directory not found: ${path}`);
      error.code = 'NOT_FOUND';
      throw error;
    }

    // Collect files in directory
    const files = [];
    for (const [filePath, file] of this.files) {
      if (filePath.startsWith(path + '/') && !filePath.substring(path.length + 1).includes('/')) {
        files.push({
          name: filePath.substring(path.length + 1),
          type: file.type,
          size: file.size,
          mtime: file.mtime,
        });
      }
    }

    // Collect subdirectories
    for (const dirPath of this.directories) {
      if (dirPath.startsWith(path + '/') && !dirPath.substring(path.length + 1).includes('/')) {
        files.push({
          name: dirPath.substring(path.length + 1),
          type: 'directory',
          size: 0,
          mtime: null,
        });
      }
    }

    // Enforce max entries
    if (files.length > 5000) {
      files.splice(5000);
    }

    return { files };
  }

  /**
   * Simulate C# GetFileStatsAsync
   *
   * @param {string} path - File path
   * @returns {Promise<{size, type, mtime, exists}>}
   */
  async getFileStats(path) {
    this.operations.push({ op: 'getFileStats', path });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    // Check if it's a file
    if (this.files.has(path)) {
      const file = this.files.get(path);
      return {
        size: file.size,
        type: 'file',
        mtime: file.mtime,
        exists: true,
      };
    }

    // Check if it's a directory
    if (this.directories.has(path)) {
      return {
        size: 0,
        type: 'directory',
        mtime: new Date().toISOString(),
        exists: true,
      };
    }

    // Not found
    const error = new Error(`Path not found: ${path}`);
    error.code = 'NOT_FOUND';
    throw error;
  }

  /**
   * Simulate C# CreateDirectoryAsync
   *
   * @param {string} path - Directory path
   * @param {boolean} createParents - Create parent directories
   * @returns {Promise<{path, created}>}
   */
  async createDirectory(path, createParents = true) {
    this.operations.push({ op: 'createDirectory', path, createParents });
    this.normalizePath(path);

    // Check for injected error
    if (this.injectedErrors.has(path)) {
      const error = this.injectedErrors.get(path);
      throw new Error(error.message);
    }

    // Check depth limit
    const depth = path.split('/').filter((p) => p).length;
    if (depth > 50) {
      const error = new Error('Directory depth exceeds maximum (50 levels)');
      error.code = 'DEPTH_EXCEEDED';
      throw error;
    }

    // Create parent directories if requested
    if (createParents) {
      const parts = path.split('/');
      for (let i = 1; i < parts.length; i++) {
        const parentPath = parts.slice(0, i + 1).join('/');
        if (parentPath && !this.directories.has(parentPath)) {
          this.directories.add(parentPath);
        }
      }
    }

    const created = !this.directories.has(path);
    this.directories.add(path);

    return { path, created };
  }

  // ========================================================================
  // TEST UTILITIES
  // ========================================================================

  /**
   * Get operation history (for assertions)
   */
  getOperations() {
    return [...this.operations];
  }

  /**
   * Get count of specific operation type
   */
  getOperationCount(operationType) {
    return this.operations.filter((op) => op.op === operationType).length;
  }
}

// ============================================================================
// FACTORY FUNCTIONS
// ============================================================================

/**
 * Create a standard mock collector (for most tests)
 */
export function createMockFileSystemCollector() {
  return new MockFileSystemCollector();
}

/**
 * Create a strict mock that throws on unexpected operations
 */
export function createStrictMockFileSystemCollector() {
  const mock = new MockFileSystemCollector();
  const originalRead = mock.readFile.bind(mock);

  // Wrap to track unexpected calls
  mock.readFile = async function (path) {
    if (!this.files.has(path)) {
      throw new Error(`Test assertion: file ${path} not pre-simulated`);
    }
    return originalRead(path);
  };

  return mock;
}

export default MockFileSystemCollector;
