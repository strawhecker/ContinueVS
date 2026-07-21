#!/usr/bin/env node

/**
 * regression-comparison-engine.mjs
 * Step 112: Regression Test Suite - Comparison Engine
 * 
 * Orchestrates regression detection across 3 metric sources:
 * - Performance metrics (latency p50/p95/p99, throughput)
 * - Stress test metrics (memory, error rates)
 * - Compliance metrics (handler registration status)
 * 
 * @module regression-comparison-engine
 */

/**
 * Custom error class for comparison operations.
 */
export class ComparisonError extends Error {
  constructor(message, code = 'COMPARISON_ERROR', details = null) {
    super(message);
    this.name = 'ComparisonError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Custom error class for tier validation.
 */
export class TierValidationError extends Error {
  constructor(message, code = 'TIER_VALIDATION_ERROR', details = null) {
    super(message);
    this.name = 'TierValidationError';
    this.code = code;
    this.details = details;
  }
}

/**
 * Regression comparison engine: detect and classify regressions.
 */
export class RegressionComparisonEngine {
  constructor(options = {}) {
    this.tolerancePercent = options.tolerancePercent || 10;
    this.severityThresholds = options.severityThresholds || {
      critical: { p99: 50, throughput: 40, errorRate: 10 },
      high: { p99: 25, throughput: 20, errorRate: 5 },
      medium: { p99: 15, throughput: 10, errorRate: 2 },
      low: { p99: 10, throughput: 5, errorRate: 1 }
    };
    this.memoryThresholds = options.memoryThresholds || {
      heapCritical: 50,    // 50MB
      heapHigh: 20,        // 20MB
      heapMedium: 10,      // 10MB
      nonHeapCritical: 20, // 20MB
      nonHeapHigh: 10      // 10MB
    };
    this.logger = options.logger;
    this.handlerTiers = options.handlerTiers || {
      fast: [],
      medium: [],
      slow: []
    };
  }

  /**
   * Compare current metrics against baseline.
   * @param {Object} current - Current metrics snapshot
   * @param {Object} baseline - Baseline metrics snapshot
   * @param {number} [customTolerance] - Override tolerance %
   * @returns {Object} Comparison result with regressions
   */
  compareMetrics(current, baseline, customTolerance) {
    if (!baseline || !baseline.handlers) {
      this.logger?.log?.('[Comparison] No baseline available, skipping comparison');
      return {
        handlerRegressions: [],
        summary: {
          totalHandlers: Object.keys(current).length,
          passedHandlers: Object.keys(current).length,
          regressionCount: 0,
          baselineUnavailable: true
        },
        tierStatus: { fast: true, medium: true, slow: true }
      };
    }

    const tolerance = customTolerance ?? this.tolerancePercent;
    const handlerRegressions = [];

    for (const [handlerName, currentMetrics] of Object.entries(current)) {
      const baselineMetrics = baseline.handlers[handlerName];
      if (!baselineMetrics) {
        this.logger?.log?.(`[Comparison] Handler ${handlerName} not in baseline, skipping`);
        continue;
      }

      const regression = this._analyzeHandler(
        handlerName,
        currentMetrics,
        baselineMetrics,
        tolerance
      );

      if (regression) {
        handlerRegressions.push(regression);
        this.logger?.log?.(`[Comparison] Handler ${handlerName}: ${regression.overallSeverity}`);
      }
    }

    // Validate tier gates
    const tierStatus = this._validateTierGates(handlerRegressions);

    return {
      handlerRegressions,
      summary: this._generateSummary(handlerRegressions, current),
      tierStatus,
      baselineVersion: baseline.version,
      baselineTimestamp: baseline.timestamp
    };
  }

  /**
   * Analyze single handler for regressions.
   * @private
   */
  _analyzeHandler(handlerName, current, baseline, tolerance) {
    const analysis = {
      handler: handlerName,
      timestamp: Date.now(),
      metrics: {}
    };

    // Analyze latency
    if (current.latency && baseline.latency) {
      analysis.metrics.latency = this._analyzeLatency(
        current.latency,
        baseline.latency,
        tolerance
      );
    }

    // Analyze throughput
    if (current.throughput && baseline.throughput) {
      analysis.metrics.throughput = this._analyzeThroughput(
        current.throughput,
        baseline.throughput,
        tolerance
      );
    }

    // Analyze memory
    if (current.memory && baseline.memory) {
      analysis.metrics.memory = this._analyzeMemory(
        current.memory,
        baseline.memory
      );
    }

    // Analyze error rate
    if (current.errorRate !== undefined && baseline.errorRate !== undefined) {
      analysis.metrics.errorRate = this._analyzeErrorRate(
        current.errorRate,
        baseline.errorRate
      );
    }

    // Classify overall severity
    analysis.overallSeverity = this._classifyRegression(analysis.metrics);
    analysis.tier = this._getTierForHandler(handlerName);

    return analysis;
  }

  /**
   * Analyze latency metrics (p50, p95, p99).
   * @private
   */
  _analyzeLatency(current, baseline, tolerance) {
    const result = {
      p50: {},
      p95: {},
      p99: {}
    };

    for (const percentile of ['p50', 'p95', 'p99']) {
      const currentVal = current[percentile];
      const baselineVal = baseline[percentile];

      if (!currentVal || !baselineVal || isNaN(currentVal) || isNaN(baselineVal)) {
        continue;
      }

      const delta = currentVal - baselineVal;
      const deltaPercent = (delta / baselineVal) * 100;

      result[percentile] = {
        baseline: baselineVal,
        current: currentVal,
        delta,
        deltaPercent,
        regression: deltaPercent > tolerance
      };
    }

    return result;
  }

  /**
   * Analyze throughput metrics.
   * @private
   */
  _analyzeThroughput(current, baseline, tolerance) {
    const currentMsg = current.messagesPerSecond || current.throughput;
    const baselineMsg = baseline.messagesPerSecond || baseline.throughput;

    if (!currentMsg || !baselineMsg || isNaN(currentMsg) || isNaN(baselineMsg)) {
      return null;
    }

    const delta = currentMsg - baselineMsg;
    const deltaPercent = (delta / baselineMsg) * 100;

    return {
      baseline: baselineMsg,
      current: currentMsg,
      delta,
      deltaPercent,
      regression: Math.abs(deltaPercent) > tolerance && deltaPercent < 0
    };
  }

  /**
   * Analyze memory metrics (heap + non-heap).
   * @private
   */
  _analyzeMemory(current, baseline) {
    const result = {
      heap: {},
      nonHeap: {}
    };

    // Heap analysis
    if (current.heapUsed !== undefined && baseline.heapUsed !== undefined) {
      const heapDelta = current.heapUsed - baseline.heapUsed;
      result.heap = {
        baseline: baseline.heapUsed,
        current: current.heapUsed,
        delta: heapDelta,
        regression: heapDelta > this.memoryThresholds.heapMedium
      };
    }

    // Non-heap analysis
    if (current.external !== undefined && baseline.external !== undefined) {
      const nonHeapDelta = current.external - baseline.external;
      result.nonHeap = {
        baseline: baseline.external,
        current: current.external,
        delta: nonHeapDelta,
        regression: nonHeapDelta > this.memoryThresholds.nonHeapHigh
      };
    }

    return result;
  }

  /**
   * Analyze error rate changes.
   * @private
   */
  _analyzeErrorRate(current, baseline) {
    const deltaBasis = (current - baseline) * 100; // Convert to percentage points

    return {
      baseline: baseline,
      current: current,
      deltaPercent: deltaBasis,
      regression: deltaBasis > 2 // 2% absolute threshold
    };
  }

  /**
   * Classify regression severity based on metrics.
   * @private
   */
  _classifyRegression(metrics) {
    if (!metrics || Object.keys(metrics).length === 0) {
      return 'NONE';
    }

    let severityScore = 0; // 0=NONE, 1=LOW, 2=MEDIUM, 3=HIGH, 4=CRITICAL

    // Latency severity
    if (metrics.latency) {
      const p99 = metrics.latency.p99?.deltaPercent || 0;
      if (p99 > this.severityThresholds.critical.p99) severityScore = 4;
      else if (p99 > this.severityThresholds.high.p99) severityScore = Math.max(severityScore, 3);
      else if (p99 > this.severityThresholds.medium.p99) severityScore = Math.max(severityScore, 2);
      else if (p99 > this.severityThresholds.low.p99) severityScore = Math.max(severityScore, 1);
    }

    // Throughput severity
    if (metrics.throughput?.regression) {
      const throughput = Math.abs(metrics.throughput.deltaPercent);
      if (throughput > this.severityThresholds.critical.throughput) severityScore = 4;
      else if (throughput > this.severityThresholds.high.throughput) severityScore = Math.max(severityScore, 3);
      else if (throughput > this.severityThresholds.medium.throughput) severityScore = Math.max(severityScore, 2);
    }

    // Memory severity
    if (metrics.memory?.heap?.regression) {
      const heapDelta = metrics.memory.heap.delta;
      if (heapDelta > this.memoryThresholds.heapCritical) severityScore = 4;
      else if (heapDelta > this.memoryThresholds.heapHigh) severityScore = Math.max(severityScore, 3);
      else if (heapDelta > this.memoryThresholds.heapMedium) severityScore = Math.max(severityScore, 2);
    }

    // Error rate severity
    if (metrics.errorRate?.regression) {
      const errorDelta = metrics.errorRate.deltaPercent;
      if (errorDelta > this.severityThresholds.critical.errorRate) severityScore = 4;
      else if (errorDelta > this.severityThresholds.high.errorRate) severityScore = Math.max(severityScore, 3);
      else if (errorDelta > this.severityThresholds.medium.errorRate) severityScore = Math.max(severityScore, 2);
    }

    return ['NONE', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL'][severityScore];
  }

  /**
   * Validate tier gates (fast, medium, slow).
   * @private
   */
  _validateTierGates(regressions) {
    const tierStatus = {
      fast: true,
      medium: true,
      slow: true
    };

    for (const regression of regressions) {
      if (regression.tier && regression.overallSeverity !== 'NONE' && regression.overallSeverity !== 'LOW') {
        // MEDIUM or higher in any tier fails that tier
        tierStatus[regression.tier] = false;
      }
    }

    return tierStatus;
  }

  /**
   * Get tier for handler name.
   * @private
   */
  _getTierForHandler(handlerName) {
    if (this.handlerTiers.fast.includes(handlerName)) return 'fast';
    if (this.handlerTiers.medium.includes(handlerName)) return 'medium';
    if (this.handlerTiers.slow.includes(handlerName)) return 'slow';
    return 'unknown';
  }

  /**
   * Generate summary statistics.
   * @private
   */
  _generateSummary(regressions, current) {
    const criticalCount = regressions.filter(r => r.overallSeverity === 'CRITICAL').length;
    const highCount = regressions.filter(r => r.overallSeverity === 'HIGH').length;
    const mediumCount = regressions.filter(r => r.overallSeverity === 'MEDIUM').length;
    const lowCount = regressions.filter(r => r.overallSeverity === 'LOW').length;
    const passedCount = regressions.filter(r => r.overallSeverity === 'NONE').length;
    const regressionCount = regressions.length - passedCount;

    return {
      totalHandlers: Object.keys(current).length,
      testedHandlers: regressions.length,
      passedHandlers: passedCount,
      regressionCount,
      criticalCount,
      highCount,
      mediumCount,
      lowCount,
      releaseGate: criticalCount > 0 ? 'BLOCKED' : 'PASSED'
    };
  }

  /**
   * Set handler tier mapping.
   */
  setHandlerTiers(tiers) {
    this.handlerTiers = tiers;
  }

  /**
   * Set severity thresholds.
   */
  setSeverityThresholds(thresholds) {
    this.severityThresholds = Object.assign(this.severityThresholds, thresholds);
  }

  /**
   * Set memory thresholds.
   */
  setMemoryThresholds(thresholds) {
    this.memoryThresholds = Object.assign(this.memoryThresholds, thresholds);
  }
}

/**
 * Factory function to create comparison engine.
 */
export function createComparisonEngine(options = {}) {
  return new RegressionComparisonEngine(options);
}
