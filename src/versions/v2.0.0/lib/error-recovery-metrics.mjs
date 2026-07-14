#!/usr/bin/env node

/**
 * Error Recovery Metrics Collectors (Step 74)
 *
 * Tracks error recovery metrics:
 * - Error rate over sliding window (detects systemic issues)
 * - Error type histogram (identifies patterns)
 * - Recovery success rate (measures effectiveness)
 *
 * @module src/versions/v2.0.0/lib/error-recovery-metrics.mjs
 * @version 1.0.0
 * @author Bridge Architecture Team
 *
 * Related Steps:
 *   - Step 26: IBridgeTelemetryCollector (integrates)
 *   - Step 74: ErrorRecoveryHook (records metrics)
 */

// ============================================================================
// ERROR RATE COLLECTOR
// ============================================================================

/**
 * Tracks error rate over sliding time window.
 *
 * Records errors and calculates rate: errorCount / totalRequests
 * Uses sliding 5-second window to detect sustained problems.
 *
 * @class ErrorRateCollector
 */
export class ErrorRateCollector {
  /**
   * @param {number} [windowMs=5000] - Sliding window duration in milliseconds
   */
  constructor(windowMs = 5000) {
    this.windowMs = windowMs;
    this.errorTimestamps = []; // Array of { timestamp, errorType }
    this.totalRequests = 0;
    this.lastCleanup = Date.now();
  }

  /**
   * Record an error occurrence.
   *
   * @param {string} errorType - Type of error ('validation', 'timeout', 'handler', etc.)
   * @param {string} [messageId] - Correlation ID (optional)
   */
  recordError(errorType, messageId = null) {
    const now = Date.now();
    this.errorTimestamps.push({
      timestamp: now,
      errorType,
      messageId,
    });

    // Cleanup old entries periodically
    if (now - this.lastCleanup > 1000) {
      this._cleanupOldEntries();
    }
  }

  /**
   * Record a successful request.
   */
  recordSuccess() {
    this.totalRequests++;
  }

  /**
   * Get current error rate.
   *
   * @returns {Object} { rate: 0-1, errorCount, totalCount, windowMs }
   */
  getErrorRate() {
    this._cleanupOldEntries();

    const rate = this.totalRequests > 0
      ? this.errorTimestamps.length / this.totalRequests
      : 0;

    return {
      rate,
      errorCount: this.errorTimestamps.length,
      totalCount: this.totalRequests,
      windowMs: this.windowMs,
    };
  }

  /**
   * Check if error rate exceeds threshold.
   *
   * @param {number} [threshold=0.01] - Threshold (default 1%)
   * @returns {boolean}
   */
  isAlertThresholdExceeded(threshold = 0.01) {
    const { rate } = this.getErrorRate();
    return rate > threshold;
  }

  /**
   * Clear all metrics.
   */
  clear() {
    this.errorTimestamps = [];
    this.totalRequests = 0;
    this.lastCleanup = Date.now();
  }

  /**
   * Remove entries older than window size.
   * @private
   */
  _cleanupOldEntries() {
    const now = Date.now();
    const cutoff = now - this.windowMs;

    this.errorTimestamps = this.errorTimestamps.filter(
      (entry) => entry.timestamp > cutoff
    );

    this.lastCleanup = now;
  }

  /**
   * Get error entries within current window.
   *
   * @returns {Array} Entries in window
   */
  getEntriesInWindow() {
    this._cleanupOldEntries();
    return [...this.errorTimestamps];
  }
}

// ============================================================================
// ERROR TYPE HISTOGRAM
// ============================================================================

/**
 * Tracks error frequency by type.
 *
 * Provides breakdown of:
 * - validation errors
 * - timeout errors
 * - handler errors
 * - recovery errors
 * - unknown errors
 *
 * @class ErrorTypeHistogram
 */
export class ErrorTypeHistogram {
  constructor() {
    this.counts = {
      validation: 0,
      timeout: 0,
      handler: 0,
      recovery: 0,
      alerting: 0,
      unknown: 0,
    };
    this.total = 0;
  }

  /**
   * Record error by type.
   *
   * @param {string} errorType - One of the counts keys
   */
  recordByType(errorType) {
    const normalized = errorType.toLowerCase();
    if (normalized in this.counts) {
      this.counts[normalized]++;
    } else {
      this.counts.unknown++;
    }
    this.total++;
  }

  /**
   * Get histogram as object.
   *
   * @returns {Object} { validation, timeout, handler, recovery, alerting, unknown, total }
   */
  getHistogram() {
    return {
      ...this.counts,
      total: this.total,
    };
  }

  /**
   * Get error type distribution as percentages.
   *
   * @returns {Object} { validation: %, timeout: %, ... }
   */
  getDistribution() {
    if (this.total === 0) {
      return { all: 0 };
    }

    const distribution = {};
    for (const [type, count] of Object.entries(this.counts)) {
      distribution[type] = (count / this.total) * 100;
    }
    return distribution;
  }

  /**
   * Get most common error type.
   *
   * @returns {string|null} Most frequent type or null if empty
   */
  getMostCommonType() {
    if (this.total === 0) {
      return null;
    }

    let max = 0;
    let maxType = null;

    for (const [type, count] of Object.entries(this.counts)) {
      if (count > max) {
        max = count;
        maxType = type;
      }
    }

    return maxType;
  }

  /**
   * Clear histogram.
   */
  clear() {
    this.counts = {
      validation: 0,
      timeout: 0,
      handler: 0,
      recovery: 0,
      alerting: 0,
      unknown: 0,
    };
    this.total = 0;
  }
}

// ============================================================================
// RECOVERY SUCCESS TRACKER
// ============================================================================

/**
 * Tracks recovery attempt outcomes.
 *
 * Records:
 * - Success/failure of recovery actions
 * - Delay introduced by recovery (latency cost)
 * - Error types that were recovered vs. terminal
 *
 * @class RecoverySuccessTracker
 */
export class RecoverySuccessTracker {
  constructor() {
    this.attempts = []; // Array of { success, errorType, delayMs, timestamp }
    this.totalAttempts = 0;
    this.successfulAttempts = 0;
    this.totalDelayMs = 0;
  }

  /**
   * Record recovery attempt.
   *
   * @param {boolean} success - Whether recovery succeeded
   * @param {string} errorType - Type of error that was recovered
   * @param {number} [delayMs=0] - Delay incurred by recovery action
   */
  recordRecoveryAttempt(success, errorType, delayMs = 0) {
    this.attempts.push({
      success,
      errorType,
      delayMs,
      timestamp: new Date().toISOString(),
    });

    this.totalAttempts++;
    if (success) {
      this.successfulAttempts++;
    }
    this.totalDelayMs += delayMs;
  }

  /**
   * Get recovery statistics.
   *
   * @returns {Object} { successRate: 0-1, totalAttempts, avgDelayMs }
   */
  getRecoveryStats() {
    const successRate =
      this.totalAttempts > 0
        ? this.successfulAttempts / this.totalAttempts
        : 0;

    const avgDelayMs =
      this.totalAttempts > 0
        ? this.totalDelayMs / this.totalAttempts
        : 0;

    return {
      successRate,
      totalAttempts: this.totalAttempts,
      successfulAttempts: this.successfulAttempts,
      avgDelayMs: Math.round(avgDelayMs),
      totalDelayMs: this.totalDelayMs,
    };
  }

  /**
   * Get recovery success rate by error type.
   *
   * @returns {Object} { errorType: successRate }
   */
  getSuccessByType() {
    const byType = {};

    for (const attempt of this.attempts) {
      if (!byType[attempt.errorType]) {
        byType[attempt.errorType] = {
          total: 0,
          successful: 0,
        };
      }

      byType[attempt.errorType].total++;
      if (attempt.success) {
        byType[attempt.errorType].successful++;
      }
    }

    const result = {};
    for (const [type, data] of Object.entries(byType)) {
      result[type] = data.total > 0 ? data.successful / data.total : 0;
    }

    return result;
  }

  /**
   * Get recent recovery attempts.
   *
   * @param {number} [limit=10] - Maximum number of recent attempts
   * @returns {Array}
   */
  getRecentAttempts(limit = 10) {
    return this.attempts.slice(-limit);
  }

  /**
   * Clear tracker.
   */
  clear() {
    this.attempts = [];
    this.totalAttempts = 0;
    this.successfulAttempts = 0;
    this.totalDelayMs = 0;
  }
}

// ============================================================================
// COMPOSITE METRICS COLLECTOR
// ============================================================================

/**
 * Aggregates all error recovery metrics.
 *
 * Provides unified interface for:
 * - Error rate tracking
 * - Error type distribution
 * - Recovery success rates
 *
 * @class ErrorRecoveryMetricsCollector
 */
export class ErrorRecoveryMetricsCollector {
  /**
   * @param {number} [windowMs=5000] - Error rate window duration
   */
  constructor(windowMs = 5000) {
    this.errorRateCollector = new ErrorRateCollector(windowMs);
    this.errorTypeHistogram = new ErrorTypeHistogram();
    this.recoverySuccessTracker = new RecoverySuccessTracker();
    this.createdAt = new Date().toISOString();
  }

  /**
   * Record error.
   *
   * @param {string} errorType
   * @param {string} [messageId]
   */
  recordError(errorType, messageId = null) {
    this.errorRateCollector.recordError(errorType, messageId);
    this.errorTypeHistogram.recordByType(errorType);
  }

  /**
   * Record successful request.
   */
  recordSuccess() {
    this.errorRateCollector.recordSuccess();
  }

  /**
   * Record recovery attempt.
   *
   * @param {boolean} success
   * @param {string} errorType
   * @param {number} [delayMs]
   */
  recordRecoveryAttempt(success, errorType, delayMs = 0) {
    this.recoverySuccessTracker.recordRecoveryAttempt(
      success,
      errorType,
      delayMs
    );
  }

  /**
   * Get comprehensive metrics summary.
   *
   * @returns {Object} All metrics in one object
   */
  getMetrics() {
    return {
      timestamp: new Date().toISOString(),
      createdAt: this.createdAt,
      errorRate: this.errorRateCollector.getErrorRate(),
      errorDistribution: this.errorTypeHistogram.getDistribution(),
      recoveryStats: this.recoverySuccessTracker.getRecoveryStats(),
      recoveryByType: this.recoverySuccessTracker.getSuccessByType(),
      alertThresholdExceeded: this.errorRateCollector.isAlertThresholdExceeded(),
    };
  }

  /**
   * Get abbreviated metrics for logging.
   *
   * @returns {Object} Key metrics only
   */
  getSummary() {
    const errorRate = this.errorRateCollector.getErrorRate();
    const recoveryStats = this.recoverySuccessTracker.getRecoveryStats();

    return {
      errorRate: (errorRate.rate * 100).toFixed(2) + '%',
      errorCount: errorRate.errorCount,
      totalRequests: errorRate.totalCount,
      recoverySuccessRate: (recoveryStats.successRate * 100).toFixed(2) + '%',
      avgRecoveryDelayMs: recoveryStats.avgDelayMs,
    };
  }

  /**
   * Clear all metrics.
   */
  clear() {
    this.errorRateCollector.clear();
    this.errorTypeHistogram.clear();
    this.recoverySuccessTracker.clear();
  }

  /**
   * Export metrics as JSON for storage/transmission.
   *
   * @returns {string} JSON-serialized metrics
   */
  toJSON() {
    return JSON.stringify(this.getMetrics(), null, 2);
  }
}

/**
 * Factory function to create metrics collector.
 *
 * @param {number} [windowMs]
 * @returns {ErrorRecoveryMetricsCollector}
 */
export function createErrorRecoveryMetricsCollector(windowMs = 5000) {
  return new ErrorRecoveryMetricsCollector(windowMs);
}
