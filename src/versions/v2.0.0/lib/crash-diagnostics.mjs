/**
 * Crash Diagnostics Collector
 * 
 * Captures diagnostic state when bridge crashes occur, including:
 * - Bridge version and configuration
 * - Handler registry status
 * - Recent logs (last 100 entries)
 * - Error traces and stack information
 * - Timestamped artifact storage to ~/.continue/crash-diagnostics
 */

import { promises as fs } from 'fs';
import { resolve } from 'path';
import { homedir } from 'os';

/**
 * Error class for diagnostics operations
 */
export class CrashDiagnosticsError extends Error {
  constructor(message, originalError = null) {
    super(message);
    this.name = 'CrashDiagnosticsError';
    this.originalError = originalError;
  }
}

/**
 * Diagnostic snapshot with bridge state information
 */
export class DiagnosticSnapshot {
  constructor({
    timestamp = Date.now(),
    bridgeVersion = null,
    nodeVersion = null,
    handlerRegistry = [],
    recentLogs = [],
    errorTraces = [],
    bridgeState = {},
    contextInfo = {},
  } = {}) {
    this.timestamp = timestamp;
    this.bridgeVersion = bridgeVersion;
    this.nodeVersion = nodeVersion;
    this.handlerRegistry = handlerRegistry;
    this.recentLogs = recentLogs;
    this.errorTraces = errorTraces;
    this.bridgeState = bridgeState;
    this.contextInfo = contextInfo;
  }

  /**
   * Convert to JSON-serializable object
   */
  toJSON() {
    return {
      timestamp: this.timestamp,
      bridgeVersion: this.bridgeVersion,
      nodeVersion: this.nodeVersion,
      handlerRegistry: this.handlerRegistry,
      recentLogs: this.recentLogs,
      errorTraces: this.errorTraces,
      bridgeState: this.bridgeState,
      contextInfo: this.contextInfo,
    };
  }

  /**
   * Generate human-readable diagnostic report
   */
  toReport() {
    const lines = [];
    lines.push('='.repeat(80));
    lines.push('CRASH DIAGNOSTIC REPORT');
    lines.push('='.repeat(80));
    lines.push('');

    lines.push('TIMESTAMP INFORMATION');
    lines.push('-'.repeat(40));
    lines.push(`Crash Time: ${new Date(this.timestamp).toISOString()}`);
    lines.push('');

    lines.push('ENVIRONMENT');
    lines.push('-'.repeat(40));
    lines.push(`Bridge Version: ${this.bridgeVersion || 'unknown'}`);
    lines.push(`Node.js Version: ${this.nodeVersion || 'unknown'}`);
    lines.push('');

    lines.push('HANDLER REGISTRY STATUS');
    lines.push('-'.repeat(40));
    if (this.handlerRegistry.length === 0) {
      lines.push('No handlers in registry');
    } else {
      for (const handler of this.handlerRegistry) {
        const status = handler.isActive ? '✓ ACTIVE' : '✗ INACTIVE';
        lines.push(`  [${status}] ${handler.handlerId} - Errors: ${handler.errorCount}`);
      }
    }
    lines.push('');

    lines.push('RECENT LOGS (Last 100 entries)');
    lines.push('-'.repeat(40));
    if (this.recentLogs.length === 0) {
      lines.push('No logs available');
    } else {
      for (const log of this.recentLogs.slice(-20)) {
        const time = new Date(log.timestamp).toISOString();
        lines.push(`[${time}] ${log.level.toUpperCase()}: ${log.message}`);
      }
      if (this.recentLogs.length > 20) {
        lines.push(`... (${this.recentLogs.length - 20} more entries)`);
      }
    }
    lines.push('');

    lines.push('ERROR TRACES');
    lines.push('-'.repeat(40));
    if (this.errorTraces.length === 0) {
      lines.push('No error traces captured');
    } else {
      for (let i = 0; i < this.errorTraces.length; i++) {
        lines.push(`Trace ${i + 1}:`);
        lines.push(this.errorTraces[i].substring(0, 500));
        if (this.errorTraces[i].length > 500) {
          lines.push('... (truncated)');
        }
        lines.push('');
      }
    }

    lines.push('BRIDGE STATE');
    lines.push('-'.repeat(40));
    lines.push(JSON.stringify(this.bridgeState, null, 2));
    lines.push('');

    lines.push('CONTEXT INFORMATION');
    lines.push('-'.repeat(40));
    lines.push(JSON.stringify(this.contextInfo, null, 2));
    lines.push('');

    lines.push('='.repeat(80));
    lines.push('END OF REPORT');
    lines.push('='.repeat(80));

    return lines.join('\n');
  }

  /**
   * Create from JSON object
   */
  static fromJSON(data) {
    return new DiagnosticSnapshot(data);
  }
}

/**
 * Crash Diagnostics Collector - Main orchestrator
 */
export class CrashDiagnosticsCollector {
  constructor({
    logger = null,
    metrics = null,
  } = {}) {
    this.logger = logger;
    this.metrics = metrics;
    this.diagnosticsDir = resolve(homedir(), '.continue', 'crash-diagnostics');
  }

  /**
   * Initialize diagnostics directory
   */
  async initialize() {
    try {
      await fs.mkdir(this.diagnosticsDir, { recursive: true });
      this._logDebug('Diagnostics directory initialized');
    } catch (error) {
      throw new CrashDiagnosticsError(
        `Failed to initialize diagnostics directory: ${error.message}`,
        error
      );
    }
  }

  /**
   * Capture complete diagnostic snapshot
   */
  async captureDiagnosticSnapshot({
    bridgeVersion = null,
    handlerRegistry = null,
    bridgeLogger = null,
    bridgeState = {},
    contextInfo = {},
  } = {}) {
    try {
      const nodeVersion = process.version;
      const recentLogs = this._collectRecentLogs(bridgeLogger);
      const errorTraces = this._collectErrorTraces(bridgeLogger);
      const handlerStatus = this._buildHandlerRegistry(handlerRegistry);

      const snapshot = new DiagnosticSnapshot({
        timestamp: Date.now(),
        bridgeVersion,
        nodeVersion,
        handlerRegistry: handlerStatus,
        recentLogs,
        errorTraces,
        bridgeState,
        contextInfo,
      });

      this._logDebug('Diagnostic snapshot captured successfully');
      this._recordMetric('diagnostics.snapshot_captured', 1);

      return snapshot;
    } catch (error) {
      this._logError(`Failed to capture diagnostic snapshot: ${error.message}`);
      throw new CrashDiagnosticsError(
        `Failed to capture diagnostic snapshot: ${error.message}`,
        error
      );
    }
  }

  /**
   * Persist diagnostic snapshot to file
   */
  async persistDiagnosticSnapshot(snapshot) {
    try {
      const timestamp = new Date(snapshot.timestamp).toISOString().replace(/[:.]/g, '-');
      const filename = `crash-${timestamp}.json`;
      const filepath = resolve(this.diagnosticsDir, filename);

      await fs.writeFile(
        filepath,
        JSON.stringify(snapshot.toJSON(), null, 2),
        'utf-8'
      );

      this._logDebug(`Diagnostic snapshot persisted to ${filename}`);
      this._recordMetric('diagnostics.snapshot_persisted', 1);

      return filepath;
    } catch (error) {
      this._logError(`Failed to persist diagnostic snapshot: ${error.message}`);
      throw new CrashDiagnosticsError(
        `Failed to persist diagnostic snapshot: ${error.message}`,
        error
      );
    }
  }

  /**
   * Persist human-readable diagnostic report
   */
  async persistDiagnosticReport(snapshot) {
    try {
      const timestamp = new Date(snapshot.timestamp).toISOString().replace(/[:.]/g, '-');
      const filename = `crash-${timestamp}-report.txt`;
      const filepath = resolve(this.diagnosticsDir, filename);

      const report = snapshot.toReport();
      await fs.writeFile(filepath, report, 'utf-8');

      this._logDebug(`Diagnostic report persisted to ${filename}`);
      this._recordMetric('diagnostics.report_persisted', 1);

      return filepath;
    } catch (error) {
      this._logError(`Failed to persist diagnostic report: ${error.message}`);
      throw new CrashDiagnosticsError(
        `Failed to persist diagnostic report: ${error.message}`,
        error
      );
    }
  }

  /**
   * Capture and persist diagnostics (complete workflow)
   */
  async captureAndPersist(captureOptions) {
    try {
      const snapshot = await this.captureDiagnosticSnapshot(captureOptions);
      const jsonPath = await this.persistDiagnosticSnapshot(snapshot);
      const reportPath = await this.persistDiagnosticReport(snapshot);

      this._logDebug(`Crash diagnostics captured and persisted`);
      this._recordMetric('diagnostics.capture_and_persist', 1);

      return {
        snapshot,
        jsonPath,
        reportPath,
      };
    } catch (error) {
      this._logError(`Failed to capture and persist diagnostics: ${error.message}`);
      throw new CrashDiagnosticsError(
        `Failed to capture and persist diagnostics: ${error.message}`,
        error
      );
    }
  }

  /**
   * Clean old diagnostic artifacts (>7 days)
   */
  async cleanOldDiagnostics(maxAgeMs = 7 * 24 * 60 * 60 * 1000) {
    try {
      const files = await fs.readdir(this.diagnosticsDir);
      const now = Date.now();
      let deletedCount = 0;

      for (const file of files) {
        const filepath = resolve(this.diagnosticsDir, file);
        const stat = await fs.stat(filepath);
        const age = now - stat.mtimeMs;

        if (age > maxAgeMs) {
          await fs.unlink(filepath);
          deletedCount++;
        }
      }

      this._logDebug(`Cleaned ${deletedCount} old diagnostic artifacts`);
      this._recordMetric('diagnostics.cleaned', deletedCount);

      return deletedCount;
    } catch (error) {
      this._logError(`Failed to clean old diagnostics: ${error.message}`);
      // Don't throw - cleanup is non-critical
      return 0;
    }
  }

  /**
   * Collect recent logs from bridge logger
   */
  _collectRecentLogs(bridgeLogger, maxEntries = 100) {
    if (!bridgeLogger) {
      return [];
    }

    try {
      // Attempt to get structured logs if available
      if (typeof bridgeLogger.getRecentLogs === 'function') {
        return bridgeLogger.getRecentLogs(maxEntries);
      }
      // Fallback: try to get log buffer
      if (Array.isArray(bridgeLogger.logs)) {
        return bridgeLogger.logs.slice(-maxEntries);
      }
    } catch (error) {
      this._logError(`Failed to collect recent logs: ${error.message}`);
    }

    return [];
  }

  /**
   * Collect error traces from bridge logger
   */
  _collectErrorTraces(bridgeLogger, maxTraces = 5) {
    if (!bridgeLogger) {
      return [];
    }

    try {
      // Attempt to get error traces if available
      if (typeof bridgeLogger.getErrorTraces === 'function') {
        return bridgeLogger.getErrorTraces(maxTraces);
      }
      // Fallback: try to get errors array
      if (Array.isArray(bridgeLogger.errors)) {
        return bridgeLogger.errors
          .map(e => e.stack || e.toString())
          .slice(-maxTraces);
      }
    } catch (error) {
      this._logError(`Failed to collect error traces: ${error.message}`);
    }

    return [];
  }

  /**
   * Build handler registry status from handler registry
   */
  _buildHandlerRegistry(handlerRegistry) {
    if (!handlerRegistry) {
      return [];
    }

    try {
      // Attempt to get handler status if available
      if (typeof handlerRegistry.getHandlerStatus === 'function') {
        return handlerRegistry.getHandlerStatus();
      }
      // Fallback: try to iterate handlers map
      if (typeof handlerRegistry.entries === 'function') {
        return Array.from(handlerRegistry.entries()).map(([id, handler]) => ({
          handlerId: id,
          isActive: handler.isActive !== false,
          pendingRequests: handler.pendingRequests || 0,
          errorCount: handler.errorCount || 0,
        }));
      }
    } catch (error) {
      this._logError(`Failed to build handler registry: ${error.message}`);
    }

    return [];
  }

  /**
   * Log debug message
   */
  _logDebug(message) {
    if (this.logger && typeof this.logger.debug === 'function') {
      this.logger.debug(message);
    }
  }

  /**
   * Log error message
   */
  _logError(message) {
    if (this.logger && typeof this.logger.error === 'function') {
      this.logger.error(message);
    }
  }

  /**
   * Record metric
   */
  _recordMetric(name, value) {
    if (this.metrics && typeof this.metrics.record === 'function') {
      this.metrics.record(name, value);
    }
  }
}

/**
 * Factory function to create diagnostics collector
 */
export function createCrashDiagnosticsCollector(options = {}) {
  return new CrashDiagnosticsCollector(options);
}
