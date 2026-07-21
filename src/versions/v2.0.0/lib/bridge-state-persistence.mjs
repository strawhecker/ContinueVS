/**
 * Bridge State Persistence Module
 * 
 * Manages persistence and recovery of bridge runtime state.
 * Saves/loads handler statuses, subscription maps, pending work,
 * and initialization progress to ~/.continue/bridge-state.json
 * 
 * Optional feature: bridge works without persisted state (graceful degradation).
 * Persisted state is best-effort only—not critical for correctness.
 * 
 * Related Steps: 45 (lifecycle shutdown), 103 (crash recovery), 104 (config),
 *                101 (metrics), 110 (E2E), 112 (regression), 115 (Part III gate)
 */

import fs from 'fs';
import path from 'path';
import os from 'os';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

/**
 * BridgeStateCheckpoint - Represents a point-in-time snapshot of bridge state.
 * 
 * Schema:
 * - timestamp: ISO 8601 UTC when checkpoint was created
 * - phase: current bridge initialization phase (bootstrap, connected, subscribed, ready, degraded)
 * - handlers: map of handler name → {status: 'active'|'idle'|'error', errorCount: number, timeoutCount: number}
 * - subscriptions: {count: number, types: string[]} - count of active subscriptions and event type names
 * - pendingRequests: {count: number} - count of pending RPC messages awaiting response
 * - uptime: seconds bridge has been running
 * - bridgeVersion: semantic version string (e.g., "2.0.0")
 */
class BridgeStateCheckpoint {
  constructor(data = {}) {
    this.timestamp = data.timestamp || new Date().toISOString();
    this.phase = data.phase || 'bootstrap'; // bootstrap, connected, subscribed, ready, degraded
    this.handlers = data.handlers || {};
    this.subscriptions = data.subscriptions || { count: 0, types: [] };
    this.pendingRequests = data.pendingRequests || { count: 0 };
    this.uptime = data.uptime || 0;
    this.bridgeVersion = data.bridgeVersion || '2.0.0';
  }

  /**
   * Validate checkpoint structure and required fields.
   * @returns {boolean} true if valid, false otherwise
   * @throws {Error} if validation fails in strict mode
   */
  validate(strict = false) {
    const errors = [];

    // Required fields
    if (!this.timestamp || typeof this.timestamp !== 'string') {
      errors.push('Missing or invalid timestamp (must be ISO 8601)');
    }
    if (!this.phase || typeof this.phase !== 'string') {
      errors.push('Missing or invalid phase (must be string)');
    }
    if (!['bootstrap', 'connected', 'subscribed', 'ready', 'degraded'].includes(this.phase)) {
      errors.push(`Invalid phase value: ${this.phase}`);
    }
    if (typeof this.handlers !== 'object' || this.handlers === null) {
      errors.push('handlers must be an object');
    }
    if (typeof this.subscriptions !== 'object' || !('count' in this.subscriptions)) {
      errors.push('subscriptions must be an object with count property');
    }
    if (typeof this.pendingRequests !== 'object' || !('count' in this.pendingRequests)) {
      errors.push('pendingRequests must be an object with count property');
    }
    if (typeof this.uptime !== 'number' || this.uptime < 0) {
      errors.push('uptime must be a non-negative number');
    }

    // Validate handlers structure
    for (const [name, handlerState] of Object.entries(this.handlers)) {
      if (!handlerState.status || !['active', 'idle', 'error'].includes(handlerState.status)) {
        errors.push(`Handler ${name}: invalid status ${handlerState.status}`);
      }
      if (typeof handlerState.errorCount !== 'number' || handlerState.errorCount < 0) {
        errors.push(`Handler ${name}: errorCount must be non-negative number`);
      }
      if (typeof handlerState.timeoutCount !== 'number' || handlerState.timeoutCount < 0) {
        errors.push(`Handler ${name}: timeoutCount must be non-negative number`);
      }
    }

    if (errors.length > 0) {
      if (strict) {
        throw new Error(`Checkpoint validation failed: ${errors.join('; ')}`);
      }
      return false;
    }
    return true;
  }

  /**
   * Check if checkpoint is stale (older than maxAgeMs).
   * @param {number} maxAgeMs - maximum age in milliseconds (default 7 days)
   * @returns {boolean} true if older than maxAgeMs
   */
  isStale(maxAgeMs = 7 * 24 * 60 * 60 * 1000) {
    try {
      const checkpointTime = new Date(this.timestamp);
      const ageMs = Date.now() - checkpointTime.getTime();
      return ageMs > maxAgeMs;
    } catch {
      return true; // treat invalid timestamp as stale
    }
  }

  /**
   * Serialize checkpoint to JSON-compatible object.
   * @returns {object}
   */
  toJSON() {
    return {
      timestamp: this.timestamp,
      phase: this.phase,
      handlers: this.handlers,
      subscriptions: this.subscriptions,
      pendingRequests: this.pendingRequests,
      uptime: this.uptime,
      bridgeVersion: this.bridgeVersion,
    };
  }

  /**
   * Deserialize checkpoint from JSON object.
   * @param {object} json
   * @returns {BridgeStateCheckpoint}
   */
  static fromJSON(json) {
    return new BridgeStateCheckpoint(json);
  }
}

/**
 * BridgeStatePersistence - Orchestrator for saving/loading bridge state.
 * 
 * Provides async operations for checkpoint creation and recovery.
 * Gracefully handles file errors, corruption, and missing files.
 */
class BridgeStatePersistence {
  constructor(options = {}) {
    this.stateDir = options.stateDir || path.join(os.homedir(), '.continue');
    this.stateFile = options.stateFile || path.join(this.stateDir, 'bridge-state.json');
    this.logger = options.logger || null;
    this.metrics = options.metrics || null;
    this.maxAgeMs = options.maxAgeMs || 7 * 24 * 60 * 60 * 1000; // 7 days
  }

  /**
   * Log a message via logger if available.
   * @private
   */
  _log(level, message) {
    if (this.logger && typeof this.logger.log === 'function') {
      this.logger.log(level, message);
    }
  }

  /**
   * Record a metric if metrics collector available.
   * @private
   */
  _recordMetric(name, value) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(name, value);
    }
  }

  /**
   * Save a checkpoint to disk.
   * Creates directory if needed. Writes atomically (write to temp, then rename).
   * Performance gate: <500ms
   * 
   * @param {BridgeStateCheckpoint} checkpoint - state to persist
   * @returns {Promise<boolean>} true if success, false if error
   */
  async saveAsync(checkpoint) {
    const startTime = Date.now();
    try {
      // Validate checkpoint
      if (!checkpoint.validate(false)) {
        this._log('warn', `[BridgeStatePersistence] Checkpoint validation failed, skipping save`);
        return false;
      }

      // Ensure directory exists
      if (!fs.existsSync(this.stateDir)) {
        fs.mkdirSync(this.stateDir, { recursive: true });
      }

      // Write to temporary file first (atomic write pattern)
      const tempFile = `${this.stateFile}.tmp`;
      const jsonData = JSON.stringify(checkpoint.toJSON(), null, 2);

      await fs.promises.writeFile(tempFile, jsonData, 'utf8');

      // Atomically rename temp to target
      await fs.promises.rename(tempFile, this.stateFile);

      const duration = Date.now() - startTime;
      this._log('info', `[BridgeStatePersistence] Checkpoint saved in ${duration}ms`);
      this._recordMetric('bridge.state.save.duration_ms', duration);

      if (duration > 500) {
        this._log('warn', `[BridgeStatePersistence] Save took ${duration}ms (exceeds 500ms gate)`);
      }

      return true;
    } catch (error) {
      this._log('error', `[BridgeStatePersistence] Failed to save checkpoint: ${error.message}`);
      this._recordMetric('bridge.state.save.error', 1);
      return false;
    }
  }

  /**
   * Load a checkpoint from disk.
   * Returns null if file missing, corrupted, or stale.
   * Performance gate: <200ms
   * 
   * @returns {Promise<BridgeStateCheckpoint|null>} checkpoint or null
   */
  async loadAsync() {
    const startTime = Date.now();
    try {
      // Check if file exists
      if (!fs.existsSync(this.stateFile)) {
        this._log('debug', `[BridgeStatePersistence] No checkpoint file found at ${this.stateFile}`);
        return null;
      }

      // Read file
      const jsonData = await fs.promises.readFile(this.stateFile, 'utf8');
      const parsed = JSON.parse(jsonData);

      // Reconstruct checkpoint
      const checkpoint = BridgeStateCheckpoint.fromJSON(parsed);

      // Validate structure
      if (!checkpoint.validate(false)) {
        this._log('warn', `[BridgeStatePersistence] Checkpoint failed validation, discarding`);
        this._recordMetric('bridge.state.load.validation_error', 1);
        return null;
      }

      // Check staleness
      if (checkpoint.isStale(this.maxAgeMs)) {
        this._log('info', `[BridgeStatePersistence] Checkpoint is stale (>7 days), discarding`);
        this._recordMetric('bridge.state.load.stale', 1);
        return null;
      }

      const duration = Date.now() - startTime;
      this._log('info', `[BridgeStatePersistence] Checkpoint loaded in ${duration}ms`);
      this._recordMetric('bridge.state.load.duration_ms', duration);

      if (duration > 200) {
        this._log('warn', `[BridgeStatePersistence] Load took ${duration}ms (exceeds 200ms gate)`);
      }

      return checkpoint;
    } catch (error) {
      // Gracefully handle corruption: JSON parse error, file read error, etc.
      this._log('warn', `[BridgeStatePersistence] Failed to load checkpoint: ${error.message}`);
      this._recordMetric('bridge.state.load.error', 1);
      return null;
    }
  }

  /**
   * Delete the checkpoint file.
   * Used for cleanup or resetting bridge state.
   * 
   * @returns {Promise<boolean>} true if success or file didn't exist
   */
  async deleteAsync() {
    try {
      if (fs.existsSync(this.stateFile)) {
        await fs.promises.unlink(this.stateFile);
        this._log('info', `[BridgeStatePersistence] Checkpoint deleted`);
      }
      return true;
    } catch (error) {
      this._log('error', `[BridgeStatePersistence] Failed to delete checkpoint: ${error.message}`);
      return false;
    }
  }
}

/**
 * Factory function to create BridgeStatePersistence instance.
 * 
 * @param {object} options - configuration object
 * @param {string} options.stateDir - directory for state files (default: ~/.continue)
 * @param {object} options.logger - optional IBridgeLogger instance
 * @param {object} options.metrics - optional IBridgeTelemetryCollector instance
 * @returns {BridgeStatePersistence}
 */
export function createBridgeStatePersistence(options = {}) {
  return new BridgeStatePersistence(options);
}

/**
 * Export classes for testing and direct instantiation.
 */
export { BridgeStateCheckpoint, BridgeStatePersistence };
