#!/usr/bin/env node

/**
 * Git Integration Handler (Step 81)
 *
 * Provides git repository state operations: status, log, branches, diff, currentBranch.
 *
 * **Handler Type**: Repository integration handler
 * **Message Type**: bridge:gitStatus
 * **Input**: BridgeMessage with { operation, cwd, cache, params }
 * **Output**: BridgeResponse containing { result, metadata }
 *
 * **Supported Operations**:
 * - `status`: Working tree state (staged, unstaged, untracked)
 * - `log`: Commit history (sha, author, message, date)
 * - `branches`: List branches (current, all branches)
 * - `diff`: File-level diffs (working tree vs. HEAD)
 * - `currentBranch`: Active branch name
 *
 * **Architecture Flow**:
 * ```
 * [Continue/IDE] → bridge:gitStatus request
 *   ↓
 * [dispatcher] routes to createGitIntegrationHandler()
 *   ↓
 * [handler] routes to sub-operation (status, log, branches, diff, currentBranch)
 *   ↓
 * [handler] checks cache (if enabled)
 *   ↓
 * [handler] executes git CLI command via child_process.execFile
 *   ↓
 * [handler] normalizes response to JSON
 *   ↓
 * [handler] caches result if enabled
 *   ↓
 * [core-server] sends response back
 * ```
 *
 * **Error Handling**:
 * - Git not installed → GitCommandError
 * - Not a git repository → GitRepositoryError
 * - Invalid operation → GitValidationError
 * - Command timeout → GitCommandError
 * - Malformed output → GitCommandError
 *
 * **Performance**:
 * - Single operation: <200ms (repo dependent)
 * - Cached operation: <2ms
 * - Memory: <5MB per request
 * - Cache TTL: 3000ms (configurable)
 *
 * **Dependencies**:
 * - Node.js child_process (built-in)
 * - Node.js path, fs (built-in)
 * - Bridge logger (optional) — injected via context
 * - Bridge metrics (optional) — injected via context
 *
 * @module src/versions/v2.0.0/lib/git-integration-handler.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 14: handler-dispatcher.js (dispatcher routing)
 *   - Step 47: message-routing-middleware.js (middleware integration)
 *   - Step 71: handler-registry.mjs (handler registration)
 *   - Step 72: message-logging-middleware.js (logging integration)
 *   - Step 83: file-system-handler.mjs (related handler)
 */

import { execFile } from 'child_process';
import { promisify } from 'util';
import path from 'path';
import fs from 'fs';

// ============================================================================
// CUSTOM ERROR CLASSES
// ============================================================================

/**
 * Base error for git operations
 *
 * @class GitError
 * @extends {Error}
 */
export class GitError extends Error {
  constructor(message, code = 'GIT_ERROR', details = null) {
    super(message);
    this.name = 'GitError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Git command execution error (e.g., git not installed, command failed)
 *
 * @class GitCommandError
 * @extends {GitError}
 */
export class GitCommandError extends GitError {
  constructor(message, command = null, exitCode = null) {
    super(message, 'GIT_COMMAND_ERROR', { command, exitCode });
    this.name = 'GitCommandError';
    this.command = command;
    this.exitCode = exitCode;
  }
}

/**
 * Repository error (e.g., not a git repo, no .git folder)
 *
 * @class GitRepositoryError
 * @extends {GitError}
 */
export class GitRepositoryError extends GitError {
  constructor(message, cwd = null) {
    super(message, 'GIT_REPOSITORY_ERROR', { cwd });
    this.name = 'GitRepositoryError';
    this.cwd = cwd;
  }
}

/**
 * Validation error for git operation requests
 *
 * @class GitValidationError
 * @extends {GitError}
 */
export class GitValidationError extends GitError {
  constructor(fieldName, message, value = null) {
    super(`${fieldName}: ${message}`, 'GIT_VALIDATION_ERROR', { fieldName, value });
    this.name = 'GitValidationError';
    this.fieldName = fieldName;
  }
}

// ============================================================================
// GIT OPERATION CACHE
// ============================================================================

/**
 * Simple LRU cache for git operations with TTL support
 *
 * @class GitOperationCache
 */
export class GitOperationCache {
  constructor(ttlMs = 3000, maxSize = 100) {
    this.ttlMs = ttlMs;
    this.maxSize = maxSize;
    this.cache = new Map();
    this.timestamps = new Map();
    this.hits = 0;
    this.misses = 0;
  }

  /**
   * Get value from cache if valid (not expired)
   *
   * @param {string} key - Cache key
   * @returns {*|null} - Cached value or null if expired/missing
   */
  get(key) {
    if (!this.cache.has(key)) {
      this.misses++;
      return null;
    }

    const timestamp = this.timestamps.get(key);
    if (Date.now() - timestamp > this.ttlMs) {
      this.cache.delete(key);
      this.timestamps.delete(key);
      this.misses++;
      return null;
    }

    this.hits++;
    return this.cache.get(key);
  }

  /**
   * Set value in cache
   *
   * @param {string} key - Cache key
   * @param {*} value - Value to cache
   */
  set(key, value) {
    // Evict oldest if at capacity
    if (this.cache.size >= this.maxSize) {
      const oldestKey = this.cache.keys().next().value;
      this.cache.delete(oldestKey);
      this.timestamps.delete(oldestKey);
    }

    this.cache.set(key, value);
    this.timestamps.set(key, Date.now());
  }

  /**
   * Invalidate specific cache entry
   *
   * @param {string} key - Cache key
   */
  invalidate(key) {
    this.cache.delete(key);
    this.timestamps.delete(key);
  }

  /**
   * Clear entire cache
   */
  clear() {
    this.cache.clear();
    this.timestamps.clear();
  }

  /**
   * Get cache statistics
   *
   * @returns {Object} - { hits, misses, size, hitRate }
   */
  getStats() {
    const total = this.hits + this.misses;
    return {
      hits: this.hits,
      misses: this.misses,
      size: this.cache.size,
      hitRate: total > 0 ? (this.hits / total * 100).toFixed(2) + '%' : '0%',
    };
  }
}

// ============================================================================
// GIT CLI WRAPPER
// ============================================================================

/**
 * Execute git command via child_process with timeout and error handling
 *
 * @param {string[]} args - Git command arguments
 * @param {Object} options - Configuration
 * @param {string} [options.cwd] - Working directory
 * @param {number} [options.timeout] - Timeout in ms (default 5000)
 * @param {*} [options.logger] - Logger instance
 * @returns {Promise<Object>} - { stdout, stderr, exitCode, command }
 * @throws {GitCommandError} - If git not found or command fails
 * @throws {GitRepositoryError} - If not a git repository
 */
async function executeGitCommand(args, options = {}) {
  const { cwd = process.cwd(), timeout = 5000, logger = null } = options;

  // Validate cwd exists
  try {
    fs.statSync(cwd);
  } catch (err) {
    throw new GitRepositoryError(`Directory not found: ${cwd}`, cwd);
  }

  return new Promise((resolve, reject) => {
    const child = execFile('git', args, { cwd, timeout }, (error, stdout, stderr) => {
      if (error) {
        // Check if git not installed
        if (error.code === 'ENOENT') {
          return reject(
            new GitCommandError(
              'git command not found. Ensure git is installed and in PATH.',
              `git ${args.join(' ')}`,
              null
            )
          );
        }

        // Check if not a git repository
        if (stderr && stderr.includes('not a git repository')) {
          return reject(
            new GitRepositoryError(
              `Not a git repository: ${cwd}`,
              cwd
            )
          );
        }

        // Generic command error
        return reject(
          new GitCommandError(
            stderr || error.message,
            `git ${args.join(' ')}`,
            error.code
          )
        );
      }

      resolve({
        stdout: stdout.toString().trim(),
        stderr: stderr.toString().trim(),
        exitCode: 0,
        command: `git ${args.join(' ')}`,
      });
    });

    // Handle timeout explicitly
    setTimeout(() => {
      if (child.exitCode === null) {
        child.kill();
        reject(
          new GitCommandError(
            `Git command timeout after ${timeout}ms`,
            `git ${args.join(' ')}`,
            'TIMEOUT'
          )
        );
      }
    }, timeout + 100);
  });
}

// ============================================================================
// RESPONSE NORMALIZERS
// ============================================================================

/**
 * Normalize git status output
 *
 * @param {string} stdout - Raw git status output
 * @returns {Object} - { clean, staged, unstaged, untracked }
 */
function normalizeStatusResponse(stdout) {
  const staged = [];
  const unstaged = [];
  const untracked = [];

  if (!stdout) {
    return { clean: true, staged, unstaged, untracked };
  }

  const lines = stdout.split('\n').filter(line => line.trim());

  for (const line of lines) {
    const status = line.substring(0, 2);
    const filePath = line.substring(3).replace(/\\/g, '/');

    if (status === '??') {
      untracked.push(filePath);
    } else if (status[0] !== ' ') {
      staged.push({ status: status[0], path: filePath });
    } else if (status[1] !== ' ') {
      unstaged.push({ status: status[1], path: filePath });
    }
  }

  const clean = staged.length === 0 && unstaged.length === 0 && untracked.length === 0;

  return { clean, staged, unstaged, untracked };
}

/**
 * Normalize git log output
 *
 * @param {string} stdout - Raw git log output
 * @returns {Object} - { commits }
 */
function normalizeLogResponse(stdout) {
  const commits = [];

  if (!stdout) {
    return { commits };
  }

  // Format: sha|author|message|date
  const lines = stdout.split('\n').filter(line => line.trim());

  for (const line of lines) {
    const [sha, author, message, date] = line.split('|');
    if (sha) {
      commits.push({
        sha: sha.trim(),
        author: author ? author.trim() : 'Unknown',
        message: message ? message.trim() : '',
        date: date ? new Date(date.trim()).toISOString() : new Date().toISOString(),
      });
    }
  }

  return { commits };
}

/**
 * Normalize git branches output
 *
 * @param {string} stdout - Raw git branch output
 * @param {string} currentBranch - Current branch name
 * @returns {Object} - { current, branches }
 */
function normalizeBranchesResponse(stdout, currentBranch) {
  const branches = [];

  if (stdout) {
    const lines = stdout.split('\n').filter(line => line.trim());

    for (const line of lines) {
      const trimmed = line.replace(/^\*\s+/, '').trim();
      if (trimmed) {
        const isRemote = trimmed.startsWith('remotes/');
        branches.push({
          name: trimmed,
          remote: isRemote,
        });
      }
    }
  }

  return { current: currentBranch, branches };
}

/**
 * Normalize git diff output
 *
 * @param {string} stdout - Raw git diff output
 * @param {string} filePath - File path being diffed
 * @returns {Object} - { path, additions, deletions, diff }
 */
function normalizeDiffResponse(stdout, filePath) {
  let additions = 0;
  let deletions = 0;

  if (stdout) {
    const lines = stdout.split('\n');
    for (const line of lines) {
      if (line.startsWith('+') && !line.startsWith('+++')) {
        additions++;
      } else if (line.startsWith('-') && !line.startsWith('---')) {
        deletions++;
      }
    }
  }

  return {
    path: filePath.replace(/\\/g, '/'),
    additions,
    deletions,
    diff: stdout,
  };
}

// ============================================================================
// OPERATION HANDLERS
// ============================================================================

/**
 * Handle 'status' operation
 *
 * @param {string} cwd - Repository path
 * @param {*} logger - Logger instance
 * @param {GitOperationCache} cache - Cache instance
 * @param {boolean} useCache - Whether to use cache
 * @returns {Promise<Object>} - Git status response
 */
async function handleStatus(cwd, logger, cache, useCache = true) {
  const cacheKey = `status:${cwd}`;
  const cached = useCache ? cache.get(cacheKey) : null;

  if (cached) {
    if (logger) logger.debug('[git] status cache hit');
    return { ...cached, cached: true };
  }

  const result = await executeGitCommand(['status', '--porcelain'], { cwd, logger });
  const normalized = normalizeStatusResponse(result.stdout);

  if (useCache) {
    cache.set(cacheKey, normalized);
  }

  return { ...normalized, cached: false };
}

/**
 * Handle 'log' operation
 *
 * @param {string} cwd - Repository path
 * @param {number} count - Number of commits to retrieve
 * @param {*} logger - Logger instance
 * @param {GitOperationCache} cache - Cache instance
 * @param {boolean} useCache - Whether to use cache
 * @returns {Promise<Object>} - Git log response
 */
async function handleLog(cwd, count = 10, logger = null, cache = null, useCache = true) {
  const cacheKey = `log:${cwd}:${count}`;
  const cached = useCache ? cache?.get(cacheKey) : null;

  if (cached) {
    if (logger) logger.debug('[git] log cache hit');
    return { ...cached, cached: true };
  }

  const format = '%H|%an|%s|%ai';
  const result = await executeGitCommand(
    ['log', '-n', count.toString(), `--pretty=format:${format}`],
    { cwd, logger }
  );
  const normalized = normalizeLogResponse(result.stdout);

  if (useCache && cache) {
    cache.set(cacheKey, normalized);
  }

  return { ...normalized, cached: false };
}

/**
 * Handle 'branches' operation
 *
 * @param {string} cwd - Repository path
 * @param {*} logger - Logger instance
 * @param {GitOperationCache} cache - Cache instance
 * @param {boolean} useCache - Whether to use cache
 * @returns {Promise<Object>} - Git branches response
 */
async function handleBranches(cwd, logger = null, cache = null, useCache = true) {
  const cacheKey = `branches:${cwd}`;
  const cached = useCache ? cache?.get(cacheKey) : null;

  if (cached) {
    if (logger) logger.debug('[git] branches cache hit');
    return { ...cached, cached: true };
  }

  // Get current branch
  const currentResult = await executeGitCommand(
    ['rev-parse', '--abbrev-ref', 'HEAD'],
    { cwd, logger }
  );
  const currentBranch = currentResult.stdout;

  // Get all branches
  const branchResult = await executeGitCommand(
    ['branch', '-a'],
    { cwd, logger }
  );
  const normalized = normalizeBranchesResponse(branchResult.stdout, currentBranch);

  if (useCache && cache) {
    cache.set(cacheKey, normalized);
  }

  return { ...normalized, cached: false };
}

/**
 * Handle 'diff' operation
 *
 * @param {string} cwd - Repository path
 * @param {string} filePath - File to diff
 * @param {string} baseRef - Base reference (default 'HEAD')
 * @param {*} logger - Logger instance
 * @param {GitOperationCache} cache - Cache instance
 * @param {boolean} useCache - Whether to use cache
 * @returns {Promise<Object>} - Git diff response
 */
async function handleDiff(cwd, filePath, baseRef = 'HEAD', logger = null, cache = null, useCache = false) {
  // Note: diffs are typically not cached (always fresh)
  const cacheKey = `diff:${cwd}:${filePath}:${baseRef}`;
  const cached = useCache ? cache?.get(cacheKey) : null;

  if (cached) {
    if (logger) logger.debug('[git] diff cache hit');
    return { ...cached, cached: true };
  }

  if (!filePath) {
    throw new GitValidationError('filePath', 'must not be empty');
  }

  const result = await executeGitCommand(
    ['diff', baseRef, '--', filePath],
    { cwd, logger }
  );
  const normalized = normalizeDiffResponse(result.stdout, filePath);

  if (useCache && cache) {
    cache.set(cacheKey, normalized);
  }

  return { ...normalized, cached: false };
}

/**
 * Handle 'currentBranch' operation
 *
 * @param {string} cwd - Repository path
 * @param {*} logger - Logger instance
 * @param {GitOperationCache} cache - Cache instance
 * @param {boolean} useCache - Whether to use cache
 * @returns {Promise<Object>} - Current branch response
 */
async function handleCurrentBranch(cwd, logger = null, cache = null, useCache = true) {
  const cacheKey = `currentBranch:${cwd}`;
  const cached = useCache ? cache?.get(cacheKey) : null;

  if (cached) {
    if (logger) logger.debug('[git] currentBranch cache hit');
    return { ...cached, cached: true };
  }

  const result = await executeGitCommand(
    ['rev-parse', '--abbrev-ref', 'HEAD'],
    { cwd, logger }
  );
  const normalized = { branch: result.stdout };

  if (useCache && cache) {
    cache.set(cacheKey, normalized);
  }

  return { ...normalized, cached: false };
}

// ============================================================================
// HANDLER FACTORY
// ============================================================================

/**
 * Factory function to create git-integration handler
 *
 * @param {Object} options - Configuration
 * @param {number} [options.cacheTtl] - Cache TTL in ms (default 3000)
 * @param {*} [options.logger] - Logger instance
 * @param {*} [options.metrics] - Metrics instance
 * @returns {Function} - Async handler function
 */
export function createGitIntegrationHandler(options = {}) {
  const { cacheTtl = 3000, logger = null, metrics = null } = options;
  const cache = new GitOperationCache(cacheTtl);

  /**
   * Main handler function
   *
   * @param {Object} message - Bridge message
   * @param {Object} context - Handler context
   * @returns {Promise<Object>} - Response object
   */
  return async (message, context) => {
    const startTime = Date.now();

    try {
      // Validate message structure
      if (!message.data) {
        throw new GitValidationError('data', 'message.data is required');
      }

      const { operation, cwd, cache: useCache = true, params = {} } = message.data;

      // Validate required fields
      if (!operation) {
        throw new GitValidationError('operation', 'must be one of: status, log, branches, diff, currentBranch');
      }

      if (!cwd) {
        throw new GitValidationError('cwd', 'working directory path is required');
      }

      // Route to operation handler
      let result;
      switch (operation) {
        case 'status':
          result = await handleStatus(cwd, logger, cache, useCache);
          break;

        case 'log':
          result = await handleLog(cwd, params.count || 10, logger, cache, useCache);
          break;

        case 'branches':
          result = await handleBranches(cwd, logger, cache, useCache);
          break;

        case 'diff':
          result = await handleDiff(
            cwd,
            params.filePath || '',
            params.baseRef || 'HEAD',
            logger,
            cache,
            useCache
          );
          break;

        case 'currentBranch':
          result = await handleCurrentBranch(cwd, logger, cache, useCache);
          break;

        default:
          throw new GitValidationError(
            'operation',
            `unsupported operation: ${operation}. Valid: status, log, branches, diff, currentBranch`
          );
      }

      const durationMs = Date.now() - startTime;

      // Track metrics
      if (metrics) {
        metrics.recordOperation('git', operation, durationMs, 'success');
        metrics.recordCacheHit('git', result.cached ? 'hit' : 'miss');
      }

      if (logger) {
        logger.debug(`[git] ${operation} completed in ${durationMs}ms`, { cached: result.cached });
      }

      // Remove cached flag from returned result, include it in metadata
      const { cached, ...cleanResult } = result;

      return {
        success: true,
        result: cleanResult,
        metadata: {
          timestamp: new Date().toISOString(),
          cached,
          durationMs,
          cacheStats: cache.getStats(),
        },
      };
    } catch (error) {
      const durationMs = Date.now() - startTime;

      // Track error metrics
      if (metrics) {
        metrics.recordOperation(
          'git',
          message.data?.operation || 'unknown',
          durationMs,
          'error'
        );
      }

      if (logger) {
        logger.error(`[git] operation failed: ${error.message}`, {
          code: error.code,
          operation: message.data?.operation,
        });
      }

      return {
        success: false,
        error: {
          code: error.code || 'GIT_ERROR',
          message: error.message,
          details: error.details || null,
        },
        metadata: {
          timestamp: new Date().toISOString(),
          durationMs,
        },
      };
    }
  };
}

/**
 * Default handler export for direct use
 */
export const gitIntegrationHandler = createGitIntegrationHandler();

/**
 * Export for backward compatibility
 */
export default createGitIntegrationHandler;
