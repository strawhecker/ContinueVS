/**
 * baseline-manager.mjs
 * Step 98: Baseline Persistence & Versioning
 * 
 * Manages baseline storage, versioning, checksums, and comparison logic.
 */

import fs from 'fs';
import path from 'path';
import { createHash } from 'crypto';
import { fileURLToPath } from 'url';
import { PerformanceError } from './performance-test-framework.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Baseline data structure
 */
export class Baseline {
  constructor(data) {
    this.version = data.version;
    this.schema = data.schema || '1.0';
    this.timestamp = data.timestamp;
    this.environment = data.environment;
    this.systemChecks = data.systemChecks;
    this.handlers = data.handlers;
    this.checksum = data.checksum;
  }

  isValid() {
    return this.version && this.timestamp && this.handlers && this.checksum;
  }
}

/**
 * Baseline manager: persist, load, and compare baselines
 */
export class BaselineManager {
  constructor(options = {}) {
    this.baselineDir = options.baselineDir || this._getDefaultBaselineDir();
    this.logger = options.logger;
  }

  /**
   * Get default baseline directory: ~/.continue/baselines/
   */
  _getDefaultBaselineDir() {
    const home = process.env.USERPROFILE || process.env.HOME;
    return path.join(home, '.continue', 'baselines');
  }

  /**
   * Save current metrics as a versioned baseline
   */
  async saveBaseline(metrics, metadata = {}) {
    const baseline = {
      version: metadata.version || '2.0.0',
      schema: '1.0',
      timestamp: Date.now(),
      environment: metadata.environment || {},
      systemChecks: metadata.systemChecks || {},
      handlers: metrics,
      checksum: null
    };

    // Compute checksum for integrity validation
    const contentForChecksum = JSON.stringify(baseline, null, 2);
    baseline.checksum = createHash('sha256').update(contentForChecksum).digest('hex');

    // Create filename: baseline-v2.0.0-2024-01-15T10-30-00.json
    const isoDate = new Date(baseline.timestamp).toISOString()
      .split('T')[0]; // YYYY-MM-DD
    const timeStr = new Date(baseline.timestamp).toISOString()
      .split('T')[1]
      .replace(/[:.]/g, '-')
      .substring(0, 8); // HH-MM-SS

    const filename = `baseline-v${baseline.version}-${isoDate}T${timeStr}.json`;
    const filepath = path.join(this.baselineDir, filename);

    // Ensure directory exists
    await fs.promises.mkdir(this.baselineDir, { recursive: true });

    // Write baseline file
    const fullContent = JSON.stringify(baseline, null, 2);
    await fs.promises.writeFile(filepath, fullContent, 'utf-8');

    this.logger?.log?.(`Baseline saved: ${filepath}`);

    // Prune old baselines (keep last 5)
    await this._pruneOldBaselines(baseline.version, 5);

    return {
      filepath,
      version: baseline.version,
      timestamp: baseline.timestamp,
      checksum: baseline.checksum,
      filename
    };
  }

  /**
   * Load baseline from file
   */
  async loadBaseline(version = '2.0.0', environment = 'local') {
    try {
      const files = await fs.promises.readdir(this.baselineDir);
      const matching = files
        .filter(f => f.startsWith(`baseline-v${version}`))
        .sort()
        .reverse(); // Latest first

      if (matching.length === 0) {
        return null;
      }

      const filepath = path.join(this.baselineDir, matching[0]);
      const content = await fs.promises.readFile(filepath, 'utf-8');
      const baseline = JSON.parse(content);

      // Verify checksum
      const storedChecksum = baseline.checksum;
      delete baseline.checksum;
      const contentForVerification = JSON.stringify(baseline, null, 2);
      const computedChecksum = createHash('sha256')
        .update(contentForVerification)
        .digest('hex');

      if (storedChecksum !== computedChecksum) {
        throw new PerformanceError(
          `Baseline checksum mismatch: ${filepath}`,
          { stored: storedChecksum, computed: computedChecksum }
        );
      }

      baseline.checksum = storedChecksum;

      this.logger?.log?.(`Baseline loaded: ${filepath}`);
      return new Baseline(baseline);
    } catch (err) {
      if (err.code === 'ENOENT') {
        return null;
      }
      throw err;
    }
  }

  /**
   * Get baseline for comparison (auto-select latest)
   */
  async getBaselineForComparison(options = {}) {
    const version = options.version || '2.0.0';
    return this.loadBaseline(version);
  }

  /**
   * List all available baselines
   */
  async listBaselines() {
    try {
      const files = await fs.promises.readdir(this.baselineDir);
      const baselineFiles = files.filter(f => f.startsWith('baseline-v'));

      const infos = await Promise.all(
        baselineFiles.map(async (filename) => {
          const filepath = path.join(this.baselineDir, filename);
          const stat = await fs.promises.stat(filepath);
          const content = await fs.promises.readFile(filepath, 'utf-8');
          const data = JSON.parse(content);

          return {
            filename,
            filepath,
            version: data.version,
            timestamp: data.timestamp,
            handlers: Object.keys(data.handlers || {}).length,
            createdAt: stat.birthtime
          };
        })
      );

      return infos.sort((a, b) => b.timestamp - a.timestamp);
    } catch (err) {
      if (err.code === 'ENOENT') {
        return [];
      }
      throw err;
    }
  }

  /**
   * Delete old baselines, keeping only the most recent N
   */
  async _pruneOldBaselines(version, keepCount = 5) {
    try {
      const files = await fs.promises.readdir(this.baselineDir);
      const matching = files
        .filter(f => f.startsWith(`baseline-v${version}`))
        .sort()
        .reverse();

      const toDelete = matching.slice(keepCount);
      let deletedCount = 0;

      for (const filename of toDelete) {
        const filepath = path.join(this.baselineDir, filename);
        await fs.promises.unlink(filepath);
        this.logger?.log?.(`Deleted old baseline: ${filename}`);
        deletedCount++;
      }

      return deletedCount;
    } catch (err) {
      this.logger?.error?.(`Error pruning baselines: ${err.message}`);
      return 0;
    }
  }

  /**
   * Delete all baselines (cleanup)
   */
  async pruneBaselines(keepCount = 5) {
    try {
      const allBaselines = await this.listBaselines();
      const versioned = new Map();

      // Group by version
      for (const baseline of allBaselines) {
        if (!versioned.has(baseline.version)) {
          versioned.set(baseline.version, []);
        }
        versioned.get(baseline.version).push(baseline);
      }

      let totalDeleted = 0;

      // Prune each version
      for (const [version, baselines] of versioned) {
        const toDelete = baselines.slice(keepCount);
        for (const baseline of toDelete) {
          await fs.promises.unlink(baseline.filepath);
          totalDeleted++;
        }
      }

      return totalDeleted;
    } catch (err) {
      this.logger?.error?.(`Error in pruneBaselines: ${err.message}`);
      return 0;
    }
  }
}

/**
 * Factory function
 */
export function createBaselineManager(options = {}) {
  return new BaselineManager(options);
}
