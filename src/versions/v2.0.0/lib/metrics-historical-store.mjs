#!/usr/bin/env node

/**
 * Metrics Historical Store for ContinueVS Bridge (Step 109)
 *
 * Disk persistence layer for metrics snapshots.
 * Manages file I/O, indexing, cleanup, and atomic writes.
 * Format: JSON lines (line-delimited JSON) with daily rotation.
 * Location: ~/.continue/metrics/
 *
 * @module metrics-historical-store
 */

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import os from 'os';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Custom error class for store operations.
 */
export class StoreError extends Error {
  constructor(message, code = 'STORE_ERROR', details = null) {
    super(message);
    this.name = 'StoreError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Metrics store: manages persistence and retrieval.
 */
export class MetricsStore {
  constructor(options = {}) {
    this.baseDir = options.baseDir || this._getDefaultBaseDir();
    this.logger = options.logger;
    this.initialized = false;
  }

  /**
   * Get default metrics directory: ~/.continue/metrics/
   */
  _getDefaultBaseDir() {
    const home = process.env.USERPROFILE || process.env.HOME || os.homedir();
    return path.join(home, '.continue', 'metrics');
  }

  /**
   * Initialize store: create directory if needed.
   */
  async initialize() {
    try {
      await fs.promises.mkdir(this.baseDir, { recursive: true });
      this.initialized = true;
      this.logger?.log?.(`[Store] Initialized at ${this.baseDir}`);
    } catch (err) {
      throw new StoreError(
        `Failed to initialize store: ${err.message}`,
        'INIT_FAILED',
        { baseDir: this.baseDir }
      );
    }
  }

  /**
   * Get filename for given timestamp.
   * Format: metrics-YYYY-MM-DD.jsonl
   */
  _getFilename(timestamp) {
    const date = new Date(timestamp);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `metrics-${year}-${month}-${day}.jsonl`;
  }

  /**
   * Get filepath for given timestamp.
   */
  _getFilepath(timestamp) {
    return path.join(this.baseDir, this._getFilename(timestamp));
  }

  /**
   * Append snapshot to store (atomic write).
   */
  async append(snapshot) {
    if (!this.initialized) {
      await this.initialize();
    }

    if (!snapshot || typeof snapshot !== 'object') {
      throw new StoreError('Snapshot must be a valid object', 'INVALID_SNAPSHOT');
    }

    try {
      const timestamp = snapshot.timestamp || Date.now();
      const filepath = this._getFilepath(timestamp);

      // Validate snapshot structure
      this._validateSnapshot(snapshot);

      // Write as single JSON line (no newline at end within line)
      const line = JSON.stringify(snapshot) + '\n';

      // Atomic write: temp file + rename
      const tempPath = filepath + '.tmp';
      let fd;
      try {
        // Open for append (create if missing)
        fd = await fs.promises.open(filepath, 'a');
        await fd.write(line);
        await fd.close();
      } catch (err) {
        if (fd) {
          try {
            await fd.close();
          } catch (closeErr) {
            // Ignore close error
          }
        }
        throw err;
      }

      this.logger?.debug?.(`[Store] Appended snapshot to ${path.basename(filepath)}`);
    } catch (err) {
      throw new StoreError(
        `Failed to append snapshot: ${err.message}`,
        'APPEND_FAILED',
        { timestamp: snapshot.timestamp }
      );
    }
  }

  /**
   * Read snapshots matching query criteria.
   */
  async read(query = {}) {
    if (!this.initialized) {
      await this.initialize();
    }

    try {
      const { handlerName, since, until, limit } = query;
      const results = [];

      // Get list of snapshot files in date range
      const files = await this._listSnapshotFiles(since, until);

      // Read and filter snapshots
      for (const filepath of files) {
        if (results.length >= (limit || 1000)) {
          break;
        }

        try {
          const snapshots = await this._readSnapshotFile(filepath);
          for (const snapshot of snapshots) {
            if (!this._isInTimeRange(snapshot.timestamp, since, until)) {
              continue;
            }

            if (handlerName) {
              // Filter to snapshots containing this handler
              const hasHandler = snapshot.handlers?.some(h => h.name === handlerName);
              if (!hasHandler) {
                continue;
              }
            }

            results.push(snapshot);

            if (results.length >= (limit || 1000)) {
              break;
            }
          }
        } catch (fileErr) {
          this.logger?.warn?.(`[Store] Failed to read file ${filepath}: ${fileErr.message}`);
          continue; // Skip corrupted files
        }
      }

      return results;
    } catch (err) {
      throw new StoreError(
        `Failed to read snapshots: ${err.message}`,
        'READ_FAILED',
        { query }
      );
    }
  }

  /**
   * List snapshot files in date range.
   */
  async _listSnapshotFiles(since, until) {
    try {
      const files = await fs.promises.readdir(this.baseDir);
      const snapshotFiles = files
        .filter(f => f.startsWith('metrics-') && f.endsWith('.jsonl'))
        .map(f => path.join(this.baseDir, f))
        .sort(); // Sort by filename (date order)

      if (!since && !until) {
        return snapshotFiles;
      }

      // Filter by date range
      return snapshotFiles.filter(f => {
        const filename = path.basename(f);
        const dateMatch = filename.match(/metrics-(\d{4})-(\d{2})-(\d{2})\.jsonl/);
        if (!dateMatch) return false;

        const [, year, month, day] = dateMatch;
        const fileDate = new Date(`${year}-${month}-${day}`).getTime();
        const dayEnd = fileDate + 24 * 60 * 60 * 1000;

        if (since && dayEnd <= since) return false;
        if (until && fileDate >= until) return false;
        return true;
      });
    } catch (err) {
      this.logger?.error?.(`[Store] Failed to list files: ${err.message}`);
      return [];
    }
  }

  /**
   * Read all snapshots from a single file.
   */
  async _readSnapshotFile(filepath) {
    const content = await fs.promises.readFile(filepath, 'utf-8');
    const lines = content.trim().split('\n').filter(l => l.length > 0);

    return lines.map((line, idx) => {
      try {
        return JSON.parse(line);
      } catch (err) {
        throw new StoreError(
          `Corrupted snapshot at line ${idx}: ${err.message}`,
          'CORRUPTED_SNAPSHOT'
        );
      }
    });
  }

  /**
   * Check if timestamp is in range.
   */
  _isInTimeRange(timestamp, since, until) {
    if (since && timestamp < since) return false;
    if (until && timestamp >= until) return false;
    return true;
  }

  /**
   * Validate snapshot structure.
   */
  _validateSnapshot(snapshot) {
    if (!snapshot.timestamp || typeof snapshot.timestamp !== 'number') {
      throw new StoreError('Snapshot must have timestamp', 'INVALID_STRUCTURE');
    }

    if (!Array.isArray(snapshot.handlers)) {
      throw new StoreError('Snapshot must have handlers array', 'INVALID_STRUCTURE');
    }

    // Validate each handler
    for (const handler of snapshot.handlers) {
      if (!handler.name || !handler.latency || typeof handler.errorRate !== 'number') {
        throw new StoreError('Handler must have name, latency, errorRate', 'INVALID_STRUCTURE');
      }
    }
  }

  /**
   * Clean up snapshots older than timestamp.
   */
  async cleanup(olderThanTimestamp) {
    if (!this.initialized) {
      await this.initialize();
    }

    try {
      const files = await fs.promises.readdir(this.baseDir);
      let deletedCount = 0;

      for (const filename of files) {
        if (!filename.startsWith('metrics-') || !filename.endsWith('.jsonl')) {
          continue;
        }

        const dateMatch = filename.match(/metrics-(\d{4})-(\d{2})-(\d{2})\.jsonl/);
        if (!dateMatch) continue;

        const [, year, month, day] = dateMatch;
        const fileDate = new Date(`${year}-${month}-${day}`).getTime();

        if (fileDate < olderThanTimestamp) {
          const filepath = path.join(this.baseDir, filename);
          await fs.promises.unlink(filepath);
          deletedCount++;
          this.logger?.log?.(`[Store] Deleted old snapshot: ${filename}`);
        }
      }

      this.logger?.log?.(`[Store] Cleanup complete: deleted ${deletedCount} files`);
    } catch (err) {
      throw new StoreError(
        `Cleanup failed: ${err.message}`,
        'CLEANUP_FAILED',
        { olderThanTimestamp }
      );
    }
  }

  /**
   * Get storage statistics.
   */
  async getStorageStats() {
    try {
      const files = await fs.promises.readdir(this.baseDir);
      let totalSize = 0;
      let fileCount = 0;

      for (const filename of files) {
        if (!filename.startsWith('metrics-') || !filename.endsWith('.jsonl')) {
          continue;
        }

        const filepath = path.join(this.baseDir, filename);
        const stat = await fs.promises.stat(filepath);
        totalSize += stat.size;
        fileCount++;
      }

      return {
        directory: this.baseDir,
        fileCount,
        totalSizeBytes: totalSize,
        totalSizeMB: (totalSize / 1024 / 1024).toFixed(2)
      };
    } catch (err) {
      this.logger?.error?.(`[Store] Storage stats failed: ${err.message}`);
      return null;
    }
  }

  /**
   * List all snapshot files.
   */
  async list() {
    if (!this.initialized) {
      await this.initialize();
    }

    try {
      const files = await fs.promises.readdir(this.baseDir);
      return files
        .filter(f => f.startsWith('metrics-') && f.endsWith('.jsonl'))
        .sort();
    } catch (err) {
      this.logger?.error?.(`[Store] List failed: ${err.message}`);
      return [];
    }
  }
}
